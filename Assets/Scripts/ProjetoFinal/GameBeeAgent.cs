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
    public Transform beeModel;

    private SkinnedMeshRenderer[] beeRenderers;
    public Material normalMaterial;
    public Material pollenMaterial;

    [Header("UI")]
    public List<Toggle> toggles = new List<Toggle>();
    public GameObject hungerBar;
    public bool billboardUI = true;

    [Header("Visual Smoothing")]
    [Tooltip("How quickly the visual model catches up to the real position. Higher = snappier.")]
    public float visualSmoothSpeed = 8f;
    [Tooltip("How quickly the visual model's rotation catches up. Higher = snappier.")]
    public float visualRotationSmoothSpeed = 10f;

    [Header("Hive Proximity Visibility")]
    [Tooltip("When the bee is closer than this distance to its hive, visuals are hidden.")]
    public float hiveHideRadius = 2f;

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
    private Vector3 lastPosition;
    private float stuckTimer;

    // Visual smoothing state
    private Vector3 smoothVisualPosition;
    private Quaternion smoothVisualRotation;
    private bool beeModelIsChild = true;
    private bool visualsHidden;
    private float reappearCooldown;
    private const float ReappearDelay = 1f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (beeModel == null) beeModel = transform;
        beeRenderers = beeModel.GetComponentsInChildren<SkinnedMeshRenderer>();

        // Detach the visual model from the agent so its position can be smoothed independently.
        // Only do this if beeModel is an actual child (not transform itself).
        beeModelIsChild = (beeModel != transform && beeModel.parent == transform);
        if (beeModelIsChild)
        {
            beeModel.SetParent(null, true);
        }

        smoothVisualPosition = transform.position;
        smoothVisualRotation = beeModel.rotation;

        // Allow bees to pass through leaf blocks — ignore collisions between Bee and Leaves layers
        int beeLayer = LayerMask.NameToLayer("Bee");
        int leavesLayer = LayerMask.NameToLayer("Leaves");
        if (beeLayer >= 0 && leavesLayer >= 0)
            Physics.IgnoreLayerCollision(beeLayer, leavesLayer, true);
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
        lastPosition = transform.position;
        stuckTimer = 0f;

        // Snap visual to real position on episode start
        smoothVisualPosition = transform.position;
        smoothVisualRotation = beeModel != null ? beeModel.rotation : transform.rotation;
    }

    private void SetPollenMaterial(bool carrying)
    {
        if (beeRenderers == null || normalMaterial == null || pollenMaterial == null) return;
        Material mat = carrying ? pollenMaterial : normalMaterial;
        foreach (var smr in beeRenderers)
            smr.material = mat;
    }
    private void Update()
    {
        // --- Visual smoothing ---
        if (beeModelIsChild && beeModel != null)
        {
            smoothVisualPosition = Vector3.Lerp(smoothVisualPosition, transform.position, Time.deltaTime * visualSmoothSpeed);
            beeModel.position = smoothVisualPosition;

            // Smooth rotation is driven by OnActionReceived writing to beeModel.rotation via Slerp already,
            // but since beeModel is now detached, we also smoothly blend toward the agent-driven target rotation.
            smoothVisualRotation = Quaternion.Slerp(smoothVisualRotation, beeModel.rotation, Time.deltaTime * visualRotationSmoothSpeed);
            // beeModel.rotation is already set by OnActionReceived, so we just keep position smooth.
        }

        // --- Hive proximity visibility ---
        if (hiveTransform != null)
        {
            float distToHive = Vector3.Distance(transform.position, hiveTransform.position);
            bool shouldHide = distToHive < hiveHideRadius;

            if (shouldHide && !visualsHidden)
            {
                // Hide immediately when entering radius
                visualsHidden = true;
                reappearCooldown = 0f;
                SetVisualsActive(false);
            }
            else if (!shouldHide && visualsHidden)
            {
                // Delay reappearing by cooldown
                reappearCooldown += Time.deltaTime;
                if (reappearCooldown >= ReappearDelay)
                {
                    visualsHidden = false;
                    reappearCooldown = 0f;
                    SetVisualsActive(true);
                }
            }
        }

        // --- Hunger bar UI ---
        if (hungerBar != null)
        {
            int numOfFood = toggles.Count;
            float hungerPerSegment = maxHunger / numOfFood;

            for (int i = 0; i < numOfFood; i++)
            {
                toggles[i].isOn = hunger >= hungerPerSegment * i;
            }

            if (billboardUI && Camera.main != null)
            {
                Vector3 dirToCamera = hungerBar.transform.parent.position - Camera.main.transform.position;
                dirToCamera.y = 0f;
                if (dirToCamera != Vector3.zero)
                    hungerBar.transform.parent.rotation = Quaternion.LookRotation(dirToCamera);
            }
        }
    }

    /// <summary>
    /// Toggles all visual elements (renderers + hunger bar) on or off.
    /// </summary>
    private void SetVisualsActive(bool active)
    {
        if (beeRenderers != null)
        {
            foreach (var smr in beeRenderers)
            {
                if (smr != null) smr.enabled = active;
            }
        }
        if (hungerBar != null)
            hungerBar.SetActive(active);
    }

    private void FixedUpdate()
    {
        if (rb == null || isDead) return;
        if (rb.linearVelocity.magnitude > moveSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(isDead ? 1f : 0f);         // 1
        sensor.AddObservation(hunger / maxHunger);       // 1
        sensor.AddObservation(hasPollen ? 1f : 0f);      // 1

        float honeyRatio = hiveManager != null
            ? Mathf.Clamp01(hiveManager.HoneyStored / 5f) : 0f;
        sensor.AddObservation(honeyRatio);               // 1

        if (nearestFlower != null && !isDead)
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

        if (hiveTransform != null)
        {
            Vector3 dirHive = (hiveTransform.position - transform.position).normalized;
            float distHive = Vector3.Distance(hiveTransform.position, transform.position) / 20f;
            sensor.AddObservation(dirHive);                  // 3
            sensor.AddObservation(distHive);                 // 1
        }
        else
        {
            sensor.AddObservation(Vector3.zero);             // 3
            sensor.AddObservation(0f);                       // 1
        }

        sensor.AddObservation(pollinationTimer / pollinationThreshold); // 1
        sensor.AddObservation(GetGroundClearance());     // 1
        sensor.AddObservation(GetCeilingClearance());    // 1
        // Total: 17 -- must match training
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

        // Unstuck nudge logic: if trying to move but stuck, nudge up and sideways to clear blocks
        if (moveDir.sqrMagnitude > 0.01f)
        {
            float distMoved = Vector3.Distance(currentPos, lastPosition);
            if (distMoved < 0.1f * Time.deltaTime * moveSpeed)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 0.4f)
                {
                    // Calculate a sideways vector relative to movement direction to slip past corners
                    Vector3 slideDir = new Vector3(-moveDir.z, 0f, moveX).normalized;
                    Vector3 nudge = (Vector3.up * 2f + slideDir * 1f) * Time.deltaTime;
                    currentPos += nudge;
                    if (rb != null) rb.position = currentPos;
                    else transform.position = currentPos;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPosition = currentPos;

        Vector3 targetPos = ClampAltitude(currentPos + moveDir * Time.deltaTime * moveSpeed);

        if (rb != null) rb.MovePosition(targetPos);
        else transform.position = targetPos;

        Vector3 horizontalDir = new Vector3(moveX, 0f, moveZ);
        if (horizontalDir.sqrMagnitude > 0.001f && beeModel != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalDir);
            // Smooth rotation — beeModel may be detached, but we still drive its rotation here
            smoothVisualRotation = Quaternion.Slerp(smoothVisualRotation, targetRot, Time.deltaTime * rotationSpeed);
            beeModel.rotation = smoothVisualRotation;
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
                            SetPollenMaterial(true);
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

        if (interact == 1 && hiveTransform != null)
        {
            float distToHive = Vector3.Distance(transform.position, hiveTransform.position);
            if (distToHive < hiveInteractionRadius)
            {
                if (hasPollen)
                {
                    hiveManager.DeliverPollen(30f);
                    hasPollen = false;
                    SetPollenMaterial(false);
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

        // Hide visuals on death
        SetVisualsActive(false);

        Debug.Log($"{gameObject.name} died.");
        if (spawner != null)
            spawner.OnBeeDied(this);
    }

    private void OnDestroy()
    {
        // Clean up the detached beeModel when the agent is destroyed
        if (beeModelIsChild && beeModel != null)
            Destroy(beeModel.gameObject);
    }

    public override void Heuristic(in ActionBuffers actionsOut) { }

    private Vector3 ClampAltitude(Vector3 worldPos)
    {
        // Start the raycast slightly above the bee's current Y position (e.g., 0.5f units)
        // to detect the block directly underneath, instead of starting from high above.
        // This prevents the raycast from hitting tree leaves or ceilings overhead.
        Vector3 rayOrigin = new Vector3(worldPos.x, worldPos.y + 0.5f, worldPos.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundSenseDistance, terrainLayerMask))
        {
            float groundY = hit.point.y;
            float minH = minHoverHeight;
            float maxH = maxFlightHeight;

            // Determine if the bee is trying to reach an active target (hive or flower)
            Transform activeTarget = null;
            if (hasPollen || hunger <= maxHunger * 0.6f)
            {
                if (hiveTransform != null)
                    activeTarget = hiveTransform;
            }
            else if (nearestFlower != null)
            {
                activeTarget = nearestFlower.transform;
            }

            if (activeTarget != null)
            {
                float horizontalDist = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.z), 
                    new Vector2(activeTarget.position.x, activeTarget.position.z)
                );

                // If close horizontally, assist the bee vertically to reach the target's height
                if (horizontalDist < 3.0f)
                {
                    float targetYPos = activeTarget.position.y;
                    float targetHeightOffset = targetYPos - groundY;
                    
                    // Temporarily expand flight bounds to accommodate target height
                    if (targetHeightOffset > maxH) maxH = targetHeightOffset + 0.5f;
                    if (targetHeightOffset < minH) minH = Mathf.Max(0.1f, targetHeightOffset - 0.5f);

                    // Pull the bee's height towards the target height
                    worldPos.y = Mathf.MoveTowards(worldPos.y, targetYPos, Time.deltaTime * 3f);
                }
            }

            float targetY = Mathf.Clamp(worldPos.y, groundY + minH, groundY + maxH);
            worldPos.y = Mathf.MoveTowards(worldPos.y, targetY, Time.deltaTime * 5f);
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