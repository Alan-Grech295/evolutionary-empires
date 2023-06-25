using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DualContourTest))]
public class DualContourTestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        DualContourTest dualContourTest = (DualContourTest)target;
        if (GUILayout.Button("Generate"))
        {
            dualContourTest.Run();
        }
    }
}
