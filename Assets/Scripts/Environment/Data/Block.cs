using UnityEngine;
using System.Collections.Generic;


public class Block
{

    public enum CubeFace { Front, Back, Top, Bottom, Left, Right }


    public enum BlockType { GRASS, DIRT, STONE, COBBLESTONE, BEDROCK, WATER, AIR, WOOD, LEAVES, SHORT_GRASS, FLOWER, HIVE, SAND, CACTUS, SNOW, PINE_LEAVES, PINE_WOOD, JUNGLE_WOOD, JUNGLE_LEAVES, MOSS, SNOWBUSH }


    public BlockType type;

    public Vector3 position;

    public bool isSolid;

    public bool isTranslucent;

    public bool isCrossMesh;


    private static Dictionary<string, Rect> textureUVs = new Dictionary<string, Rect>();


    public static Material AtlasMaterial { get; private set; }


    static readonly Vector3 v0 = new Vector3(-0.5f, -0.5f, 0.5f);
    static readonly Vector3 v1 = new Vector3(0.5f, -0.5f, 0.5f);
    static readonly Vector3 v2 = new Vector3(0.5f, -0.5f, -0.5f);
    static readonly Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
    static readonly Vector3 v4 = new Vector3(-0.5f, 0.5f, 0.5f);
    static readonly Vector3 v5 = new Vector3(0.5f, 0.5f, 0.5f);
    static readonly Vector3 v6 = new Vector3(0.5f, 0.5f, -0.5f);
    static readonly Vector3 v7 = new Vector3(-0.5f, 0.5f, -0.5f);


    public Block(BlockType type, Vector3 position)
    {
        this.position = position;
        SetType(type);
    }


    public void SetType(BlockType newType)
    {
        this.type = newType;
        this.isCrossMesh = (type == BlockType.SHORT_GRASS || type == BlockType.FLOWER || type == BlockType.SNOWBUSH);

        this.isSolid = (type != BlockType.AIR && type != BlockType.WATER && !this.isCrossMesh);
        this.isTranslucent = (type == BlockType.LEAVES || type == BlockType.PINE_LEAVES || type == BlockType.JUNGLE_LEAVES || type == BlockType.HIVE);
    }


    public static void InitializeAtlas(Material baseMaterial)
    {
        if (textureUVs.Count > 0) return;

        List<Texture2D> texturesToPack = new List<Texture2D>();
        List<string> textureKeys = new List<string>();


        System.Action<string, bool> loadTex = (fileName, isTransparent) =>
        {
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


        loadTex("dirt", false);
        loadTex("stone", false);
        loadTex("cobblestone", false);
        loadTex("bedrock", false);
        loadTex("water", false);
        loadTex("grass_top", false);
        loadTex("grass_side", false);
        loadTex("wood_top", false);
        loadTex("wood_side", false);
        loadTex("sand", false);
        loadTex("snow", false);
        loadTex("cactus", false);
        loadTex("grass_snow", false);
        loadTex("wood_top_pine", false);
        loadTex("wood_side_pine", false);
        loadTex("wood_top_jungle", false);
        loadTex("wood_side_jungle", false);
        loadTex("moss", false);
        loadTex("hive_top", false);
        loadTex("hive_side", false);


        loadTex("leaves", true);
        loadTex("short_grass", true);
        loadTex("flower", true);
        loadTex("leaves_pine", true);
        loadTex("leaves_jungle", true);
        loadTex("snowbush", true);


        Texture2D runtimeAtlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, true);
        runtimeAtlas.filterMode = FilterMode.Point;
        runtimeAtlas.anisoLevel = 1;


        Rect[] rects = runtimeAtlas.PackTextures(texturesToPack.ToArray(), 4, 2048, false);


        DilateEdges(runtimeAtlas, rects);


        for (int i = 0; i < textureKeys.Count; i++)
        {
            textureUVs.Add(textureKeys[i], rects[i]);
        }


        AtlasMaterial = new Material(baseMaterial);
        AtlasMaterial.mainTexture = runtimeAtlas;
        AtlasMaterial.color = Color.white;

    }


    private static string GetTextureKey(CubeFace face, BlockType type)
    {
        switch (type)
        {
            case BlockType.GRASS:
                if (face == CubeFace.Top) return "grass_top";
                if (face == CubeFace.Bottom) return "dirt";
                return "grass_side";
            case BlockType.SNOW:
                if (face == CubeFace.Top) return "snow";
                if (face == CubeFace.Bottom) return "dirt";
                return "grass_snow";
            case BlockType.MOSS: return "moss";
            case BlockType.SNOWBUSH: return "snowbush";
            case BlockType.WOOD:
                if (face == CubeFace.Top || face == CubeFace.Bottom) return "wood_top";
                return "wood_side";
            case BlockType.PINE_WOOD:
                if (face == CubeFace.Top || face == CubeFace.Bottom) return "wood_top_pine";
                return "wood_side_pine";
            case BlockType.JUNGLE_WOOD:
                if (face == CubeFace.Top || face == CubeFace.Bottom) return "wood_top_jungle";
                return "wood_side_jungle";
            case BlockType.HIVE:
                if (face == CubeFace.Top || face == CubeFace.Bottom) return "hive_top";
                return "hive_side";
            case BlockType.DIRT: return "dirt";
            case BlockType.STONE: return "stone";
            case BlockType.COBBLESTONE: return "cobblestone";
            case BlockType.BEDROCK: return "bedrock";
            case BlockType.WATER: return "water";
            case BlockType.LEAVES: return "leaves";
            case BlockType.PINE_LEAVES: return "leaves_pine";
            case BlockType.JUNGLE_LEAVES: return "leaves_jungle";
            case BlockType.SHORT_GRASS: return "short_grass";
            case BlockType.FLOWER: return "flower";
            case BlockType.SAND: return "sand";
            case BlockType.CACTUS: return "cactus";
            default: return "stone";
        }
    }


    public static Vector2[] GetUVs(CubeFace face, BlockType type)
    {
        string key = GetTextureKey(face, type);


        Rect rect = textureUVs.ContainsKey(key) ? textureUVs[key] : new Rect(0, 0, 1, 1);



        float inset = 0.0005f;
        Vector2 uv00 = new Vector2(rect.xMin + inset, rect.yMin + inset);
        Vector2 uv10 = new Vector2(rect.xMax - inset, rect.yMin + inset);
        Vector2 uv01 = new Vector2(rect.xMin + inset, rect.yMax - inset);
        Vector2 uv11 = new Vector2(rect.xMax - inset, rect.yMax - inset);

        return new[] { uv11, uv01, uv00, uv10 };
    }


    private static void DilateEdges(Texture2D atlas, Rect[] rects)
    {
        Color32[] pixels = atlas.GetPixels32();
        int width = atlas.width;
        int height = atlas.height;

        foreach (Rect r in rects)
        {
            int rx = Mathf.RoundToInt(r.xMin * width);
            int ry = Mathf.RoundToInt(r.yMin * height);
            int rw = Mathf.RoundToInt(r.width * width);
            int rh = Mathf.RoundToInt(r.height * height);


            for (int p = 1; p <= 2; p++)
            {
                if (rx - p < 0) continue;
                for (int y = 0; y < rh; y++)
                    pixels[(ry + y) * width + (rx - p)] = pixels[(ry + y) * width + rx];
            }


            for (int p = 1; p <= 2; p++)
            {
                if (rx + rw - 1 + p >= width) continue;
                for (int y = 0; y < rh; y++)
                    pixels[(ry + y) * width + (rx + rw - 1 + p)] = pixels[(ry + y) * width + (rx + rw - 1)];
            }


            for (int p = 1; p <= 2; p++)
            {
                if (ry + rh - 1 + p >= height) continue;
                for (int x = -2; x < rw + 2; x++)
                {
                    int px = Mathf.Clamp(rx + x, rx, rx + rw - 1);
                    int writeX = Mathf.Clamp(rx + x, 0, width - 1);
                    pixels[(ry + rh - 1 + p) * width + writeX] = pixels[(ry + rh - 1) * width + px];
                }
            }


            for (int p = 1; p <= 2; p++)
            {
                if (ry - p < 0) continue;
                for (int x = -2; x < rw + 2; x++)
                {
                    int px = Mathf.Clamp(rx + x, rx, rx + rw - 1);
                    int writeX = Mathf.Clamp(rx + x, 0, width - 1);
                    pixels[(ry - p) * width + writeX] = pixels[ry * width + px];
                }
            }
        }

        atlas.SetPixels32(pixels);


        atlas.Apply(true);
    }


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

        if (type == BlockType.LEAVES || type == BlockType.PINE_LEAVES || type == BlockType.JUNGLE_LEAVES)
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


    public void AddCrossToMeshData(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        int vertexIndex = vertices.Count;
        Vector2[] uv = GetUVs(CubeFace.Front, type);



        Vector3 q1v0 = new Vector3(-0.5f, -0.5f, -0.5f) + position;
        Vector3 q1v1 = new Vector3(0.5f, -0.5f, 0.5f) + position;
        Vector3 q1v2 = new Vector3(0.5f, 0.5f, 0.5f) + position;
        Vector3 q1v3 = new Vector3(-0.5f, 0.5f, -0.5f) + position;


        Vector3 q2v0 = new Vector3(-0.5f, -0.5f, 0.5f) + position;
        Vector3 q2v1 = new Vector3(0.5f, -0.5f, -0.5f) + position;
        Vector3 q2v2 = new Vector3(0.5f, 0.5f, -0.5f) + position;
        Vector3 q2v3 = new Vector3(-0.5f, 0.5f, 0.5f) + position;

        Vector3[] crossVertices = {
            q1v0, q1v1, q1v2, q1v3,
            q1v1, q1v0, q1v3, q1v2,
            q2v0, q2v1, q2v2, q2v3,
            q2v1, q2v0, q2v3, q2v2
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





