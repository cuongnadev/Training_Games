using UnityEngine;

namespace BusJam
{
    public sealed class SlotDebug : MonoBehaviour
    {
        [ContextMenu("Unlock Next Slot")]
        private void Unlock() => SlotProgress.TryUnlockNext();

        [ContextMenu("Reset Slots To Default")]
        private void ResetSlots() => SlotProgress.ResetToDefault();
    }
}