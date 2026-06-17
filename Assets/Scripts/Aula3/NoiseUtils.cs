using UnityEngine;

/**
 * Classe na qual se encontram métodos matemáticos para geração de ruído contínuo e natural.
 */
public class NoiseUtils
{
    /**
     * Calcula o Fractal Brownian Motion (fBm) em 2D, sobrepondo múltiplas camadas (oitavas) de Perlin Noise.
     *
     * @param x: Coordenada X do bloco no mundo.
     * @param z: Coordenada Z do bloco no mundo.
     * @param octaves: Ver acima.
     * @param scale: A escala base do ruído (controla a largura das montanhas).
     * @param persistence: A taxa de decréscimo da amplitude a cada oitava.
     * @param lacunarity: A taxa de crescimento da frequência a cada oitava.
     * @return: O valor do ruído fractal normalizado para o intervalo [0, 1].
     */
    public static float FBm(float x, float z, int octaves, float scale,
    float persistence = 0.5f, float lacunarity = 2.0f)
    {
        float value = 0f;          // Acumulador que guarda a soma dos valores de ruído a cada oitava
        float amplitude = 1f;      // Peso da oitava atual; diminui a cada iteração de acordo com 'persistence'
        float frequency = 1f;      // Nível de detalhe da oitava atual; varia a cada iteração de acordo com a 'lacunarity'
        float totalAmplitude = 0f; // Soma de todas as amplitudes aplicadas (necessária para normalizar o valor final)

        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * scale * frequency,
            z * scale * frequency) * amplitude;

            totalAmplitude += amplitude;

            amplitude *= persistence; // decresce a cada oitava
            frequency *= lacunarity; // cresce a cada oitava
        }
        return value / totalAmplitude; // normalizar para [0, 1]
    }

    /**
     * Simula ruído de Perlin 3D através da média de 6 amostragens de ruído 2D planas, cruzando os três eixos.
     *  
     * @param x: Coordenada X do bloco no mundo.
     * @param y: Coordenada Y do bloco no mundo.
     * @param z: Coordenada Z do bloco no mundo.
     * @return: O valor de ruído 3D suavizado.
     */
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
