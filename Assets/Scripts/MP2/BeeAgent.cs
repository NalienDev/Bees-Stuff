using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using UnityEngine.UI;

public class BeeAgent : Agent
{
    [Header("Referencias da Cena")]
    public HiveManager hiveManager;
    public Transform hiveTransform;
    public MeshRenderer floorRenderer;
    public Transform beeModel;

    [Header("UI")]
    public Slider hungerSlider;
    public bool billboardUI = true;

    [Header("Feedback Visual")]
    public Material defaultMaterial;
    public Material winMaterial;
    public Material loseMaterial;

    [Header("Parametros de Movimento")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 1f;

    [Header("Parametros de Voo")]
    /** Camadas consideradas "terreno" para efeitos de altura e colisão. */
    public LayerMask terrainLayerMask;
    /** Altura mínima que a abelha mantém acima do terreno por baixo de si. */
    public float minHoverHeight = 0.5f;
    /** Altura máxima permitida acima do terreno por baixo de si. */
    public float maxFlightHeight = 12f;
    /** Distância máxima usada para "sentir" o solo/teto por raycast. */
    public float groundSenseDistance = 40f;

    [Header("Parametros de Fome")]
    public float maxHunger = 100f;
    public float hungerRestoreAmount = 40f;
    public float hungerDecayMultiplier = 5f;

    [Header("Parametros de Polinizacao")]
    public float pollinationThreshold = 1.0f;
    public float interactionRadius = 1.2f;
    public float hiveInteractionRadius = 0.8f;

    [Header("Estado Atual")]
    [SerializeField] private float hunger;
    private bool hasPollen;
    private float hungerDecayRate;
    private float pollinationTimer = 0f;
    private bool finished = false;

    private FlowerController nearestFlower;
    private Rigidbody rb;

    public override void Initialize()
    {
        if (hiveManager != null)
            hiveManager.RegisterBee(this);
        if (beeModel == null) beeModel = transform;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"{gameObject.name}: BeeAgent precisa de um Rigidbody para voar com colisão de terreno.");
        }
        else
        {
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

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

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Hard cap on velocity - prevents any physics impulse from
        // sending the bee flying at uncontrolled speeds
        if (rb.linearVelocity.magnitude > moveSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
        }
    }

    public override void OnEpisodeBegin()
    {
        hungerDecayRate = (float)Academy.Instance.EnvironmentParameters
            .GetWithDefault("hunger_decay_rate", 0.01f);

        hasPollen = false;
        pollinationTimer = 0f;
        finished = false;
        hunger = maxHunger;

        if (hiveManager != null)
            hiveManager.NotifyBeeReset(this);

        ReleaseCurrentFlower();
        nearestFlower = null;

        if (hiveTransform != null)
        {
            if (rb != null)
            {
                rb.position = hiveTransform.position;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                transform.position = hiveTransform.position;
            }
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
        sensor.AddObservation(dirHive);   // 3
        sensor.AddObservation(distHive);  // 1
        sensor.AddObservation(pollinationTimer / pollinationThreshold); // 1

        sensor.AddObservation(GetGroundClearance());  // 1
        sensor.AddObservation(GetCeilingClearance()); // 1
        // Total: 17
    }

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
            UpdateNearestFlower();

        AddReward(-1f / MaxStep);

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        Vector3 moveDir = new Vector3(moveX, moveY, moveZ);

        Vector3 currentPos = rb != null ? rb.position : transform.position;
        Vector3 targetPos = ClampAltitude(currentPos + moveDir * Time.deltaTime * moveSpeed);

        if (rb != null) rb.MovePosition(targetPos);
        else transform.position = targetPos;

        // Rodar o modelo (apenas yaw) com base no movimento horizontal
        Vector3 horizontalDir = new Vector3(moveX, 0f, moveZ);
        if (horizontalDir.sqrMagnitude > 0.001f && beeModel != null)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDir);
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
            pollinationTimer = 0f;

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
                else if (hunger <= maxHunger * 0.6f)
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

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f); // subir/descer
        ca[2] = Input.GetAxis("Vertical");

        var da = actionsOut.DiscreteActions;
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    /**
     * Mantém a posição alvo dentro de uma faixa de altura segura relativa ao terreno
     * imediatamente abaixo. Se não houver terreno detetável (ex: sobre um abismo), 
     * a altura não é alterada nesse frame.
     */
    private Vector3 ClampAltitude(Vector3 worldPos)
    {
        Vector3 rayOrigin = new Vector3(worldPos.x, worldPos.y + groundSenseDistance, worldPos.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundSenseDistance * 2f, terrainLayerMask))
        {
            float groundY = hit.point.y;
            worldPos.y = Mathf.Clamp(worldPos.y, groundY + minHoverHeight, groundY + maxFlightHeight);
        }
        return worldPos;
    }

    private float GetGroundClearance()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundSenseDistance, terrainLayerMask))
            return Mathf.Clamp01(hit.distance / groundSenseDistance);
        return 1f;
    }

    private float GetCeilingClearance()
    {
        if (Physics.Raycast(transform.position, Vector3.up, out RaycastHit hit, groundSenseDistance, terrainLayerMask))
            return Mathf.Clamp01(hit.distance / groundSenseDistance);
        return 1f;
    }

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

    private void ReleaseCurrentFlower()
    {
        if (nearestFlower != null && nearestFlower.ReservedBy == this)
            nearestFlower.ReservedBy = null;
    }
}