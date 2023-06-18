using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MarchingCubesTest))]
public class MarchingCubesTestEditor : Editor
{
    private float slice = 0;
    void OnEnable()
    {
        
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MarchingCubesTest marchingCubesTest = (MarchingCubesTest)target;

        if (GUILayout.Button("Generate Noise"))
        {
            marchingCubesTest.GenerateNoiseTex();
        }

        if (GUILayout.Button("Generate Mesh"))
        {
            marchingCubesTest.GenerateMesh();
        }

        //marchingCubesTest.GenerateMesh();
    }
}
