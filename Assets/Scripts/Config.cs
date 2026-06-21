using UnityEngine;

public static class Config
{
    [Header("Dimensões do Chunk")]

    public static readonly int chunkSize = 16;

    public static readonly int chunkHeight = 64;

    [Header("Configurações do Mundo")]
    public static readonly int renderDistance = 5;

    public static readonly int worldHeight = 256;

    [Header("Seed Settings")]

    public static float seedOffsetX = 0f;
    public static float seedOffsetZ = 0f;

    public static int seedOffsetHash = 0;
}