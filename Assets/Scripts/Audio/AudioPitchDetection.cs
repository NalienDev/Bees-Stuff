using System;
using UnityEngine;

public class AudioPitchDetection : MonoBehaviour
{
    [Header("Microfone")]
    public int sampleWindow = 1024;

    [Header("Range de assobío")]
    public float minWhistleFreq = 1000f;
    public float maxWhistleFreq = 5000f;

    [Header("Minimo loudness")]
    public float minRms = 0.02f;

    [Header("Range de procura de pitch")]
    public float minDetectableFreq = 300f;
    public float maxDetectableFreq = 8000f;

    [Header("Estabilidade")]
    public int requiredConsecutiveFrames = 3;

    public static event Action<Transform> onWhistleDetected;
    public static event Action<Transform> onWhistleStopped;

    public float currentPitch;
    public bool isWhistling;

    private AudioClip microphoneClip;
    private string microphoneName;
    private float[] waveData;
    private int whistlingFrameCount;
    private int silentFrameCount;

    private void Start()
    {
        MicrophoneToAudioClip();
    }

    public void MicrophoneToAudioClip()
    {
        microphoneName = Microphone.devices[0];
        microphoneClip = Microphone.Start(microphoneName, true, 20, AudioSettings.outputSampleRate);
    }

    private void Update()
    {
        currentPitch = GetPitchFromMicrophone();
        bool rawWhistling = currentPitch >= minWhistleFreq && currentPitch <= maxWhistleFreq;

        if (rawWhistling)
        {
            whistlingFrameCount++;
            silentFrameCount = 0;
        }
        else
        {
            silentFrameCount++;
            whistlingFrameCount = 0;
        }

        if (!isWhistling && whistlingFrameCount >= requiredConsecutiveFrames)
        {
            isWhistling = true;
            onWhistleDetected?.Invoke(transform);
        }
        else if (isWhistling && silentFrameCount >= requiredConsecutiveFrames)
        {
            isWhistling = false;
            onWhistleStopped?.Invoke(transform);
        }
    }

    public float GetPitchFromMicrophone()
    {
        return GetPitchFromAudioClip(Microphone.GetPosition(microphoneName), microphoneClip);
    }

    public float GetPitchFromAudioClip(int clipPosition, AudioClip clip)
    {
        int startPosition = clipPosition - sampleWindow;
        if (startPosition < 0)
        {
            startPosition = 0;
        }

        if (waveData == null || waveData.Length != sampleWindow)
        {
            waveData = new float[sampleWindow];
        }
        clip.GetData(waveData, startPosition);

        float rms = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            rms += waveData[i] * waveData[i];
        }
        rms = Mathf.Sqrt(rms / sampleWindow);
        if (rms < minRms)
        {
            return 0f;
        }

        return AutocorrelatePitch(waveData, clip.frequency);
    }

    private float AutocorrelatePitch(float[] samples, int sampleRate)
    {
        int n = samples.Length;
        float mean = 0f;
        for (int i = 0; i < n; i++) mean += samples[i];
        mean /= n;

        int minLag = Mathf.Max(1, Mathf.FloorToInt(sampleRate / maxDetectableFreq));
        int maxLag = Mathf.Min(n - 1, Mathf.CeilToInt(sampleRate / minDetectableFreq));

        float bestCorrelation = 0f;
        int bestLag = -1;

        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float correlation = 0f;
            int count = n - lag;
            for (int i = 0; i < count; i++)
            {
                correlation += (samples[i] - mean) * (samples[i + lag] - mean);
            }
            correlation /= count;

            if (correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
                bestLag = lag;
            }
        }

        if (bestLag <= 0) return 0f;

        return (float)sampleRate / bestLag;
    }
}