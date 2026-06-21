using UnityEngine;
using System.Collections.Generic;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Sprite Animation Settings")]
    [Tooltip("Frames of the animation to play in the bottom-right corner.")]
    public Texture2D[] animationFrames;
    [Tooltip("Speed of the animation in frames per second.")]
    public float framesPerSecond = 10f;
    [Tooltip("Target visual scale size of the sprite in pixels.")]
    public Vector2 spriteSize = new Vector2(64f, 64f);
    [Tooltip("Marginal offset from the bottom-right corner in pixels.")]
    public Vector2 cornerOffset = new Vector2(20f, 20f);

    [Header("Center Image Settings")]
    [Tooltip("Static image to show in the center of the loading screen.")]
    public Texture2D centerImage;
    [Tooltip("Target size of the center image in pixels.")]
    public Vector2 centerImageSize = new Vector2(256f, 256f);

    private bool isLoaded = false;
    private GUIStyle backgroundStyle;
    private Texture2D bgTexture;

    private List<GameHiveManager> activeHives = new List<GameHiveManager>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.12f, 1f));
        bgTexture.Apply();
    }

    public void RegisterHive(GameHiveManager hive)
    {
        if (!activeHives.Contains(hive))
        {
            activeHives.Add(hive);
        }
    }

    public void UnregisterHive(GameHiveManager hive)
    {
        activeHives.Remove(hive);
    }

    private void Update()
    {
        if (isLoaded) return;


        if (Time.timeSinceLevelLoad < 2f) return;

        bool allHivesSpawned = true;
        foreach (var hive in activeHives)
        {
            if (hive == null) continue;
            BeeSpawner spawner = hive.GetComponent<BeeSpawner>();
            if (spawner == null || !spawner.HasSpawnedInitial)
            {
                allHivesSpawned = false;
                break;
            }
        }

        if (allHivesSpawned)
        {
            isLoaded = true;
        }
    }

    private void OnGUI()
    {
        if (isLoaded) return;

        if (backgroundStyle == null)
        {
            backgroundStyle = new GUIStyle();
            backgroundStyle.normal.background = bgTexture;
        }

        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", backgroundStyle);

        if (centerImage != null)
        {
            float cx = (Screen.width - centerImageSize.x) * 0.5f;
            float cy = (Screen.height - centerImageSize.y) * 0.5f;
            GUI.DrawTexture(new Rect(cx, cy, centerImageSize.x, centerImageSize.y), centerImage, ScaleMode.ScaleToFit);
        }

        if (animationFrames != null && animationFrames.Length > 0)
        {
            int index = Mathf.FloorToInt(Time.time * framesPerSecond) % animationFrames.Length;
            Texture2D currentFrame = animationFrames[index];

            if (currentFrame != null)
            {
                float x = Screen.width - spriteSize.x - cornerOffset.x;
                float y = Screen.height - spriteSize.y - cornerOffset.y;

                GUI.DrawTexture(new Rect(x, y, spriteSize.x, spriteSize.y), currentFrame, ScaleMode.ScaleToFit);
            }
        }
    }
}
