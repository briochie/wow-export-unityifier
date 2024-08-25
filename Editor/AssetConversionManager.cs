using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class AssetConversionManager
    {
        private static readonly ConcurrentQueue<string> importedModelPathQueue = new();
        private static readonly ConcurrentQueue<string> importedWMOPathQueue = new();
        private static readonly ConcurrentQueue<string> physicsQueue = new();
        private static bool isBusy = false;

        public static void QueuePostprocess(string filePath)
        {
            importedModelPathQueue.Enqueue(filePath);
        }

        public static bool HasQueue()
        {
            return importedModelPathQueue.Count + importedWMOPathQueue.Count + physicsQueue.Count > 0;
        }

        public static bool IsBusy()
        {
            return isBusy;
        }

        public static string ReadAssetJson(string path)
        {
            var dirName = Path.GetDirectoryName(path);
            string pathToMetadata = dirName + "/" + Path.GetFileNameWithoutExtension(path) + ".json";
            string mainDataPath = Application.dataPath.Replace("Assets", "");

            var sr = new StreamReader(mainDataPath + pathToMetadata);
            var jsonData = sr.ReadToEnd();
            sr.Close();

            return jsonData;
        }

        public static void RunPostProcessImports()
        {
            var itemsToProcess = importedModelPathQueue.Count;
            var itemsProcessed = 0f;

            List<(string, TextAsset)> hasPlacement = new();

            AssetDatabase.StartAssetEditing();
            try
            {
                while (importedModelPathQueue.TryDequeue(out string path))
                {
                    Debug.Log($"{path}: postprocessing");

                    var jsonData = ReadAssetJson(path);

                    if (WMOUtility.IsWMO(jsonData))
                    {
                        // process this separately because of StartAssetEditing issues
                        importedWMOPathQueue.Enqueue(path);
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar("Postprocessing WoW assets", path, itemsProcessed / itemsToProcess))
                    {
                        return;
                    }

                    M2Utility.PostProcessImport(path, jsonData);

                    // process this separately because of StartAssetEditing issues
                    physicsQueue.Enqueue(path);

                    itemsProcessed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            while (importedWMOPathQueue.TryDequeue(out string path))
            {
                Debug.Log($"{path}: postprocessing");

                if (EditorUtility.DisplayCancelableProgressBar("Postprocessing WoW WMOs", path, itemsProcessed / itemsToProcess))
                {
                    return;
                }

                WMOUtility.PostProcessImport(path, ReadAssetJson(path));
                SetupPhysics(path);

                TextAsset placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(path.Replace(".obj", "_ModelPlacementInformation.csv"));
                if (placementData != null)
                {
                    hasPlacement.Add((path, placementData));
                }

                itemsProcessed++;
            }

            itemsToProcess = physicsQueue.Count;
            itemsProcessed = 0f;

            while (physicsQueue.TryDequeue(out string path))
            {
                Debug.Log($"{path}: setup physics");

                if (EditorUtility.DisplayCancelableProgressBar("Setting up physics", path, itemsProcessed / itemsToProcess))
                {
                    return;
                }

                SetupPhysics(path);

                itemsProcessed++;
            }

            itemsToProcess = hasPlacement.Count;
            itemsProcessed = 0f;

            foreach (var (path, placementData) in hasPlacement)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Placing doodads", path, itemsProcessed / itemsToProcess))
                {
                    return;
                }
                Debug.Log($"{path}: placing models");
                ItemCollectionUtility.PlaceModels(M2Utility.FindPrefab(path), placementData);
                itemsProcessed++;
            }
        }

        public static void SetupPhysics(string path)
        {
            GameObject physicsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(".obj", ".phys.obj"));
            if (physicsPrefab == null)
            {
                return;
            }

            var prefab = M2Utility.FindPrefab(path);

            if (prefab.transform.Find("Collision") != null)
            {
                return;
            }

            var collisionMesh = physicsPrefab.GetComponentInChildren<MeshFilter>();
            if (collisionMesh == null)
            {
                return;
            }

            var prefabInst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            prefabInst.GetComponentsInChildren<MeshCollider>().ToList().ForEach(collider => Object.DestroyImmediate(collider));

            GameObject collider = new();
            collider.transform.SetParent(prefabInst.transform);
            collider.name = "Collision";
            MeshCollider parentCollider = collider.AddComponent<MeshCollider>();
            parentCollider.sharedMesh = collisionMesh.sharedMesh;
            PrefabUtility.ApplyPrefabInstance(prefabInst, InteractionMode.AutomatedAction);
            PrefabUtility.SavePrefabAsset(prefab);

            Object.DestroyImmediate(prefabInst);
        }

        public static void PostProcessImports()
        {
            if (importedModelPathQueue.Count + importedWMOPathQueue.Count + physicsQueue.Count == 0)
            {
                return;
            }

            isBusy = true;

            try
            {
                RunPostProcessImports();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("PostProcessImports done");
            isBusy = false;
        }

        public static void JobPostprocessAllAssets()
        {
            importedModelPathQueue.Clear();
            importedWMOPathQueue.Clear();
            physicsQueue.Clear();

            EditorUtility.DisplayProgressBar("Postprocessing WoW assets", "Looking for assets.", 0);
            try
            {
                string[] allAssets = AssetDatabase.GetAllAssetPaths();
                foreach (string path in allAssets)
                {
                    if (WoWExportUnityPostprocessor.ValidAsset(path) && !ADTUtility.IsAdtObj(path))
                    {
                        QueuePostprocess(path);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            PostProcessImports();
        }
    }
}
