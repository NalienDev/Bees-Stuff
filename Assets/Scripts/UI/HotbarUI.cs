using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    [SerializeField] private GameObject _hotbar;
    [SerializeField] private List<HotbarSlot> _slots;
    private int _currentHighlightedIndex = 1;

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
        _slots[_currentHighlightedIndex].isActive = false;
        _currentHighlightedIndex = ((_currentHighlightedIndex + (isPositive ? 1 : -1)) + _slots.Count) % _slots.Count;
        _slots[_currentHighlightedIndex].isActive = true;
    }
}
