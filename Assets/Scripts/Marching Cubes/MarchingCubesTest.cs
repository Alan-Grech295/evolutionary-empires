using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

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

    private const int HASH_BUFFER_SIZE = 16384;
    private const int THREAD_BLOCKS = HASH_BUFFER_SIZE / 64;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GenerateMesh()
    {
        System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        GenerateNoiseTex();

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        MeshFilter filter = GetComponent<MeshFilter>();
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        mesh.Clear();

        int k_HashInit = marchingCubesCompute.FindKernel("Initialize");
        int k_Main = marchingCubesCompute.FindKernel("CSMain");

        // Initialize hash table
        ComputeBuffer vertHashTable = new ComputeBuffer(HASH_BUFFER_SIZE, sizeof(int) * 2); // Size is power of 2
        marchingCubesCompute.SetBuffer(k_HashInit, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);
        marchingCubesCompute.Dispatch(k_HashInit, THREAD_BLOCKS, 1, 1);

        // Create marching cubes
        ComputeBuffer vertBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels * 3, sizeof(float) * 3, ComputeBufferType.Counter);
        vertBuffer.SetCounterValue(0);
        ComputeBuffer indexBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels * 3, sizeof(int), ComputeBufferType.Counter);
        indexBuffer.SetCounterValue(0);

        marchingCubesCompute.SetTexture(k_Main, "DensityTexture", noise);
        marchingCubesCompute.SetBuffer(k_Main, "Vertices", vertBuffer);
        marchingCubesCompute.SetBuffer(k_Main, "Indices", indexBuffer);

        marchingCubesCompute.SetInt("size", numVoxels);
        marchingCubesCompute.SetFloat("surfaceLevel", surfaceLevel);

        // Hash table buffer
        marchingCubesCompute.SetBuffer(k_Main, "b_hash", vertHashTable);
        marchingCubesCompute.SetInt("e_hashBufferSize", HASH_BUFFER_SIZE);

        uint3 kernelThreads;
        marchingCubesCompute.GetKernelThreadGroupSizes(k_Main, out kernelThreads.x, out kernelThreads.y, out kernelThreads.z);
        int3 numThreads = new int3(Mathf.CeilToInt(numVoxels / kernelThreads.x),
                                   Mathf.CeilToInt(numVoxels / kernelThreads.y),
                                   Mathf.CeilToInt(numVoxels / kernelThreads.z));

        marchingCubesCompute.Dispatch(k_Main, numThreads.x, numThreads.y, numThreads.z);

        // Create mesh
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        int[] vertexCount = new int[1];
        ComputeBuffer.CopyCount(vertBuffer, countBuffer, 0);
        countBuffer.GetData(vertexCount);

        Vector3[] verts = new Vector3[vertexCount[0]];
        vertBuffer.GetData(verts);

        int[] indicesCount = new int[1];
        ComputeBuffer.CopyCount(indexBuffer, countBuffer, 0);
        countBuffer.GetData(indicesCount);

        int[] indices = new int[indicesCount[0] * 3];
        indexBuffer.GetData(indices);

        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();

        filter.sharedMesh = mesh;

        vertBuffer.Release();
        indexBuffer.Release();

        timer.Stop();
        Debug.Log($"Generated map in {timer.Elapsed.TotalMilliseconds}ms");
    }

    public void GenerateNoiseTex()
    {
        Create3DTex(ref noise, numVoxels + 1, "Noise Texure");
        noiseCompute.SetTexture(0, "DensityTexture", noise);
        noiseCompute.SetInt("textureSize", numVoxels + 1);
        noiseCompute.SetFloat("noiseScale", noiseScale);

        uint3 kernelThreads;
        noiseCompute.GetKernelThreadGroupSizes(0, out kernelThreads.x, out kernelThreads.y, out kernelThreads.z);
        int3 numThreads = new int3(Mathf.CeilToInt((numVoxels + 1f) / kernelThreads.x),
                                   Mathf.CeilToInt((numVoxels + 1f) / kernelThreads.y),
                                   Mathf.CeilToInt((numVoxels + 1f) / kernelThreads.z));

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
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;

            texture.Create();
        }

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.name = name;
    }

    private void OnDrawGizmos()
    {
        return;

        if (mesh == null)
            return;

        Vector3[] verts = mesh.vertices;

        foreach (Vector3 vert in verts)
        {
            Gizmos.DrawSphere(vert, 0.1f);
        }
        Gizmos.color = Color.green;

        if (showTri < 0 || showTri >= mesh.triangles.Length)
        {
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                Gizmos.DrawLine(verts[i], verts[i + 1]);
                Gizmos.DrawLine(verts[i + 1], verts[i + 2]);
                Gizmos.DrawLine(verts[i + 2], verts[i]);
            }
        }
        else
        {
            int i = showTri * 3;
            Gizmos.DrawLine(verts[i], verts[i + 1]);
            Gizmos.DrawLine(verts[i + 1], verts[i + 2]);
            Gizmos.DrawLine(verts[i + 2], verts[i]);
        }
        
    }
}
