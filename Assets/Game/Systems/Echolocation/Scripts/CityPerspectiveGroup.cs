using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class CityPerspectiveGroup : MonoBehaviour
    {
        private CitySequenceController sequence;
        private int perspective;
        private MeshRenderer[] sources;
        private bool[] originalEnabled;
        public bool IsVisibleAt(Vector3 position) => sequence != null && sequence.PerspectiveVisibility(perspective, position) > 0.1f;

        public void Configure(CitySequenceController controller, int identity)
        {
            sequence = controller;
            perspective = identity;
            EchoReactiveSurface[] surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            sources = new MeshRenderer[surfaces.Length];
            originalEnabled = new bool[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                sources[i] = surfaces[i].GetComponent<MeshRenderer>();
                originalEnabled[i] = sources[i].enabled;
                sources[i].enabled = false;
                surfaces[i].SetCityPerspective(identity);
            }
        }

        private void OnDestroy()
        {
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].enabled = originalEnabled[i];
        }
    }
}
