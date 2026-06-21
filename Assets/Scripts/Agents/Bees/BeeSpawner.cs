using UnityEngine;
using System.Collections.Generic;

public class BeeSpawner : MonoBehaviour
{
    [Header("Setup")]
    public GameObject beePrefab;
    public Transform hiveTransform;
    public int maxBees = 3;
    public GameHiveManager hiveManager;
    public LayerMask terrainLayerMask;

    private List<GameBeeAgent> aliveBees = new List<GameBeeAgent>();

    public bool HasSpawnedInitial { get; private set; } = false;

    private void Start()
    {
        for (int i = 0; i < maxBees; i++)
            SpawnBee();
        HasSpawnedInitial = true;
    }

    public void SpawnBee()
    {
        if (aliveBees.Count >= maxBees) return;

        float angle = aliveBees.Count * (360f / Mathf.Max(1, maxBees));
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.right * 0.8f;
        Vector3 spawnPos = hiveTransform.position + offset;

        GameObject beeGO = Instantiate(beePrefab, spawnPos, Quaternion.identity);
        GameBeeAgent bee = beeGO.GetComponent<GameBeeAgent>();

        bee.spawner = this;
        bee.hiveManager = hiveManager;
        bee.hiveTransform = hiveTransform;
        bee.terrainLayerMask = terrainLayerMask;

        hiveManager.RegisterBee(bee);
        aliveBees.Add(bee);
    }

    public void OnBeeDied(GameBeeAgent bee)
    {
        aliveBees.Remove(bee);
        hiveManager.UnregisterBee(bee);
        Destroy(bee.gameObject);


        Invoke(nameof(SpawnBee), 2f);
    }

    private void OnDestroy()
    {
        if (aliveBees != null)
        {
            foreach (var bee in aliveBees)
            {
                if (bee != null)
                {
                    Destroy(bee.gameObject);
                }
            }
            aliveBees.Clear();
        }
    }
}