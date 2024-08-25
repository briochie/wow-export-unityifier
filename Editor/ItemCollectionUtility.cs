using System.Globalization;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WowUnity
{
    public class ItemCollectionUtility
    {
        public static readonly char CSV_LINE_SEPERATOR = '\n';
        public static readonly char CSV_COLUMN_SEPERATOR = ';';

        public static readonly float MAXIMUM_DISTANCE_FROM_ORIGIN = 51200f / 3f;
        public static readonly float MAP_SIZE = MAXIMUM_DISTANCE_FROM_ORIGIN * 2f;
        public static readonly float ADT_SIZE = MAP_SIZE / 64f;

        public static void PlaceModels(GameObject prefab, TextAsset modelPlacementInformation)
        {
            GameObject instantiatedGameObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instantiatedGameObject.isStatic = true;
            foreach (Transform childTransform in instantiatedGameObject.transform)
            {
                childTransform.gameObject.isStatic = true;
            }

            PlaceModelsOnPrefab(instantiatedGameObject, modelPlacementInformation);

            PrefabUtility.ApplyPrefabInstance(instantiatedGameObject, InteractionMode.AutomatedAction);
            PrefabUtility.SavePrefabAsset(prefab);

            Object.DestroyImmediate(instantiatedGameObject);
        }

        private static Transform GetOrCreateRoot(Transform destination, string name)
        {
            var root = destination.Find(name);
            if (root == null)
            {
                var newRootGO = new GameObject(name) { isStatic = true };
                newRootGO.transform.parent = destination;
                root = newRootGO.transform;
            }
            return root;
        }

        private static void PlaceModelsOnPrefab(GameObject instantiatedPrefabGObj, TextAsset modelPlacementInformation)
        {
            var isAdt = Regex.IsMatch(modelPlacementInformation.name, @"adt_\d+_\d+");

            var doodadSetRoot = GetOrCreateRoot(instantiatedPrefabGObj.transform, isAdt ? "EnvironmentSet" : "DoodadSets");

            if (doodadSetRoot == null)
            {
                Debug.LogWarning("No doodad set root found in " + instantiatedPrefabGObj.name);
                return;
            }

            if (doodadSetRoot.Find("doodadsplaced") != null)
                return;

            ParseFileAndSpawnDoodads(doodadSetRoot, modelPlacementInformation);
            if (isAdt)
                ParseFileAndSpawnDoodads(GetOrCreateRoot(instantiatedPrefabGObj.transform, "WMOSet"), modelPlacementInformation, typeToPlace: "wmo");

            var placed = new GameObject("doodadsplaced");
            placed.transform.parent = doodadSetRoot.transform;
        }

        private static void ParseFileAndSpawnDoodads(Transform doodadSetRoot, TextAsset modelPlacementInformation, string typeToPlace = "m2", bool useSetSubtrees = true)
        {
            var isAdt = Regex.IsMatch(modelPlacementInformation.name, @"adt_\d+_\d+");

            var placementFileDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(modelPlacementInformation));
            string[] records = modelPlacementInformation.text.Split(CSV_LINE_SEPERATOR);
            foreach (string record in records.Skip(1))
            {
                string[] fields = record.Split(CSV_COLUMN_SEPERATOR);
                if (fields.Length < 11)
                    continue;

                string doodadPath = placementFileDir + Path.DirectorySeparatorChar + fields[0];
                doodadPath = Path.GetFullPath(doodadPath);
                doodadPath = $"Assets{Path.DirectorySeparatorChar}" + doodadPath.Substring(Application.dataPath.Length + 1); //This is so nifty :3

                Vector3 doodadPosition = Vector3.zero;
                Quaternion doodadRotation = Quaternion.identity;
                float doodadScale = float.Parse(fields[8], CultureInfo.InvariantCulture);

                if (isAdt)
                {
                    var doodadType = fields[10];
                    if (doodadType != typeToPlace)
                        continue;

                    doodadPosition.x = MAXIMUM_DISTANCE_FROM_ORIGIN - float.Parse(fields[1], CultureInfo.InvariantCulture);
                    doodadPosition.z = (MAXIMUM_DISTANCE_FROM_ORIGIN - float.Parse(fields[3], CultureInfo.InvariantCulture)) * -1f;
                    doodadPosition.y = float.Parse(fields[2], CultureInfo.InvariantCulture);

                    Vector3 eulerRotation = Vector3.zero;
                    eulerRotation.x = float.Parse(fields[6], CultureInfo.InvariantCulture) * -1;
                    eulerRotation.y = float.Parse(fields[5], CultureInfo.InvariantCulture) * -1 - 90;
                    eulerRotation.z = float.Parse(fields[4], CultureInfo.InvariantCulture) * -1;

                    doodadRotation.eulerAngles = eulerRotation;

                    var spawned = SpawnDoodad(doodadPath, doodadPosition, doodadRotation, doodadScale, doodadSetRoot);
                    if (doodadType == "wmo")
                    {
                        var doodadSets = fields[13].Split(",");
                        foreach (var setName in doodadSets)
                        {
                            var childObj = spawned.transform.Find($"DoodadSets/{setName}");
                            if (childObj != null)
                            {
                                childObj.gameObject.SetActive(true);
                            }
                        }
                    }
                }
                else
                {
                    doodadPosition = new Vector3(
                        float.Parse(fields[1], CultureInfo.InvariantCulture),
                        float.Parse(fields[3], CultureInfo.InvariantCulture),
                        float.Parse(fields[2], CultureInfo.InvariantCulture)
                    );
                    doodadRotation = new Quaternion(
                        float.Parse(fields[5], CultureInfo.InvariantCulture) * -1,
                        float.Parse(fields[7], CultureInfo.InvariantCulture),
                        float.Parse(fields[6], CultureInfo.InvariantCulture) * -1,
                        float.Parse(fields[4], CultureInfo.InvariantCulture) * -1
                    );

                    var setName = fields[9];
                    var placementRoot = doodadSetRoot;
                    if (useSetSubtrees)
                    {
                        placementRoot = GetOrCreateRoot(doodadSetRoot, setName);
                        placementRoot.gameObject.SetActive(setName == "Set_$DefaultGlobal");
                    }

                    SpawnDoodad(doodadPath, doodadPosition, doodadRotation, doodadScale, placementRoot);
                }
            }
        }

        private static GameObject SpawnDoodad(string path, Vector3 position, Quaternion rotation, float scaleFactor, Transform parent)
        {
            GameObject exisitingPrefab = M2Utility.FindPrefab(path);

            if (exisitingPrefab == null)
            {
                Debug.LogWarning("Object was not spawned because it could not be found: " + path);
                return null;
            }

            GameObject newDoodadInstance = PrefabUtility.InstantiatePrefab(exisitingPrefab, parent) as GameObject;

            newDoodadInstance.transform.localPosition = position;
            newDoodadInstance.transform.localRotation = rotation;
            newDoodadInstance.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

            return newDoodadInstance;
        }
    }
}
