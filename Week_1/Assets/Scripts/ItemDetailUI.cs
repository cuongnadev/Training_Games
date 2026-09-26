using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ItemDetailUI : MonoBehaviour
{
    [Header("References (kéo từ Hierarchy vào)")]
    public Image itemIcon;
    public TextMeshProUGUI itemName;
    public TextMeshProUGUI itemType;
    public TextMeshProUGUI quantity;
    public TextMeshProUGUI description;

    [Header("Buttons")]
    public Button useButton;
    public Button deleteButton;

    private CanvasGroup canvasGroup;

    [HideInInspector] public ItemDataSO currentData;
    [HideInInspector] public ItemView currentView;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (useButton != null) useButton.onClick.AddListener(OnUseClicked);
        if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);

        HidePanel();
    }

    private void OnDestroy()
    {
        if (useButton != null) useButton.onClick.RemoveListener(OnUseClicked);
        if (deleteButton != null) deleteButton.onClick.RemoveListener(OnDeleteClicked);
    }

    private void OnEnable()
    {
        ItemView.OnItemSelected += HandleItemSelected;
    }

    private void OnDisable()
    {
        ItemView.OnItemSelected -= HandleItemSelected;
    }

    private void HandleItemSelected(ItemDataSO data, ItemView view)
    {
        currentData = data;
        currentView = view;

        if (itemIcon != null) itemIcon.sprite = data.icon;
        if (itemName != null) itemName.text = data.itemName;
        if (itemType != null) itemType.text = "Type: " + data.type.ToString();
        // if (quantity != null) quantity.text = "SL: " + view.currentStack;
        if (description != null) description.text = data.description;

        UpdateQuantityUI();

        if (useButton != null)
        {
            bool isConsumable = data.type.ToString().Equals("Consumable", System.StringComparison.OrdinalIgnoreCase);
            useButton.interactable = isConsumable;
        }

        ShowPanel();
    }

    private void UpdateQuantityUI()
    {
        if (quantity != null && currentView != null)
        {
            quantity.text = "SL: " + currentView.currentStack;
        }
    }

    // ----- USE -----

    private void OnUseClicked()
    {
        if (currentView == null)
        {
            HidePanel();
            return;
        }

        bool isDestroyed = currentView.UseOne();

        if (isDestroyed)
        {
            HidePanel();
        }
        else
        {
            UpdateQuantityUI();
        }
    }

    // ----- DELETE -----

    private void OnDeleteClicked()
    {
        if (currentView != null)
        {
            currentView.RemoveSelf();
        }
        
        HidePanel();
    }

    public void ShowPanel()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void HidePanel()
    {
        currentData = null;
        currentView = null;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
