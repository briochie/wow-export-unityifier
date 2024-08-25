using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WowUnity;

public class WoWUnityWindow : EditorWindow
{
    private Dictionary<string, GameObject> selectedAssets;

    [MenuItem("Window/wow.unity")]
    public static void ShowWindow()
    {
        GetWindow<WoWUnityWindow>("wow.unity");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChangeForMap;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChangeForMap;
    }

    void OnSelectionChangeForMap()
    {
        selectedAssets = new();
        foreach (var obj in Selection.objects)
        {
            if (obj.GetType() != typeof(GameObject))
            {
                continue;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
            if (!ADTUtility.IsAdtAny(path))
            {
                continue;
            }

            selectedAssets[obj.name] = obj as GameObject;
        }
        Repaint();
    }

    private void OnGUI()
    {
        GUILayout.Label("All Assets", EditorStyles.boldLabel);
        if (GUILayout.Button("Process"))
        {
            ProcessAssets();
        }

        GUILayout.Space(10);

        GUILayout.Label("Map", EditorStyles.boldLabel);

        if (selectedAssets == null || selectedAssets.Count == 0)
        {
            GUILayout.Label("No tiles selected. Select some in the project window.");
        } else
        {
            GUILayout.Label("Selected tiles:");

            foreach (var asset in selectedAssets.Values)
            {
                EditorGUILayout.ObjectField(asset, typeof(GameObject), false);
            }

            GUILayout.BeginHorizontal();

            try
            {

                if (GUILayout.Button("Setup Terrain"))
                {
                    SetupTerrain();
                }

                if (GUILayout.Button("Place Doodads"))
                {
                    PlaceDoodads();
                }
            } catch (System.Exception) {
                throw;
            } finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    void ProcessAssets()
    {
        AssetConversionManager.JobPostprocessAllAssets();
    }

    List<string> GetRootAdtPaths() {
        List<string> paths = new();
        HashSet<string> pathsH = new();

        foreach (var selectedAsset in selectedAssets.Values)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);
            if (pathsH.Contains(path))
            {
                continue;
            }

            pathsH.Add(path);
            paths.Add(path);
        }

        return paths;
    }

    void SetupTerrain()
    {
        ADTUtility.PostProcessImports(GetRootAdtPaths());
        Debug.Log("Done setting up terrain.");
    }

    void PlaceDoodads()
    {
        ProcessAssets();
        SetupTerrain();

        foreach (var path in GetRootAdtPaths())
        {
            GameObject prefab = M2Utility.FindPrefab(Path.ChangeExtension(path, "prefab"));

            TextAsset placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.ChangeExtension(path, "obj").Replace(".obj", "_ModelPlacementInformation.csv"));
            if (placementData == null)
            {
                Debug.LogWarning($"{path}: ModelPlacementInformation.csv not found.");
                continue;
            }

            Debug.Log($"{path}: placing doodads...");
            ItemCollectionUtility.PlaceModels(prefab, placementData);
        }

        Debug.Log("Done placing doodads.");
    }
}
