using UnityEngine;

namespace AnEchoHasNoShape.Core
{
    internal static class GameplayPhysicsMaterials
    {
        private static PhysicsMaterial playerMaterial;
        private static PhysicsMaterial iceMaterial;

        public static PhysicsMaterial Player
        {
            get
            {
                if (playerMaterial == null)
                {
                    playerMaterial = CreateMaterial(
                        "Player Low Friction (Runtime)",
                        0.03f,
                        0f);
                }

                return playerMaterial;
            }
        }

        public static PhysicsMaterial Ice
        {
            get
            {
                if (iceMaterial == null)
                {
                    iceMaterial = CreateMaterial(
                        "Slippery Ice (Runtime)",
                        0.012f,
                        0f);
                }

                return iceMaterial;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMaterials()
        {
            playerMaterial = null;
            iceMaterial = null;
        }

        private static PhysicsMaterial CreateMaterial(string materialName, float dynamicFriction, float staticFriction)
        {
            return new PhysicsMaterial(materialName)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                hideFlags = HideFlags.DontSave
            };
        }
    }
}
