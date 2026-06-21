using UnityEngine;


public static class CaveGenerator
{

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


    public static void GenerateWorms(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, int wormsPerChunk, int steps, float radius,
        float stepSize, float directionScale)
    {
        for (int i = 0; i < wormsPerChunk; i++)
        {

            System.Random rng = new System.Random(Config.seedOffsetHash + worldOffset.x * 1234 + worldOffset.y * 5678);


            float globalStartX = worldOffset.x * chunkSize + (float)(rng.NextDouble() * chunkSize);
            float globalStartZ = worldOffset.y * chunkSize + (float)(rng.NextDouble() * chunkSize);
            float globalStartY = (float)(rng.NextDouble() * (chunkHeight * 0.5f - 5f)) + 5f;

            Vector3 wormStart = new Vector3(globalStartX, globalStartY, globalStartZ);
            CarveWorm(chunkData, chunkSize, chunkHeight, worldOffset, wormStart, steps, radius, stepSize, directionScale);
        }
    }


    static void CarveWorm(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, Vector3 start, int steps, float radius,
        float stepSize, float directionScale)
    {
        Vector3 pos = start;
        for (int i = 0; i < steps; i++)
        {


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



    static void CarveAt(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, Vector3 center, float radius)
    {

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


    public static bool IsAir(Block[,,] chunkData, int x, int y, int z)
    {
        return chunkData[x, y, z].type == Block.BlockType.AIR;
    }


    static bool IsCarvedAir(Block[,,] chunkData, int chunkSize, int chunkHeight,
        Vector2Int worldOffset, float densityScale, int octaves, float scale, int x, int y, int z)
    {

        if (!IsAir(chunkData, x, y, z)) return false;


        float heightNoise = NoiseUtils.FBm(x, z, octaves, scale) * chunkHeight;
        float densityNoise = NoiseUtils.Perlin3D(
            (worldOffset.x * chunkSize + x) * densityScale,
            y * densityScale,
            (worldOffset.y * chunkSize + z) * densityScale);
        return (heightNoise - y) + densityNoise > 0f;
    }
}

