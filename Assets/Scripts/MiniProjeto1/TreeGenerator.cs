using UnityEngine;
using static Config;

/**
 * Gerador procedural de arvores para o mundo voxel.
 *
 * Cada arvore e definida por tres parametros de variacao, amostrados de mapas de ruido
 * independentes baseados nas coordenadas globais do tronco:
 *   - Altura do tronco  : [trunkHeightMin, trunkHeightMax]
 *   - Raio da copa      : [canopyRadiusMin, canopyRadiusMax]
 *   - Inclinacao da copa: desvio horizontal do centro da copa relativamente ao topo do tronco
 *
 * Regras de spawn:
 *   1. O bloco de superficie tem de ser GRASS.
 *   2. A superficie tem de estar acima do nivel do mar (surfaceY > seaLevel).
 *   3. Um mapa de ruido de baixa frequencia tem de ultrapassar o limiar treeThreshold.
 *   4. Tem de haver espaco vertical suficiente (trunkHeight + canopyRadius + 1 < chunkHeight - surfaceY).
 *   5. A posicao nao pode estar na margem do chunk (edgeMargin blocos de distancia das bordas),
 *      evitando escritas fora dos limites da matriz.
 */
public static class TreeGenerator
{
    // --- Parametros de variacao das arvores (podem ser expostos via Config se necessario) ---

    /** Altura minima do tronco em blocos. */
    private const int TrunkHeightMin = 4;
    /** Altura maxima do tronco em blocos. */
    private const int TrunkHeightMax = 7;

    /** Raio minimo da copa esferica em blocos. */
    private const float CanopyRadiusMin = 2.0f;
    /** Raio maximo da copa esferica em blocos. */
    private const float CanopyRadiusMax = 3.2f;

    /** Desvio maximo do centro da copa em relacao ao topo do tronco (blocos). */
    private const float MaxCanopyOffset = 1.2f;

    /**
     * Limiar do mapa de ruido de spawn de arvores.
     * Valores mais altos = arvores mais raras; valores mais baixos = arvores mais densas.
     * Intervalo recomendado: [0.55, 0.75].
     */
    public static float treeThreshold = 0.62f;

    /**
     * Escala do mapa de ruido de spawn de arvores.
     * Valores mais baixos criam manchas maiores de floresta; valores mais altos criam distribuicao mais atomizada.
     */
    public static float treeNoiseScale = 0.08f;

    /** Margem de seguranca em relacao as bordas do chunk, em blocos. */
    private const int EdgeMargin = 3;

    // Offsets de seed para os diferentes mapas de ruido de arvore,
    // garantindo que nao interferem com o mapa de terreno.
    private const float SEED_SPAWN  = 3713.5f;
    private const float SEED_HEIGHT = 8941.1f;
    private const float SEED_RADIUS = 2347.7f;
    private const float SEED_OFFSET = 6128.3f;

    /** Tamanho da celula para a grelha de jitter (espacamento organico). */
    private const int CellSize = 4;

    /**
     * Ponto de entrada principal. Percorre as colunas do chunk usando uma grelha
     * com jitter (para evitar padroes em linha/cruz) e planta arvores onde as 
     * condicoes sao satisfeitas.
     *
     * @param chunkData  : Matriz 3D de blocos do chunk (leitura e escrita).
     * @param worldOffset: Coordenada do chunk no mundo (em unidades de chunk).
     * @param seaLevel   : Nivel do mar atual (bloco Y abaixo do qual nao crescem arvores).
     */
    public static void PlantTrees(Block[,,] chunkData, Vector2Int worldOffset, int seaLevel)
    {
        for (int cx = EdgeMargin; cx < chunkSize - EdgeMargin; cx += CellSize)
        {
            for (int cz = EdgeMargin; cz < chunkSize - EdgeMargin; cz += CellSize)
            {
                int cellGlobalX = worldOffset.x * chunkSize + cx;
                int cellGlobalZ = worldOffset.y * chunkSize + cz;

                // Gerar offset aleatorio dentro da celula de forma deterministica
                float rx = PseudoRandom(cellGlobalX, cellGlobalZ, 1);
                float rz = PseudoRandom(cellGlobalX, cellGlobalZ, 2);

                int localX = cx + Mathf.FloorToInt(rx * CellSize);
                int localZ = cz + Mathf.FloorToInt(rz * CellSize);

                // Garantir que o offset nao atira a arvore para a margem proibida
                if (localX >= chunkSize - EdgeMargin) localX = chunkSize - EdgeMargin - 1;
                if (localZ >= chunkSize - EdgeMargin) localZ = chunkSize - EdgeMargin - 1;

                float globalX = worldOffset.x * chunkSize + localX;
                float globalZ = worldOffset.y * chunkSize + localZ;

                // --- Condicao 3: mapa de ruido de spawn ---
                float spawnNoise = Mathf.PerlinNoise(
                    (globalX + SEED_SPAWN) * treeNoiseScale,
                    (globalZ + SEED_SPAWN) * treeNoiseScale);
                if (spawnNoise < treeThreshold) continue;

                // --- Encontrar a altura de superficie desta coluna ---
                int surfaceY = FindSurfaceY(chunkData, localX, localZ);
                if (surfaceY < 0) continue;

                // --- Condicao 1: bloco de superficie tem de ser GRASS ---
                if (chunkData[localX, surfaceY, localZ].type != Block.BlockType.GRASS) continue;

                // --- Condicao 2: acima do nivel do mar ---
                if (surfaceY <= seaLevel) continue;

                // --- Calcular variacao da arvore a partir de ruido ---
                float heightNoise = Mathf.PerlinNoise(
                    (globalX + SEED_HEIGHT) * treeNoiseScale * 2f,
                    (globalZ + SEED_HEIGHT) * treeNoiseScale * 2f);
                int trunkHeight = Mathf.RoundToInt(
                    Mathf.Lerp(TrunkHeightMin, TrunkHeightMax, heightNoise));

                float radiusNoise = Mathf.PerlinNoise(
                    (globalX + SEED_RADIUS) * treeNoiseScale * 2f,
                    (globalZ + SEED_RADIUS) * treeNoiseScale * 2f);
                float canopyRadius = Mathf.Lerp(CanopyRadiusMin, CanopyRadiusMax, radiusNoise);

                // --- Condicao 4: espaco vertical suficiente ---
                int requiredHeight = trunkHeight + Mathf.CeilToInt(canopyRadius) + 2;
                if (surfaceY + requiredHeight >= chunkHeight) continue;

                // --- Calcular inclinacao da copa ---
                float offsetNoise = Mathf.PerlinNoise(
                    (globalX + SEED_OFFSET) * treeNoiseScale * 3f,
                    (globalZ + SEED_OFFSET) * treeNoiseScale * 3f);
                // Mapear [0,1] -> [-MaxCanopyOffset, MaxCanopyOffset] para X e Z independentemente
                float offsetNoise2 = Mathf.PerlinNoise(
                    (globalZ + SEED_OFFSET + 500f) * treeNoiseScale * 3f,
                    (globalX + SEED_OFFSET + 500f) * treeNoiseScale * 3f);
                float canopyOffsetX = (offsetNoise  * 2f - 1f) * MaxCanopyOffset;
                float canopyOffsetZ = (offsetNoise2 * 2f - 1f) * MaxCanopyOffset;

                // --- Plantar a arvore ---
                PlantTree(chunkData, localX, surfaceY, localZ, trunkHeight, canopyRadius, canopyOffsetX, canopyOffsetZ, Mathf.RoundToInt(globalX), Mathf.RoundToInt(globalZ));
            }
        }
    }

    /**
     * Hash deterministico para gerar ruido branco [0, 1) baseado em coordenadas inteiras.
     */
    private static float PseudoRandom(int x, int z, int seed)
    {
        return Mathf.Abs(Mathf.Sin(x * 12.9898f + z * 78.233f + seed * 37.719f) * 43758.5453f) % 1f;
    }

    /**
     * Escreve os blocos de uma arvore individual na matriz do chunk.
     *
     * @param chunkData   : Matriz 3D de blocos (escrita direta).
     * @param baseX       : Coordenada X local do pe do tronco.
     * @param surfaceY    : Altura Y do bloco de superficie (o tronco comeca em surfaceY + 1).
     * @param baseZ       : Coordenada Z local do pe do tronco.
     * @param trunkHeight : Numero de blocos de madeira no tronco.
     * @param canopyRadius: Raio da esfera de folhagem.
     * @param canopyOffX  : Desvio horizontal da copa no eixo X.
     * @param canopyOffZ  : Desvio horizontal da copa no eixo Z.
     * @param globalX     : Coordenada global X (usada para seeds deterministas).
     * @param globalZ     : Coordenada global Z (usada para seeds deterministas).
     */
    private static void PlantTree(
        Block[,,] chunkData,
        int baseX, int surfaceY, int baseZ,
        int trunkHeight, float canopyRadius,
        float canopyOffX, float canopyOffZ,
        int globalX, int globalZ)
    {
        // 1. Colocar blocos de madeira no tronco (surfaceY+1 ate surfaceY+trunkHeight)
        for (int t = 1; t <= trunkHeight; t++)
        {
            int y = surfaceY + t;
            if (y < chunkHeight)
                SetBlock(chunkData, baseX, y, baseZ, Block.BlockType.WOOD);
        }

        // 2. Centro da copa: topo do tronco mais o desvio horizontal
        float cx = baseX + canopyOffX;
        float cy = surfaceY + trunkHeight;
        float cz = baseZ + canopyOffZ;

        // 3. Percorrer um bounding box em torno do centro da copa
        int r = Mathf.CeilToInt(canopyRadius);
        float r2 = canopyRadius * canopyRadius;

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r + 1; dy++) // +1 para copa ligeiramente mais alta
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int bx = baseX + dx;
                    int by = (int)(cy + dy);
                    int bz = baseZ + dz;

                    // Verificar limites do chunk
                    if (bx < 0 || bx >= chunkSize || by < 0 || by >= chunkHeight || bz < 0 || bz >= chunkSize)
                        continue;

                    // Distancia ao centro da copa (usando coordenadas reais para o desvio)
                    float fdx = bx - cx;
                    float fdy = by - cy;
                    float fdz = bz - cz;

                    // Espremer ligeiramente a esfera no eixo Y para copa mais achatada
                    float dist2 = fdx * fdx + (fdy * fdy * 1.3f) + fdz * fdz;
                    if (dist2 > r2) continue;

                    // Nao substituir blocos de madeira ja colocados
                    if (chunkData[bx, by, bz].type == Block.BlockType.WOOD) continue;

                    // Deixar "buracos" aleatorios nas extremidades da copa para aspeto organico.
                    // Quanto mais longe do centro, mais provavel e o buraco.
                    float fillChance = 1f - Mathf.Clamp01((dist2 / r2 - 0.5f) * 2f);
                    // Usar ruido deterministico baseado na posicao global para reproducibilidade por chunk.
                    float leafNoise = Mathf.PerlinNoise(bx * 0.7f + bz * 0.31f, by * 0.53f);
                    if (leafNoise > fillChance) continue;

                    SetBlock(chunkData, bx, by, bz, Block.BlockType.LEAVES);
                }
            }
        }

        // 4. Chance de gerar uma colmeia (HIVE)
        // Probabilidade muita rara (ex: 5%)
        bool spawnHive = PseudoRandom(globalX, globalZ, 42) < 0.05f;
        if (spawnHive && trunkHeight >= 4)
        {
            // Colocar num nivel baixo, logo abaixo das folhas
            int hiveY = surfaceY + trunkHeight - 2; 
            
            // Escolher um lado aleatorio ao redor do tronco
            int side = Mathf.FloorToInt(PseudoRandom(globalX, globalZ, 99) * 4f);
            int hx = baseX;
            int hz = baseZ;
            
            if (side == 0) hx += 1;
            else if (side == 1) hx -= 1;
            else if (side == 2) hz += 1;
            else if (side == 3) hz -= 1;

            // Apenas colocar se o bloco estiver vazio (nao substitui madeira nem terreno)
            if (chunkData[hx, hiveY, hz].type == Block.BlockType.AIR)
            {
                SetBlock(chunkData, hx, hiveY, hz, Block.BlockType.HIVE);
            }
        }
    }

    /**
     * Devolve o Y mais alto de um bloco solido numa coluna (x, z) do chunk.
     * Percorre de cima para baixo e devolve -1 se a coluna estiver vazia.
     *
     * @param chunkData: Matriz 3D de blocos.
     * @param x        : Coordenada X local.
     * @param z        : Coordenada Z local.
     * @return         : Y da superficie, ou -1 se nao encontrado.
     */
    private static int FindSurfaceY(Block[,,] chunkData, int x, int z)
    {
        for (int y = chunkHeight - 1; y >= 0; y--)
        {
            if (chunkData[x, y, z].isSolid)
                return y;
        }
        return -1;
    }

    /**
     * Auxiliar que escreve um bloco na posicao indicada, verificando limites.
     */
    private static void SetBlock(Block[,,] chunkData, int x, int y, int z, Block.BlockType type)
    {
        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize) return;
        chunkData[x, y, z] = new Block(type, new Vector3(x, y, z));
    }
}
