using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public int meshLOD;
    public bool showOctree = false;
    public bool printOctree = false;
    Mesh mesh;
    MeshFilter filter;
    MeshCollider collider;
    MeshRenderer renderer;

    Vector4[] octree;

    public void Recreate(Mesh mesh, Material material, int meshLOD, Vector4[] octree = null)
    {
        this.mesh = mesh;
        this.meshLOD = meshLOD;
        this.octree = octree;
        filter = GetComponent<MeshFilter>();
        collider = GetComponent<MeshCollider>();
        renderer = GetComponent<MeshRenderer>();

        filter.mesh = mesh;
        collider.sharedMesh = mesh;
        renderer.material = material;
    }

    private void OnDrawGizmos()
    {
        if(showOctree && octree != null)
        {
            foreach (float4 oct in octree)
            {
                if (oct.w <= 0) continue;
                Gizmos.color = new Color(0, 1, 0, 1f / oct.w);
                Gizmos.DrawWireCube((float3)transform.position + (oct.xyz + (oct.w / 2f)) * transform.localScale, new float3(oct.w) * transform.localScale);
            }
        }

        if(octree != null && printOctree)
        {
            printOctree = false;
            foreach (float4 oct in octree)
            {
                if (oct.w <= 0) continue;
                Debug.Log($"Position: {oct.xyz}, Size: {oct.w}");
            }
        }
    }
}
