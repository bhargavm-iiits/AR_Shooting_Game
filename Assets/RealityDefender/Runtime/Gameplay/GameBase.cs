using UnityEngine;

namespace RealityDefender
{
    /// <summary>The surface-level, meter-scale contract shared by AR and practice.</summary>
    public sealed class GameBase : MonoBehaviour
    {
        public Transform[] targetSlots = new Transform[3];

        public bool HasValidSlots => targetSlots != null && targetSlots.Length == 3 &&
            targetSlots[0] && targetSlots[1] && targetSlots[2] &&
            targetSlots[0] != targetSlots[1] && targetSlots[1] != targetSlots[2] && targetSlots[0] != targetSlots[2];

        void OnDrawGizmosSelected()
        {
            if (targetSlots == null) return;
            Gizmos.color = Color.cyan;
            foreach (var slot in targetSlots)
                if (slot) Gizmos.DrawWireSphere(slot.position, .07f);
        }
    }
}
