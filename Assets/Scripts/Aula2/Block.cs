using UnityEngine;
using System.Collections.Generic;

/**
 * Classe que representa um bloco individual no mundo.
 * NOTA: Não herda de MonoBehaviour. Esta abordagem elimina a criação de GameObjects temporários 
 * e permite agrupar milhares de blocos numa única draw call.
 */
public class Block
{
    /* Enumeração das 6 faces possíveis de um cubo. */
    public enum CubeFace { Front, Back, Top, Bottom, Left, Right }
    /* Enumeração dos tipos de bloco disponíveis, definindo a textura e dando confirmação de existência. */
    public enum BlockType { GRASS, DIRT, STONE, COBBLESTONE, BEDROCK, WATER, AIR }
    /* Tipo do bloco individual. */
    public BlockType type;
    /* Posição local do bloco dentro do chunk. */
    public Vector3 position;
    /* Define se o bloco é sólido (desenhado na mesh) ou invisível (ex.: ar). */
    public bool isSolid;
    /* Os 8 vértices locais que compõem um cubo unitário centrado na origem [aula01]. */
    static readonly Vector3 v0 = new Vector3(-0.5f, -0.5f, 0.5f);
    static readonly Vector3 v1 = new Vector3(0.5f, -0.5f, 0.5f);
    static readonly Vector3 v2 = new Vector3(0.5f, -0.5f, -0.5f);
    static readonly Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
    static readonly Vector3 v4 = new Vector3(-0.5f, 0.5f, 0.5f);
    static readonly Vector3 v5 = new Vector3(0.5f, 0.5f, 0.5f);
    static readonly Vector3 v6 = new Vector3(0.5f, 0.5f, -0.5f);
    static readonly Vector3 v7 = new Vector3(-0.5f, 0.5f, -0.5f);

    /* CONSTRUTOR: Inicializa uma nova instância de um bloco */
    public Block(BlockType type, Vector3 position)
    {
        this.type = type;
        this.position = position;
        isSolid = (type != BlockType.AIR && type != BlockType.WATER);
    }

    /**
     * Calcula as coordenadas UV para uma face específica do bloco com base num atlas de texturas.
     * 
     * * @param face: A face do cubo para a qual se pretende obter as coordenadas UV.
     * * @param type: O tipo de bloco, que determina a textura a procurar no atlas.
     * * @return: Um array de Vector2 com as 4 coordenadas UV correspondentes à textura no atlas.
     */
    public static Vector2[] GetUVs(CubeFace face, BlockType type)
    {
        // Canto inferior-esquerdo de cada textura no atlas (coluna, linha) / 16
        Vector2 lbc;
        if (type == BlockType.GRASS)
        {
            if (face == CubeFace.Top) lbc = new Vector2(2f, 6f) / 16;
            else if (face == CubeFace.Bottom) lbc = new Vector2(2f, 15f) / 16;
            else lbc = new Vector2(3f, 15f) / 16;
        }
        else if (type == BlockType.DIRT) lbc = new Vector2(2f, 15f) / 16;
        else if (type == BlockType.WATER) lbc = new Vector2(15f, 3f) / 16;
        else if (type == BlockType.COBBLESTONE) lbc = new Vector2(0f, 14f) / 16;
        else if (type == BlockType.BEDROCK) lbc = new Vector2(1f, 14f) / 16;
        else lbc = new Vector2(1f, 15f) / 16;
        Vector2 uv00 = lbc;                            // inferior-esquerdo
        Vector2 uv10 = lbc + new Vector2(1f, 0f) / 16; // inferior-direito
        Vector2 uv01 = lbc + new Vector2(0f, 1f) / 16; // superior-esquerdo
        Vector2 uv11 = lbc + new Vector2(1f, 1f) / 16; // superior-direito
        return new[] { uv11, uv01, uv00, uv10 };
    }

    /**
     * Adiciona os vértices, triângulos e UVs de uma face visível às listas partilhadas do chunk.
     * NOTA: Utiliza um offset (vertexIndex) para garantir que os índices dos triângulos referenciam os vértices corretos 
     * desta face, prevenindo corrupção da mesh do chunk ao juntar múltiplos blocos no primeiro índice.
     * 
     * * @param face: A face do cubo a processar.
     * * @param vertices: Lista partilhada de vértices do chunk.
     * * @param triangles: Lista partilhada de índices de triângulos do chunk.
     * * @param uvs: Lista partilhada de coordenadas UV do chunk.
     */
    public void AddFaceToMeshData(CubeFace face, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        // Obter o indice do último vértice.
        int vertexIndex = vertices.Count;

        // 2. Obter os 4 vértices da face (ver tabela da aula01)
        Vector3[] faceVertices;

        switch (face)
        {
            default:
                faceVertices = null;
                break;
            case CubeFace.Front:
                faceVertices = new Vector3[] { v4, v5, v1, v0 };
                break;
            case CubeFace.Back:
                faceVertices = new Vector3[] { v6, v7, v3, v2 };
                break;
            case CubeFace.Top:
                faceVertices = new Vector3[] { v7, v6, v5, v4 };
                break;
            case CubeFace.Bottom:
                faceVertices = new Vector3[] { v0, v1, v2, v3 };
                break;
            case CubeFace.Left:
                faceVertices = new Vector3[] { v7, v4, v0, v3 };
                break;
            case CubeFace.Right:
                faceVertices = new Vector3[] { v5, v6, v2, v1 };
                break;
        }

        int[] triangle = new int[] { 3, 1, 0, 3, 2, 1 };
        Vector2[] uv = GetUVs(face, type);

        // 3. Somar this.position a cada vértice
        for (int i = 0; i < 4; i++)
        {
            faceVertices[i] += this.position;

            // 4. Adicionar vértices e UVs às listas
            vertices.Add(faceVertices[i]);
            uvs.Add(uv[i]);
        }

        // 5. Adicionar triângulos COM OFFSET (vertexIndex + ...)
        for (int i = 0; i < 6; i++)
        {
            triangles.Add(vertexIndex + triangle[i]);
        }
    }
}