using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using UnityEngine.UI;

/**
 * Agente de Reinforcement Learning que simula uma abelha.
 * Responsável por navegar no ambiente, recolher pólen das flores, 
 * depositar na colmeia e gerir o seu próprio nível de fome.
 */
public class BeeAgent : Agent
{
    [Header("Referencias da Cena")]
    /** Gestor global da colmeia. */
    public HiveManager hiveManager;
    /** Referência espacial para a colmeia. */
    public Transform hiveTransform;
    /** Referência do chão para alterar o feedback visual. */
    public MeshRenderer floorRenderer;
    /** Modelo visual específico da abelha para aplicar rotações independentes da raiz. */
    public Transform beeModel;

    [Header("UI")]
    /** Barra de progresso visual para a fome da abelha. */
    public Slider hungerSlider;
    /** Define se a interface visual deve rodar para acompanhar a câmara. */
    public bool billboardUI = true;

    [Header("Feedback Visual")] 
    public Material defaultMaterial;
    public Material winMaterial;
    public Material loseMaterial;

    [Header("Parametros de Movimento")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 1f;

    [Header("Parametros de Fome")]
    /** Nível máximo de fome suportado pela abelha. */
    public float maxHunger = 100f;
    /** Quantidade de fome restaurada ao consumir mel na colmeia. */
    public float hungerRestoreAmount = 40f;
    /** Multiplicador da taxa de decaimento da fome. */
    public float hungerDecayMultiplier = 5f;

    [Header("Parametros de Polinizacao")]
    /** Tempo necessário (em segundos) a interagir com uma flor para recolher pólen. */
    public float pollinationThreshold = 1.0f;
    /** Raio máximo para conseguir interagir com uma flor. */
    public float interactionRadius = 1.2f;
    /** Raio máximo para conseguir interagir com a colmeia. */
    public float hiveInteractionRadius = 0.8f;

    [Header("Estado Atual")]
    [SerializeField] private float hunger;
    private bool hasPollen;
    private float hungerDecayRate;
    private float pollinationTimer = 0f;
    private bool finished = false;

    /** Referência temporária para a flor alvo mais próxima. */
    private FlowerController nearestFlower;

    /**
     * Regista a abelha no gestor da colmeia e inicializa o modelo visual.
     */
    public override void Initialize()
    {
        if (hiveManager != null)
            hiveManager.RegisterBee(this);
        if (beeModel == null) beeModel = transform;
    }

    /**
     * Atualiza a interface gráfica do utilizador a cada frame.
     * Sincroniza a barra de fome e aplica o efeito de billboard caso ativado.
     */
    private void Update()
    {
        if (hungerSlider != null)
        {
            hungerSlider.value = hunger;

            if (billboardUI && Camera.main != null)
            {
                hungerSlider.transform.parent.rotation = Camera.main.transform.rotation;
            }
        }
    }

    /**
     * Prepara o agente para um novo episódio.
     * Repõe o estado da fome, inventário, contadores de tempo e reposiciona o agente.
     */
    public override void OnEpisodeBegin()
    {
        hungerDecayRate = (float)Academy.Instance.EnvironmentParameters
            .GetWithDefault("hunger_decay_rate", 0.01f);

        hasPollen = false;
        pollinationTimer = 0f;
        finished = false;
        hunger = maxHunger;

        if (hiveManager != null)
        {
            hiveManager.NotifyBeeReset(this);
        }

        ReleaseCurrentFlower();
        nearestFlower = null;

        if (hiveTransform != null)
        {
            transform.position = hiveTransform.position;
        }

        if (floorRenderer != null)
            floorRenderer.material = defaultMaterial;

        if (hiveManager != null)
        {
            foreach (var fc in hiveManager.AllFlowers)
                fc.ResetFlower();
        }

        UpdateNearestFlower();
    }

    /**
     * Recolhe as observações do ambiente para alimentar a rede neuronal.
     * Transmite estados internos, estado da colmeia e vetores espaciais relativos (flor e colmeia).
     * * @param sensor: Sensor utilizado para empilhar observações vetoriais contínuas.
     */
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(finished ? 1f : 0f); // 1
        sensor.AddObservation(hunger / maxHunger);  // 1
        sensor.AddObservation(hasPollen ? 1f : 0f); // 1
        
        float honeyRatio = 0f;
        if (hiveManager != null)
            honeyRatio = Mathf.Clamp01(hiveManager.HoneyStored / 5f);
        sensor.AddObservation(honeyRatio); // 1

        if (nearestFlower != null && !finished)
        {
            Vector3 dirFlower = (nearestFlower.transform.position - transform.position).normalized;
            float distFlower = Vector3.Distance(nearestFlower.transform.position, transform.position) / 20f;
            sensor.AddObservation(dirFlower);   // 3
            sensor.AddObservation(distFlower);  // 1
            sensor.AddObservation(nearestFlower.IsCharged ? 1f : 0f); // 1
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // 3
            sensor.AddObservation(0f);           // 1
            sensor.AddObservation(0f);           // 1
        }
        
        sensor.AddObservation(hasPollen ? 1f : 0f); // 1

        Vector3 dirHive = (hiveTransform.position - transform.position).normalized;
        float distHive = Vector3.Distance(hiveTransform.position, transform.position) / 20f;
        sensor.AddObservation(dirHive);                  // 3
        sensor.AddObservation(distHive);                 // 1
        sensor.AddObservation(pollinationTimer / pollinationThreshold); // 1
        // Total: 16
    }

    /**
     * Processa as ações tomadas pelo agente (via rede neuronal ou input manual), movendo o objeto,
     * rodando o modelo e lidando com o sistema de recolha e entrega de recursos/alimento.
     * * @param actions: Buffers contendo as decisões contínuas e discretas.
     */
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (finished) return;

        hunger -= hungerDecayRate * Time.deltaTime * hungerDecayMultiplier;
        if (hunger <= 0f)
        {
            AddReward(-1f);
            if (floorRenderer != null) floorRenderer.material = loseMaterial;
            finished = true;
            hiveManager.BeeDone();
            return;
        }

        if (!hasPollen)
        {
            UpdateNearestFlower();
        }

        AddReward(-1f / MaxStep);

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        Vector3 moveDir = new Vector3(moveX, 0f, moveZ);
        
        transform.localPosition += moveDir * Time.deltaTime * moveSpeed;

        // Rodar o modelo ao invés da raíz
        if (moveDir.sqrMagnitude > 0.001f && beeModel != null)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            beeModel.rotation = Quaternion.Slerp(beeModel.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        int interact = actions.DiscreteActions[0];

        bool isNearFlower = false;
        if (nearestFlower != null)
        {
            float distToFlower = Vector3.Distance(transform.position, nearestFlower.transform.position);
            if (distToFlower < interactionRadius)
            {
                isNearFlower = true;
                if (!hasPollen)
                {
                    if (interact == 1)
                    {
                        pollinationTimer += Time.deltaTime;
                        if (pollinationTimer >= pollinationThreshold)
                        {
                            hasPollen = nearestFlower.TryCollectPollen();
                            if (hasPollen)
                            {
                                AddReward(+0.3f);
                                pollinationTimer = 0f;
                                ReleaseCurrentFlower();
                                nearestFlower = null; 
                            }
                        }
                    }
                    else
                    {
                        pollinationTimer = Mathf.Max(0, pollinationTimer - Time.deltaTime * 0.5f);
                    }
                }
                else
                {
                    AddReward(-0.001f);
                }
            }
        }

        if (!isNearFlower)
        {
            pollinationTimer = 0f;
        }

        if (interact == 1)
        {
            float distToHive = Vector3.Distance(transform.position, hiveTransform.position);
            if (distToHive < hiveInteractionRadius)
            {
                if (hasPollen)
                {
                    hiveManager.DeliverPollen(30f);
                    hasPollen = false;
                    AddReward(+1f);
                    if (floorRenderer != null) floorRenderer.material = winMaterial;
                }
                else
                {
                    if (hunger <= maxHunger * 0.6f)
                    {
                        float eaten = hiveManager.ConsumeHoney(hungerRestoreAmount);
                        if (eaten > 0f)
                        {
                            float oldHunger = hunger;
                            hunger = Mathf.Min(maxHunger, hunger + eaten);
                            AddReward(+0.3f);
                            Debug.Log($"{gameObject.name}: I ate honey! Hunger: {oldHunger:F1} -> {hunger:F1}");
                        }
                    }
                }
            }
        }
    }

    /**
     * Processa as interações com colisores definidos como 'Trigger'.
     * Aplica uma penalidade caso a abelha colida com as paredes e termina o seu percurso.
     * * @param other: Colisor com o qual o agente colidiu.
     */
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wall"))
        {
            AddReward(-1.5f);
            if (floorRenderer != null) floorRenderer.material = loseMaterial;
            finished = true;
            hiveManager.BeeDone();
        }
    }

    /**
     * Define o controlo manual da abelha para permitir testes heurísticos.
     * * @param actionsOut: Buffer de ações a ser populado com dados oriundos dos inputs do teclado.
     */
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");

        var da = actionsOut.DiscreteActions;
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    /**
     * Identifica e reserva a flor carregada e não-reservada mais próxima do agente.
     * Liberta reservas anteriores caso encontre um alvo mais adequado ou não necessite de procurar.
     */
    private void UpdateNearestFlower()
    {
        if (hiveManager == null || hasPollen) 
        {
            ReleaseCurrentFlower();
            nearestFlower = null;
            return;
        }

        List<FlowerController> flowers = hiveManager.AllFlowers;
        float bestDist = Mathf.Infinity;
        FlowerController bestFlower = null;

        foreach (var fc in flowers)
        {
            if (fc == null || !fc.gameObject.activeSelf || !fc.IsCharged) continue;
            if (fc.ReservedBy != null && fc.ReservedBy != this) continue;

            float d = Vector3.Distance(transform.position, fc.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                bestFlower = fc;
            }
        }

        bool currentStillValid = nearestFlower != null && 
                                nearestFlower.IsCharged && 
                                (nearestFlower.ReservedBy == null || nearestFlower.ReservedBy == this);

        if (bestFlower != null)
        {
            float currentDist = (nearestFlower != null) ? Vector3.Distance(transform.position, nearestFlower.transform.position) : Mathf.Infinity;
            
            if (!currentStillValid || bestDist < currentDist - 0.5f)
            {
                ReleaseCurrentFlower();
                nearestFlower = bestFlower;
                nearestFlower.ReservedBy = this;
            }
        }
        else if (!currentStillValid)
        {
            ReleaseCurrentFlower();
            nearestFlower = null;
        }
    }

    /**
     * Liberta a reserva lógica sobre a flor atual, 
     * tornando-a disponível para outros agentes da colmeia.
     */
    private void ReleaseCurrentFlower()
    {
        if (nearestFlower != null && nearestFlower.ReservedBy == this)
        {
            nearestFlower.ReservedBy = null;
        }
    }
}
