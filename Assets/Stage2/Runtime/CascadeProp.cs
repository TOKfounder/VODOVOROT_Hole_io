using UnityEngine;

namespace Vodovorot.Stage2
{
    /// <summary>A support dependency for one of the sixteen rearranged City props only.</summary>
    public sealed class CascadeProp : MonoBehaviour
    {
        private FallingObject item;
        private FallingObject[] supports;
        private Bounds[] initialSupports;
        private Vector3 floorResetPosition;
        private HoleParent lastHole;

        internal void Configure(FallingObject owner, FallingObject[] supportingItems, Vector3 floorPosition)
        {
            item = owner;
            supports = supportingItems;
            floorResetPosition = floorPosition;
            initialSupports = new Bounds[supports.Length];
            for (int i = 0; i < supports.Length; i++) initialSupports[i] = supports[i].CascadeBounds;
        }

        internal void RememberHole(HoleParent hole) => lastHole = hole;

        private bool LostSupport(int index)
        {
            FallingObject support = supports[index];
            if (support == null || !support.isActiveAndEnabled || support.value <= 0) return true;
            Bounds current = support.CascadeBounds;
            Bounds original = initialSupports[index];
            if (current.size.sqrMagnitude <= .000001f) return true;
            // Waking a support is not itself removal: it must actually sink or move away.
            if (current.max.y < original.max.y - Mathf.Max(.025f, original.size.y * .15f)) return true;
            Bounds above = item.CascadeBounds;
            return current.max.x < above.min.x || current.min.x > above.max.x ||
                   current.max.z < above.min.z || current.min.z > above.max.z;
        }

        private void FixedUpdate()
        {
            if (item == null || !item.CanArrangeCascade || supports == null) return;
            for (int i = 0; i < supports.Length; i++)
            {
                if (!LostSupport(i)) continue;
                CascadeProp support = supports[i] != null ? supports[i].GetComponent<CascadeProp>() : null;
                HoleParent cause = support != null ? support.lastHole : lastHole;
                // This checks the ORIGINAL cached dimensions through CanBeEatenBy. No bypass,
                // attraction, points, or collision/scale changes are introduced by the cascade.
                if (cause != null && item.TryBeginCascadeFall(cause)) return;
            }
        }

        internal Vector3 SafeResetPosition(Vector3 stackedPosition)
        {
            if (item == null || supports == null || supports.Length == 0) return stackedPosition;
            for (int i = 0; i < supports.Length; i++)
                if (!LostSupport(i)) return stackedPosition;
            // Preserve the existing four-second recovery without restoring a floating tower
            // after all of its supports were already collected.
            return floorResetPosition;
        }
    }
}
