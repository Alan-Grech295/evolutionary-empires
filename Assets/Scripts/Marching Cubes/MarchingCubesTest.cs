using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class MarchingCubesTest : MonoBehaviour
{
    public RenderTexture noise;
    public int numVoxels = 32;
    public float noiseScale = 1.0f;
    public float surfaceLevel = 0.5f;

    public int showTri = 0;

    public ComputeShader noiseCompute;
    public ComputeShader marchingCubesCompute;

    private Mesh mesh;

    private int HASH_BUFFER_SIZE;
    private int THREAD_BLOCKS;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Generates the mesh
    // The 3D noise is generated first
    // The vertices are then created and stored in a hash table. Each voxel is responsible
    // for creating specific vertices such that no vertices are duplicated. Right now indices
    // are stored as the edge hash not the index of the vertex in the vertex buffer as vertices 
    // not have been created yet.
    // In the final pass the index buffer values are converted from the edge hashes to the actual
    // vertex index.
    public void GenerateMesh()
    {
        // Calculating the size of the hash table buffer
        HASH_BUFFER_SIZE = numVoxels * numVoxels * numVoxels;
        // Calculates the nearest power of 2
        HASH_BUFFER_SIZE--;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 1;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 2;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 4;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 8;
        HASH_BUFFER_SIZE |= HASH_BUFFER_SIZE >> 16;
        HASH_BUFFER_SIZE++;
        
        THREAD_BLOCKS = HASH_BUFFER_SIZE / 64;

        System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        // Generates the 3D noise texture
        GenerateNoiseTex();

        mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        MeshFilter filter = GetComponent<MeshFilter>();
        MeshCollider collider = GetComponent<MeshCollider>();

        int k_HashInit = marchingCubesCompute.FindKernel("Initialize");
        int k_CreateVerts = marchingCubesCompute.FindKernel("CreateVerts");
        int k_Finalize = marchingCubesCompute.FindKernel("Finalize");

        // Initialize hash table
        ComputeBuffer vertHashTable = new ComputeBuffer(HASH_BUFFER_SIZE, sizeof(int) * 2); // Size is power of 2

        marchingCubesCompute.SetBuffer(k_HashInit, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        // Sets the hash table to empty values
        marchingCubesCompute.Dispatch(k_HashInit, THREAD_BLOCKS, 1, 1);

        // Create marching cubes
        ComputeBuffer vertBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels / 4, sizeof(float) * 3, ComputeBufferType.Counter);
        vertBuffer.SetCounterValue(0);
        ComputeBuffer normalBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels / 4, sizeof(float) * 3, ComputeBufferType.Structured);
        
        ComputeBuffer indexBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels, sizeof(int), ComputeBufferType.Counter);
        indexBuffer.SetCounterValue(0);

        marchingCubesCompute.SetTexture(k_CreateVerts, "DensityTexture", noise);
        marchingCubesCompute.SetBuffer(k_CreateVerts, "Vertices", vertBuffer);
        marchingCubesCompute.SetBuffer(k_CreateVerts, "Normals", normalBuffer);
        marchingCubesCompute.SetBuffer(k_CreateVerts, "Indices", indexBuffer);

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
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        int[] indicesCount = new int[1];
        ComputeBuffer.CopyCount(indexBuffer, countBuffer, 0);
        countBuffer.GetData(indicesCount);
        indicesCount[0] *= 3;

        // Finalization
        marchingCubesCompute.SetBuffer(k_Finalize, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        marchingCubesCompute.SetBuffer(k_Finalize, "Indices", indexBuffer);
        marchingCubesCompute.SetInt("numTris", indicesCount[0]);

        marchingCubesCompute.Dispatch(k_Finalize, Mathf.CeilToInt(indicesCount[0] / 512f), 1, 1);

        // Copies the vertex, normal and index data to the mesh
        int[] vertexCount = new int[1];
        ComputeBuffer.CopyCount(vertBuffer, countBuffer, 0);
        countBuffer.GetData(vertexCount);

        Vector3[] verts = new Vector3[vertexCount[0]];
        vertBuffer.GetData(verts);
        Vector3[] normals = new Vector3[vertexCount[0]];
        normalBuffer.GetData(normals);

        int[] indices = new int[indicesCount[0]];
        indexBuffer.GetData(indices);

        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.SetNormals(normals);

        collider.sharedMesh = mesh;

        filter.sharedMesh = mesh;

        // Releases the buffers
        vertBuffer.Release();
        indexBuffer.Release();
        normalBuffer.Release();
        vertHashTable.Release();

        timer.Stop();
        Debug.Log($"Generated map in {timer.Elapsed.TotalMilliseconds}ms");
    }

    public void GenerateNoiseTex()
    {
        Create3DTex(ref noise, numVoxels + 3, "Noise Texure");
        noiseCompute.SetTexture(0, "DensityTexture", noise);
        // Padding 1 pixel all arounnd cube texture so that vertex normals can be calculated
        noiseCompute.SetInt("textureSize", numVoxels + 3);
        noiseCompute.SetFloat("noiseScale", noiseScale);
        noiseCompute.SetVector("offset", new Vector4(transform.position.x, transform.position.y, transform.position.z, 0));

        uint3 kernelThreads;
        noiseCompute.GetKernelThreadGroupSizes(0, out kernelThreads.x, out kernelThreads.y, out kernelThreads.z);
        int3 numThreads = new int3(Mathf.CeilToInt((numVoxels + 3f) / kernelThreads.x),
                                   Mathf.CeilToInt((numVoxels + 3f) / kernelThreads.y),
                                   Mathf.CeilToInt((numVoxels + 3f) / kernelThreads.z));

        noiseCompute.Dispatch(0, numThreads.x, numThreads.y, numThreads.z);
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
}
