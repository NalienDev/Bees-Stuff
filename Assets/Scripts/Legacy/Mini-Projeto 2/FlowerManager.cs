using UnityEngine;
using System.Collections.Generic;

public class FlowerManager : MonoBehaviour
{
    public GameObject flowerPrefab;
    public int flowersPerQuarter = 3;
    public HiveManager hiveManager;

    [Header("Terreno")]
    public LayerMask terrainLayerMask;
    public float maxTerrainHeight = 50f;
    public float flowerHeightOffset = 0.25f;
    public int maxSpawnAttempts = 10;

    private List<GameObject> spawnedFlowers = new List<GameObject>();

    public void SpawnFlowers(List<Rect> quarters)
    {
        foreach (var flower in spawnedFlowers)
        {
            if (flower != null) Destroy(flower);
        }
        spawnedFlowers.Clear();

        foreach (var rect in quarters)
        {
            for (int i = 0; i < flowersPerQuarter; i++)
            {
                Vector3 spawnPos = GetGroundedPosition(rect, flowerHeightOffset);
                GameObject flower = Instantiate(flowerPrefab, transform);
                flower.transform.position = spawnPos; // posição em mundo, já não local
                spawnedFlowers.Add(flower);
            }
        }

        if (hiveManager != null)
        {
            List<FlowerController> flowerControllers = new List<FlowerController>();
            foreach (var f in spawnedFlowers)
                flowerControllers.Add(f.GetComponent<FlowerController>());
            hiveManager.SetFlowers(flowerControllers);
        }
    }

    private Vector3 GetGroundedPosition(Rect rect, float heightOffset)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float x = Random.Range(rect.xMin, rect.xMax);
            float z = Random.Range(rect.yMin, rect.yMax);
            Vector3 rayOrigin = new Vector3(x, maxTerrainHeight, z);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxTerrainHeight * 2f, terrainLayerMask))
                return hit.point + Vector3.up * heightOffset;
        }

        Debug.LogWarning("FlowerManager: terreno não encontrado, a usar fallback.");
        return new Vector3(rect.center.x, heightOffset, rect.center.y);
    }
}