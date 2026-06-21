using UnityEngine;

public static class Config
{
    [Header("Dimensões do Chunk")]
    /* Tamanho do chunk nos eixos X e Z — define a largura e profundidade em blocos */
    public static readonly int chunkSize = 16;
    /* Tamanho do chunk no eixo Y — define a altura máxima em blocos */
    public static readonly int chunkHeight = 64;

    [Header("Configurações do Mundo")]
    public static readonly int renderDistance = 5;
    /* Altura máxima do mundo */
    public static readonly int worldHeight = 256;

    [Header("Seed Settings")]
    /* Offsets globais usados para deslocar as coordenadas de ruído, atuando como uma seed para a geração */
    public static float seedOffsetX = 0f;
    public static float seedOffsetZ = 0f;
    /* Hash inteiro usado para inicializar geradores de números pseudo-aleatórios como o System.Random */
    public static int seedOffsetHash = 0;
}