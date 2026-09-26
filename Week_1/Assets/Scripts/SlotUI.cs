using UnityEngine;
using UnityEngine.EventSystems;

public class SlotUI : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null) return;

        ItemView droppedItem = droppedObj.GetComponent<ItemView>();
        if (droppedItem == null) return;

        if (droppedObj.transform.parent == transform) return;

        Transform originalParent = droppedItem.OriginalParent;

        if (transform.childCount == 0)
        {
            droppedObj.transform.SetParent(transform);
        }
        else
        {
            Transform existingItem = transform.GetChild(0);
            existingItem.SetParent(originalParent);
            existingItem.localPosition = Vector3.zero;

            droppedObj.transform.SetParent(transform);
        }

        droppedObj.transform.localPosition = Vector3.zero;
    }
}
