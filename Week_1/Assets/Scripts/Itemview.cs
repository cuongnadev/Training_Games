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

    public Transform OriginalParent { get; private set; }

    public static event Action<ItemDataSO, ItemView> OnItemSelected;
    private static ItemView currentlySelected;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(ItemDataSO data, int stack = 1)
    {
        itemData = data;
        currentStack = stack;

        if (iconImage != null)
            iconImage.sprite = data.icon;

        UpdateQuantityText();
    }

    private void UpdateQuantityText()
    {
        if (quantityText == null) return;

        if (currentStack > 1)
        {
            quantityText.gameObject.SetActive(true);
            quantityText.text = "x" + currentStack;
        }
        else
        {
            quantityText.gameObject.SetActive(false);
        }
    }

    // ----- CLICK / SELECT -----

    public void OnPointerClick(PointerEventData eventData)
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

    // ----- DRAG & DROP -----

    public void OnBeginDrag(PointerEventData eventData)
    {
        OriginalParent = transform.parent;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        transform.SetParent(transform.root, true);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        if (transform.parent == transform.root)
        {
            transform.SetParent(OriginalParent, false);
        }

        rectTransform.anchoredPosition = Vector2.zero;
    }

    public bool UseOne()
    {
        currentStack--;
        UpdateQuantityText();

        if (currentStack <= 0)
        {
            RemoveSelf();
            return true;
        }

        return false;
    }

    public void RemoveSelf()
    {
        if (currentlySelected == this)
        {
            currentlySelected = null;
        }
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}