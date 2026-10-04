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
    [Min(1)] public int slotCount = 16;

    [Header("Tùy chỉnh Random")]
    [Range(0.1f, 1f)] 
    public float fillRatio = 0.75f;
    [Min(1)] public int minStack = 1;
    [Min(1)] public int maxStack = 10;

    private List<GameObject> spawnedSlots = new List<GameObject>();

    private readonly List<Transform> slots = new List<Transform>();
    private readonly List<int> indexBuffer = new List<int>();

    private void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        if (slotPrefab == null || itemViewPrefab == null)
        {
            Debug.LogError("GridManager: chưa gán slotPrefab hoặc itemViewPrefab.", this);
            return;
        }

        EnsureSlots();
        ClearItems();

        if (sampleItems == null || sampleItems.Count == 0) return;

        int itemsToSpawn = Mathf.Min(Mathf.RoundToInt(slotCount * fillRatio), slotCount);

        indexBuffer.Clear();

        for (int i = 0; i < slotCount; i++) indexBuffer.Add(i);

        for (int i = 0; i < itemsToSpawn; i++)
        {
            int j = Random.Range(i, slotCount);
            (indexBuffer[i], indexBuffer[j]) = (indexBuffer[j], indexBuffer[i]);
            SpawnItem(slots[indexBuffer[i]]);
        }
    }

    public void ResetGrid()
    {
        GenerateGrid();
    }

    private void EnsureSlots()
    {
        if (slots.Count == slotCount) return;
 
        foreach (Transform s in slots)
        {
            if (s != null) Destroy(s.gameObject);
        }
        slots.Clear();
 
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(Instantiate(slotPrefab, transform).transform);
        }
    }

    private void ClearItems()
    {
        foreach (Transform slot in slots)
        {
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                Transform child = slot.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
    }

    private void SpawnItem(Transform slot)
    {
        ItemDataSO data = sampleItems[Random.Range(0, sampleItems.Count)];
        if (data == null) return;
 
        GameObject obj = Instantiate(itemViewPrefab, slot);
        ItemView view = obj.GetComponent<ItemView>();
        if (view == null)
        {
            Debug.LogWarning("itemViewPrefab thiếu component ItemView.", itemViewPrefab);
            Destroy(obj);
            return;
        }
 
        int stack = 1;
        if (data.IsConsumable)
        {
            int hi = Mathf.Min(maxStack, data.MaxStack);
            int lo = Mathf.Min(minStack, hi);
            stack = Random.Range(lo, hi + 1);
        }
 
        view.Setup(data, stack);
    }
}
