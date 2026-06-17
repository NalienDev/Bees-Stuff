using UnityEngine;
using System.Collections.Generic;

/**
 * Classe que representa um bloco individual no mundo.
 * NOTA: Nao herda de MonoBehaviour. Esta abordagem elimina a criacao de GameObjects temporarios
 * e permite agrupar milhares de blocos numa unica draw call.
 */
public class Block
{
    /* Enumeracao das 6 faces possiveis de um cubo. */
    public enum CubeFace { Front, Back, Top, Bottom, Left, Right }

    /* Enumeracao dos tipos de bloco disponiveis, definindo a textura e dando confirmacao de existencia. */
    public enum BlockType { GRASS, DIRT, STONE, COBBLESTONE, BEDROCK, WATER, AIR, WOOD, LEAVES, TALL_GRASS, FLOWER, HIVE }

    /* Tipo do bloco individual. */
    public BlockType type;
    /* Posicao local do bloco dentro do chunk. */
    public Vector3 position;
    /* Define se o bloco e solido (contribui para colisoes e culling normal). */
    public bool isSolid;
    /* Define se o bloco e translucido (ex: folhas). Translucidos sao solidos visualmente mas nao ocluem faces vizinhas. */
    public bool isTranslucent;
    /* Define se o bloco usa uma malha em cruz (duas faces cruzadas) para vegetacao, em vez de um cubo. */
    public bool isCrossMesh;

    /* Os 8 vertices locais que compoem um cubo unitario centrado na origem [aula01]. */
    static readonly Vector3 v0 = new Vector3(-0.5f, -0.5f,  0.5f);
    static readonly Vector3 v1 = new Vector3( 0.5f, -0.5f,  0.5f);
    static readonly Vector3 v2 = new Vector3( 0.5f, -0.5f, -0.5f);
    static readonly Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
    static readonly Vector3 v4 = new Vector3(-0.5f,  0.5f,  0.5f);
    static readonly Vector3 v5 = new Vector3( 0.5f,  0.5f,  0.5f);
    static readonly Vector3 v6 = new Vector3( 0.5f,  0.5f, -0.5f);
    static readonly Vector3 v7 = new Vector3(-0.5f,  0.5f, -0.5f);

    /* CONSTRUTOR: Inicializa uma nova instancia de um bloco */
    public Block(BlockType type, Vector3 position)
    {
        this.type   = type;
        this.position = position;
        
        isCrossMesh = (type == BlockType.TALL_GRASS || type == BlockType.FLOWER);
        // Cross meshes nao sao solidas (nao tem colisao nem participam no face culling)
        isSolid = (type != BlockType.AIR && type != BlockType.WATER && !isCrossMesh);
        isTranslucent = (type == BlockType.LEAVES || type == BlockType.HIVE); // Hive tem cantos transparentes? Vamos tratar como translucido pra garantir que nao corta coisas.
    }

    /**
     * Calcula as coordenadas UV para uma face especifica do bloco com base num atlas de texturas.
     * Atlas: 16x16 tiles. Coordenadas (coluna, linha) contadas a partir do canto inferior-esquerdo.
     *
     * @param face: A face do cubo para a qual se pretende obter as coordenadas UV.
     * @param type: O tipo de bloco, que determina a textura a procurar no atlas.
     * @return: Um array de Vector2 com as 4 coordenadas UV correspondentes a textura no atlas.
     */
    public static Vector2[] GetUVs(CubeFace face, BlockType type)
    {
        // Canto inferior-esquerdo de cada textura no atlas (coluna, linha) / 16
        Vector2 lbc;

        if (type == BlockType.GRASS)
        {
            if      (face == CubeFace.Top)    lbc = new Vector2(2f,  6f) / 16f;
            else if (face == CubeFace.Bottom) lbc = new Vector2(2f, 15f) / 16f;
            else                              lbc = new Vector2(3f, 15f) / 16f;
        }
        else if (type == BlockType.DIRT)        lbc = new Vector2(2f, 15f) / 16f;
        else if (type == BlockType.WATER)       lbc = new Vector2(15f, 3f) / 16f;
        else if (type == BlockType.COBBLESTONE) lbc = new Vector2(0f, 14f) / 16f;
        else if (type == BlockType.BEDROCK)     lbc = new Vector2(1f, 14f) / 16f;
        // Wood log: top/bottom shows rings, sides show bark
        else if (type == BlockType.WOOD)
        {
            if (face == CubeFace.Top || face == CubeFace.Bottom)
                lbc = new Vector2(5f, 14f) / 16f; // log top (rings)
            else
                lbc = new Vector2(4f, 14f) / 16f; // log side (bark)
        }
        // Leaves: single tile
        else if (type == BlockType.LEAVES)      lbc = new Vector2(4f, 12f) / 16f;
        else if (type == BlockType.TALL_GRASS)  lbc = new Vector2(7f, 13f) / 16f;
        else if (type == BlockType.FLOWER)      lbc = new Vector2(12f, 15f) / 16f; // red flower
        else if (type == BlockType.HIVE)
        {
            if (face == CubeFace.Top || face == CubeFace.Bottom)
                lbc = new Vector2(5f, 14f) / 16f; // wood top for hive
            else
                lbc = new Vector2(11f, 8f) / 16f; // generic texture for hive (pumpkin face/crafting table front, etc. Pick 11,8 or similar)
        }
        else                                    lbc = new Vector2(1f, 15f) / 16f; // fallback: stone

        Vector2 uv00 = lbc;
        Vector2 uv10 = lbc + new Vector2(1f, 0f) / 16f;
        Vector2 uv01 = lbc + new Vector2(0f, 1f) / 16f;
        Vector2 uv11 = lbc + new Vector2(1f, 1f) / 16f;
        return new[] { uv11, uv01, uv00, uv10 };
    }

    /**
     * Adiciona os vertices, triangulos e UVs de uma face visivel as listas partilhadas do chunk.
     * NOTA: Utiliza um offset (vertexIndex) para garantir que os indices dos triangulos referenciam os vertices corretos
     * desta face, prevenindo corrupcao da mesh do chunk ao juntar multiplos blocos no primeiro indice.
     *
     * @param face: A face do cubo a processar.
     * @param vertices: Lista partilhada de vertices do chunk.
     * @param triangles: Lista partilhada de indices de triangulos do chunk.
     * @param uvs: Lista partilhada de coordenadas UV do chunk.
     */
    public void AddFaceToMeshData(CubeFace face, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        int vertexIndex = vertices.Count;

        Vector3[] faceVertices;
        switch (face)
        {
            case CubeFace.Front:  faceVertices = new[] { v4, v5, v1, v0 }; break;
            case CubeFace.Back:   faceVertices = new[] { v6, v7, v3, v2 }; break;
            case CubeFace.Top:    faceVertices = new[] { v7, v6, v5, v4 }; break;
            case CubeFace.Bottom: faceVertices = new[] { v0, v1, v2, v3 }; break;
            case CubeFace.Left:   faceVertices = new[] { v7, v4, v0, v3 }; break;
            case CubeFace.Right:  faceVertices = new[] { v5, v6, v2, v1 }; break;
            default:              faceVertices = null; break;
        }

        if (faceVertices == null) return;

        int[] tri = new int[] { 3, 1, 0, 3, 2, 1 };
        Vector2[] uv = GetUVs(face, type);

        for (int i = 0; i < 4; i++)
        {
            vertices.Add(faceVertices[i] + this.position);
            uvs.Add(uv[i]);
        }
        for (int i = 0; i < 6; i++)
        {
            triangles.Add(vertexIndex + tri[i]);
        }
    }

    /**
     * Adiciona a malha em cruz (duas faces intersetadas em X) para vegetacao.
     * Nao sofre face culling e nao tem colisao.
     */
    public void AddCrossToMeshData(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        int vertexIndex = vertices.Count;
        Vector2[] uv = GetUVs(CubeFace.Front, type); // textura unica

        // Definir os dois quads cruzados nas diagonais do cubo unitario
        // Quad 1: (-0.5, -0.5, -0.5) a (0.5, 0.5, 0.5)
        Vector3 q1v0 = new Vector3(-0.5f, -0.5f, -0.5f) + position;
        Vector3 q1v1 = new Vector3( 0.5f, -0.5f,  0.5f) + position;
        Vector3 q1v2 = new Vector3( 0.5f,  0.5f,  0.5f) + position;
        Vector3 q1v3 = new Vector3(-0.5f,  0.5f, -0.5f) + position;

        // Quad 2: (-0.5, -0.5, 0.5) a (0.5, 0.5, -0.5)
        Vector3 q2v0 = new Vector3(-0.5f, -0.5f,  0.5f) + position;
        Vector3 q2v1 = new Vector3( 0.5f, -0.5f, -0.5f) + position;
        Vector3 q2v2 = new Vector3( 0.5f,  0.5f, -0.5f) + position;
        Vector3 q2v3 = new Vector3(-0.5f,  0.5f,  0.5f) + position;

        Vector3[] crossVertices = { 
            q1v0, q1v1, q1v2, q1v3, // Frente quad 1
            q1v1, q1v0, q1v3, q1v2, // Tras quad 1
            q2v0, q2v1, q2v2, q2v3, // Frente quad 2
            q2v1, q2v0, q2v3, q2v2  // Tras quad 2
        };

        Vector2[] crossUvs = {
            uv[2], uv[3], uv[0], uv[1],
            uv[2], uv[3], uv[0], uv[1],
            uv[2], uv[3], uv[0], uv[1],
            uv[2], uv[3], uv[0], uv[1]
        };

        int[] triTemplate = { 0, 2, 1, 0, 3, 2 };

        for (int i = 0; i < 16; i++)
        {
            vertices.Add(crossVertices[i]);
            uvs.Add(crossUvs[i]);
        }

        for (int q = 0; q < 4; q++)
        {
            for (int i = 0; i < 6; i++)
            {
                triangles.Add(vertexIndex + (q * 4) + triTemplate[i]);
            }
        }
    }
}
