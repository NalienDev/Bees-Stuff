using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeeWhistleFlee : MonoBehaviour
{
    [Header("Detecao")]
    public float detectionRadius = 8f;

    [Header("Fugir")]
    public float fleeAngle = 45f;
    public float fleeSpeed = 10f;

    [Header("Diminuir")]
    public float shrinkDelay = 1f;
    public float shrinkDuration = 1.5f;

    private Rigidbody rb;
    private GameBeeAgent agent;
    private bool isFleeing;
    private AudioSource audioSource;
    private AudioClip fleeClip;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        fleeClip = Resources.Load<AudioClip>("Sounds/bee_flee");
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<GameBeeAgent>();
    }

    private void OnEnable()
    {
        AudioPitchDetection.onWhistleDetected += HandleWhistleDetected;
    }

    private void OnDisable()
    {
        AudioPitchDetection.onWhistleDetected -= HandleWhistleDetected;
    }

    private void Update()
    {
        if (isFleeing && agent != null && agent.beeModel != null && agent.beeModel != transform)
        {
            agent.beeModel.position = transform.position;
            agent.beeModel.rotation = transform.rotation;
        }
    }

    private void HandleWhistleDetected(Transform whistleSource)
    {
        if (isFleeing || agent.visualsHidden) return;

        if (Vector3.Distance(transform.position, whistleSource.position) > detectionRadius)
            return;

        StartFleeing(whistleSource);
    }

    private void StartFleeing(Transform whistleSource)
    {
        isFleeing = true;
        audioSource.Stop();
        audioSource.clip = fleeClip;
        audioSource.loop = false;
        audioSource.spatialBlend = 0;
        audioSource.Play();

        if (agent != null) agent.enabled = false;

        Vector3 horizontalLook = whistleSource.forward;
        horizontalLook.y = 0f;
        if (horizontalLook.sqrMagnitude < 0.0001f) horizontalLook = transform.forward;
        horizontalLook.Normalize();

        float angleRad = fleeAngle * Mathf.Deg2Rad;
        Vector3 fleeDirection = (Mathf.Cos(angleRad) * horizontalLook + Mathf.Sin(angleRad) * Vector3.up).normalized;

        rb.linearVelocity = fleeDirection * fleeSpeed;
        transform.rotation = Quaternion.LookRotation(fleeDirection);

        StartCoroutine(ShrinkAndDestroy());
    }

    private IEnumerator ShrinkAndDestroy()
    {
        yield return new WaitForSeconds(shrinkDelay);

        Vector3 startScale = transform.localScale;

        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        Destroy(gameObject);
    }
}