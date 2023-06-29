using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TreeEditor;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public static class Perlin
{
    #region Noise functions

    public static float Noise(float x)
    {
        var X = Mathf.FloorToInt(x) & 0xff;
        x -= Mathf.Floor(x);
        var u = Fade(x);
        return Lerp(u, Grad(perm[X], x), Grad(perm[X + 1], x - 1)) * 2;
    }

    public static float Noise(float x, float y)
    {
        var X = Mathf.FloorToInt(x) & 0xff;
        var Y = Mathf.FloorToInt(y) & 0xff;
        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        var u = Fade(x);
        var v = Fade(y);
        var A = (perm[X] + Y) & 0xff;
        var B = (perm[X + 1] + Y) & 0xff;
        return Lerp(v, Lerp(u, Grad(perm[A], x, y), Grad(perm[B], x - 1, y)),
                       Lerp(u, Grad(perm[A + 1], x, y - 1), Grad(perm[B + 1], x - 1, y - 1)));
    }

    public static float Noise(Vector2 coord)
    {
        return Noise(coord.x, coord.y);
    }

    public static float Noise(float x, float y, float z)
    {
        var X = Mathf.FloorToInt(x) & 0xff;
        var Y = Mathf.FloorToInt(y) & 0xff;
        var Z = Mathf.FloorToInt(z) & 0xff;
        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);
        var u = Fade(x);
        var v = Fade(y);
        var w = Fade(z);
        var A = (perm[X] + Y) & 0xff;
        var B = (perm[X + 1] + Y) & 0xff;
        var AA = (perm[A] + Z) & 0xff;
        var BA = (perm[B] + Z) & 0xff;
        var AB = (perm[A + 1] + Z) & 0xff;
        var BB = (perm[B + 1] + Z) & 0xff;
        return Lerp(w, Lerp(v, Lerp(u, Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z)),
                               Lerp(u, Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z))),
                       Lerp(v, Lerp(u, Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1)),
                               Lerp(u, Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1))));
    }

    public static float Noise(Vector3 coord)
    {
        return Noise(coord.x, coord.y, coord.z);
    }

    #endregion

    #region fBm functions

    public static float Fbm(float x, int octave)
    {
        var f = 0.0f;
        var w = 0.5f;
        for (var i = 0; i < octave; i++)
        {
            f += w * Noise(x);
            x *= 2.0f;
            w *= 0.5f;
        }
        return f;
    }

    public static float Fbm(Vector2 coord, int octave)
    {
        var f = 0.0f;
        var w = 0.5f;
        for (var i = 0; i < octave; i++)
        {
            f += w * Noise(coord);
            coord *= 2.0f;
            w *= 0.5f;
        }
        return f;
    }

    public static float Fbm(float x, float y, int octave)
    {
        return Fbm(new Vector2(x, y), octave);
    }

    public static float Fbm(Vector3 coord, int octave)
    {
        var f = 0.0f;
        var w = 0.5f;
        for (var i = 0; i < octave; i++)
        {
            f += w * Noise(coord);
            coord *= 2.0f;
            w *= 0.5f;
        }
        return f;
    }

    public static float Fbm(float x, float y, float z, int octave)
    {
        return Fbm(new Vector3(x, y, z), octave);
    }

    #endregion

    #region Private functions

    static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    static float Lerp(float t, float a, float b)
    {
        return a + t * (b - a);
    }

    static float Grad(int hash, float x)
    {
        return (hash & 1) == 0 ? x : -x;
    }

    static float Grad(int hash, float x, float y)
    {
        return ((hash & 1) == 0 ? x : -x) + ((hash & 2) == 0 ? y : -y);
    }

    static float Grad(int hash, float x, float y, float z)
    {
        var h = hash & 15;
        var u = h < 8 ? x : y;
        var v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    static int[] perm = {
        151,160,137,91,90,15,
        131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
        88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
        77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
        102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
        135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
        5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
        223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
        129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
        251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
        49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
        138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
        151
    };

    #endregion
}

public class Noise : IComputeEmulator
{
    public float scale;
    public float mult;
    public float add;
    public void Main(int3 id)
    {
        GPUEmulator.Texture("noise")[id] = -id.y * mult + add + Mathf.Pow((Perlin.Fbm(new Vector3(id.x, id.y, id.z) * scale, 5) + 1f) / 2f, 4f);
    }
}

public class CollapseOctree : IComputeEmulator
{
    public int scale;
    public int size;
    public float surfaceLevel;
    public void Main(int3 id)
    {
        int3 coord = id * scale;
        bool belowSurface = GPUEmulator.Texture("noise")[coord + 1] < surfaceLevel;
        for (int z = 0; z <= scale; z += scale / 2)
        {
            for (int y = 0; y <= scale; y += scale / 2)
            {
                for (int x = 0; x <= scale; x += scale / 2)
                {
                    int3 neighbourCoord = coord + new int3(x, y, z);
                    int index = neighbourCoord.x + (neighbourCoord.y + (neighbourCoord.z * size)) * size;
                    if (x < scale && y < scale && z < scale && ((float4)GPUEmulator.Buffer("Octree")[index]).w != scale / 2)
                        return;

                    float density = GPUEmulator.Texture("noise")[neighbourCoord + 1];
                    if ((density < surfaceLevel) != belowSurface)
                        return;
                }
            }
        }

        for (int z = 0; z < scale; z += scale / 2)
        {
            for (int y = 0; y < scale; y += scale / 2)
            {
                for (int x = 0; x < scale; x += scale / 2)
                {
                    if (x == 0 && y == 0 && z == 0)
                        continue;

                    int3 neighbourCoord = coord + new int3(x, y, z);
                    int index = neighbourCoord.x + (neighbourCoord.y + (neighbourCoord.z * size)) * size;
                    GPUEmulator.Buffer("Octree")[index] = new float4(0);
                }
            }
        }

        int octreeIndex = coord.x + (coord.y + (coord.z * size)) * size;
        GPUEmulator.Buffer("Octree")[octreeIndex] = new float4(coord.xyz, scale);
    }
}

public class CreateVertices : IComputeEmulator
{
    public int size;
    public float surfaceLevel;

    static int[] cornerIndexAFromEdge = new int[12]{ 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
    static int[] cornerIndexBFromEdge = new int[12]{ 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

    static int3 int3One = new int3(1, 1, 1);

    public Dictionary<int, int> vertHash = new Dictionary<int, int>();

    public void Main(int3 id)
    {
        if (id.x >= size || id.y >= size || id.z >= size)
            return;

        int3 coord = id;

        Texture3DEmulator DensityTexture = GPUEmulator.Texture("noise");

        float4[] cubeCorners = new float4[8]
        {
            // Bottom 4 corners
            new float4(coord.x, coord.y, coord.z,          DensityTexture[id + int3One]),
            new float4(coord.x, coord.y, coord.z + 1,      DensityTexture[id + int3One + new int3(0, 0, 1)]),
            new float4(coord.x + 1, coord.y, coord.z + 1,  DensityTexture[id + int3One + new int3(1, 0, 1)]),
            new float4(coord.x + 1, coord.y, coord.z,      DensityTexture[id + int3One + new int3(1, 0, 0)]),
        
            // Top 4 corners
            new float4(coord.x, coord.y + 1, coord.z,          DensityTexture[id + int3One + new int3(0, 1, 0)]),
            new float4(coord.x, coord.y + 1, coord.z + 1,      DensityTexture[id + int3One + new int3(0, 1, 1)]),
            new float4(coord.x + 1, coord.y + 1, coord.z + 1,  DensityTexture[id + int3One + new int3(1, 1, 1)]),
            new float4(coord.x + 1, coord.y + 1, coord.z,      DensityTexture[id + int3One + new int3(1, 1, 0)]),
        };

        float3[] edgePoints = new float3[12];
        int index = 0;

        float3 vertPos = float3.zero;

        for (int i = 0; i < 12; i++)
        {
            float4 a = cubeCorners[cornerIndexAFromEdge[i]];
            float4 b = cubeCorners[cornerIndexBFromEdge[i]];

            if ((a.w > surfaceLevel) != (b.w > surfaceLevel))
            {
                float t = (surfaceLevel - a.w) / (b.w - a.w);
                float3 vert = a.xyz + t * (b.xyz - a.xyz);
                edgePoints[index] = vert;

                vertPos += vert;
                index++;
            }
        }

        if (index > 0)
        {
            vertPos /= index;
            int vertIndex = GPUEmulator.Buffer("Vertices").IncrementCounter();

            uint mortonCode = 0;
            float step = size / 2f;
            float3 centre = new float3(step);

            for (int i = 0; i < 30; i += 3)
            {
                step *= 0.5f;
                if (vertPos.x < centre.x)
                {
                    centre.x -= step;
                }
                else
                {
                    centre.x += step;
                    mortonCode |= (uint)(1 << i);
                }

                if (vertPos.y < centre.y)
                {
                    centre.y -= step;
                }
                else
                {
                    centre.y += step;
                    mortonCode |= (uint)(1 << (i + 1));
                }

                if (vertPos.z < centre.z)
                {
                    centre.z -= step;
                }
                else
                {
                    centre.z += step;
                    mortonCode |= (uint)(1 << (i + 2));
                }
            }

            GPUEmulator.Buffer("Vertices")[vertIndex] = vertPos;
            GPUEmulator.Buffer("MortonCode")[vertIndex] = mortonCode;
            vertHash[id.x + id.y * size + id.z * size * size] = vertIndex;
        }

        int octreeIndex = coord.x + (coord.y + (coord.z * size)) * size;
        GPUEmulator.Buffer("Octree")[octreeIndex] = new float4(id.xyz, 1);
    }
}

public class DualContourTest : MonoBehaviour
{
    public int size;
    public float scale = 1.0f;
    public float surfaceLevel = 0.0f;
    public Texture3D noise;

    public float mult;
    public float add;

    [Header("Debug")]
    public bool showMortonCodes;
    public bool showBinaryCodes;
    public bool showOctree;

    float3[] vertices;
    float4[] octree;
    uint[] mortonCodes;

    public void Run()
    {
        Noise noiseShader = new Noise();
        noiseShader.scale = scale;
        noiseShader.mult = mult;
        noiseShader.add = add;

        Texture3DEmulator noiseTexture = new Texture3DEmulator(size + 4, size + 4, size + 4);
        GPUEmulator.BindTexture("noise", ref noiseTexture);

        GPUEmulator.Dispatch(size + 4, size + 4, size + 4, noiseShader);

        noise = noiseTexture.Get();

        CreateVertices createVertices = new CreateVertices();
        createVertices.size = size + 2;
        createVertices.surfaceLevel = surfaceLevel;

        BufferEmulator vertexBuffer = new BufferEmulator(size * size * size);
        vertexBuffer.SetCounter(0);
        BufferEmulator mortonCodeBuffer = new BufferEmulator(size * size * size);
        GPUEmulator.BindBuffer("Vertices", ref vertexBuffer);
        GPUEmulator.BindBuffer("MortonCode", ref mortonCodeBuffer);

        BufferEmulator octreeBuffer = new BufferEmulator((size + 2) * (size + 2) * (size + 2));
        GPUEmulator.BindBuffer("Octree", ref octreeBuffer);

        GPUEmulator.Dispatch(size + 2, size + 2, size + 2, createVertices);

        CollapseOctree collapseOctree = new CollapseOctree();
        int curScale = 2;
        while(curScale < size + 2)
        {
            collapseOctree.size = size + 2;
            collapseOctree.scale = curScale;
            collapseOctree.surfaceLevel = surfaceLevel;

            GPUEmulator.Dispatch((size + 2) / curScale, (size + 2) / curScale, (size + 2) / curScale, collapseOctree);

            curScale *= 2;
        }
        
        vertices = new float3[vertexBuffer.GetCount()];
        mortonCodes = new uint[vertices.Length];
        octree = new float4[(size + 2) * (size + 2) * (size + 2)];

        vertexBuffer.GetData(ref vertices);
        mortonCodeBuffer.GetData(ref mortonCodes);
        octreeBuffer.GetData(ref octree);

        int count = 0;
        foreach(float4 f in octree)
        {
            if (f.w > 0) count++;
        }

        Debug.Log($"Raw: {(size + 2) * (size + 2) * (size + 2)}, Octree: {count}, Compression ratio of {(float)count / ((size + 2) * (size + 2) * (size + 2))}");
    }

    static void drawString(string text, Vector3 worldPos, UnityEngine.Color? colour = null)
    {
        UnityEditor.Handles.BeginGUI();
        if (colour.HasValue) GUI.color = colour.Value;
        var view = UnityEditor.SceneView.currentDrawingSceneView;
        Vector3 screenPos = view.camera.WorldToScreenPoint(worldPos);

        if (screenPos.y < 0 || screenPos.y > Screen.height || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.z < 0)
        {
            UnityEditor.Handles.EndGUI();
            return;
        }

        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(screenPos.x - (size.x / 2), -screenPos.y + view.position.height + 4, size.x, size.y), text);
        UnityEditor.Handles.EndGUI();
    }

    private void OnDrawGizmos()
    {
        if(vertices != null)
        {
            for(int i = 0; i < vertices.Length; i++)
            {
                float3 v = vertices[i];
                Gizmos.DrawSphere(v, 0.1f);
                if(showMortonCodes)
                {
                    if(showBinaryCodes)
                        drawString(Convert.ToString(mortonCodes[i], 2), v);
                    else
                        drawString(mortonCodes[i].ToString(), v);
                }
            }
        }

        Gizmos.color = UnityEngine.Color.green;
        float centre = (size + 2) / 2f;
        //Gizmos.DrawWireCube(new float3(centre), new float3(size));

        //Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.3f);
        //Gizmos.DrawWireCube(new float3(centre), new float3(size + 2));

        if(octree != null && showOctree)
        {
            foreach(float4 oct in octree)
            {
                if (oct.w <= 0) continue;
                Gizmos.color = new UnityEngine.Color(0, 1, 0, 1f / oct.w);
                Gizmos.DrawWireCube(oct.xyz + (oct.w / 2f), new float3(oct.w));
            }
        }
    }
}
