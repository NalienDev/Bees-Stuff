using System.Collections.Generic;
using UnityEngine;
using static Config;

/**
 * Classe que representa um chunk (matriz 3D) de blocos no mundo.
 */
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    [Header("Chunk")]
    [Tooltip("Matriz 3D que armazena todas as instâncias de blocos gerados neste chunk")]
    public Block[,,] chunkData;
    [Tooltip("Material aplicado à malha gerada — deve conter o atlas de texturas dos blocos")]
    public Material chunkMaterial;
    [Tooltip("Coordenada do chunk no mundo em unidades de chunk — usado para calcular posições globais")]
    public Vector2Int worldOffset;

    [Header("Terreno")]
    [Tooltip("Escala do ruído FBm base — valores menores criam relevos mais suaves e extensos, valores maiores criam relevos mais acidentados")]
    public float scale = 0.05f;
    [Tooltip("Número de camadas de ruído FBm sobrepostas — valores maiores adicionam mais detalhe topográfico ao terreno")]
    public int octaves = 4;
    [Tooltip("Escala do ruído 3D de densidade volumétrica — controla a variação orgânica da superfície e criação de saliências")]
    public float densityScale = 0.02f;
    [Tooltip("Limiar de densidade para decidir se um bloco é sólido — valores mais altos criam menos terreno sólido")]
    public float densityThreshold = 0f;
    [Tooltip("Altura máxima em blocos até onde o terreno sólido base pode ser gerado")]
    public int maxSolidHeight = 26;
    [Tooltip("Altura do mar")]
    public int seaLevel = 8;
    [Tooltip("Amplitude a que é gerado detalhe no terreno")]
    public int detailAmplitude = 3;
    [Tooltip("Peso do Perlin3D de detalhe — valores maiores criam mais overhangs e variação vertical")]
    public float detailWeight = 8f;

    [Header("Grutas")]
    [Tooltip("Escala do ruído 3D para escavação — valores maiores criam grutas mais pequenas e frequentes")]
    public float caveScale = 0.2f;
    [Tooltip("Limiar que determina a frequência de escavação — valores mais altos criam menos grutas")]
    public float caveThreshold = 0.4f;
    [Tooltip("Margem de segurança para evitar escavação nos limites verticais do chunk")]
    public int margin = 2;
    [Tooltip("Altura máxima permitida para a superfície base")]
    public int maxSurfaceHeight = 5;

    [Header("Worms")]
    [Tooltip("Número de worms gerados por chunk — valores altos criam mais túneis mas aumentam o custo de geração")]
    public int wormsPerChunk = 1;
    [Tooltip("Número de passos que cada worm percorre — valores maiores criam túneis mais longos")]
    public int steps = 200;
    [Tooltip("Raio da esfera de escavação em cada passo do worm")]
    public float radius = 3f;
    [Tooltip("Distância percorrida pelo worm em cada passo — valores maiores criam túneis mais espaçados")]
    public float stepSize = 10f;
    [Tooltip("Escala do ruído de direcção do worm — valores menores criam túneis mais suaves e curvos")]
    public float directionScale = 0.05f;

    [Header("Cross-Chunk Culling")]
    // Extensão D — referência ao WorldManager para cross-chunk culling
    public WorldManager worldManager;

    /**
     * Inicializa os atributos do chunk e faz o arranque da geração da matriz de blocos.
     * 
     * @param offset: A coordenada 2D deste chunk na grelha do mundo.
     * @param mat: O material que será atribuído ao MeshRenderer.
     * @param manager: Referência opcional ao WorldManager (Extensão D).
     */
    public void Initialize(Vector2Int offset, Material mat, WorldManager manager = null)
    {
        worldOffset = offset;
        chunkMaterial = mat;
        worldManager = manager;
        // geração controlada pelo WorldManager via GenerateChunkData()
    }

    /**
     * Preenche a matriz de blocos 'chunkData':
     * 1. Cálculo da altura da superfície (heightmap 2D);
     * 2. Geração da densidade 3D (densitymap 3D);
     * 3. Escavação de cavernas (Worms - CaveGenerator.cs);
     * 4. Colocação de água;
     * 5. Texturização das paredes interiores das grutas.
     */
    void InitializeChunk()
    {
        chunkData = new Block[chunkSize, chunkHeight, chunkSize];
        int[,] surfaceHeight = new int[chunkSize, chunkSize];

        // 1. Calcular alturas
        GetColumnSurfaceHeight(surfaceHeight);
        // 2. Gerar terreno sólido com camadas base
        CreateInitialChunkData(surfaceHeight);
        // 3. Escavar grutas (transforma blocos em AIR)
        CaveGenerator.GenerateWorms(chunkData, chunkSize, chunkHeight, worldOffset, wormsPerChunk, steps, radius, stepSize, directionScale);
        // 4. Gerar água
        GenerateWater();
        // 5. Atualizar terra exposta pela escavação
        UpdateExposedDirt();
        // 6. Plantar árvores e vegetação
        TreeGenerator.PlantTrees(chunkData, worldOffset, seaLevel);
        VegetationGenerator.PlantVegetation(chunkData, worldOffset, seaLevel);
    }

    /**
     * Gera apenas os dados do chunk — seguro para correr fora da main thread.
     * Não usa nenhuma API do Unity que exija a main thread.
     */
    public void GenerateChunkData()
    {
        int[,] surfaceHeight = new int[chunkSize, chunkSize];
        GetColumnSurfaceHeight(surfaceHeight);
        CreateInitialChunkData(surfaceHeight);
        CaveGenerator.GenerateWorms(chunkData, chunkSize, chunkHeight, worldOffset, wormsPerChunk, steps, radius, stepSize, directionScale);
        GenerateWater();
        TextureCaveWalls(surfaceHeight);
        TreeGenerator.PlantTrees(chunkData, worldOffset, seaLevel);
        VegetationGenerator.PlantVegetation(chunkData, worldOffset, seaLevel);
    }

    /**
     * Calcula o ponto mais alto de terreno sólido para cada coluna (x, z) do chunk.
     * NOTA: Combina ruído FBm com Perlin 3D para permitir geração de overhangs.
     * 
     * @param surfaceHeight: Matriz 2D onde será guardada a altura máxima de cada coluna
     */
    private void GetColumnSurfaceHeight(int[,] surfaceHeight)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                surfaceHeight[x, z] = 0;
                for (int y = chunkHeight - 1; y >= 0; y--)
                {
                    float globalX = worldOffset.x * chunkSize + x + Config.seedOffsetX;
                    float globalZ = worldOffset.y * chunkSize + z + Config.seedOffsetZ;

                    float continentalness = NoiseUtils.FBm(globalX, globalZ, octaves / 2, densityScale / 4);
                    float baseHeight = NoiseUtils.FBm(globalX, globalZ, octaves, densityScale);
                    float detail = NoiseUtils.FBm(globalX, globalZ, (octaves / 2) * 3, densityScale * 5);

                    float h = Mathf.Lerp(seaLevel, maxSolidHeight,
                    continentalness * baseHeight)
                    + detail * detailAmplitude;


                    float d = NoiseUtils.Perlin3D(
                        (worldOffset.x * chunkSize + x) * densityScale,
                        y * densityScale,
                        (worldOffset.y * chunkSize + z) * densityScale);
                    if ((h - y) + d > 0f)
                    {
                        surfaceHeight[x, z] = y;
                        break;
                    }
                }
            }
        }
    }

    /**
     * Inicializa o terreno base e escava grutas, atribuindo tipos de bloco em camadas orgânicas (Bedrock a Grass).
     * Utiliza ruído Perlin aliado à profundidade da superfície para gerar transições geológicas naturais e irregulares.
     * * @param surfaceHeight: Matriz 2D com a altura da superfície por coluna, usada para calcular a profundidade.
     */
    private void CreateInitialChunkData(int[,] surfaceHeight)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float globalX = worldOffset.x * chunkSize + x + Config.seedOffsetX;
                float globalZ = worldOffset.y * chunkSize + z + Config.seedOffsetZ;

                for (int y = 0; y < chunkHeight; y++)
                {
                    // Cálculo da densidade do bloco atual
                    float heightNoise = NoiseUtils.FBm(globalX, globalZ, octaves, scale) * chunkHeight;
                    float densityNoise = NoiseUtils.Perlin3D(globalX * densityScale, y * densityScale, globalZ * densityScale);
                    float finalDensity = (heightNoise - y) + densityNoise * detailWeight;
                    bool solid = finalDensity > 0f;

                    // Escavação de cavernas por threshold
                    if (solid && y > 1 && y < maxSurfaceHeight - margin)
                    {
                        float cx = globalX * caveScale;
                        float cy = y * caveScale;
                        float cz = globalZ * caveScale;
                        float caveNoise = NoiseUtils.Perlin3D(cx, cy, cz);
                        if (caveNoise > caveThreshold)
                            solid = false;
                    }

                    // Cálculo corrigido da densidade do bloco acima para determinar com precisão se é superfície real
                    float densityNoiseAbove = NoiseUtils.Perlin3D(globalX * densityScale, (y + 1) * densityScale, globalZ * densityScale);
                    float finalDensityAbove = (heightNoise - (y + 1)) + densityNoiseAbove * detailWeight;
                    bool surfaceBlock = finalDensityAbove <= 0f;

                    // --- LÓGICA DE 'CAMADAS ORGÂNICAS' ---
                    Block.BlockType type;

                    // Ruído Perlin 3D para perturbar as transições geológicas
                    float layerNoise = NoiseUtils.Perlin3D(globalX * 0.1f, y * 0.1f, globalZ * 0.1f) * 4f;
                    int depthFromSurface = surfaceHeight[x, z] - y;

                    if (!solid)
                    {
                        type = Block.BlockType.AIR; //
                    }
                    else if (y <= 1 + (NoiseUtils.Perlin3D(globalX * 0.5f, y * 0.5f, globalZ * 0.5f) * 2f))
                    {
                        // Bedrock na base absoluta (y = 0 a ~3 dependendo do ruído)
                        type = Block.BlockType.BEDROCK;
                    }
                    else if (depthFromSurface > 25 + layerNoise)
                    {
                        // Camada muito profunda
                        type = Block.BlockType.COBBLESTONE;
                    }
                    else if (depthFromSurface > 0 + (layerNoise * 0.3f))
                    {
                        // Debaixo da terra
                        type = Block.BlockType.STONE;
                    }
                    else
                    {
                        // Camada superficial padrão
                        type = Block.BlockType.DIRT;
                    }

                    // --- MUTAÇÃO AMBIENTAL ---
                    // Se o bloco gerado for terra e a posição acima não for sólida (superfície), sofre mutação para relva
                    if (type == Block.BlockType.DIRT && surfaceBlock)
                    {
                        type = Block.BlockType.GRASS;
                    }

                    chunkData[x, y, z] = new Block(type, new Vector3(x, y, z)); //
                }
            }
        }
    }

    /**
     * Preenche os espaços de ar localizados abaixo do nível do mar (seaLevel) com blocos de água.
     */
    private void GenerateWater()
    {

        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (chunkData[x, y, z].type == Block.BlockType.AIR && y < seaLevel)
                        chunkData[x, y, z] = new Block(Block.BlockType.WATER, new Vector3(x, y, z));
                }
    }

    /**
     * Identifica superfícies interiores do terreno  e transforma blocos seriam terra em pedra (STONE).
     * 
     * @param surfaceHeight: Matriz que contém a altura da superfície para referência do que é interior
     */
    private void TextureCaveWalls(int[,] surfaceHeight)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    if (CaveGenerator.IsAir(chunkData, x, y, z)) continue;

                    // blocos na superfície ou acima não são paredes de gruta
                    if (y >= surfaceHeight[x, z]) continue;

                    bool nextToCarvedAir = CaveGenerator.HasCarvedAirNeighbour(
                        chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x, y, z);
                    if (nextToCarvedAir)
                        chunkData[x, y, z] = new Block(Block.BlockType.STONE, new Vector3(x, y, z));
                }
            }
        }
    }

    /**
     * Percorre o chunk após a escavação de grutas e transforma blocos de DIRT recém-expostos em GRASS.
     */
    private void UpdateExposedDirt()
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    // Apenas nos preocupamos com blocos que são DIRT
                    if (chunkData[x, y, z].type == Block.BlockType.DIRT)
                    {
                        // Se o bloco imediatamente acima for ar (porque foi escavado ou já o era), sofre mutação
                        if (y < chunkHeight - 1 && chunkData[x, y + 1, z].type == Block.BlockType.AIR)
                        {
                            chunkData[x, y, z].type = Block.BlockType.GRASS;
                        }
                    }
                }
            }
        }
    }

    /**
     * Constrói apenas a mesh de colisão do chunk — chamado sempre independentemente da visibilidade.
     */
    public void BuildCollisionMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (!block.isSolid) continue;
                    if (!HasSolidNeighbour(x, y, z + 1)) block.AddFaceToMeshData(Block.CubeFace.Front, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x, y, z - 1)) block.AddFaceToMeshData(Block.CubeFace.Back, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x, y + 1, z)) block.AddFaceToMeshData(Block.CubeFace.Top, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x, y - 1, z)) block.AddFaceToMeshData(Block.CubeFace.Bottom, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x - 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Left, vertices, triangles, uvs);
                    if (!HasSolidNeighbour(x + 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Right, vertices, triangles, uvs);
                }

        Mesh collisionMesh = new Mesh();
        collisionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        collisionMesh.vertices = vertices.ToArray();
        collisionMesh.triangles = triangles.ToArray();
        collisionMesh.RecalculateBounds();

        MeshCollider col = gameObject.GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = collisionMesh;
    }

    /**
     * Constrói a Mesh do chunk iterando pela sua matriz 3D.
     * Através de Face Culling, omite vértices e triângulos das faces ocultas entre blocos.
     */
    public void DrawChunk()
    {
        // 1. Criar listas partilhadas (vertices, triangles, uvs)
        List<Vector3> sharedVertices = new List<Vector3>();
        List<int> sharedTriangles = new List<int>();
        List<Vector2> sharedUvs = new List<Vector2>();

        // 2. Para cada bloco sólido ou vegetação: adicionar faces visíveis
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (block.isCrossMesh) 
                    {
                        block.AddCrossToMeshData(sharedVertices, sharedTriangles, sharedUvs);
                        continue;
                    }
                    if (!block.isSolid) continue;
                    if (!IsOpaqueOrSameNeighbour(x, y, z + 1, block.type)) block.AddFaceToMeshData(Block.CubeFace.Front, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x, y, z - 1, block.type)) block.AddFaceToMeshData(Block.CubeFace.Back, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x, y + 1, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Top, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x, y - 1, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Bottom, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x - 1, y, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Left, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x + 1, y, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Right, sharedVertices, sharedTriangles, sharedUvs);
                }

        // 3. Para cada bloco de água: adicionar faces contra ar
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (block.type != Block.BlockType.WATER) continue;
                    if (IsAir(x, y, z + 1)) block.AddFaceToMeshData(Block.CubeFace.Front, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x, y, z - 1)) block.AddFaceToMeshData(Block.CubeFace.Back, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x, y + 1, z)) block.AddFaceToMeshData(Block.CubeFace.Top, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x, y - 1, z)) block.AddFaceToMeshData(Block.CubeFace.Bottom, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x - 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Left, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x + 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Right, sharedVertices, sharedTriangles, sharedUvs);
                }

        // 4. Criar e atribuir mesh visual
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = sharedVertices.ToArray();
        mesh.triangles = sharedTriangles.ToArray();
        mesh.uv = sharedUvs.ToArray();

        // 4. RecalculateNormals + RecalculateBounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 5. Atribuir ao MeshFilter e MeshRenderer
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
        mr.material = chunkMaterial;
        // garantir que o renderer está ativo depois de desenhar
        mr.enabled = true;
    }

    bool IsAir(int x, int y, int z)
    {
        if (y < 0 || y >= chunkHeight) return true;
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            return chunkData[x, y, z].type == Block.BlockType.AIR;
        if (worldManager == null) return true;

        Vector2Int neighbourOffset = worldOffset;
        int localX = x;
        int localZ = z;

        if (x < 0) { neighbourOffset += Vector2Int.left; localX = chunkSize - 1; }
        else if (x >= chunkSize) { neighbourOffset += Vector2Int.right; localX = 0; }

        if (z < 0) { neighbourOffset += Vector2Int.down; localZ = chunkSize - 1; }
        else if (z >= chunkSize) { neighbourOffset += Vector2Int.up; localZ = 0; }

        Chunk neighbour = worldManager.GetChunk(neighbourOffset);
        if (neighbour == null || neighbour.chunkData == null) return true;

        return neighbour.chunkData[localX, y, localZ].type == Block.BlockType.AIR;
    }

    /**
     * Verifica se um bloco adjacente é sólido.
     * NOTE: Suporta consultas tanto dentro do próprio chunk como em chunks vizinhos geridos pelo WorldManager (Cross-chunk culling),
     * garantindo que não são geradas faces inúteis nas fronteiras dos chunks.
     * 
     * @param x: Coordenada X vizinha a inspecionar.
     * @param y: Coordenada Y vizinha a inspecionar.
     * @param z: Coordenada Z vizinha a inspecionar.
     * @return: True se o vizinho for sólido; False se for ar, vazio, ou se sair do limite vertical da altura do chunk.
     */
    bool HasSolidNeighbour(int x, int y, int z)
    {
        // y fora dos limites — sem chunks acima/abaixo
        if (y < 0 || y >= chunkHeight)
            return false;

        // Dentro do chunk — consulta local (comportamento original)
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            return chunkData[x, y, z].isSolid;

        // Extensão D — cross-chunk culling
        // Se não há WorldManager, trata a fronteira como vazia (comportamento original)
        if (worldManager == null)
            return false;

        // Calcular qual o chunk vizinho e a coordenada local dentro dele
        Vector2Int neighbourOffset = worldOffset;
        int localX = x;
        int localZ = z;

        if (x < 0)
        {
            neighbourOffset += Vector2Int.left;
            localX = chunkSize - 1;
        }
        else if (x >= chunkSize)
        {
            neighbourOffset += Vector2Int.right;
            localX = 0;
        }

        if (z < 0)
        {
            neighbourOffset += Vector2Int.down;
            localZ = chunkSize - 1;
        }
        else if (z >= chunkSize)
        {
            neighbourOffset += Vector2Int.up;
            localZ = 0;
        }

        Chunk neighbour = worldManager.GetChunk(neighbourOffset);

        // Se o vizinho não existe ou ainda não tem dados, trata como vazio
        if (neighbour == null || neighbour.chunkData == null)
            return false;

        return neighbour.chunkData[localX, y, localZ].isSolid;
    }

    /**
     * Verifica se o vizinho é opaco, ou se é do mesmo tipo translúcido.
     * Isto previne que blocos translúcidos (como folhas) ocultem faces de blocos sólidos,
     * mas permite que ocultem faces de outros blocos idênticos para poupar geometria.
     */
    bool IsOpaqueOrSameNeighbour(int x, int y, int z, Block.BlockType myType)
    {
        if (y < 0 || y >= chunkHeight)
            return false;

        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            Block b = chunkData[x, y, z];
            return b.isSolid && (!b.isTranslucent || b.type == myType);
        }

        if (worldManager == null)
            return false;

        Vector2Int neighbourOffset = worldOffset;
        int localX = x;
        int localZ = z;

        if (x < 0) { neighbourOffset += Vector2Int.left; localX = chunkSize - 1; }
        else if (x >= chunkSize) { neighbourOffset += Vector2Int.right; localX = 0; }

        if (z < 0) { neighbourOffset += Vector2Int.down; localZ = chunkSize - 1; }
        else if (z >= chunkSize) { neighbourOffset += Vector2Int.up; localZ = 0; }

        Chunk neighbour = worldManager.GetChunk(neighbourOffset);
        if (neighbour == null || neighbour.chunkData == null)
            return false;

        Block nb = neighbour.chunkData[localX, y, localZ];
        return nb.isSolid && (!nb.isTranslucent || nb.type == myType);
    }
}

