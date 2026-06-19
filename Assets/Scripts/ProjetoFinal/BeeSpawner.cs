using UnityEngine;
using System.Collections.Generic;

public class BeeSpawner : MonoBehaviour
{
    [Header("Setup")] 
    public GameObject beePrefab;
    public Transform hiveTransform;
    public int maxBees = 3;
    public GameHiveManager hiveManager;

    private List<GameBeeAgent> aliveBees = new List<GameBeeAgent>();

    private void Start()
    {
        for (int i = 0; i < maxBees; i++)
            SpawnBee();
    }

    public void SpawnBee()
    {
        if (aliveBees.Count >= maxBees) return;

        float angle = aliveBees.Count * (360f / maxBees);
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.right * 0.8f;
        Vector3 spawnPos = hiveTransform.position + offset;

        GameObject beeGO = Instantiate(beePrefab, spawnPos, Quaternion.identity);
        GameBeeAgent bee = beeGO.GetComponent<GameBeeAgent>();
        bee.spawner = this;
        bee.hiveManager = hiveManager;
        bee.hiveTransform = hiveTransform;
        hiveManager.RegisterBee(bee);
        aliveBees.Add(bee);
    }

    public void OnBeeDied(GameBeeAgent bee)
    {
        aliveBees.Remove(bee);
        hiveManager.UnregisterBee(bee);
        Destroy(bee.gameObject);

        // Spawn a replacement after a short delay
        Invoke(nameof(SpawnBee), 2f);
    }
}