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
}
