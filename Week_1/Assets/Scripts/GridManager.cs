using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Item Data mẫu (kéo 5 ItemDataSO vào đây)")]
    public List<ItemDataSO> sampleItems;

    [Header("Prefabs (kéo từ folder Prefabs vào)")]
    public GameObject slotPrefab;
    public GameObject itemViewPrefab;

    [Header("Cấu hình Grid")]
    public int slotCount = 16;

    [Header("Tùy chỉnh Random")]
    [Range(0.1f, 1f)] 
    public float fillRatio = 0.75f;
    public int minStack = 1;
    public int maxStack = 10;

    private List<GameObject> spawnedSlots = new List<GameObject>();

    private void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        ClearGrid();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slot = Instantiate(slotPrefab, transform);
            spawnedSlots.Add(slot);
        }

        if (sampleItems == null || sampleItems.Count == 0) return;

        int itemsToSpawn = Mathf.RoundToInt(slotCount * fillRatio);

        List<int> availableIndices = new List<int>();
        for (int i = 0; i < slotCount; i++)
        {
            availableIndices.Add(i);
        }

        for (int i = 0; i < itemsToSpawn; i++)
        {
            if (availableIndices.Count == 0) break;

            int randomIndexInList = Random.Range(0, availableIndices.Count);
            int targetSlotIndex = availableIndices[randomIndexInList];
            availableIndices.RemoveAt(randomIndexInList);

            ItemDataSO randomData = sampleItems[Random.Range(0, sampleItems.Count)];

            GameObject itemObj = Instantiate(itemViewPrefab, spawnedSlots[targetSlotIndex].transform);
            ItemView itemView = itemObj.GetComponent<ItemView>();

            if (itemView != null)
            {
                bool isConsumable = randomData.type.ToString().Equals("Consumable", System.StringComparison.OrdinalIgnoreCase);
                int randomStack = isConsumable ? Random.Range(minStack, maxStack + 1) : 1;

                itemView.Setup(randomData, randomStack);
            }
        }
    }

    private void ClearGrid()
    {
        foreach (var slot in spawnedSlots)
        {
            if (slot != null) Destroy(slot);
        }
        spawnedSlots.Clear();
    }

    public void ResetGrid()
    {
        GenerateGrid();
    }
}
