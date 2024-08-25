using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class ADTUtility
    {
        public static bool IsAdtObj(string path)
        {
            return Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+(_.+)?\.obj$");
        }

        public static bool IsAdtAny(string path)
        {
            return Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+(_.+)?\.(prefab|obj)$");
        }

        public static void PostProcessImports(List<string> paths)
        {
            var total = 0f;
            var itemsProcessed = 0f;

            List<(string, GameObject)> instances = new();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    if (M2Utility.FindPrefab(path) != null)
                    {
                        continue;
                    }

                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();
                    total += renderers.Count() + 1;
                }

                var mainDataPath = Application.dataPath.Replace("Assets", "");
                foreach (var path in paths)
                {
                    if (M2Utility.FindPrefab(path) != null)
                    {
                        continue;
                    }

                    var importedInstance = M2Utility.InstantiateImported(path);

                    var dirName = Path.GetDirectoryName(path);

                    Renderer[] renderers = importedInstance.GetComponentsInChildren<Renderer>();

                    foreach (var renderer in renderers)
                    {
                        var pathToMetadata = $"{dirName}/tex_{renderer.name}.json";

                        if (EditorUtility.DisplayCancelableProgressBar("Creating terrain materials.", pathToMetadata, itemsProcessed / total))
                        {
                            return;
                        }

                        var sr = new StreamReader(mainDataPath + pathToMetadata);
                        var jsonData = sr.ReadToEnd();
                        sr.Close();

                        var metadata = JsonConvert.DeserializeObject<Tex>(jsonData);
                        if (metadata.layers.Count == 0)
                        {
                            continue;
                        }

                        for (var idx = 0; idx < metadata.layers.Count; idx++)
                        {
                            var texture = metadata.layers[idx];
                            texture.assetPath = Path.GetRelativePath(mainDataPath, Path.GetFullPath(Path.Join(dirName, texture.file)));
                            metadata.layers[idx] = texture;
                        }

                        renderer.material = MaterialUtility.GetTerrainMaterial(dirName, renderer.name, metadata);
                        itemsProcessed++;
                    }

                    instances.Add((path, importedInstance));
                }
            }
            catch (System.Exception)
            {
                Debug.LogError($"failed processing terrain materials");
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
            }

            try
            {
                foreach (var (path, instance) in instances)
                {
                    M2Utility.SaveAsPrefab(instance, path);
                    itemsProcessed++;
                }
            } finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public class Tex
        {
            public List<Layer> layers;
        }

        public class Layer
        {
            public uint index;
            public uint effectID;
            public float scale;
            public uint fileDataID;
            public string file;
            public string assetPath;
        }

        public class EffectModel
        {
            public int fileDataID;
            public string fileName;
        }

        public class Effect
        {
            public int ID;
            public int Density;
            public int Sound;
            public List<int> DoodadID;
            public List<float> DoodadWeight;
            public Dictionary<string, EffectModel> DoodadModelIDs;
        }
    }
}
