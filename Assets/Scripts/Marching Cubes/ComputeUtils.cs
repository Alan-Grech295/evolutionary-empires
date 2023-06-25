using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ComputeUtils
{
    public static void Dispatch(ComputeShader shader, int x, int y, int z, int kernelIndex = 0)
    {
        if (x == 0 || y == 0 || z == 0)
        {
            return;
        }
        uint3 kernelThreads;
        shader.GetKernelThreadGroupSizes(kernelIndex, out kernelThreads.x, out kernelThreads.y, out kernelThreads.z);
        int3 numThreads = new int3(Mathf.CeilToInt((float)x / kernelThreads.x),
                                   Mathf.CeilToInt((float)y / kernelThreads.y),
                                   Mathf.CeilToInt((float)z / kernelThreads.z));

        shader.Dispatch(kernelIndex, numThreads.x, numThreads.y, numThreads.z);
    }

    public static void Release(params ComputeBuffer[] buffers)
    {
        foreach(var buffer in buffers)
        {
            buffer.Release();
        }
    }

    public static Tuple<T[], int> ReadData<T>(ComputeBuffer src, ComputeBuffer count, int multiplier = 1)
    {
        int[] countData = new int[1];
        ComputeBuffer.CopyCount(src, count, 0);
        count.GetData(countData);

        T[] values = new T[countData[0] * multiplier];
        src.GetData(values);

        return new Tuple<T[], int>(values, countData[0] * multiplier);
    }

    public static int GetSize(ComputeBuffer src, ComputeBuffer count)
    {
        int[] countData = new int[1];
        ComputeBuffer.CopyCount(src, count, 0);
        count.GetData(countData);

        return countData[0];
    }

    public static int GetStride<T>()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
    }
}
