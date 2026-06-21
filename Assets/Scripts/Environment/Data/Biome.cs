using UnityEngine;

public class Biome
{
    public enum BiomeType { PLAINS, DESERT, SNOW, JUNGLE }
    
    public string name;
    public BiomeType biomeType;

    // Terrain parameters
    public float scale;
    public int octaves;
    public int maxSolidHeight;
    public float detailWeight;

    // Surface definition
    public Block.BlockType surfaceBlock;
    public Block.BlockType subSurfaceBlock;

    // Flora parameters
    public float treeThreshold;
    public float grassDensity;
    public float flowerDensity;

    public Biome(string name, BiomeType biomeType, float scale, int octaves, int maxSolidHeight, float detailWeight, Block.BlockType surface, Block.BlockType subSurface, float treeThreshold, float grassDensity, float flowerDensity)
    {
        this.name = name;
        this.biomeType = biomeType;
        this.scale = scale;
        this.octaves = octaves;
        this.maxSolidHeight = maxSolidHeight;
        this.detailWeight = detailWeight;
        this.surfaceBlock = surface;
        this.subSurfaceBlock = subSurface;
        this.treeThreshold = treeThreshold;
        this.grassDensity = grassDensity;
        this.flowerDensity = flowerDensity;
    }
}
