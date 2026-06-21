using UnityEngine;

public class MicrophoneUIHandler : MonoBehaviour
{
    [SerializeField] private GameObject _microphone;

    private void Awake()
    {
        AudioPitchDetection.onWhistleDetected += OnWhistleDetected;
        AudioPitchDetection.onWhistleStopped += OnWhistleStopped;
    }

    private void Start()
    {
        if (_microphone == null)
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
        _microphone.SetActive(true);
    }

    private void OnWhistleStopped(Transform source)
    {
        _microphone.SetActive(false);
    }
}
