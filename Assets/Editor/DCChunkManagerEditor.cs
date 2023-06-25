using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DCChunkManager))]

public class DCChunkManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        DCChunkManager chunkManager = (DCChunkManager)target;

        if(GUILayout.Button("Generate Chunks"))
        {
            chunkManager.ReloadChunksImmediate();
        }

        if (GUILayout.Button("Clear"))
        {
            while(chunkManager.transform.childCount > 0)
            {
                DestroyImmediate(chunkManager.transform.GetChild(0).gameObject);
            }
        }
    }
}
