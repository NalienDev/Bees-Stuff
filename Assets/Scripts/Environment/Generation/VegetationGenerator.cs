using UnityEngine;
using static Config;


public static class VegetationGenerator
{
    private const float GRASS_DENSITY_BASE = 0.3f;
    private const float GRASS_DENSITY_FOREST = 0.8f;

    private const float FLOWER_DENSITY_BASE = 0.01f;
    private const float FLOWER_DENSITY_FOREST = 0.15f;

    public static void PlantVegetation(Block[,,] chunkData, Vector2Int worldOffset, int seaLevel, Biome[,] columnBiomes)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int surfaceY = FindSurfaceY(chunkData, x, z);
                if (surfaceY < 0 || surfaceY >= chunkHeight - 1) continue;

                Biome currentBiome = columnBiomes[x, z];


                if (chunkData[x, surfaceY, z].type != currentBiome.surfaceBlock) continue;

                if (surfaceY <= seaLevel) continue;

                float globalX = worldOffset.x * chunkSize + x + Config.seedOffsetX;
                float globalZ = worldOffset.y * chunkSize + z + Config.seedOffsetZ;

                float forestNoise = Mathf.PerlinNoise(
                    (globalX + 3713.5f) * TreeGenerator.treeNoiseScale,
                    (globalZ + 3713.5f) * TreeGenerator.treeNoiseScale);

                bool isForest = forestNoise >= currentBiome.treeThreshold;

                float grassProb = currentBiome.grassDensity;
                float flowerProb = currentBiome.flowerDensity;

                if (isForest)
                {
                    grassProb *= 2.5f;
                    flowerProb *= 5.0f;
                }

                float clumpNoise = Mathf.PerlinNoise(globalX * 0.2f, globalZ * 0.2f);
                grassProb *= clumpNoise;
                flowerProb *= clumpNoise;

                float rand = PseudoRandom(Mathf.RoundToInt(globalX), Mathf.RoundToInt(globalZ));

                if (rand < flowerProb)
                {
                    chunkData[x, surfaceY + 1, z] = new Block(Block.BlockType.FLOWER, new Vector3(x, surfaceY + 1, z));
                }
                else if (rand < grassProb + flowerProb)
                {
                    Block.BlockType vegetationType = currentBiome.biomeType == Biome.BiomeType.SNOW ? Block.BlockType.SNOWBUSH : Block.BlockType.SHORT_GRASS;
                    chunkData[x, surfaceY + 1, z] = new Block(vegetationType, new Vector3(x, surfaceY + 1, z));
                }
            }
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

    private static float PseudoRandom(int x, int z)
    {
        return Mathf.Abs(Mathf.Sin(x * 43.123f + z * 87.321f) * 31415.9265f) % 1f;
    }
}
