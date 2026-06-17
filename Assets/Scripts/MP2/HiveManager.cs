using UnityEngine;
using System.Collections.Generic;

public class HiveManager : MonoBehaviour
{
    /**
     * Gestor global da arena.
     * Coordena o inventário partilhado de mel, a lista de agentes ativos, as instâncias 
     * globais das flores e gere o ciclo de reinicialização espacial e estatística da arena.
     */
    private void Awake()
    {
        // Singleton removido para suportar múltiplas arenas
    }

    [Header("Referencias de Spawning")]
    [SerializeField] private FlowerManager flowerManager;
    [SerializeField] private Transform hiveTransform;
    [SerializeField] private MeshRenderer floorRenderer;

    [Header("Estado")]
    [SerializeField] private float honeyStored = 0f;
    private List<BeeAgent> registeredBees = new List<BeeAgent>();
    private List<FlowerController> allFlowers = new List<FlowerController>();
    private int beesDoneCount = 0;
    private int beesResetCount = 0;
    private bool arenaAlreadyReset = false;

    /** Reserva global de mel da colmeia. */
    public float HoneyStored => honeyStored;
    /** Número total de abelhas registadas nesta arena. */
    public int BeeCount => registeredBees.Count;
    /** Lista de referência para todas as flores ativas na arena. */
    public List<FlowerController> AllFlowers => allFlowers;

    /**
     * Invocado no arranque da cena. Força o primeiro preenchimento da arena.
     */
    private void Start()
    {
        ResetHive();
    }

    /**
     * Adiciona mel à reserva coletiva quando uma abelha entrega pólen.
     * ´* @param amount: Quantidade de pólen depositada.
     */
    public void DeliverPollen(float amount)
    {
        honeyStored += amount;
        Debug.Log($"HiveManager: Honey delivered! Total stored: {honeyStored:F2}");
    }

    /**
     * Processa a tentativa de uma abelha consumir mel da reserva global.
     * * @param amount: Quantidade ideal que o agente deseja consumir.
     * * @return: Quantidade que foi efetivamente consumida (pode ser inferior se não houver mel suficiente).
     */
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

    /**
     * Regista uma nova abelha para acompanhamento do ciclo de vida.
     * * @param bee: Instância do BeeAgent a registar.
     */
    public void RegisterBee(BeeAgent bee)
    {
        if (!registeredBees.Contains(bee))
            registeredBees.Add(bee);
    }

    /**
     * Atualiza a lista global de flores disponíveis na arena.
     * * @param flowers: Nova lista de FlowerControllers.
     */
    public void SetFlowers(List<FlowerController> flowers)
    {
        allFlowers.Clear();
        allFlowers.AddRange(flowers);
    }

    /**
     * Regista que uma abelha concluiu a sua execução (por morte ou falha).
     * Se todas as abelhas falharem, força o reinício global da arena.
     */
    public void BeeDone()
    {
        beesDoneCount++;
        Debug.Log($"HiveManager: Bee finished/died. {beesDoneCount}/{registeredBees.Count} bees done.");

        if (beesDoneCount >= registeredBees.Count)
        {
            if (!arenaAlreadyReset)
            {
                Debug.Log("HiveManager: All bees finished/dead! Resetting arena...");
                ResetHive();
                arenaAlreadyReset = true;
            }
            
            foreach (var bee in registeredBees)
            {
                bee.EndEpisode();
            }
        }
    }

    /**
     * Regista um pedido de reinicialização originado por um timeout (atingiu o MaxSteps no ML-Agents).
     * Sincroniza a reinicialização física da arena caso todas as abelhas tenham expirado simultaneamente.
     * * @param bee: Instância do agente que foi reposto.
     */
    public void NotifyBeeReset(BeeAgent bee)
    {
        beesResetCount++;
        
        // If all bees have reset (whether by death or timeout)
        if (beesResetCount >= registeredBees.Count)
        {
            // If the arena wasn't reset by BeeDone (timeout case), reset it now
            if (!arenaAlreadyReset)
            {
                Debug.Log("HiveManager: Timeout reset detected (all bees reset without death). Randomizing arena...");
                ResetHive();
            }

            // Start of a new cycle
            arenaAlreadyReset = false;
            beesResetCount = 0;
            beesDoneCount = 0;
        }
    }

    /**
     * Reinicializa fisicamente a arena.
     * Zera o inventário global, calcula os quadrantes de spawn com base nas dimensões do chão,
     * recoloca a colmeia e aciona a reinstanciação dinâmica de todas as flores.
     */
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

        // 1. Calculate quarters based on floor bounds with a safety margin
        Bounds bounds = floorRenderer.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        // Shrink the effective area to avoid spawning on/outside walls
        float margin = 1.5f; 
        float usableWidth = size.x - (margin * 2);
        float usableDepth = size.z - (margin * 2);
        float halfUsableWidth = usableWidth / 2f;
        float halfUsableDepth = usableDepth / 2f;

        // Origin of the usable rectangle (bottom-left)
        float startX = center.x - halfUsableWidth;
        float startZ = center.z - halfUsableDepth;

        List<Rect> quarters = new List<Rect>
        {
            new Rect(startX, center.z, halfUsableWidth, halfUsableDepth),                   // Top-Left
            new Rect(center.x, center.z, halfUsableWidth, halfUsableDepth),                 // Top-Right
            new Rect(startX, startZ, halfUsableWidth, halfUsableDepth),                     // Bottom-Left
            new Rect(center.x, startZ, halfUsableWidth, halfUsableDepth)                    // Bottom-Right
        };

        // 2. Choose Hive quarter
        int hiveQuarterIndex = Random.Range(0, 4);
        Rect hiveRect = quarters[hiveQuarterIndex];

        // 3. Spawn Hive in its quarter with extra internal padding
        float hivePadding = 0.5f;
        hiveTransform.position = new Vector3(
            Random.Range(hiveRect.xMin + hivePadding, hiveRect.xMax - hivePadding),
            hiveTransform.position.y,
            Random.Range(hiveRect.yMin + hivePadding, hiveRect.yMax - hivePadding)
        );

        // 4. Spawn Flowers in other quarters
        quarters.RemoveAt(hiveQuarterIndex);
        flowerManager.SpawnFlowers(quarters);
    }
}
