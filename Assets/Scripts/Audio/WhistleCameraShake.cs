using UnityEngine;
using Cinemachine;

public class WhistleCameraShake : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The virtual camera to shake. Leave empty to auto-find one in the scene.")]
    public CinemachineVirtualCamera targetVCam;

    [Header("Shake settings")]
    public float amplitudeGain = 1.5f;
    public float frequencyGain = 1.5f;
    [Tooltip("How fast the shake ramps in/out (higher = snappier)")]
    public float fadeSpeed = 8f;

    private CinemachineBasicMultiChannelPerlin perlin;
    private float currentStrength;
    private float targetStrength;

    private void Awake()
    {
        if (targetVCam == null)
        {
            targetVCam = FindFirstObjectByType<CinemachineVirtualCamera>();
        }

        if (targetVCam != null)
        {
            perlin = targetVCam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            if (perlin == null)
            {
                Debug.LogWarning("WhistleCameraShake: no CinemachineBasicMultiChannelPerlin found on "
                    + targetVCam.name + ". Add Extension > CinemachineBasicMultiChannelPerlin "
                    + "and assign a Noise Profile in its inspector.");
            }
        }
        else
        {
            Debug.LogWarning("WhistleCameraShake: no CinemachineVirtualCamera found/assigned.");
        }
    }

    private void OnEnable()
    {
        AudioPitchDetection.onWhistleDetected += HandleWhistleDetected;
        AudioPitchDetection.onWhistleStopped += HandleWhistleStopped;
    }

    private void OnDisable()
    {
        AudioPitchDetection.onWhistleDetected -= HandleWhistleDetected;
        AudioPitchDetection.onWhistleStopped -= HandleWhistleStopped;
    }

    private void HandleWhistleDetected(Transform whistler)
    {
        targetStrength = 1f;
    }

    private void HandleWhistleStopped(Transform whistler)
    {
        targetStrength = 0f;
    }

    private void Update()
    {
        if (perlin == null) return;

        currentStrength = Mathf.MoveTowards(currentStrength, targetStrength, fadeSpeed * Time.deltaTime);

        perlin.m_AmplitudeGain = amplitudeGain * currentStrength;
        perlin.m_FrequencyGain = frequencyGain * currentStrength;
    }
}