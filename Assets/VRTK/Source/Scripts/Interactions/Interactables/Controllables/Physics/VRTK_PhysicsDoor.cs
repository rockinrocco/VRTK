// Physics Door|PhysicsControllables|110030
namespace VRTK.Controllables.PhysicsBased
{
    using UnityEngine;
    using VRTK.GrabAttachMechanics;
    using VRTK.SecondaryControllerGrabActions;

    /// <summary>
    /// A physics based openable door.
    /// </summary>
    /// <remarks>
    /// **Required Components:**
    ///  * `Collider` - A Unity Collider to determine when an interaction has occured. Can be a compound collider set in child GameObjects. Will be automatically added at runtime.
    ///  * `Rigidbody` - A Unity Rigidbody to allow the GameObject to be affected by the Unity Physics System. Will be automatically added at runtime.
    ///
    /// **Optional Components:**
    ///  * `VRTK_ControllerRigidbodyActivator` - A Controller Rigidbody Activator to automatically enable the controller rigidbody when near the door. Will be automatically created if the `Auto Interaction` paramter is checked.
    /// 
    /// **Script Usage:**
    ///  * Place the `VRTK_PhysicsDoor` script onto the GameObject that is to become the door.
    ///  * Create a nested GameObject under the door GameObject and position it where the hinge should operate.
    ///  * Apply the nested hinge GameObject to the `Hinge Point` parameter on the Physics Door script.
    /// </remarks>
    [AddComponentMenu("VRTK/Scripts/Interactables/Controllables/Physics/VRTK_PhysicsDoor")]
    public class VRTK_PhysicsDoor : VRTK_BasePhysicsControllable
    {
        /// <summary>
        /// Type of Grab Mechanic
        /// </summary>
        public enum GrabMechanic
        {
            /// <summary>
            /// The Track Object Grab Mechanic
            /// </summary>
            TrackObject,
            /// <summary>
            /// The Rotator Track Grab Mechanic
            /// </summary>
            RotatorTrack
        }

        [Header("Hinge Settings")]

        [Tooltip("A Transform that denotes the position where the door hinge will be created.")]
        public Transform hingePoint;
        [Tooltip("The minimum angle the door can rotate to.")]
        [Range(-180f, 180f)]
        public float minimumAngle = -180f;
        [Tooltip("The maximum angle the door can rotate to.")]
        [Range(-180f, 180f)]
        public float maximumAngle = 180f;
        [Tooltip("The angle at which the door rotation can be within the minimum or maximum angle before the minimum or maximum angles are considered reached.")]
        public float minMaxThresholdAngle = 1f;
        [Tooltip("The angle at which will be considered as the resting position of the door.")]
        public float restingAngle = 0f;
        [Tooltip("The threshold angle from the `Resting Angle` that the current angle of the door needs to be within to snap the door back to the `Resting Angle`.")]
        public float forceShutThresholdAngle = 1f;
        [Tooltip("The target angle to rotate the door to.")]
        public float angleTarget = 0f;
        [Tooltip("If this is checked then the door will not be able to be moved.")]
        public bool isLocked = false;

        [Header("Interaction Settings")]

        [Tooltip("The type of Interactable Object grab mechanic to use when operating the door.")]
        public GrabMechanic grabMechanic = GrabMechanic.RotatorTrack;
        [Tooltip("The maximum distance the grabbing object is away from the door before it is automatically released.")]
        public float detachDistance = 1f;
        [Tooltip("If this is checked then the `Grabbed Friction` value will be used as the Rigidbody drag value when the door is grabbed and the `Released Friction` value will be used as the Rigidbody drag value when the door is released.")]
        public bool useFrictionOverrides = false;
        [Tooltip("The Rigidbody drag value when the door is grabbed.")]
        public float grabbedFriction = 0f;
        [Tooltip("The Rigidbody drag value when the door is released.")]
        public float releasedFriction = 0f;
        [Tooltip("A collection of GameObjects that will be used as the valid collisions to determine if the door can be interacted with.")]
        public GameObject[] onlyInteractWith = new GameObject[0];

        protected VRTK_InteractableObject controlInteractableObject;
        protected VRTK_TrackObjectGrabAttach controlGrabAttach;
        protected VRTK_SwapControllerGrabAction controlSecondaryGrabAction;
        protected bool createControlInteractableObject;
        protected HingeJoint controlJoint;
        protected bool createControlJoint;
        protected RigidbodyConstraints savedConstraints;
        protected bool stillLocked;
        protected bool stillResting;
        protected float previousValue;
        protected float previousAngleTarget;

        /// <summary>
        /// The GetValue method returns the current rotation value of the door.
        /// </summary>
        /// <returns>The actual rotation of the door.</returns>
        public override float GetValue()
        {
            float currentValue = transform.localEulerAngles[(int)operateAxis];
            return (currentValue > 180f ? currentValue - 360f : currentValue);
        }

        /// <summary>
        /// The GetNormalizedValue method returns the current rotation value of the door normalized between `0f` and `1f`.
        /// </summary>
        /// <returns>The normalized rotation of the door.</returns>
        public override float GetNormalizedValue()
        {
            return VRTK_SharedMethods.NormalizeValue(GetValue(), minimumAngle, maximumAngle);
        }

        /// <summary>
        /// The IsResting method returns whether the door is at the resting angle or within the resting angle threshold.
        /// </summary>
        /// <returns>Returns `true` if the door is at the resting angle or within the resting angle threshold.</returns>
        public override bool IsResting()
        {
            float currentValue = GetValue();
            return (!IsGrabbed() && (currentValue <= restingAngle + minMaxThresholdAngle && currentValue >= restingAngle - minMaxThresholdAngle));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            stillLocked = false;
            stillResting = false;
            previousAngleTarget = float.MaxValue;
            previousValue = float.MaxValue;
            savedConstraints = controlRigidbody.constraints;
            SetupInteractableObject();
            SetupJoint();
            CheckLock();
            UpdateToAngle(angleTarget);
            EmitEvents();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (createControlInteractableObject)
            {
                ManageInteractableObjectListeners(false);
                Destroy(controlSecondaryGrabAction);
                Destroy(controlGrabAttach);
                Destroy(controlInteractableObject);
            }
            else
            {
                ManageInteractableObjectListeners(false);
            }
            if (createControlJoint)
            {
                Destroy(controlJoint);
            }
        }

        protected virtual void Update()
        {
            ManageSpringState();
            ForceAngleTarget();
            SetJointLimits();

            EmitEvents();
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (hingePoint != null)
            {
                Bounds doorBounds = VRTK_SharedMethods.GetBounds(transform, transform);
                Vector3 limits = transform.rotation * ((AxisDirection() * doorBounds.size[(int)operateAxis]) * 0.53f);
                Vector3 hingeStart = hingePoint.transform.position - limits;
                Vector3 hingeEnd = hingePoint.transform.position + limits;
                Gizmos.DrawLine(hingeStart, hingeEnd);
                Gizmos.DrawSphere(hingeStart, 0.01f);
                Gizmos.DrawSphere(hingeEnd, 0.01f);
            }
        }

        protected virtual void SetupInteractableObject()
        {
            createControlInteractableObject = false;
            controlInteractableObject = GetComponent<VRTK_InteractableObject>();
            if (controlInteractableObject == null)
            {
                controlInteractableObject = gameObject.AddComponent<VRTK_InteractableObject>();
                createControlInteractableObject = true;
                controlInteractableObject.isGrabbable = true;
                controlInteractableObject.ignoredColliders = (onlyInteractWith.Length > 0 ? VRTK_SharedMethods.ColliderExclude(GetComponentsInChildren<Collider>(true), VRTK_SharedMethods.GetCollidersInGameObjects(onlyInteractWith, true, true)) : new Collider[0]);

                SetupGrabMechanic();
                SetupSecondaryAction();
                ManageInteractableObjectListeners(true);
            }
        }

        protected virtual void SetupGrabMechanic()
        {
            switch (grabMechanic)
            {
                case GrabMechanic.TrackObject:
                    controlGrabAttach = controlInteractableObject.gameObject.AddComponent<VRTK_TrackObjectGrabAttach>();
                    controlGrabAttach.precisionGrab = false;
                    break;
                case GrabMechanic.RotatorTrack:
                    controlGrabAttach = controlInteractableObject.gameObject.AddComponent<VRTK_RotatorTrackGrabAttach>();
                    controlGrabAttach.precisionGrab = true;
                    break;
            }
            SetGrabMechanicParameters();
            controlInteractableObject.grabAttachMechanicScript = controlGrabAttach;
        }

        protected virtual void SetGrabMechanicParameters()
        {
            if (controlGrabAttach != null)
            {
                controlGrabAttach.detachDistance = detachDistance;
            }
        }

        protected virtual void SetupSecondaryAction()
        {
            controlSecondaryGrabAction = controlInteractableObject.gameObject.AddComponent<VRTK_SwapControllerGrabAction>();
            controlInteractableObject.secondaryGrabActionScript = controlSecondaryGrabAction;
        }

        protected virtual void ManageInteractableObjectListeners(bool state)
        {
            if (controlInteractableObject != null)
            {
                if (state)
                {
                    controlInteractableObject.InteractableObjectTouched += InteractableObjectTouched;
                    controlInteractableObject.InteractableObjectUntouched += InteractableObjectUntouched;
                    controlInteractableObject.InteractableObjectGrabbed += InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed += InteractableObjectUngrabbed;
                }
                else
                {
                    controlInteractableObject.InteractableObjectTouched -= InteractableObjectTouched;
                    controlInteractableObject.InteractableObjectUntouched -= InteractableObjectUntouched;
                    controlInteractableObject.InteractableObjectGrabbed -= InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed -= InteractableObjectUngrabbed;
                }
            }
        }

        protected virtual void InteractableObjectTouched(object sender, InteractableObjectEventArgs e)
        {
            CheckLock();
            if (autoInteraction)
            {
                ManageSpring(false, restingAngle);
            }
        }

        protected virtual void InteractableObjectUntouched(object sender, InteractableObjectEventArgs e)
        {
            CheckLock();
        }

        protected virtual void InteractableObjectGrabbed(object sender, InteractableObjectEventArgs e)
        {
            SetGrabMechanicParameters();
            ManageSpring(false, restingAngle);
            if (useFrictionOverrides)
            {
                SetRigidbodyDrag(grabbedFriction);
            }
        }

        protected virtual void InteractableObjectUngrabbed(object sender, InteractableObjectEventArgs e)
        {
            if (useFrictionOverrides)
            {
                SetRigidbodyDrag(releasedFriction);
            }
        }

        protected virtual void CheckLock()
        {
            if (controlRigidbody != null)
            {
                if (isLocked && !stillLocked)
                {
                    savedConstraints = controlRigidbody.constraints;
                    SetRigidbodyConstraints(RigidbodyConstraints.FreezeRotation);
                    stillLocked = true;
                }
                else if (!isLocked && stillLocked)
                {
                    SetRigidbodyConstraints(savedConstraints);
                    stillLocked = false;
                }
            }
        }

        protected virtual void UpdateToAngle(float givenTargetAngle)
        {
            bool activateSpring = (Mathf.Abs(GetValue() - givenTargetAngle) >= equalityFidelity);
            ManageSpring(activateSpring, givenTargetAngle);
        }

        protected virtual void ManageSpringState()
        {
            if (controlJoint != null && !controlJoint.useSpring && controlInteractableObject != null && !controlInteractableObject.IsGrabbed() && (!autoInteraction || !controlInteractableObject.IsTouched()))
            {
                if ((GetValue() < (restingAngle + forceShutThresholdAngle) && GetValue() > (restingAngle - forceShutThresholdAngle)))
                {
                    ManageSpring(true, restingAngle);
                }
            }
        }

        protected virtual void ForceAngleTarget()
        {
            if (previousAngleTarget != angleTarget)
            {
                UpdateToAngle(angleTarget);
            }
            previousAngleTarget = angleTarget;
        }

        protected virtual void SetupJoint()
        {
            createControlJoint = false;
            controlJoint = GetComponent<HingeJoint>();
            if (controlJoint == null && hingePoint != null)
            {
                controlJoint = gameObject.AddComponent<HingeJoint>();
                createControlJoint = true;
                controlJoint.axis = AxisDirection();
                controlJoint.connectedBody = connectedTo;
                hingePoint.SetParent(transform);
                controlJoint.anchor = (hingePoint != null ? hingePoint.localPosition : Vector3.zero);
                controlJoint.useLimits = true;
                SetJointLimits();
            }
        }

        protected virtual void SetJointLimits()
        {
            if (controlJoint != null)
            {
                JointLimits controlJointLimits = new JointLimits();
                controlJointLimits.min = minimumAngle;
                controlJointLimits.max = maximumAngle;
                controlJoint.limits = controlJointLimits;
            }
        }

        protected virtual void ManageSpring(bool activate, float springTarget)
        {
            if (controlJoint != null)
            {
                controlJoint.useSpring = activate;
                JointSpring controlJointSpring = new JointSpring();
                controlJointSpring.spring = 100f;
                controlJointSpring.damper = 10f;
                controlJointSpring.targetPosition = springTarget;
                controlJoint.spring = controlJointSpring;
            }
        }

        protected virtual bool IsGrabbed()
        {
            return (controlInteractableObject != null && controlInteractableObject.IsGrabbed());
        }

        protected virtual void EmitEvents()
        {
            bool valueChanged = Mathf.Abs(GetValue() - previousValue) >= equalityFidelity;

            if (valueChanged)
            {
                ControllableEventArgs payload = EventPayload();
                float currentAngle = GetValue();
                float minThreshold = minimumAngle + minMaxThresholdAngle;
                float maxThreshold = maximumAngle - minMaxThresholdAngle;
                stillResting = false;

                OnValueChanged(payload);

                if (currentAngle >= maxThreshold && !AtMaxLimit())
                {
                    atMaxLimit = true;
                    OnMaxLimitReached(payload);
                }
                else if (currentAngle <= (minimumAngle + minMaxThresholdAngle) && !AtMinLimit())
                {
                    atMinLimit = true;
                    OnMinLimitReached(payload);
                }
                else if (currentAngle > minThreshold && currentAngle < maxThreshold)
                {
                    if (AtMinLimit())
                    {
                        OnMinLimitExited(payload);
                    }
                    if (AtMaxLimit())
                    {
                        OnMaxLimitExited(payload);
                    }
                    atMinLimit = false;
                    atMaxLimit = false;
                }

                previousValue = GetValue();
            }

            if (!stillResting && IsResting() && !valueChanged)
            {
                OnRestingPointReached(EventPayload());
                stillResting = true;
            }
        }
    }
}