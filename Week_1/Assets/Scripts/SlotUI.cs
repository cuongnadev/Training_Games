using UnityEngine;
using UnityEngine.EventSystems;

public class SlotUI : MonoBehaviour, IDropHandler
{
    public ItemView CurrentItem => GetComponentInChildren<ItemView>();

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null || !droppedObj.TryGetComponent(out ItemView dropped)) return;

        Transform origin = dropped.OriginalParent;

        ItemView droppedItem = droppedObj.GetComponent<ItemView>();

        if (origin == transform) return;

        ItemView existing = CurrentItem;

        if (existing == null)
        {
            dropped.PlaceInto(transform);
            return;
        }

        if (dropped.CanStackWith(existing) && existing.Space > 0)
        {
            bool wasSelected = dropped.IsSelected;
            bool mergedAll = dropped.MergeInto(existing);
 
            // Item đang chọn đã biến mất -> chuyển lựa chọn sang item nhận.
            if (mergedAll && wasSelected) existing.Select();
 
            // Gộp một phần: phần dư tự về slot cũ ở OnEndDrag.
            return;
        }

        existing.PlaceInto(origin);
        dropped.PlaceInto(transform);
    }
}
