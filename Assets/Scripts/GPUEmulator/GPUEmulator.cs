using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;



public static class GPUEmulator
{
    static Dictionary<string, BufferEmulator> boundBuffers = new Dictionary<string, BufferEmulator>();
    static Dictionary<string, Texture3DEmulator> boundTextures = new Dictionary<string, Texture3DEmulator>();

    public static void BindBuffer(string name, ref BufferEmulator buffer)
    {
        boundBuffers[name] = buffer;
    }

    public static BufferEmulator Buffer(string name)
    {
        return boundBuffers[name];
    }

    public static void BindTexture(string name, ref Texture3DEmulator tex)
    {
        boundTextures[name] = tex;
    }

    public static Texture3DEmulator Texture(string name)
    {
        return boundTextures[name];
    }

    public static void Dispatch(int x, int y, int z, Action<Vector3Int> function)
    {
        List<Vector3Int> ids = new List<Vector3Int>(x * y * z);
        for(int zThread = 0; zThread < z; zThread++)
        {
            for (int yThread = 0; yThread < y; yThread++)
            {
                for (int xThread = 0; xThread < x; xThread++)
                {
                    ids.Add(new Vector3Int(xThread, yThread, zThread));
                }
            }
        }

        System.Random rand = new System.Random();

        while(ids.Count > 0)
        {
            int i = rand.Next(0, ids.Count);
            function.Invoke(ids[i]);
            ids.RemoveAt(i);
        }
    }
}

public class BufferEmulator
{
    object[] buffer;
    int counter = 0;

    public object this[int index]
    {
        get { return buffer[index]; }
        set { buffer[index] = value; }
    }

    public BufferEmulator(int size)
    {
        buffer = new object[size];
        counter = 0;
    }

    public T Get<T>(int index)
    {
        return (T)buffer[index];
    }

    public int IncrementCounter()
    {
        return ++counter;
    }

    public void Append<T>(T element)
    {
        buffer[counter] = element;
        counter++;
    }

    public void GetData<T>(ref T[] array)
    {
        if (array.Length == 0)
            return;

        for(int i = 0; i < array.Length; i++)
        {
            array[i] = (T)buffer[i];
        }
    }

    public void SetData<T>(in T[] array)
    {
        Array.Copy(array, buffer, array.Length);
    }

    public int GetCount()
    {
        return counter;
    }
}

public class Texture3DEmulator
{
    float[] buffer;
    int width, height, depth;

    public float this[int x, int y, int z]
    {
        get { return buffer[x + y * width + z * width * height]; }
        set { buffer[x + y * width + z * width * height] = value; }
    }

    public float this[Vector3Int id]
    {
        get { return this[id.x, id.y, id.z]; }
        set { this[id.x, id.y, id.z] = value; }
    }

    public Texture3DEmulator(int width, int height, int depth)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;

        buffer = new float[width * height * depth];
    }

    public Texture3D Get()
    {
        Texture3D tex = new Texture3D(width, height, depth, TextureFormat.RFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;

        tex.SetPixelData(buffer, 0);
        tex.Apply();

        return tex;
    }
}
