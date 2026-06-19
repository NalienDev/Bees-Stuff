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
    public enum BlockType { GRASS, DIRT, STONE, COBBLESTONE, BEDROCK, WATER, AIR, WOOD, LEAVES, SHORT_GRASS, FLOWER, HIVE }

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

    /* Dicionario global para guardar as coordenadas UV de cada textura gerada dinamicamente. */
    private static Dictionary<string, Rect> textureUVs = new Dictionary<string, Rect>();

    /* O material unico gerado que contem o atlas de texturas agrupado em tempo de execucao. */
    public static Material AtlasMaterial { get; private set; }

    /* Os 8 vertices locais que compoem um cubo unitario centrado na origem [aula01]. */
    static readonly Vector3 v0 = new Vector3(-0.5f, -0.5f, 0.5f);
    static readonly Vector3 v1 = new Vector3(0.5f, -0.5f, 0.5f);
    static readonly Vector3 v2 = new Vector3(0.5f, -0.5f, -0.5f);
    static readonly Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
    static readonly Vector3 v4 = new Vector3(-0.5f, 0.5f, 0.5f);
    static readonly Vector3 v5 = new Vector3(0.5f, 0.5f, 0.5f);
    static readonly Vector3 v6 = new Vector3(0.5f, 0.5f, -0.5f);
    static readonly Vector3 v7 = new Vector3(-0.5f, 0.5f, -0.5f);

    /* CONSTRUTOR: Inicializa uma nova instancia de um bloco */
    public Block(BlockType type, Vector3 position)
    {
        this.type = type;
        this.position = position;

        isCrossMesh = (type == BlockType.SHORT_GRASS || type == BlockType.FLOWER);
        // Cross meshes nao sao solidas (nao tem colisao nem participam no face culling)
        isSolid = (type != BlockType.AIR && type != BlockType.WATER && !isCrossMesh);
        isTranslucent = (type == BlockType.LEAVES || type == BlockType.HIVE); // Hive tem cantos transparentes? Vamos tratar como translucido pra garantir que nao corta coisas.
    }

    /**
     * Inicializa o Atlas de Texturas em Tempo de Execucao.
     * Carrega as imagens individuais das pastas Resources/Blocks/ e Resources/TransparentBlocks/
     * e agrupa-as numa única textura (Atlas) para manter a otimizacao de 1 draw call.
     *
     * @param voxelShader: O Shader que sera utilizado para criar o material do Atlas.
     */
    public static void InitializeAtlas(Material baseMaterial)
    {
        if (textureUVs.Count > 0) return; // Ja inicializado

        List<Texture2D> texturesToPack = new List<Texture2D>();
        List<string> textureKeys = new List<string>();

        // Funcao lambda interna auxiliar para carregar ficheiros de Resources
        System.Action<string, bool> loadTex = (fileName, isTransparent) => {
            string path = (isTransparent ? "TransparentBlocks/" : "Blocks/") + fileName;
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                texturesToPack.Add(tex);
                textureKeys.Add(fileName.ToLower());
            }
            else
            {
                Debug.LogError($"[Voxel Engine] Nao foi possivel encontrar a textura em: Resources/{path}");
            }
        };

        // Carregar texturas solidas da pasta Blocks/
        loadTex("dirt", false);
        loadTex("stone", false);
        loadTex("cobblestone", false);
        loadTex("bedrock", false);
        loadTex("water", false);
        loadTex("grass_top", false);
        loadTex("grass_side", false);
        loadTex("wood_top", false);
        loadTex("wood_side", false);

        // Carregar texturas transparentes/recortadas da pasta TransparentBlocks/
        loadTex("leaves", true);
        loadTex("short_grass", true);
        loadTex("flower", true);
        loadTex("hive_top", true);
        loadTex("hive_side", true);

        // Criar a textura do atlas global em alta resolucao
        Texture2D runtimeAtlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
        runtimeAtlas.filterMode = FilterMode.Point; // Mantem o aspeto pixel-art nitido sem esborratar

        // O Unity junta as texturas todas aqui e retorna os sub-retangulos (coordenadas) correspondentes
        Rect[] rects = runtimeAtlas.PackTextures(texturesToPack.ToArray(), 2, 2048);

        // Guardar as coordenadas UV resultantes no dicionario indexadas pelo nome do ficheiro
        for (int i = 0; i < textureKeys.Count; i++)
        {
            textureUVs.Add(textureKeys[i], rects[i]);
        }

        // Criar o material unico que os seus Chunks vao partilhar
        AtlasMaterial = new Material(baseMaterial);
        AtlasMaterial.mainTexture = runtimeAtlas;
        AtlasMaterial.color = Color.white;
        AtlasMaterial.SetInt("_Cull", 0);
    }

    /**
     * Devolve o nome do ficheiro .png correspondente com base no tipo de bloco e na face especifica.
     */
    private static string GetTextureKey(CubeFace face, BlockType type)
    {
        switch (type)
        {
            case BlockType.GRASS:
                if (face == CubeFace.Top) return "grass_top";
                if (face == CubeFace.Bottom) return "dirt";
                return "grass_side";
            case BlockType.WOOD:
                if (face == CubeFace.Top || face == CubeFace.Bottom) return "wood_top";
                return "wood_side";
            case BlockType.HIVE:
                if (face == CubeFace.Top || face == CubeFace.Bottom) return "hive_top";
                return "hive_side";
            case BlockType.DIRT: return "dirt";
            case BlockType.STONE: return "stone";
            case BlockType.COBBLESTONE: return "cobblestone";
            case BlockType.BEDROCK: return "bedrock";
            case BlockType.WATER: return "water";
            case BlockType.LEAVES: return "leaves";
            case BlockType.SHORT_GRASS: return "short_grass";
            case BlockType.FLOWER: return "flower";
            default: return "stone"; // Fallback de seguranca
        }
    }

    /**
     * Calcula as coordenadas UV para uma face especifica do bloco com base no atlas gerado em tempo de execucao.
     * Mapeia dinamicamente os cantos do sub-rectangulo obtido pelo empacotamento de texturas.
     *
     * @param face: A face do cubo para a qual se pretende obter as coordenadas UV.
     * @param type: O tipo de bloco, que determina a textura a procurar.
     * @return: Um array de Vector2 com as 4 coordenadas UV correspondentes a textura no atlas.
     */
    public static Vector2[] GetUVs(CubeFace face, BlockType type)
    {
        string key = GetTextureKey(face, type);

        // Vai buscar o retangulo da textura gerada ou usa o tamanho maximo em caso de falha externa
        Rect rect = textureUVs.ContainsKey(key) ? textureUVs[key] : new Rect(0, 0, 1, 1);

        // Mapeia os 4 cantos do sub-rectangulo do atlas retornado pelo PackTextures
        Vector2 uv00 = new Vector2(rect.xMin, rect.yMin);
        Vector2 uv10 = new Vector2(rect.xMax, rect.yMin);
        Vector2 uv01 = new Vector2(rect.xMin, rect.yMax);
        Vector2 uv11 = new Vector2(rect.xMax, rect.yMax);

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
            case CubeFace.Front: faceVertices = new[] { v4, v5, v1, v0 }; break;
            case CubeFace.Back: faceVertices = new[] { v6, v7, v3, v2 }; break;
            case CubeFace.Top: faceVertices = new[] { v7, v6, v5, v4 }; break;
            case CubeFace.Bottom: faceVertices = new[] { v0, v1, v2, v3 }; break;
            case CubeFace.Left: faceVertices = new[] { v7, v4, v0, v3 }; break;
            case CubeFace.Right: faceVertices = new[] { v5, v6, v2, v1 }; break;
            default: faceVertices = null; break;
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

        if (type == BlockType.LEAVES)
        {
            int backVertexIndex = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(faceVertices[i] + this.position);
                uvs.Add(uv[i]);
            }
            int[] backTri = new int[] { 0, 1, 3, 1, 2, 3 };
            for (int i = 0; i < 6; i++)
            {
                triangles.Add(backVertexIndex + backTri[i]);
            }
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
        Vector3 q1v1 = new Vector3(0.5f, -0.5f, 0.5f) + position;
        Vector3 q1v2 = new Vector3(0.5f, 0.5f, 0.5f) + position;
        Vector3 q1v3 = new Vector3(-0.5f, 0.5f, -0.5f) + position;

        // Quad 2: (-0.5, -0.5, 0.5) a (0.5, 0.5, -0.5)
        Vector3 q2v0 = new Vector3(-0.5f, -0.5f, 0.5f) + position;
        Vector3 q2v1 = new Vector3(0.5f, -0.5f, -0.5f) + position;
        Vector3 q2v2 = new Vector3(0.5f, 0.5f, -0.5f) + position;
        Vector3 q2v3 = new Vector3(-0.5f, 0.5f, 0.5f) + position;

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




