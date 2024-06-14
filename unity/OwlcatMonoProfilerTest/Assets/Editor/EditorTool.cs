using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EditorTool
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [MenuItem("Tests/GC_dump")]
    private static void Dump()
    {
        Application.GC_dump();
    }
}
