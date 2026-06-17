using UnityEngine;
using UnityEngine.Events;

public static class EventManager
{
    // BlockInHandChange events & methods
    public static event UnityAction<bool> BlockInHandChange;

    public static void OnBlockInHandChange(bool isPositive)
    {
        BlockInHandChange?.Invoke(isPositive);
    }

}