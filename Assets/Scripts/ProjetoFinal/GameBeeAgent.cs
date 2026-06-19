using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class GameBeeAgent : Agent
{
    [Header("Referencias")]
    [HideInInspector] public GameHiveManager hiveManager;
    [HideInInspector] public Transform hiveTransform;
    public MeshRenderer floorRenderer;
    public Transform beeModel;

    [Header("UI")]
    public Slider hungerSlider;
    public bool billboardUI = true;

    [Header("Voo")]
    public LayerMask terrainLayerMask;
    public float minHoverHeight = 0.5f;
    public float maxFlightHeight = 12f;
    public float groundSenseDistance = 40f;

    [Header("Movimento")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 3f;

    [Header("Fome")]
    public float maxHunger = 100f;
    public float hungerRestoreAmount = 40f;
    public float hungerDecayMultiplier = 5f;
    public float hungerDecayRate = 0.01f; // fixed rate, no Academy in game

    [Header("Polinizacao")]
    public float pollinationThreshold = 1.0f;
    public float interactionRadius = 1.2f;
    public float hiveInteractionRadius = 0.8f;

    // Internal state
    [HideInInspector] public BeeSpawner spawner;
    private float hunger;
    private bool hasPollen;
    private float pollinationTimer;
    private bool isDead;
    private FlowerController nearestFlower;
    private Rigidbody rb;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (beeModel == null) beeModel = transform;
    }

    // OnEpisodeBegin fires once on first init since MaxStep = 0
    public override void OnEpisodeBegin()
    {
        hunger = maxHunger;
        hasPollen = false;
        pollinationTimer = 0f;
        isDead = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        StartCoroutine(ZeroVelocityNextFrame());
        UpdateNearestFlower();
    }

    private void Update()
    {
        if (hungerSlider != null)
        {
            hungerSlider.value = hunger;
            if (billboardUI && Camera.main != null)
                hungerSlider.transform.parent.rotation = Camera.main.transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null || isDead) return;
        if (rb.linearVelocity.magnitude > moveSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(hunger / maxHunger);       // 1
        sensor.AddObservation(hasPollen ? 1f : 0f);      // 1

        float honeyRatio = hiveManager != null
            ? Mathf.Clamp01(hiveManager.HoneyStored / 5f) : 0f;
        sensor.AddObservation(honeyRatio);               // 1

        if (nearestFlower != null)
        {
            Vector3 dirFlower = (nearestFlower.transform.position - transform.position).normalized;
            float distFlower = Vector3.Distance(nearestFlower.transform.position, transform.position) / 20f;
            sensor.AddObservation(dirFlower);            // 3
            sensor.AddObservation(distFlower);           // 1
            sensor.AddObservation(nearestFlower.IsCharged ? 1f : 0f); // 1
        }
        else
        {
            sensor.AddObservation(Vector3.zero);         // 3
            sensor.AddObservation(0f);                   // 1
            sensor.AddObservation(0f);                   // 1
        }

        sensor.AddObservation(hasPollen ? 1f : 0f);     // 1

        Vector3 dirHive = (hiveTransform.position - transform.position).normalized;
        float distHive = Vector3.Distance(hiveTransform.position, transform.position) / 20f;
        sensor.AddObservation(dirHive);                  // 3
        sensor.AddObservation(distHive);                 // 1
        sensor.AddObservation(pollinationTimer / pollinationThreshold); // 1
        sensor.AddObservation(GetGroundClearance());     // 1
        sensor.AddObservation(GetCeilingClearance());    // 1
        // Total: 17 — must match training
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDead) return;

        // Hunger decay
        hunger -= hungerDecayRate * Time.deltaTime * hungerDecayMultiplier;
        if (hunger <= 0f)
        {
            Die();
            return;
        }

        if (!hasPollen)
            UpdateNearestFlower();

        // Movement
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        Vector3 moveDir = new Vector3(moveX, moveY, moveZ);

        Vector3 currentPos = rb != null ? rb.position : transform.position;
        Vector3 targetPos = ClampAltitude(currentPos + moveDir * Time.deltaTime * moveSpeed);

        if (rb != null) rb.MovePosition(targetPos);
        else transform.position = targetPos;

        Vector3 horizontalDir = new Vector3(moveX, 0f, moveZ);
        if (horizontalDir.sqrMagnitude > 0.001f && beeModel != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalDir);
            beeModel.rotation = Quaternion.Slerp(beeModel.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        // Interactions
        int interact = actions.DiscreteActions[0];
        bool isNearFlower = false;

        if (nearestFlower != null)
        {
            float distToFlower = Vector3.Distance(transform.position, nearestFlower.transform.position);
            if (distToFlower < interactionRadius)
            {
                isNearFlower = true;
                if (!hasPollen && interact == 1)
                {
                    pollinationTimer += Time.deltaTime;
                    if (pollinationTimer >= pollinationThreshold)
                    {
                        hasPollen = nearestFlower.TryCollectPollen();
                        if (hasPollen)
                        {
                            pollinationTimer = 0f;
                            ReleaseCurrentFlower();
                            nearestFlower = null;
                        }
                    }
                }
                else if (!hasPollen)
                {
                    pollinationTimer = Mathf.Max(0, pollinationTimer - Time.deltaTime * 0.5f);
                }
            }
        }

        if (!isNearFlower) pollinationTimer = 0f;

        if (interact == 1)
        {
            float distToHive = Vector3.Distance(transform.position, hiveTransform.position);
            if (distToHive < hiveInteractionRadius)
            {
                if (hasPollen)
                {
                    hiveManager.DeliverPollen(30f);
                    hasPollen = false;
                }
                else if (hunger <= maxHunger * 0.6f)
                {
                    float eaten = hiveManager.ConsumeHoney(hungerRestoreAmount);
                    if (eaten > 0f)
                        hunger = Mathf.Min(maxHunger, hunger + eaten);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wall"))
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        ReleaseCurrentFlower();

        Debug.Log($"{gameObject.name} died.");
        if (spawner != null)
            spawner.OnBeeDied(this);
    }

    // No rewards needed in game mode — these are here
    // only because Agent requires the override
    public override void Heuristic(in ActionBuffers actionsOut) { }

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
        if (hasPollen)
        {
            ReleaseCurrentFlower();
            nearestFlower = null;
            return;
        }

        // Usar a lista global do WorldBeeManager em vez da do hive local
        List<FlowerController> flowers = WorldBeeManager.Instance != null
            ? WorldBeeManager.Instance.AllFlowers
            : new List<FlowerController>();

        float bestDist = Mathf.Infinity;
        FlowerController bestFlower = null;

        foreach (var fc in flowers)
        {
            if (fc == null || !fc.gameObject.activeSelf || !fc.IsCharged) continue;
            if (fc.ReservedBy != null && fc.ReservedBy != this) continue;
            float d = Vector3.Distance(transform.position, fc.transform.position);
            if (d < bestDist) { bestDist = d; bestFlower = fc; }
        }

        bool currentStillValid = nearestFlower != null && nearestFlower.IsCharged &&
                                 (nearestFlower.ReservedBy == null || nearestFlower.ReservedBy == this);

        if (bestFlower != null)
        {
            float currentDist = nearestFlower != null
                ? Vector3.Distance(transform.position, nearestFlower.transform.position)
                : Mathf.Infinity;

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

    private IEnumerator ZeroVelocityNextFrame()
    {
        yield return new WaitForFixedUpdate();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}