using UnityEngine;


public class NoiseUtils
{

    public static float FBm(float x, float z, int octaves, float scale,
    float persistence = 0.5f, float lacunarity = 2.0f)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * scale * frequency,
            z * scale * frequency) * amplitude;

            totalAmplitude += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return value / totalAmplitude;
    }


    public static float Perlin3D(float x, float y, float z)
    {
        float xy = Mathf.PerlinNoise(x, y);
        float yz = Mathf.PerlinNoise(y, z);
        float xz = Mathf.PerlinNoise(x, z);
        float yx = Mathf.PerlinNoise(y, x);
        float zy = Mathf.PerlinNoise(z, y);
        float zx = Mathf.PerlinNoise(z, x);
        return (xy + yz + xz + yx + zy + zx) / 6f;
    }
}
