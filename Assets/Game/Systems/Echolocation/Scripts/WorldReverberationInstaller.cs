using AnEchoHasNoShape.Interaction;
using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    public static class WorldReverberationInstaller
    {
        private const string CenterObjectName = "ReverberationCenter";
        private const string FirstTriggerTabletName = "TabletMain";
        private const string FirstPhaseVolumeName = "HiddenDoorStone";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            GameObject center = GameObject.Find(CenterObjectName);
            if (center != null && center.GetComponent<WorldReverberationController>() == null)
            {
                center.AddComponent<WorldReverberationController>();
            }

            GameObject tablet = GameObject.Find(FirstTriggerTabletName);
            ReadableTextInteractable readable = tablet != null
                ? tablet.GetComponent<ReadableTextInteractable>()
                : null;
            readable?.EnableWorldReverberationAfterFirstRead();
            readable?.EnableHiddenDoorAfterFirstRead();

            GameObject hiddenDoor = GameObject.Find(FirstPhaseVolumeName);
            if (hiddenDoor != null)
            {
                PhaseThroughVolume phaseVolume = hiddenDoor.GetComponent<PhaseThroughVolume>();
                if (phaseVolume == null)
                {
                    phaseVolume = hiddenDoor.AddComponent<PhaseThroughVolume>();
                }

                phaseVolume.SetArmed(false);
            }
        }
    }
}
