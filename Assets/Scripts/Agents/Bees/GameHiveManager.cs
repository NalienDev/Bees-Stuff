using UnityEngine;
using System.Collections.Generic;

public class GameHiveManager : MonoBehaviour
{
    [SerializeField] private float honeyStored = 0f;
    private List<GameBeeAgent> bees = new List<GameBeeAgent>();
    private List<FlowerController> allFlowers = new List<FlowerController>();

    public float HoneyStored => honeyStored;
    public List<FlowerController> AllFlowers => allFlowers;
    public int BeesCount => bees.Count;

    private void Start()
    {
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.RegisterHive(this);
        }
    }

    private void OnDestroy()
    {
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.UnregisterHive(this);
        }
    }

    public void RegisterBee(GameBeeAgent bee)
    {
        if (!bees.Contains(bee)) bees.Add(bee);
    }

    public void UnregisterBee(GameBeeAgent bee)
    {
        bees.Remove(bee);
    }

    public void SetFlowers(List<FlowerController> flowers)
    {
        allFlowers.Clear();
        allFlowers.AddRange(flowers);
    }

    public void DeliverPollen(float amount)
    {
        honeyStored += amount;
    }

    public float ConsumeHoney(float amount)
    {
        float consumed = Mathf.Min(amount, honeyStored);
        honeyStored -= consumed;
        return consumed;
    }
}