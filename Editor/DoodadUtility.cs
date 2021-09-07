using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WowExportUnityifier
{
    public class DoodadUtility
    {
        public static readonly char CSV_LINE_SEPERATOR = '\n';
        public static readonly char CSV_COLUMN_SEPERATOR = ';';

        public static readonly float MAXIMUM_DISTANCE_FROM_ORIGIN = 51200f / 3f;
        public static readonly float MAP_SIZE = MAXIMUM_DISTANCE_FROM_ORIGIN * 2f;
        public static readonly float ADT_SIZE = MAP_SIZE / 64f;

        private static List<string> queuedPlacementInformationPaths = new List<string>();
        private static List<string> missingFilesInQueue = new List<string>();

        public static bool isADT(TextAsset modelPlacementInformation)
        {
            return Regex.IsMatch(modelPlacementInformation.name, @"adt_\d{2}_\d{2}");
        }

        public static void Generate(GameObject prefab, TextAsset modelPlacementInformation)
        {
            GameObject instantiatedGameObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            string path = AssetDatabase.GetAssetPath(prefab);

            ParseFileAndSpawnDoodads(instantiatedGameObject, modelPlacementInformation);

            string parentPath = AssetDatabase.GetAssetPath(prefab);

            if (Path.GetExtension(parentPath) == ".prefab")
            {
                PrefabUtility.ApplyPrefabInstance(instantiatedGameObject, InteractionMode.UserAction);
                PrefabUtility.SavePrefabAsset(prefab);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(instantiatedGameObject, parentPath.Replace(Path.GetExtension(parentPath), ".prefab"));
            }

            Object.DestroyImmediate(instantiatedGameObject);
        }

        private static void ParseFileAndSpawnDoodads(GameObject instantiatedPrefabGObj, TextAsset modelPlacementInformation)
        {
            string[] records = modelPlacementInformation.text.Split(CSV_LINE_SEPERATOR);
            foreach (string record in records.Skip(1))
            {
                string[] fields = record.Split(CSV_COLUMN_SEPERATOR);
                string doodadPath = Path.GetDirectoryName(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instantiatedPrefabGObj)) + "\\" + fields[0];
                doodadPath = Path.GetFullPath(doodadPath);
                doodadPath = "Assets\\" + doodadPath.Substring(Application.dataPath.Length + 1); //This is so nifty :3

                Vector3 doodadPosition = new Vector3(float.Parse(fields[1]) * -1, float.Parse(fields[3]), float.Parse(fields[2]) * -1);
                Quaternion doodadRotation = new Quaternion(float.Parse(fields[5]) * -1, float.Parse(fields[7]), float.Parse(fields[6]) * -1, float.Parse(fields[4]) * -1);
                float doodadScale = float.Parse(fields[8]);

                if (isADT(modelPlacementInformation))
                {

                    doodadPosition.x = (MAXIMUM_DISTANCE_FROM_ORIGIN - float.Parse(fields[1])) * -1;
                    doodadPosition.z = MAXIMUM_DISTANCE_FROM_ORIGIN - float.Parse(fields[3]);
                    doodadPosition.y = float.Parse(fields[2]);

                    Vector3 eulerRotation = Vector3.zero;
                    eulerRotation.x = float.Parse(fields[6]);
                    eulerRotation.y = float.Parse(fields[5]) * -1 - 90;
                    eulerRotation.z = float.Parse(fields[4]);

                    doodadRotation.eulerAngles = eulerRotation;
                }

                SpawnDoodad(doodadPath, doodadPosition, doodadRotation, doodadScale, instantiatedPrefabGObj.transform);
            }
        }

        private static void SpawnDoodad(string path, Vector3 position, Quaternion rotation, float scaleFactor, Transform parent)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            GameObject exisitingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);


            if (exisitingPrefab == null)
            {
                GameObject importedModelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (importedModelRoot == null)
                {
                    missingFilesInQueue.Add(path);
                    return;
                }

                GameObject rootModelInstance = PrefabUtility.InstantiatePrefab(importedModelRoot) as GameObject;
                exisitingPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(rootModelInstance, prefabPath, InteractionMode.AutomatedAction);
                AssetDatabase.Refresh();
                Object.DestroyImmediate(rootModelInstance);
            }

            GameObject newDoodadInstance = PrefabUtility.InstantiatePrefab(exisitingPrefab, parent) as GameObject;

            newDoodadInstance.transform.localPosition = position;
            newDoodadInstance.transform.localRotation = rotation;
            newDoodadInstance.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        }

        public static void QueuePlacementData(string filePath)
        {
            queuedPlacementInformationPaths.Add(filePath);
        }

        public static void BeginQueue()
        {
            if (EditorApplication.update != null)
            {
                EditorApplication.update -= BeginQueue;
            }

            if (queuedPlacementInformationPaths.Count == 0)
            {
                return;
            }

            List<string> iteratingList = new List<string>(queuedPlacementInformationPaths);

            foreach (string path in iteratingList)
            {
                queuedPlacementInformationPaths.Remove(path);
                TextAsset placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                string prefabPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileName(path).Replace("_ModelPlacementInformation.csv", ".obj");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                DoodadUtility.Generate(prefab, placementData);
            }

            foreach (string missingFilePath in missingFilesInQueue)
            {
                Debug.Log("Warning, import could not be found: " + missingFilePath);
            }
        }
    }
}