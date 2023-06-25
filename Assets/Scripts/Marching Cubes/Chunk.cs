using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public int meshLOD;
    Mesh mesh;
    MeshFilter filter;
    MeshCollider collider;
    MeshRenderer renderer;

    public void Recreate(Mesh mesh, Material material, int meshLOD)
    {
        this.mesh = mesh;
        this.meshLOD = meshLOD;
        filter = GetComponent<MeshFilter>();
        collider = GetComponent<MeshCollider>();
        renderer = GetComponent<MeshRenderer>();

        filter.mesh = mesh;
        collider.sharedMesh = mesh;
        renderer.material = material;
    }
}
