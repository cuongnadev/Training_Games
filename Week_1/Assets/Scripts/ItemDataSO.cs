using UnityEngine;

public enum ItemType
{
    Consumable,
    Equipment,
    Material
}

[CreateAssetMenu(fileName = "ItemDataSO", menuName = "Scriptable Objects/ItemDataSO")]
public class ItemDataSO : ScriptableObject
{
    public string id;
    public string itemName;
    public Sprite icon;
    public ItemType type;
    [Min(1)] public int maxStack = 1;
    [TextArea(2, 4)]
    public string description;

    public int MaxStack => Mathf.Max(1, maxStack);

    public bool IsConsumable => type == ItemType.Consumable;

    private void OnValidate()
    {
        if (maxStack < 1) maxStack = 1;
    }
}