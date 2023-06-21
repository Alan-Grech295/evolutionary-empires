using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MarchingCubesTest))]
[CanEditMultipleObjects]
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
            foreach(var gameObject in Selection.gameObjects)
            {
                if (gameObject.GetComponent<MarchingCubesTest>() == null)
                    continue;

                gameObject.GetComponent<MarchingCubesTest>().GenerateMesh();
            }
        }

        if(GUILayout.Button("Get Generation Average"))
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < 50; i++)
            {
                foreach (var gameObject in Selection.gameObjects)
                {
                    if (gameObject.GetComponent<MarchingCubesTest>() == null)
                        continue;

                    gameObject.GetComponent<MarchingCubesTest>().GenerateMesh();
                }
            }
            stopwatch.Stop();
            UnityEngine.Debug.Log($"Average generation time: {stopwatch.Elapsed.TotalMilliseconds / 50d}ms");
        }

        //marchingCubesTest.GenerateMesh();
    }
}
