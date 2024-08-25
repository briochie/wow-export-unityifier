using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class M2Utility
    {
        public static void PostProcessImport(string path, string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<M2>(jsonData);
            if (metadata.fileType != "m2") {
                return;
            }

            Debug.Log($"{path}: processing m2");

            if (FindPrefab(path) != null)
            {
                return;
            }

            ProcessTextures(metadata.textures, Path.GetDirectoryName(path));

            var importedInstance = InstantiateImported(path);

            Renderer[] renderers = importedInstance.GetComponentsInChildren<Renderer>();

            var skinMaterials = MaterialUtility.GetSkinMaterials(metadata);

            // Configure materials
            for (uint rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var (material, isMatDoubleSided) = skinMaterials[rendererIndex];
                renderer.material = material;
            }
            AssetDatabase.Refresh();

            SaveAsPrefab(importedInstance, path);

            if (metadata.textureTransforms.Count > 0 && metadata.textureTransforms[0].translation.timestamps.Count > 0)
            {
                for (int i = 0; i < metadata.textureTransforms.Count; i++)
                {
                    AnimationClip newClip = AnimationUtility.CreateAnimationClip(metadata.textureTransforms[i]);
                    AssetDatabase.CreateAsset(newClip, Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "[" + i +  "]" + ".anim");
                }
            }
        }

        public static GameObject InstantiateImported(string path)
        {
            var importedModelObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (importedModelObject == null)
            {
                Debug.LogWarning("Tried to create prefab, but could not find imported model: " + path);
                return null;
            }

            var rootObj = new GameObject() { isStatic = true };
            var rootModelInstance = PrefabUtility.InstantiatePrefab(importedModelObject, rootObj.transform) as GameObject;

            //Set the object as static, and all it's child objects
            rootModelInstance.isStatic = true;
            foreach (Transform childTransform in rootModelInstance.transform)
            {
                childTransform.gameObject.isStatic = true;
            }

            return rootObj;
        }

        public static void SaveAsPrefab(GameObject gameObject, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(gameObject, Path.ChangeExtension(path, ".prefab"));
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        public static GameObject FindPrefab(string path)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        public static void ProcessTextures(List<Texture> textures, string dirName) {
            string mainDataPath = Application.dataPath.Replace("Assets", "");

            for (var idx = 0; idx < textures.Count; idx++) {
                var texture = textures[idx];
                texture.assetPath = Path.GetRelativePath(mainDataPath, Path.GetFullPath(Path.Join(dirName, texture.fileNameExternal)));
                textures[idx] = texture;
            }
        }

        [Serializable]
        public class M2
        {
            public string fileType;
            public uint fileDataID;
            public string fileName;
            public string internalName;
            public Skin skin;
            public List<Texture> textures = new List<Texture>();
            public List<short> textureTypes = new List<short>();
            public List<Material> materials = new List<Material>();
            public List<short> textureCombos = new List<short>();
            public List<ColorData> colors = new List<ColorData>();
            public List<TextureTransform> textureTransforms = new List<TextureTransform>();
            public List<uint> textureTransformsLookup = new List<uint>();
        }

        [Serializable]
        public class Skin
        {
            public List<SubMesh> subMeshes = new List<SubMesh>();
            public List<TextureUnit> textureUnits = new List<TextureUnit>();
        }

        [Serializable]
        public struct SubMesh
        {
            public bool enabled;
        }

        [Serializable]
        public struct TextureUnit
        {
            public uint skinSectionIndex;
            public uint geosetIndex;
            public uint materialIndex;
            public uint colorIndex;
            public uint textureComboIndex;
        }

        [Serializable]
        public struct Texture
        {
            public string fileNameInternal;
            public string fileNameExternal;
            public string assetPath;
            public string mtlName;
            public string uniqMtlName;
            public short flag;
            public uint fileDataID;
        }

        [Serializable]
        public struct Material
        {
            public short flags;
            public uint blendingMode;
        }

        [Serializable]
        public struct ColorData
        {
            public MultiValueAnimationInformation color;
            public SingleValueAnimationInformation alpha;
        }

        [Serializable]
        public struct TextureTransform
        {
            public MultiValueAnimationInformation translation;
            public MultiValueAnimationInformation rotation;
            public MultiValueAnimationInformation scaling;
        }

        [Serializable]
        public struct SingleValueAnimationInformation
        {
            public uint globalSeq;
            public int interpolation;
            public List<List<uint>> timestamps;
            public List<List<float>> values;
        }

        [Serializable]
        public struct MultiValueAnimationInformation
        {
            public uint globalSeq;
            public int interpolation;
            public List<List<uint>> timestamps;
            public List<List<List<float>>> values;
        }
    }
}
