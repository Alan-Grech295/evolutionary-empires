using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem.Android;
using UnityEngine.Rendering;

public class ChunkManager : MonoBehaviour
{
    //public int numVoxels = 32;
    public int[] lodNumVoxels;
    public int chunkSize = 32;
    public int batchCubeSize = 3;
    public float noiseScale = 0.1f;
    public float surfaceLevel = 0.5f;
    public float maxChunkTimeMillis = 4f;
    // Distance to load in chunks
    public Vector3Int chunkLoadDistance;
    public float noiseMultiplier = 1.0f;

    public Transform playerTransform;

    public Material terrainMat;

    public ComputeShader noiseCompute;
    public ComputeShader marchingCubesCompute;

    private int HASH_BUFFER_SIZE;
    private int THREAD_BLOCKS;

    Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();
    Dictionary<Vector3Int, GameObject> batches = new Dictionary<Vector3Int, GameObject>();
    HashSet<Vector3>[] visibleChunks = new HashSet<Vector3>[2];
    RenderTexture noise;

    private int k_HashInit;
    private int k_CreateVerts;
    private int k_Finalize;

    private ComputeBuffer densityTypeFlags;

    private ComputeBuffer vertHashTable;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer indicesBuffer;
    private ComputeBuffer normalsBuffer;
    
    private ComputeBuffer countBuffer;

    private int[] indicesCount = new int[1];
    private int[] verticesCount = new int[1];

    private Vector3 pastPos;
    private int currentVisibleChunkBuffer;

    private bool restartGeneration = false;
    private bool runningGeneration = false;

    Queue<Vector3> positionQueue;

    // Start is called before the first frame update
    void Start()
    {
        visibleChunks[0] = new HashSet<Vector3>();
        visibleChunks[1] = new HashSet<Vector3>();

        positionQueue = new Queue<Vector3>(chunkLoadDistance.x * chunkLoadDistance.z);

        // Calculating the size of the hash table buffer
        int maxVoxels = lodNumVoxels[0];
        HASH_BUFFER_SIZE = maxVoxels * maxVoxels * maxVoxels;
        // Calculates the nearest power of 2
        HASH_BUFFER_SIZE--;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 1;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 2;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 4;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 8;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 16;
        HASH_BUFFER_SIZE++;

        THREAD_BLOCKS = HASH_BUFFER_SIZE / 64;

        k_HashInit = marchingCubesCompute.FindKernel("Initialize");
        k_CreateVerts = marchingCubesCompute.FindKernel("CreateVerts");
        k_Finalize = marchingCubesCompute.FindKernel("Finalize");

        InitializeComputeBuffers();

        StartCoroutine(UpdateChunks());
    }

    // Update is called once per frame
    void Update()
    {
        if((pastPos - playerTransform.position).sqrMagnitude >= chunkSize * chunkSize)
        {
            //StopAllCoroutines();
            //StartCoroutine(UpdateChunks());
            if(runningGeneration)
            {
                restartGeneration = true;
            }
            else
            {
                StartCoroutine(UpdateChunks());
            }
        }
    }

    // Used for the editor
    public void ReloadChunksImmediate()
    {
        // Calculating the size of the hash table buffer
        int maxVoxels = lodNumVoxels[0];
        HASH_BUFFER_SIZE = maxVoxels * maxVoxels * maxVoxels;
        // Calculates the nearest power of 2
        HASH_BUFFER_SIZE--;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 1;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 2;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 4;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 8;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 16;
        HASH_BUFFER_SIZE++;

        THREAD_BLOCKS = HASH_BUFFER_SIZE / 64;

        k_HashInit = marchingCubesCompute.FindKernel("Initialize");
        k_CreateVerts = marchingCubesCompute.FindKernel("CreateVerts");
        k_Finalize = marchingCubesCompute.FindKernel("Finalize");

        InitializeComputeBuffers();

        chunks = new Dictionary<Vector3, Chunk>();
        batches = new Dictionary<Vector3Int, GameObject>();
        visibleChunks[0] = new HashSet<Vector3>();
        visibleChunks[1] = new HashSet<Vector3>();
        positionQueue = new Queue<Vector3>();

        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        var chunkEnum = UpdateChunks();
        while (chunkEnum.MoveNext()) { }

        ComputeUtils.Release(vertexBuffer, indicesBuffer, countBuffer, vertHashTable, normalsBuffer);
    }

    IEnumerator UpdateChunks()
    {
        runningGeneration = true;

        Vector3Int actualChunkLoadDist = chunkLoadDistance * chunkSize;

        pastPos = playerTransform.position;
        positionQueue.Enqueue(GetClosestChunk(playerTransform.position));
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        currentVisibleChunkBuffer = 1 - currentVisibleChunkBuffer;
        while (positionQueue.Count > 0)
        {
            Vector3 chunkPos = positionQueue.Dequeue();
            visibleChunks[currentVisibleChunkBuffer].Add(chunkPos);
            visibleChunks[1 - currentVisibleChunkBuffer].Remove(chunkPos);

            Vector3 chunkDist = (chunkPos - playerTransform.position) / chunkSize;
            float maxDist = Mathf.Clamp01(Mathf.Max(Mathf.Abs(chunkDist.x / chunkLoadDistance.x),
                                      Mathf.Abs(chunkDist.y / chunkLoadDistance.y), 
                                      Mathf.Abs(chunkDist.z / chunkLoadDistance.z)));

            int lod = Mathf.RoundToInt(maxDist * (lodNumVoxels.Length - 1));
            int numVoxels = lodNumVoxels[lod];

            if (chunks.ContainsKey(chunkPos))
            {
                chunks[chunkPos].gameObject.SetActive(true);
            }
            else
            {
                GameObject chunkGO = new GameObject($"Chunk {chunkPos} LOD: {lod}");
                chunkGO.transform.position = chunkPos;
                chunkGO.transform.localScale = Vector3.one * ((float)chunkSize / numVoxels);
                chunkGO.isStatic = true;

                Vector3Int batchPosition = GetBatchPosition(chunkPos);

                if (!batches.ContainsKey(batchPosition))
                {
                    GameObject batch = new GameObject($"Batch {batchPosition}");
                    batch.isStatic = true;
                    batch.transform.position = batchPosition;
                    batch.transform.SetParent(transform);
                    chunkGO.transform.SetParent(batch.transform);
                    batches[batchPosition] = batch;
                }
                else
                {
                    chunkGO.transform.SetParent(batches[batchPosition].transform);
                }

                if(batches[batchPosition].transform.childCount >= batchCubeSize * batchCubeSize * batchCubeSize)
                {
                    StaticBatchingUtility.Combine(batches[batchPosition]);
                }

                chunkGO.AddComponent<MeshFilter>();
                chunkGO.AddComponent<MeshRenderer>();
                chunkGO.AddComponent<MeshCollider>();

                Chunk chunk = chunkGO.AddComponent<Chunk>();
                chunk.Recreate(GenerateMesh(chunkPos, numVoxels), terrainMat, lod);

                chunks.Add(chunkPos, chunk);
            }

            for (int z = -chunkSize; z <= chunkSize; z += chunkSize)
            {
                for (int y = -chunkSize; y <= chunkSize; y += chunkSize)
                {
                    for (int x = -chunkSize; x <= chunkSize; x += chunkSize)
                    {
                        if (x == 0 && y == 0 && z == 0)
                            continue;

                        Vector3 newChunkPos = chunkPos + new Vector3(x, y, z);
                        Vector3 dist = newChunkPos - playerTransform.position;
                        dist = new Vector3(Mathf.Abs(dist.x), Mathf.Abs(dist.y), Mathf.Abs(dist.z));

                        if (dist.x <= actualChunkLoadDist.x && dist.y <= actualChunkLoadDist.y && dist.z <= actualChunkLoadDist.z && 
                            !positionQueue.Contains(newChunkPos) && !visibleChunks[currentVisibleChunkBuffer].Contains(newChunkPos))
                        {
                            positionQueue.Enqueue(newChunkPos);
                        }
                    }
                }
            }

            if(restartGeneration)
            {
                restartGeneration = false;
                visibleChunks[currentVisibleChunkBuffer].UnionWith(visibleChunks[1 - currentVisibleChunkBuffer]);
                visibleChunks[1 - currentVisibleChunkBuffer].Clear();
                StartCoroutine(UpdateChunks());
                yield break;
            }

            if(stopwatch.Elapsed.TotalMilliseconds >= maxChunkTimeMillis)
            {
                yield return null;
                stopwatch.Restart();
            }
        }

        foreach (Vector3 invisibleChunkPos in visibleChunks[1 - currentVisibleChunkBuffer])
        {
            chunks[invisibleChunkPos].gameObject.SetActive(false);
        }

        visibleChunks[1 - currentVisibleChunkBuffer].Clear();

        runningGeneration = false;

        /*foreach(Vector3 invisibleChunkPos in visibleChunks[1 - currentVisibleChunkBuffer])
        {
            chunks[invisibleChunkPos].gameObject.SetActive(false);
        }
        visibleChunks[1 - currentVisibleChunkBuffer].Clear();*/
    }

    Vector3 GetClosestChunk(Vector3 position)
    {
        position /= chunkSize;
        return new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), Mathf.Round(position.z)) * chunkSize;
    }

    Vector3Int GetBatchPosition(Vector3 position)
    {
        position /= chunkSize * batchCubeSize;
        return new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z));
    }

    // Generates the mesh
    // The 3D noise is generated first
    // The vertices are then created and stored in a hash table. Each voxel is responsible
    // for creating specific vertices such that no vertices are duplicated. Right now indices
    // are stored as the edge hash not the index of the vertex in the vertex buffer as vertices 
    // not have been created yet.
    // In the final pass the index buffer values are converted from the edge hashes to the actual
    // vertex index.
    public Mesh GenerateMesh(Vector3 offset, int numVoxels)
    {
        // Generates the 3D noise texture
        if (!GenerateNoiseTex(offset, numVoxels))
            return new Mesh();

        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        marchingCubesCompute.SetBuffer(k_HashInit, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        // Sets the hash table to empty values
        marchingCubesCompute.Dispatch(k_HashInit, THREAD_BLOCKS, 1, 1);

        // Create marching cubes
        vertexBuffer.SetCounterValue(0);
        indicesBuffer.SetCounterValue(0);

        marchingCubesCompute.SetTexture(k_CreateVerts, "DensityTexture", noise);
        marchingCubesCompute.SetBuffer(k_CreateVerts, "Vertices", vertexBuffer);
        marchingCubesCompute.SetBuffer(k_CreateVerts, "Normals", normalsBuffer);
        marchingCubesCompute.SetBuffer(k_CreateVerts, "Indices", indicesBuffer);

        int inflatedVoxels = numVoxels + 2;
        // Sets the edge correction table
        // This is needed to calculate the hash for the a given edge
        marchingCubesCompute.SetInts("EdgeHashCorrections", 0, 0, 0, 0,
                                                            inflatedVoxels * inflatedVoxels, 1, 0, 0,
                                                            1, 0, 0, 0,
                                                            0, 1, 0, 0,
                                                            inflatedVoxels, 0, 0, 0,
                                                            inflatedVoxels * inflatedVoxels + inflatedVoxels, 1, 0, 0,
                                                            inflatedVoxels + 1, 0, 0, 0,
                                                            inflatedVoxels, 1, 0, 0,
                                                            0, 2, 0, 0,
                                                            inflatedVoxels * inflatedVoxels, 2, 0, 0,
                                                            inflatedVoxels * inflatedVoxels + 1, 2, 0, 0,
                                                            1, 2, 0, 0);

        marchingCubesCompute.SetInt("size", numVoxels);
        marchingCubesCompute.SetFloat("surfaceLevel", surfaceLevel);

        // Hash table buffer
        marchingCubesCompute.SetBuffer(k_CreateVerts, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        // Calculates the number of threads to be dispatched
        uint3 kernelThreads;
        marchingCubesCompute.GetKernelThreadGroupSizes(k_CreateVerts, out kernelThreads.x, out kernelThreads.y, out kernelThreads.z);
        int3 numThreads = new int3(Mathf.CeilToInt(numVoxels / kernelThreads.x),
                                   Mathf.CeilToInt(numVoxels / kernelThreads.y),
                                   Mathf.CeilToInt(numVoxels / kernelThreads.z));

        marchingCubesCompute.Dispatch(k_CreateVerts, numThreads.x, numThreads.y, numThreads.z);

        // Gets the triangle count
        ComputeBuffer.CopyCount(indicesBuffer, countBuffer, 0);
        countBuffer.GetData(indicesCount);
        indicesCount[0] *= 3;

        // Finalization
        marchingCubesCompute.SetBuffer(k_Finalize, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        marchingCubesCompute.SetBuffer(k_Finalize, "Indices", indicesBuffer);
        marchingCubesCompute.SetInt("numTris", indicesCount[0]);

        ComputeUtils.Dispatch(marchingCubesCompute, indicesCount[0], 1, 1, k_Finalize);

        // Copies the vertex, normal and index data to the mesh
        ComputeBuffer.CopyCount(vertexBuffer, countBuffer, 0);
        countBuffer.GetData(verticesCount);

        Vector3[] verts = new Vector3[verticesCount[0]];
        vertexBuffer.GetData(verts);
        Vector3[] normals = new Vector3[verticesCount[0]];
        normalsBuffer.GetData(normals);

        int[] indices = new int[indicesCount[0]];
        indicesBuffer.GetData(indices);

        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.SetNormals(normals);

        return mesh;
    }

    public bool GenerateNoiseTex(Vector3 offset, int numVoxels)
    {
        Create3DTex(ref noise, numVoxels + 3, "Noise Texure");
        densityTypeFlags.SetData(new Vector2Int[] {Vector2Int.zero, Vector2Int.zero});

        noiseCompute.SetTexture(0, "DensityTexture", noise);
        noiseCompute.SetBuffer(0, "DensityTypeFlags", densityTypeFlags);
        // Padding 1 pixel all arounnd cube texture so that vertex normals can be calculated
        noiseCompute.SetInt("textureSize", numVoxels + 3);
        noiseCompute.SetFloat("noiseScale", noiseScale);
        noiseCompute.SetFloat("chunkScale", ((float)chunkSize / numVoxels));
        noiseCompute.SetFloat("noiseMult", noiseMultiplier);
        noiseCompute.SetFloat("surfaceLevel", surfaceLevel);
        noiseCompute.SetVector("offset", new Vector4(offset.x, offset.y, offset.z, 0));

        ComputeUtils.Dispatch(noiseCompute, numVoxels + 3, numVoxels + 3, numVoxels + 3);

        Vector2Int[] flags = new Vector2Int[2];
        densityTypeFlags.GetData(flags);

        return flags[0].x == 1 && flags[0].y == 1;
    }

    private void OnDestroy()
    {
        vertexBuffer.Release();
        indicesBuffer.Release();
        normalsBuffer.Release();
        vertHashTable.Release();
        countBuffer.Release();
    }

    // Taken from Sebastian Lague https://github.com/SebLague/Terraforming/blob/main/Assets/Marching%20Cubes/Scripts/GenTest.cs#L315
    void Create3DTex(ref RenderTexture texture, int size, string name)
    {
        var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size || texture.volumeDepth != size || texture.graphicsFormat != format)
        {
            if (texture != null)
            {
                texture.Release();
            }

            const int numBitsInDepthBuffer = 0;
            texture = new RenderTexture(size, size, numBitsInDepthBuffer);
            texture.graphicsFormat = format;
            texture.volumeDepth = size;
            texture.enableRandomWrite = true;
            texture.dimension = TextureDimension.Tex3D;

            texture.Create();
        }

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.name = name;
    }

    void InitializeComputeBuffers()
    {
        densityTypeFlags = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.Structured);

        int numVoxels = lodNumVoxels[0];
        vertHashTable = new ComputeBuffer(HASH_BUFFER_SIZE, sizeof(int) * 2); // Size is power of 2
        vertexBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels / 4, sizeof(float) * 3, ComputeBufferType.Counter);
        normalsBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels / 4, sizeof(float) * 3, ComputeBufferType.Structured);
        indicesBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels, sizeof(int), ComputeBufferType.Counter);

        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }
}
