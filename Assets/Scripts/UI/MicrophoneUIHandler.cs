using UnityEngine;

public class MicrophoneUIHandler : MonoBehaviour
{
    [SerializeField] private GameObject microphone;

    private void Awake()
    {
        AudioPitchDetection.onWhistleDetected += OnWhistleDetected;
        AudioPitchDetection.onWhistleStopped += OnWhistleStopped;
    }

    private void Start()
    {
        if (microphone == null)
        {
            Debug.Log("[MicrophoneUIHandler] Microphone GameObject not assigned in the inspector");
        }
    }

    private void OnDestroy()
    {
        AudioPitchDetection.onWhistleDetected -= OnWhistleDetected;
        AudioPitchDetection.onWhistleStopped -= OnWhistleStopped;
    }

    private void OnWhistleDetected(Transform source)
    {
        microphone.SetActive(true);
    }

    private void OnWhistleStopped(Transform source)
    {
        microphone.SetActive(false);
    }
}
