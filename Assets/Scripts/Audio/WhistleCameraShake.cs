using UnityEngine;
using Cinemachine;

public class WhistleCameraShake : MonoBehaviour
{
    [Header("Camera")]
    public CinemachineVirtualCamera targetVCam;

    [Header("Definições do agitar")]
    public float amplitudeGain = 1.5f;
    public float frequencyGain = 1.5f;
    public float fadeSpeed = 8f;

    private CinemachineBasicMultiChannelPerlin perlin;
    private float currentStrength;
    private float targetStrength;

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