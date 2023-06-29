using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;


public interface IComputeEmulator
{
    public void Main(int3 id);
}

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

    public static void Dispatch(int x, int y, int z, IComputeEmulator computeShader)
    {
        List<int3> ids = new List<int3>(x * y * z);
        for(int zThread = 0; zThread < z; zThread++)
        {
            for (int yThread = 0; yThread < y; yThread++)
            {
                for (int xThread = 0; xThread < x; xThread++)
                {
                    ids.Add(new int3(xThread, yThread, zThread));
                }
            }
        }

        System.Random rand = new System.Random();

        while(ids.Count > 0)
        {
            int i = rand.Next(0, ids.Count);
            computeShader.Main(ids[i]);
            ids.RemoveAt(i);
        }
    }
}

public class BufferEmulator
{
    object[] buffer;
    int counter = 0;

    static object DeepCopy<T>(T other)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Context = new StreamingContext(StreamingContextStates.Clone);
            formatter.Serialize(ms, other);
            ms.Position = 0;
            return formatter.Deserialize(ms);
        }
    }

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
        int temp = counter;
        counter++;
        return temp;
    }

    public void SetCounter(int counter)
    {
        this.counter = counter;
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
            array[i] = unchecked((T)buffer[i]);
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

    public float this[int3 id]
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
