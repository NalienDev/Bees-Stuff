using UnityEngine;
using static Config;


public static class TreeGenerator
{
    private const int TrunkHeightMin = 4;

    private const int TrunkHeightMax = 7;

    private const float CanopyRadiusMin = 2.0f;

    private const float CanopyRadiusMax = 3.2f;

    private const float MaxCanopyOffset = 1.2f;

    public static float treeThreshold = 0.62f;

    public static float treeNoiseScale = 0.08f;

    private const int EdgeMargin = 3;

    private const float SEED_SPAWN = 3713.5f;
    private const float SEED_HEIGHT = 8941.1f;
    private const float SEED_RADIUS = 2347.7f;
    private const float SEED_OFFSET = 6128.3f;

    private const int CellSize = 4;

    public static void PlantTrees(Block[,,] chunkData, Vector2Int worldOffset, int seaLevel, Biome[,] columnBiomes)
    {
        for (int cx = EdgeMargin; cx < chunkSize - EdgeMargin; cx += CellSize)
        {
            for (int cz = EdgeMargin; cz < chunkSize - EdgeMargin; cz += CellSize)
            {
                int cellGlobalX = worldOffset.x * chunkSize + cx;
                int cellGlobalZ = worldOffset.y * chunkSize + cz;

                float rx = PseudoRandom(cellGlobalX, cellGlobalZ, 1);
                float rz = PseudoRandom(cellGlobalX, cellGlobalZ, 2);

                int localX = cx + Mathf.FloorToInt(rx * CellSize);
                int localZ = cz + Mathf.FloorToInt(rz * CellSize);

                if (localX >= chunkSize - EdgeMargin) localX = chunkSize - EdgeMargin - 1;
                if (localZ >= chunkSize - EdgeMargin) localZ = chunkSize - EdgeMargin - 1;

                float globalX = worldOffset.x * chunkSize + localX + Config.seedOffsetX;
                float globalZ = worldOffset.y * chunkSize + localZ + Config.seedOffsetZ;

                Biome currentBiome = columnBiomes[localX, localZ];

                float spawnNoise = Mathf.PerlinNoise(
                    (globalX + SEED_SPAWN) * treeNoiseScale,
                    (globalZ + SEED_SPAWN) * treeNoiseScale);
                if (spawnNoise < currentBiome.treeThreshold) continue;

                int surfaceY = FindSurfaceY(chunkData, localX, localZ);
                if (surfaceY < 0) continue;

                if (chunkData[localX, surfaceY, localZ].type != currentBiome.surfaceBlock) continue;

                if (surfaceY <= seaLevel) continue;

                float heightNoise = Mathf.PerlinNoise(
                    (globalX + SEED_HEIGHT) * treeNoiseScale * 2f,
                    (globalZ + SEED_HEIGHT) * treeNoiseScale * 2f);
                int trunkHeight = Mathf.RoundToInt(
                    Mathf.Lerp(TrunkHeightMin, TrunkHeightMax, heightNoise));

                float radiusNoise = Mathf.PerlinNoise(
                    (globalX + SEED_RADIUS) * treeNoiseScale * 2f,
                    (globalZ + SEED_RADIUS) * treeNoiseScale * 2f);
                float canopyRadius = Mathf.Lerp(CanopyRadiusMin, CanopyRadiusMax, radiusNoise);

                int requiredHeight = trunkHeight + Mathf.CeilToInt(canopyRadius) + 2;
                if (surfaceY + requiredHeight >= chunkHeight) continue;

                float offsetNoise = Mathf.PerlinNoise(
                    (globalX + SEED_OFFSET) * treeNoiseScale * 3f,
                    (globalZ + SEED_OFFSET) * treeNoiseScale * 3f);

                float offsetNoise2 = Mathf.PerlinNoise(
                    (globalZ + SEED_OFFSET + 500f) * treeNoiseScale * 3f,
                    (globalX + SEED_OFFSET + 500f) * treeNoiseScale * 3f);
                float canopyOffsetX = (offsetNoise * 2f - 1f) * MaxCanopyOffset;
                float canopyOffsetZ = (offsetNoise2 * 2f - 1f) * MaxCanopyOffset;

                if (currentBiome.biomeType == Biome.BiomeType.DESERT)
                {
                    int cactusHeight = Mathf.RoundToInt(Mathf.Lerp(3, 5, heightNoise));
                    PlantCactus(chunkData, localX, surfaceY, localZ, cactusHeight);
                }
                else if (currentBiome.biomeType == Biome.BiomeType.SNOW)
                {
                    PlantPineTree(chunkData, localX, surfaceY, localZ, trunkHeight + 2, canopyRadius, Mathf.RoundToInt(globalX), Mathf.RoundToInt(globalZ));
                }
                else if (currentBiome.biomeType == Biome.BiomeType.JUNGLE)
                {
                    PlantJungleTree(chunkData, localX, surfaceY, localZ, trunkHeight + 5, canopyRadius + 1.5f, canopyOffsetX, canopyOffsetZ, Mathf.RoundToInt(globalX), Mathf.RoundToInt(globalZ));
                }
                else
                {
                    PlantTree(chunkData, localX, surfaceY, localZ, trunkHeight, canopyRadius, canopyOffsetX, canopyOffsetZ, Mathf.RoundToInt(globalX), Mathf.RoundToInt(globalZ));
                }
            }
        }
    }


    private static float PseudoRandom(int x, int z, int seed)
    {
        return Mathf.Abs(Mathf.Sin(x * 12.9898f + z * 78.233f + seed * 37.719f) * 43758.5453f) % 1f;
    }

    private static void PlantTree(
        Block[,,] chunkData,
        int baseX, int surfaceY, int baseZ,
        int trunkHeight, float canopyRadius,
        float canopyOffX, float canopyOffZ,
        int globalX, int globalZ)
    {
        for (int t = 1; t <= trunkHeight; t++)
        {
            int y = surfaceY + t;
            if (y < chunkHeight)
                SetBlock(chunkData, baseX, y, baseZ, Block.BlockType.WOOD);
        }

        float cx = baseX + canopyOffX;
        float cy = surfaceY + trunkHeight;
        float cz = baseZ + canopyOffZ;

        int r = Mathf.CeilToInt(canopyRadius);
        float r2 = canopyRadius * canopyRadius;

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r + 1; dy++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int bx = baseX + dx;
                    int by = (int)(cy + dy);
                    int bz = baseZ + dz;

                    if (bx < 0 || bx >= chunkSize || by < 0 || by >= chunkHeight || bz < 0 || bz >= chunkSize)
                        continue;

                    float fdx = bx - cx;
                    float fdy = by - cy;
                    float fdz = bz - cz;

                    float dist2 = fdx * fdx + (fdy * fdy * 1.3f) + fdz * fdz;
                    if (dist2 > r2) continue;

                    if (chunkData[bx, by, bz].type == Block.BlockType.WOOD) continue;

                    float fillChance = 1f - Mathf.Clamp01((dist2 / r2 - 0.5f) * 2f);

                    float leafNoise = Mathf.PerlinNoise(bx * 0.7f + bz * 0.31f, by * 0.53f);
                    if (leafNoise > fillChance) continue;

                    SetBlock(chunkData, bx, by, bz, Block.BlockType.LEAVES);
                }
            }
        }



        bool spawnHive = PseudoRandom(globalX, globalZ, 42) < 0.05f;
        if (spawnHive && trunkHeight >= 4)
        {
            int hiveY = surfaceY + trunkHeight - 2;

            int side = Mathf.FloorToInt(PseudoRandom(globalX, globalZ, 99) * 4f);
            int hx = baseX;
            int hz = baseZ;

            if (side == 0) hx += 1;
            else if (side == 1) hx -= 1;
            else if (side == 2) hz += 1;
            else if (side == 3) hz -= 1;

            if (chunkData[hx, hiveY, hz].type == Block.BlockType.AIR)
            {
                SetBlock(chunkData, hx, hiveY, hz, Block.BlockType.HIVE);
            }
        }
    }

    private static void PlantPineTree(Block[,,] chunkData, int baseX, int surfaceY, int baseZ, int trunkHeight, float maxRadius, int globalX, int globalZ)
    {
        for (int t = 1; t <= trunkHeight; t++)
        {
            int y = surfaceY + t;
            if (y < chunkHeight)
                SetBlock(chunkData, baseX, y, baseZ, Block.BlockType.PINE_WOOD);
        }

        int startLeavesY = surfaceY + Mathf.RoundToInt(trunkHeight * 0.3f);
        for (int y = startLeavesY; y <= surfaceY + trunkHeight + 2; y++)
        {
            if (y >= chunkHeight) break;
            float progress = (float)(y - startLeavesY) / (trunkHeight + 2 - (trunkHeight * 0.3f));
            int r = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(maxRadius, 0, progress)));

            if (y <= surfaceY + trunkHeight && r == 0)
            {
                r = 1;
            }

            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int bx = baseX + dx;
                    int bz = baseZ + dz;
                    if (bx < 0 || bx >= chunkSize || bz < 0 || bz >= chunkSize) continue;

                    if (Mathf.Abs(dx) == r && Mathf.Abs(dz) == r && r > 1)
                    {
                        if (PseudoRandom(bx + globalX, bz + globalZ, y) > 0.5f) continue;
                    }

                    if (chunkData[bx, y, bz].type != Block.BlockType.PINE_WOOD && chunkData[bx, y, bz].type != Block.BlockType.WOOD)
                    {
                        SetBlock(chunkData, bx, y, bz, Block.BlockType.PINE_LEAVES);
                    }
                }
            }
        }
    }

    private static void PlantJungleTree(Block[,,] chunkData, int baseX, int surfaceY, int baseZ, int trunkHeight, float canopyRadius, float canopyOffX, float canopyOffZ, int globalX, int globalZ)
    {
        for (int t = 1; t <= trunkHeight; t++)
        {
            int y = surfaceY + t;
            if (y < chunkHeight)
                SetBlock(chunkData, baseX, y, baseZ, Block.BlockType.JUNGLE_WOOD);
        }

        float cx = baseX + canopyOffX;
        float cy = surfaceY + trunkHeight;
        float cz = baseZ + canopyOffZ;

        int r = Mathf.CeilToInt(canopyRadius);
        float r2 = canopyRadius * canopyRadius;
        int rY = Mathf.Max(1, Mathf.RoundToInt(r * 0.4f));

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -rY; dy <= rY; dy++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int bx = baseX + dx;
                    int by = (int)(cy + dy);
                    int bz = baseZ + dz;

                    if (bx < 0 || bx >= chunkSize || by < 0 || by >= chunkHeight || bz < 0 || bz >= chunkSize)
                        continue;

                    float fdx = bx - cx;
                    float fdy = by - cy;
                    float fdz = bz - cz;

                    float dist2 = fdx * fdx + (fdy * fdy * 4.0f) + fdz * fdz;
                    if (dist2 > r2) continue;

                    if (chunkData[bx, by, bz].type == Block.BlockType.JUNGLE_WOOD || chunkData[bx, by, bz].type == Block.BlockType.WOOD) continue;

                    float fillChance = 1f - Mathf.Clamp01((dist2 / r2 - 0.4f) * 2f);
                    float leafNoise = Mathf.PerlinNoise(bx * 0.7f + bz * 0.31f, by * 0.53f);
                    if (leafNoise > fillChance) continue;

                    SetBlock(chunkData, bx, by, bz, Block.BlockType.JUNGLE_LEAVES);
                }
            }
        }
    }

    private static void PlantCactus(Block[,,] chunkData, int baseX, int surfaceY, int baseZ, int height)
    {
        for (int t = 1; t <= height; t++)
        {
            int y = surfaceY + t;
            if (y < chunkHeight)
                SetBlock(chunkData, baseX, y, baseZ, Block.BlockType.CACTUS);
        }
    }

    private static int FindSurfaceY(Block[,,] chunkData, int x, int z)
    {
        for (int y = chunkHeight - 1; y >= 0; y--)
        {
            if (chunkData[x, y, z].isSolid)
                return y;
        }
        return -1;
    }

    private static void SetBlock(Block[,,] chunkData, int x, int y, int z, Block.BlockType type)
    {
        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize) return;
        chunkData[x, y, z] = new Block(type, new Vector3(x, y, z));
    }
}
