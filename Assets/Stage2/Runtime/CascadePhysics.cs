using UnityEngine;

namespace Vodovorot.Stage2
{
    /// <summary>The sixteen City props collide with each other without enabling Fallable x Fallable.</summary>
    public static class CascadePhysics
    {
        public const int OriginalLayer = 7;
        public const int Layer = 30;
        public const string LayerName = "CityCascade";

        public static int IncludeInFallableMask(int originalMask) =>
            (originalMask & (1 << OriginalLayer)) != 0 ? originalMask | (1 << Layer) : originalMask;

        internal static void AssignOwnedColliders(FallingObject owner)
        {
            owner.gameObject.layer = Layer;
            foreach (Collider collider in owner.GetComponentsInChildren<Collider>(true))
                if (collider.GetComponentInParent<FallingObject>() == owner) collider.gameObject.layer = Layer;
        }

        public static bool ValidateConfiguration(out string error)
        {
            error = null;
            if (LayerMask.NameToLayer(LayerName) != Layer) { error = "CityCascade must occupy only the previously empty layer 30."; return false; }
            if (!Physics.GetIgnoreLayerCollision(OriginalLayer, OriginalLayer)) { error = "Original Fallable self collision was unexpectedly enabled."; return false; }
            if (Physics.GetIgnoreLayerCollision(Layer, Layer)) { error = "CityCascade self collision is disabled."; return false; }
            for (int other = 0; other < 32; other++)
                if (other != Layer && Physics.GetIgnoreLayerCollision(Layer, other) != Physics.GetIgnoreLayerCollision(OriginalLayer, other))
                { error = "CityCascade does not inherit the original interaction with layer " + other; return false; }
            return true;
        }
    }
}
