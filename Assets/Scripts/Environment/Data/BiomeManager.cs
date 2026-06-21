using UnityEngine;

public static class BiomeManager
{
    private static Biome plainsBiome = new Biome("Plains", Biome.BiomeType.PLAINS, 0.05f, 4, 26, 8f, Block.BlockType.GRASS, Block.BlockType.DIRT, 0.62f, 0.3f, 0.05f);
    private static Biome desertBiome = new Biome("Desert", Biome.BiomeType.DESERT, 0.04f, 3, 22, 4f, Block.BlockType.SAND, Block.BlockType.SAND, 0.70f, 0.0f, 0.0f);
    private static Biome snowBiome = new Biome("Snow", Biome.BiomeType.SNOW, 0.06f, 5, 30, 10f, Block.BlockType.SNOW, Block.BlockType.DIRT, 0.65f, 0.1f, 0.0f);
    private static Biome jungleBiome = new Biome("Jungle", Biome.BiomeType.JUNGLE, 0.05f, 4, 35, 10f, Block.BlockType.MOSS, Block.BlockType.DIRT, 0.35f, 0.8f, 0.0f);

    private const float biomeNoiseScale = 0.005f;
    private const float tempOffset = 12345f;
    private const float moistOffset = 54321f;

    public static Biome GetBiomeAt(float globalX, float globalZ)
    {
        float temp = Mathf.PerlinNoise((globalX + tempOffset) * biomeNoiseScale, (globalZ + tempOffset) * biomeNoiseScale);
        float moisture = Mathf.PerlinNoise((globalX + moistOffset) * biomeNoiseScale, (globalZ + moistOffset) * biomeNoiseScale);

        if (temp > 0.55f)
        {
            if (moisture > 0.5f) return jungleBiome;
            else return desertBiome;
        }
        else if (temp < 0.45f)
        {
            return snowBiome;
        }
        else
        {
            return plainsBiome;
        }
    }
}
