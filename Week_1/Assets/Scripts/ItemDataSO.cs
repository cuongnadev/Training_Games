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
    public int maxStack;
    [TextArea(2, 4)]
    public string description;
}