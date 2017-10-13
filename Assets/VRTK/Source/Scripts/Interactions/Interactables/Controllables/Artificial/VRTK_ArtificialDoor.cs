// Artificial Door|ArtificialControllables|120020
namespace VRTK.Controllables.ArtificialBased
{
    using UnityEngine;
    using VRTK.GrabAttachMechanics;
    using VRTK.SecondaryControllerGrabActions;

    /// <summary>
    /// A artificially simulated openable door.
    /// </summary>
    /// <remarks>
    /// **Required Components:**
    ///  * `Collider` - A Unity Collider to determine when an interaction has occured. Can be a compound collider set in child GameObjects. Will be automatically added at runtime.
    /// 
    /// **Script Usage:**
    ///  * Place the `VRTK_ArtificialDoor` script onto the GameObject that is to become the door.
    ///  * Create a nested GameObject under the door GameObject and position it where the hinge should operate.
    ///  * Apply the nested hinge GameObject to the `Hinge Point` parameter on the Artificial Door script.
    ///
    ///   > At runtime, the Artificial Door script GameObject will become the child of a runtime created GameObject that determines the rotational offset for the door.
    /// </remarks>
    [AddComponentMenu("VRTK/Scripts/Interactables/Controllables/Artificial/VRTK_ArtificialDoor")]
    public class VRTK_ArtificialDoor : VRTK_BaseControllable
    {

        [Header("Hinge Settings")]

        [Tooltip("A Transform that denotes the position where the door will rotate around.")]
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
        [SerializeField]
        protected float angleTarget = 0f;
        [Tooltip("If this is checked then the door will not be able to be moved.")]
        public bool isLocked = false;

        [Header("Interaction Settings")]

        [Tooltip("The maximum distance the grabbing object is away from the door before it is automatically released.")]
        public float detachDistance = 1f;
        [Tooltip("The simulated friction when the door is grabbed.")]
        public float grabbedFriction = 1f;
        [Tooltip("The simulated friction when the door is released.")]
        public float releasedFriction = 1f;
        [Tooltip("A collection of GameObjects that will be used as the valid collisions to determine if the door can be interacted with.")]
        public GameObject[] onlyInteractWith = new GameObject[0];

        protected VRTK_InteractableObject controlInteractableObject;
        protected VRTK_RotateTransformGrabAttach controlGrabAttach;
        protected VRTK_SwapControllerGrabAction controlSecondaryGrabAction;
        protected bool createInteractableObject;
        protected GameObject parentOffset;
        protected bool createParentOffset;
        protected GameObject doorContainer;
        protected bool rotationReset;

        /// <summary>
        /// The GetValue method returns the current rotation value of the door.
        /// </summary>
        /// <returns>The actual rotation of the door.</returns>
        public override float GetValue()
        {
            float currentValue = doorContainer.transform.localEulerAngles[(int)operateAxis];
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
        /// The GetContainer method returns the GameObject that is generated to hold the door control.
        /// </summary>
        /// <returns>The GameObject container of the door control.</returns>
        public virtual GameObject GetContainer()
        {
            return doorContainer;
        }

        /// <summary>
        /// The SetAngleTarget method sets a target angle to rotate the door to.
        /// </summary>
        /// <param name="newAngle">The angle in which to rotate the door to.</param>
        public virtual void SetAngleTarget(float newAngle)
        {
            if (controlGrabAttach != null)
            {
                newAngle = Mathf.Clamp(newAngle, minimumAngle, maximumAngle);
                float currentValue = GetValue();
                angleTarget = newAngle;
                controlGrabAttach.SetRotation(angleTarget);
            }
        }

        /// <summary>
        /// The IsResting method returns whether the door is at the resting angle or within the resting angle threshold.
        /// </summary>
        /// <returns>Returns `true` if the door is at the resting angle or within the resting angle threshold.</returns>
        public override bool IsResting()
        {
            float currentValue = GetValue();
            return (!IsGrabbed() && (currentValue <= (restingAngle + minMaxThresholdAngle) && currentValue >= (restingAngle - minMaxThresholdAngle)));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            doorContainer = gameObject;
            rotationReset = false;
            SetupParentOffset();
            SetupInteractableObject();
            SetAngleTarget(angleTarget);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ManageInteractableListeners(false);
            ManageGrabbableListeners(false);
            if (createInteractableObject)
            {
                Destroy(controlInteractableObject);
            }
            RemoveParentOffset();
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

        protected virtual void SetupParentOffset()
        {
            createParentOffset = false;
            if (hingePoint != null)
            {
                hingePoint.transform.SetParent(transform.parent);
                Vector3 storedScale = transform.localScale;
                parentOffset = new GameObject(VRTK_SharedMethods.GenerateVRTKObjectName(true, name, "Controllable", "ArtificialBased", "DoorContainer"));
                parentOffset.transform.SetParent(transform.parent);
                parentOffset.transform.localPosition = transform.localPosition;
                parentOffset.transform.localRotation = transform.localRotation;
                parentOffset.transform.localScale = Vector3.one;
                transform.SetParent(parentOffset.transform);
                parentOffset.transform.localPosition = hingePoint.localPosition;
                transform.localPosition = -hingePoint.localPosition;
                transform.localScale = storedScale;
                hingePoint.transform.SetParent(transform);
                doorContainer = parentOffset;
                createParentOffset = true;
            }
        }

        protected virtual void RemoveParentOffset()
        {
            if (createParentOffset && parentOffset != null && gameObject.activeInHierarchy)
            {
                transform.SetParent(parentOffset.transform.parent);
                Destroy(parentOffset);
            }
        }

        protected virtual void SetupInteractableObject()
        {
            controlInteractableObject = GetComponent<VRTK_InteractableObject>();
            if (controlInteractableObject == null)
            {
                controlInteractableObject = doorContainer.AddComponent<VRTK_InteractableObject>();
                controlInteractableObject.isGrabbable = true;
                controlInteractableObject.ignoredColliders = (onlyInteractWith.Length > 0 ? VRTK_SharedMethods.ColliderExclude(GetComponentsInChildren<Collider>(true), VRTK_SharedMethods.GetCollidersInGameObjects(onlyInteractWith, true, true)) : new Collider[0]);
                SetupGrabMechanic();
                SetupSecondaryAction();
            }
            ManageInteractableListeners(true);
        }

        protected virtual void SetupGrabMechanic()
        {
            if (controlInteractableObject != null)
            {
                controlGrabAttach = controlInteractableObject.gameObject.AddComponent<VRTK_RotateTransformGrabAttach>();
                SetGrabMechanicParameters();
                controlInteractableObject.grabAttachMechanicScript = controlGrabAttach;
                ManageGrabbableListeners(true);
            }
        }

        protected virtual void SetGrabMechanicParameters()
        {
            if (controlGrabAttach != null)
            {
                controlGrabAttach.detachDistance = detachDistance;
                controlGrabAttach.rotateAround = (VRTK_RotateTransformGrabAttach.RotationAxis)operateAxis;
                controlGrabAttach.rotationFriction = grabbedFriction;
                controlGrabAttach.releaseDecelerationDamper = releasedFriction;
                controlGrabAttach.angleLimits = new Vector2(minimumAngle, maximumAngle);
            }
        }

        protected virtual void SetupSecondaryAction()
        {
            if (controlInteractableObject != null)
            {
                controlSecondaryGrabAction = controlInteractableObject.gameObject.AddComponent<VRTK_SwapControllerGrabAction>();
                controlInteractableObject.secondaryGrabActionScript = controlSecondaryGrabAction;
            }
        }

        protected virtual void ManageInteractableListeners(bool state)
        {
            if (controlInteractableObject != null)
            {
                if (state)
                {
                    controlInteractableObject.InteractableObjectGrabbed += InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed += InteractableObjectUngrabbed;
                }
                else
                {
                    controlInteractableObject.InteractableObjectGrabbed -= InteractableObjectGrabbed;
                    controlInteractableObject.InteractableObjectUngrabbed -= InteractableObjectUngrabbed;
                }
            }
        }

        protected virtual void InteractableObjectGrabbed(object sender, InteractableObjectEventArgs e)
        {
            CheckLock();
        }

        protected virtual void InteractableObjectUngrabbed(object sender, InteractableObjectEventArgs e)
        {
            rotationReset = false;
            ResetRotation();
        }

        protected virtual void CheckLock()
        {
            if (controlGrabAttach != null)
            {
                SetGrabMechanicParameters();
                controlGrabAttach.angleLimits = (isLocked ? Vector2.zero : new Vector2(minimumAngle, maximumAngle));
            }
        }

        protected virtual void ManageGrabbableListeners(bool state)
        {
            if (controlGrabAttach != null)
            {
                if (state)
                {
                    controlGrabAttach.AngleChanged += GrabMechanicAngleChanged;
                }
                else
                {
                    controlGrabAttach.AngleChanged -= GrabMechanicAngleChanged;
                }
            }
        }

        protected virtual void GrabMechanicAngleChanged(object sender, RotateTransformGrabAttachEventArgs e)
        {
            EmitEvents();
            if (controlInteractableObject != null && !controlInteractableObject.IsGrabbed())
            {
                ResetRotation();
            }
        }
        protected virtual void ResetRotation()
        {
            if (!rotationReset && controlGrabAttach != null)
            {
                float currentValue = GetValue();
                if (currentValue <= (restingAngle + forceShutThresholdAngle) && currentValue >= (restingAngle - forceShutThresholdAngle))
                {
                    rotationReset = true;
                    controlGrabAttach.SetRotation(restingAngle - currentValue, releasedFriction * 0.1f);
                }
            }
        }

        protected virtual bool IsGrabbed()
        {
            return (controlInteractableObject != null && controlInteractableObject.IsGrabbed());
        }

        protected virtual void EmitEvents()
        {
            ControllableEventArgs payload = EventPayload();
            float currentAngle = GetValue();
            float minThreshold = minimumAngle + minMaxThresholdAngle;
            float maxThreshold = maximumAngle - minMaxThresholdAngle;

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

            if (IsResting())
            {
                OnRestingPointReached(EventPayload());
            }
        }
    }
}