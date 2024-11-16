using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System;
using System.Text.RegularExpressions;

public class TerrainMaterialGenerator : EditorWindow
{
    public Shader terrainShader;
    public List<TextAsset> jsonFiles;

    [MenuItem("Window/Generate Terrain Material Data")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(TerrainMaterialGenerator));
    }

    void OnGUI()
    {
        GUILayout.Label("Generate Terrain Material Data", EditorStyles.boldLabel);

        // "target" can be any class derrived from ScriptableObject 
        // (could be EditorWindow, MonoBehaviour, etc)
        ScriptableObject target = this;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty terrainShaderProp = so.FindProperty("terrainShader");
        SerializedProperty jsonFileProp = so.FindProperty("jsonFiles");

        EditorGUILayout.PropertyField(terrainShaderProp, true); // True means show children
        EditorGUILayout.PropertyField(jsonFileProp, true); // True means show children
        so.ApplyModifiedProperties(); // Remember to apply modified properties

        GUILayout.Space(5f);

        if (GUILayout.Button("Generate Data"))
        {
            ParseFile();
        }
    }

    void ParseFile()
    {
        foreach (TextAsset chunkData in jsonFiles)
        {
            Chunk newChunk = JsonUtility.FromJson<Chunk>(chunkData.text);

            string filePath = AssetDatabase.GetAssetPath(chunkData);
            Match match = Regex.Match(filePath, @"tex_\d{2}_\d{2}_\d{1,3}(?=\.json)");
            string materialName = match.Groups[0].Value;

            Material currentChunkMaterial = (Material)AssetDatabase.LoadAssetAtPath("Assets/Materials/" + materialName + ".mat", typeof(Material));
            currentChunkMaterial.shader = terrainShader;

            Vector4 scaleVector = new Vector4();
            Layer currentLayer;
            for (int i = 0; i < newChunk.layers.Count; i++)
            {
                currentLayer = newChunk.layers[i];
                string texturePath = currentLayer.file.Replace("..\\..\\", "Assets\\world geometry\\");
                Debug.Log(texturePath);
                Texture2D layerTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));
                currentChunkMaterial.SetTexture("Layer_" + i, layerTexture);
                scaleVector[i] = currentLayer.scale;
            }

            currentChunkMaterial.SetVector("Scale", scaleVector);
        }
    }

    [Serializable]
    public struct Chunk
    {
        public List<Layer> layers;
    }

    [Serializable]
    public struct Layer
    {
        public int index;
        public int effectID;
        public int fileDataID;
        public int scale;
        public string file;
        public string heightFile;
        public float heightScale;
        public float heightOffset;
    }
}
