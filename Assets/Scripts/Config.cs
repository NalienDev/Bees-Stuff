using UnityEngine;

public static class Config
{
    [Header("Dimensões do Chunk")]
    /* Tamanho do chunk nos eixos X e Z — define a largura e profundidade em blocos */
    public static readonly int chunkSize = 16;
    /* Tamanho do chunk no eixo Y — define a altura máxima em blocos */
    public static readonly int chunkHeight = 64;

    [Header("Configurações do Mundo")]
    public static readonly int renderDistance = 3;
    /* Altura máxima do mundo */
    public static readonly int worldHeight = 256;

    [Header("Geração de Terreno")]
    public static readonly int seaLevel = 12;
}