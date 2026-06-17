using UnityEngine;

/**
 * Classe estática responsável pela escavação de grutas e túneis no mundo.
 */
public static class CaveGenerator
{
    /**
     * Gera grutas 3D baseadas num threshold de ruído Perlin 3D.
     * 
     * Esta técnica remove blocos onde a densidade do ruído é superior ao limite definido,
     * criando espaços irregulares no interior do terreno.
     * 
     * @param chunkData: Matriz de blocos do chunk
     * @param chunkSize: Tamanho horizontal do chunk
     * @param chunkHeight: Altura do chunk
     * @param worldOffset: Posição global do chunk
     * @param caveScale: Escala do ruído das grutas
     * @param caveThreshold: Limiar de corte (valores maiores geram grutas menores)
     * @param maxSurfaceHeight: Altura máxima para início da escavação
     * @param margin: Margem de segurança para evitar grutas à superfície
     */
    public static void GenerateCaves(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, float caveScale, float caveThreshold, int maxSurfaceHeight, int margin)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    if (IsAir(chunkData, x, y, z)) continue;
                    if (y <= 1 || y >= maxSurfaceHeight - margin) continue;

                    float cx = (worldOffset.x * chunkSize + x) * caveScale;
                    float cy = y * caveScale;
                    float cz = (worldOffset.y * chunkSize + z) * caveScale;
                    float caveNoise = NoiseUtils.Perlin3D(cx, cy, cz);
                    if (caveNoise > caveThreshold)
                        chunkData[x, y, z] = new Block(Block.BlockType.AIR, new Vector3(x, y, z));
                }
            }
        }
    }

    /**
     * Inicializa as "Perlin Worms" - agentes que "escavam" túneis através dos chunks.
     * 
     * @param wormsPerChunk: Quantidade de agentes a criar por chunk
     * @param steps: Duração da vida do agente (=comprimento do túnel)
     * @param radius: Raio de escavação do túnel
     * @param stepSize: Distância percorrida em cada passo
     * @param directionScale: Frequência do ruído que determina a mudança de direção
     */
    public static void GenerateWorms(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, int wormsPerChunk, int steps, float radius,
        float stepSize, float directionScale)
    {
        for (int i = 0; i < wormsPerChunk; i++)
        {
            System.Random rng = new System.Random(); // Necessário pois estamos a usar isto em uma thread e o unity é single threaded, então nada da unity api pode ser chamada em threads

            // ponto de início aleatório dentro do chunk em coordenadas globais
            float globalStartX = worldOffset.x * chunkSize + (float)(rng.NextDouble() * chunkSize);
            float globalStartZ = worldOffset.y * chunkSize + (float)(rng.NextDouble() * chunkSize);
            float globalStartY = (float)(rng.NextDouble() * (chunkHeight * 0.5f - 5f)) + 5f;

            Vector3 wormStart = new Vector3(globalStartX, globalStartY, globalStartZ);
            CarveWorm(chunkData, chunkSize, chunkHeight, worldOffset, wormStart, steps, radius, stepSize, directionScale);
        }
    }

    /**
     * Executa a lógica de movimento e escavação de um agente (worm).
     */
    static void CarveWorm(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, Vector3 start, int steps, float radius,
        float stepSize, float directionScale)
    {
        Vector3 pos = start;
        for (int i = 0; i < steps; i++)
        {
            // direcção determinada por noise em coordenadas globais
            // usar coordenadas globais garante continuidade entre chunks
            float gx = pos.x * directionScale;
            float gy = pos.y * directionScale;
            float gz = pos.z * directionScale;

            float nx = NoiseUtils.Perlin3D(gx, gy, gz) * 2f - 1f;
            float ny = NoiseUtils.Perlin3D(gy + 100f, gz + 100f, gx + 100f) * 2f - 1f;
            float nz = NoiseUtils.Perlin3D(gz + 200f, gx + 200f, gy + 200f) * 2f - 1f;
            Vector3 dir = new Vector3(nx, ny * 0.5f, nz).normalized;
            pos += dir * stepSize;

            CarveAt(chunkData, chunkSize, chunkHeight, worldOffset, pos, radius);
        }
    }
    // TODO: worm cave generation seed based para ter cavernas naturais que passam entre varios chunks 

    /**
     * Escava uma esfera de ar em torno de uma posição específica.
     * Converte as coordenadas globais do agente em índices locais do array do chunk.
     * 
     * @param center: Posição global do centro da escavação
     * @param radius: Raio da esfera
     */
    static void CarveAt(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, Vector3 center, float radius)
    {
        // converter coordenadas globais para locais ao chunk
        int localX = Mathf.RoundToInt(center.x) - worldOffset.x * chunkSize;
        int localY = Mathf.RoundToInt(center.y);
        int localZ = Mathf.RoundToInt(center.z) - worldOffset.y * chunkSize;
        int r = Mathf.CeilToInt(radius);
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (dx * dx + dy * dy + dz * dz > radius * radius) continue;
                    int bx = localX + dx;
                    int by = localY + dy;
                    int bz = localZ + dz;
                    if (bx >= 0 && bx < chunkSize &&
                        by > 1 && by < chunkHeight &&
                        bz >= 0 && bz < chunkSize)
                    {
                        chunkData[bx, by, bz] = new Block(Block.BlockType.AIR, new Vector3(bx, by, bz));
                    }
                }
    }

    /**
     * Verifica se algum vizinho imediato do bloco foi escavado (ar).
     * 
     * @return: True se existir um vizinho que esteja escavado
     */
    public static bool HasCarvedAirNeighbour(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, float densityScale, int octaves, float scale, int x, int y, int z)
    {
        if (x > 0 && IsCarvedAir(chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x - 1, y, z)) return true;
        if (x < chunkSize - 1 && IsCarvedAir(chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x + 1, y, z)) return true;
        if (z > 0 && IsCarvedAir(chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x, y, z - 1)) return true;
        if (z < chunkSize - 1 && IsCarvedAir(chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x, y, z + 1)) return true;
        if (y > 0 && IsCarvedAir(chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x, y - 1, z)) return true;
        if (y < chunkHeight - 1 && IsCarvedAir(chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x, y + 1, z)) return true;
        return false;
    }

    /* Atalho para verificar se um bloco é do tipo ar. */
    public static bool IsAir(Block[,,] chunkData, int x, int y, int z)
    {
        return chunkData[x, y, z].type == Block.BlockType.AIR;
    }

    /**
     * Determina se um bloco de ar atual é fruto de escavação (carving) ou se seria naturalmente ar.
     */
    static bool IsCarvedAir(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, float densityScale, int octaves, float scale, int x, int y, int z)
    {
        // o bloco tem de ser ar no chunkData
        if (!IsAir(chunkData, x, y, z)) return false;

        // e teria de ser sólido sem o carving, ou seja, é ar criado pelo carving e não pela superfície
        float heightNoise = NoiseUtils.FBm(x, z, octaves, scale) * chunkHeight;
        float densityNoise = NoiseUtils.Perlin3D(
            (worldOffset.x * chunkSize + x) * densityScale,
            y * densityScale,
            (worldOffset.y * chunkSize + z) * densityScale);
        return (heightNoise - y) + densityNoise > 0f;
    }
}