using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;

public class DualContourTest : MonoBehaviour
{
    public int size = 16;
    public float noiseScale = 1.0f;
    public float surfaceLevel = 0;
    public ComputeShader shader;
    public ComputeShader noiseCompute;

    [Header("Debug")]
    public bool showNormals;

    [Header("Debug tests")]
    public bool generateWithNormals;

    private Vector3[] verts;
    private int[] indices;

    RenderTexture noiseTexture;

    Mesh mesh;
    int offset = 0;

    static bool[][] isLocalEdge = new bool[8][]
    {
        new bool[12]{ true,     false, false,  true,  false,  false,  false,  false,  true,  false,  false,  false },
        new bool[12]{ true,     false, true,   true,  false,  false,  false,  false,  true,  false,  false,  true },
        new bool[12]{ true,     false, false,  true,  true,   false,  false,  true,   true,  false,  false,  false },
        new bool[12]{ true,     false, true,   true,  true,   false,  true,   true,   true,  false,  false,  true },
        new bool[12]{ true,     true,  false,  true,  false,  false,  false,  false,  true,  true,   false,  false },
        new bool[12]{ true,     true,  true,   true,  false,  false,  false,  false,  true,  true,   true,   true },
        new bool[12]{ true,     true,  false,  true,  true,   true,   false,  true,   true,  true,   false,  false },
        new bool[12]{ true,     true,  true,   true,  true,   true,   true,   true,   true,  true,   true,   true }
    };

    static int[] cornerIndexAFromEdge = new int[12]{ 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
    static int[] cornerIndexBFromEdge = new int[12]{ 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

    private List<Vector3> triangles = new List<Vector3>();
    private List<Tuple<int, int>> edgeIndices = new List<Tuple<int, int>>();

    float[] noise;

    [Serializable]
    public struct DebugTriData
    {
        public Vector3Int pos;
        public Vector3Int n0;
        public Vector3Int n1;
        public Vector3Int n2;

        public int edgeIndex;
         
        public Vector4 bottomCornerVals;
        public Vector4 topCornerVals;
         
        public Vector3Int tri1;
        public Vector3Int tri2;
    }


    public void Run()
    {
        Stopwatch sw = new Stopwatch();
        Stopwatch individualSW = new Stopwatch();
        sw.Start();
        individualSW.Start();
        triangles = new List<Vector3>();
        edgeIndices = new List<Tuple<int, int>>();
        int actualSize = size + 2;

        GenerateNoiseTex(transform.position, actualSize);
        UnityEngine.Debug.Log($"Generated noise in {individualSW.Elapsed.TotalMilliseconds}ms");
        individualSW.Restart();

        int HASH_BUFFER_SIZE = size * size * size;
        // Calculates the nearest power of 2
        HASH_BUFFER_SIZE--;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 1;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 2;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 4;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 8;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 16;
        HASH_BUFFER_SIZE++;

        int THREAD_BLOCKS = HASH_BUFFER_SIZE / 64;

        int k_CreateVertices = shader.FindKernel("CreateVertices");
        int k_CreateIndices = shader.FindKernel("CreateIndices");
        int k_HashInit = shader.FindKernel("Initialize");
        int k_CalculateNormals = shader.FindKernel("CalculateNormals");

        ComputeBuffer vertHashTable = new ComputeBuffer(HASH_BUFFER_SIZE, sizeof(int) * 2);

        shader.SetBuffer(k_HashInit, "b_hash", vertHashTable);
        shader.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        // Sets the hash table to empty values
        shader.Dispatch(k_HashInit, THREAD_BLOCKS, 1, 1);
        UnityEngine.Debug.Log($"Cleared hash table in {individualSW.Elapsed.TotalMilliseconds}ms");
        individualSW.Restart();

        ComputeBuffer vertexBuffer = new ComputeBuffer(actualSize * actualSize * actualSize, sizeof(float) * 3, ComputeBufferType.Counter);
        vertexBuffer.SetCounterValue(0);
        ComputeBuffer indexBuffer = new ComputeBuffer(size * size * size * 6, sizeof(int), ComputeBufferType.Counter);
        indexBuffer.SetCounterValue(0);
        /*ComputeBuffer debugBuffer = new ComputeBuffer(size * size * size * 6, ComputeUtils.GetStride<DebugTriData>(), ComputeBufferType.Counter);
        debugBuffer.SetCounterValue(0);*/
        ComputeBuffer normalsBuffer = new ComputeBuffer(actualSize * actualSize * actualSize, sizeof(float) * 3, ComputeBufferType.Structured);

        shader.SetTexture(k_CreateVertices, "DensityTexture", noiseTexture);
        shader.SetBuffer(k_CreateVertices, "Vertices", vertexBuffer);
        shader.SetBuffer(k_CreateVertices, "Indices", indexBuffer);
        shader.SetBuffer(k_CreateVertices, "Normals", normalsBuffer);

        shader.SetVector("offset", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0));

        shader.SetInt("size", actualSize);
        shader.SetFloat("noiseScale", noiseScale);
        shader.SetFloat("surfaceLevel", surfaceLevel);

        shader.SetBuffer(k_CreateVertices, "b_hash", vertHashTable);

        ComputeUtils.Dispatch(shader, actualSize, actualSize, actualSize, k_CreateVertices);

        shader.SetTexture(k_CalculateNormals, "DensityTexture", noiseTexture);
        shader.SetBuffer(k_CalculateNormals, "Vertices", vertexBuffer);
        shader.SetBuffer(k_CalculateNormals, "Normals", normalsBuffer);
        shader.SetBuffer(k_CalculateNormals, "b_hash", vertHashTable);

        ComputeUtils.Dispatch(shader, actualSize, actualSize, actualSize, k_CalculateNormals);

        UnityEngine.Debug.Log($"Created vertices in {individualSW.Elapsed.TotalMilliseconds}ms");
        individualSW.Restart();

        shader.SetInt("size", size);
        shader.SetTexture(k_CreateIndices, "DensityTexture", noiseTexture);
        shader.SetBuffer(k_CreateIndices, "Vertices", vertexBuffer);
        shader.SetBuffer(k_CreateIndices, "Indices", indexBuffer);
        shader.SetBuffer(k_CreateIndices, "b_hash", vertHashTable);

        ComputeUtils.Dispatch(shader, size, size, size, k_CreateIndices);

        UnityEngine.Debug.Log($"Created indices in {individualSW.Elapsed.TotalMilliseconds}ms");
        individualSW.Restart();

        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        int numVerts;

        (verts, numVerts) = ComputeUtils.ReadData<Vector3>(vertexBuffer, countBuffer);

        Vector3[] normals = new Vector3[numVerts];
        normalsBuffer.GetData(normals);

        (indices, _) = ComputeUtils.ReadData<int>(indexBuffer, countBuffer, 6);

        vertexBuffer.Release();
        normalsBuffer.Release();
        countBuffer.Release();
        indexBuffer.Release();

        mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        UnityEngine.Debug.Log($"Copied data in {individualSW.Elapsed.TotalMilliseconds}ms");
        individualSW.Restart();

        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        if(generateWithNormals)
            mesh.SetNormals(normals);
        else
            mesh.RecalculateNormals();

        sw.Stop();
        UnityEngine.Debug.Log($"Generated mesh in {individualSW.Elapsed.TotalMilliseconds}, total: {sw.Elapsed.TotalMilliseconds}ms");

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    public bool GenerateNoiseTex(Vector3 offset, int numVoxels)
    {
        numVoxels += 2;

        ComputeBuffer densityTypeFlags = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.Structured);

        Create3DTex(ref noiseTexture, numVoxels, "Noise Texure");
        densityTypeFlags.SetData(new Vector2Int[] { Vector2Int.zero, Vector2Int.zero });

        noiseCompute.SetTexture(0, "DensityTexture", noiseTexture);
        noiseCompute.SetBuffer(0, "DensityTypeFlags", densityTypeFlags);
        // Padding 1 pixel all arounnd cube texture so that vertex normals can be calculated
        noiseCompute.SetInt("textureSize", numVoxels);
        noiseCompute.SetFloat("noiseScale", noiseScale);
        noiseCompute.SetFloat("chunkScale", 1);
        noiseCompute.SetFloat("noiseMult", 1);
        noiseCompute.SetFloat("surfaceLevel", surfaceLevel);
        noiseCompute.SetVector("offset", new Vector4(offset.x, offset.y, offset.z, 0));

        ComputeUtils.Dispatch(noiseCompute, numVoxels, numVoxels, numVoxels);

        Vector2Int[] flags = new Vector2Int[2];
        densityTypeFlags.GetData(flags);

        return flags[0].x == 1 && flags[0].y == 1;
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (showNormals && mesh != null)
        {
            for(int i = offset; i <  mesh.vertices.Length; i += 8)
            {
                Vector3 vert = mesh.vertices[i];
                if (vert.x < 0) continue;
                Vector3 normal = mesh.normals[i];

                Gizmos.DrawLine(transform.position + vert, transform.position + vert + normal);
            }
            offset = (offset + 1) % 8;
        }
    }
}
