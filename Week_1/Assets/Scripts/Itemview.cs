using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("References (kéo từ Hierarchy vào)")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    [HideInInspector] public ItemDataSO itemData;
    [HideInInspector] public int currentStack;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private RectTransform dragLayer;
    private bool consumed;

    public Transform OriginalParent { get; private set; }

    public static event Action<ItemDataSO, ItemView> OnItemSelected;
    public static event Action<ItemView> OnItemChanged; 
    public static event Action<ItemView> OnItemRemoved; 
    private static ItemView currentlySelected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnItemSelected = null;
        OnItemChanged = null;
        OnItemRemoved = null;
        currentlySelected = null;
    }

    public bool IsSelected => currentlySelected == this;
    public int Space => itemData != null ? itemData.MaxStack - currentStack : 0;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(ItemDataSO data, int stack = 1)
    {
        itemData = data;
        currentStack = Mathf.Clamp(stack, 1, data.MaxStack);

        if (iconImage != null)
            iconImage.sprite = data.icon;

        UpdateQuantityText();
    }

    private void UpdateQuantityText()
    {
        if (quantityText == null) return;

        bool show = currentStack > 1;
        quantityText.gameObject.SetActive(show);
        if (show) quantityText.text = "x" + currentStack;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    public void Select()
    {
        if (currentlySelected != null && currentlySelected != this)
        {
            currentlySelected.SetHighlight(false);
        }
 
        currentlySelected = this;
        SetHighlight(true);
 
        OnItemSelected?.Invoke(itemData, this);
    }

    private void SetHighlight(bool on)
    {
        transform.localScale = on ? new Vector3(1.1f, 1.1f, 1.1f) : Vector3.one;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        OriginalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
 
        dragLayer = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
        transform.SetParent(dragLayer, true);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragLayer, eventData.position, eventData.pressEventCamera, out Vector3 world))
        {
            rectTransform.position = world;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (consumed) return; 
 
        canvasGroup.blocksRaycasts = true;

        if (transform.parent == dragLayer)
        {
            PlaceInto(OriginalParent);
        }
    }

    public void PlaceInto(Transform slot)
    {
        transform.SetParent(slot, false);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    public bool CanStackWith(ItemView other)
    {
        return other != null && other != this
            && itemData != null && itemData == other.itemData
            && itemData.MaxStack > 1;
    }

    public int AddStack(int amount)
    {
        int accepted = Mathf.Clamp(amount, 0, Space);
        if (accepted == 0) return 0;
 
        currentStack += accepted;
        UpdateQuantityText();
        OnItemChanged?.Invoke(this);
        return accepted;
    }

    public bool MergeInto(ItemView target)
    {
        currentStack -= target.AddStack(currentStack);
 
        if (currentStack <= 0)
        {
            consumed = true;
            RemoveSelf();
            return true;
        }
 
        UpdateQuantityText();
        OnItemChanged?.Invoke(this);
        return false;
    }
 
    public bool UseOne()
    {
        currentStack--;
 
        if (currentStack <= 0)
        {
            RemoveSelf();
            return true;
        }
 
        UpdateQuantityText();
        OnItemChanged?.Invoke(this);
        return false;
    }
 
    public void RemoveSelf()
    {
        ReleaseSelection();
        Destroy(gameObject);
    }
 
    private void ReleaseSelection()
    {
        if (currentlySelected != this) return;
 
        currentlySelected = null;
        OnItemRemoved?.Invoke(this);
    }
 
    private void OnDestroy()
    {
        ReleaseSelection();
    }
 
    private void OnDisable()
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}