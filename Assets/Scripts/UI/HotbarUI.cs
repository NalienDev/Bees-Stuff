using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    [SerializeField] private GameObject hotbar;
    [SerializeField] private List<HotbarSlot> slots;
    private int currentHighlightedIndex = 1;

    private void Awake()
    {
        EventManager.BlockInHandChange += OnBlockInHandChange;
    }

    private void OnDestroy()
    {
        EventManager.BlockInHandChange -= OnBlockInHandChange;
    }

    private void OnBlockInHandChange(bool isPositive)
    {
        slots[currentHighlightedIndex].isActive = false;
        currentHighlightedIndex = ((currentHighlightedIndex + (isPositive ? 1 : -1)) + slots.Count) % slots.Count;
        slots[currentHighlightedIndex].isActive = true;
    }
}
