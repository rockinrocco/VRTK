namespace VRTK.Examples
{
    using UnityEngine;
    using VRTK.Controllables;
    using VRTK.Controllables.PhysicsBased;
    using VRTK.Controllables.ArtificialBased;

    public class ButtonReactor : MonoBehaviour
    {
        protected VRTK_PhysicsButton buttonEvents;
        protected VRTK_ArtificialButton artbuttonEvents;

        protected virtual void OnEnable()
        {
            buttonEvents = GetComponent<VRTK_PhysicsButton>();
            if (buttonEvents != null)
            {
                buttonEvents.MaxLimitReached += MaxLimitReached;
            }
            artbuttonEvents = GetComponent<VRTK_ArtificialButton>();
            if (artbuttonEvents != null)
            {
                artbuttonEvents.MaxLimitReached += MaxLimitReached;
            }
        }

        protected virtual void OnDisable()
        {
            if (buttonEvents != null)
            {
                buttonEvents.MaxLimitReached -= MaxLimitReached;
            }
            if (artbuttonEvents != null)
            {
                artbuttonEvents.MaxLimitReached -= MaxLimitReached;
            }
        }

        protected virtual void MaxLimitReached(object sender, ControllableEventArgs e)
        {
            VRTK_BaseControllable senderButton = sender as VRTK_BaseControllable;
            VRTK_Logger.Info(senderButton.name + " was pushed");
        }
    }
}