using UnityEngine;
using System.Collections.Generic;

public class HiveManager : MonoBehaviour
{
    private void Awake() { }

    [Header("Referencias de Spawning")]
    [SerializeField] private FlowerManager flowerManager;
    [SerializeField] private Transform hiveTransform;
    [SerializeField] private MeshRenderer floorRenderer;

    [Header("Terreno")]
    [SerializeField] private LayerMask terrainLayerMask;
    [SerializeField] private float maxTerrainHeight = 50f;
    [SerializeField] private float hiveHeightOffset = 0.5f;

    [Header("Estado")]
    [SerializeField] private float honeyStored = 0f;
    private List<BeeAgent> registeredBees = new List<BeeAgent>();
    private List<FlowerController> allFlowers = new List<FlowerController>();
    private int beesDoneCount = 0;
    private int beesResetCount = 0;
    private bool arenaAlreadyReset = false;

    public float HoneyStored => honeyStored;
    public int BeeCount => registeredBees.Count;
    public List<FlowerController> AllFlowers => allFlowers;

    private void Start() { ResetHive(); }

    public void DeliverPollen(float amount)
    {
        honeyStored += amount;
        Debug.Log($"HiveManager: Honey delivered! Total stored: {honeyStored:F2}");
    }

    public float ConsumeHoney(float amount)
    {
        float consumed = Mathf.Min(amount, honeyStored);
        if (consumed > 0)
        {
            honeyStored -= consumed;
            Debug.Log($"HiveManager: Honey consumed! Remaining: {honeyStored:F2}");
        }
        return consumed;
    }

    public void RegisterBee(BeeAgent bee)
    {
        if (!registeredBees.Contains(bee)) registeredBees.Add(bee);
    }

    public void SetFlowers(List<FlowerController> flowers)
    {
        allFlowers.Clear();
        allFlowers.AddRange(flowers);
    }

    public void BeeDone()
    {
        beesDoneCount++;
        if (beesDoneCount >= registeredBees.Count)
        {
            if (!arenaAlreadyReset)
            {
                ResetHive();
                arenaAlreadyReset = true;
            }
            foreach (var bee in registeredBees) bee.EndEpisode();
        }
    }

    public void NotifyBeeReset(BeeAgent bee)
    {
        beesResetCount++;
        if (beesResetCount >= registeredBees.Count)
        {
            if (!arenaAlreadyReset) ResetHive();
            arenaAlreadyReset = false;
            beesResetCount = 0;
            beesDoneCount = 0;
        }
    }

    public void ResetHive()
    {
        honeyStored = 0f;
        beesDoneCount = 0;
        arenaAlreadyReset = false;

        if (floorRenderer == null || hiveTransform == null || flowerManager == null)
        {
            Debug.LogWarning("HiveManager: Missing references for spawning!");
            return;
        }

        Bounds bounds = floorRenderer.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        float margin = 1.5f;
        float halfUsableWidth = (size.x - margin * 2) / 2f;
        float halfUsableDepth = (size.z - margin * 2) / 2f;
        float startX = center.x - halfUsableWidth;
        float startZ = center.z - halfUsableDepth;

        List<Rect> quarters = new List<Rect>
        {
            new Rect(startX, center.z, halfUsableWidth, halfUsableDepth),
            new Rect(center.x, center.z, halfUsableWidth, halfUsableDepth),
            new Rect(startX, startZ, halfUsableWidth, halfUsableDepth),
            new Rect(center.x, startZ, halfUsableWidth, halfUsableDepth)
        };

        int hiveQuarterIndex = Random.Range(0, 4);
        Rect hiveRect = quarters[hiveQuarterIndex];

        float hivePadding = 0.5f;
        float hiveX = Random.Range(hiveRect.xMin + hivePadding, hiveRect.xMax - hivePadding);
        float hiveZ = Random.Range(hiveRect.yMin + hivePadding, hiveRect.yMax - hivePadding);
        hiveTransform.position = GetGroundedPosition(hiveX, hiveZ, hiveHeightOffset);

        quarters.RemoveAt(hiveQuarterIndex);
        flowerManager.SpawnFlowers(quarters);
    }

    private Vector3 GetGroundedPosition(float x, float z, float heightOffset)
    {
        Vector3 rayOrigin = new Vector3(x, maxTerrainHeight, z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxTerrainHeight * 2f, terrainLayerMask))
            return hit.point + Vector3.up * heightOffset;

        Debug.LogWarning("HiveManager: terreno n�o encontrado sob a colmeia, a usar fallback.");
        return new Vector3(x, heightOffset, z);
    }
}