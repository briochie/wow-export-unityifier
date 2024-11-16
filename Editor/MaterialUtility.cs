using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace WowUnity
{
    class MaterialUtility
    {
        public const string ADT_CHUNK_SHADER = "wow.unity/TerrainChunk";

        public enum MaterialFlags : short
        {
            None = 0x0,
            Unlit = 0x1,
            Unfogged = 0x2,
            TwoSided = 0x4
        }

        public enum BlendModes : short
        {
            Opaque = 0,
            AlphaKey = 1,
            Alpha = 2,
            NoAlphaAdd = 3,
            Add = 4,
            Mod = 5,
            Mod2X = 6,
            BlendAdd = 7
        }

            [Serializable]
        public struct ADTChunk
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

        public static Material ConfigureMaterial(MaterialDescription description, Material material, string modelImportPath, M2Utility.M2 metadata)
        {
            if (Regex.IsMatch(Path.GetFileNameWithoutExtension(modelImportPath), @"adt_\d{2}_\d{2}"))
                return ProcessADTMaterial(description, material, modelImportPath);

            #if UNITY_UNIVERSAL_RP_12_0_0_OR_GREATER
                URPMaterialProcessor.ConfigureMaterial(description, material, modelImportPath, metadata);
            #endif
            
            return material;
        }

        public static Color ProcessMaterialColors(Material material, M2Utility.M2 metadata)
        {
            int i, j, k;
            Color newColor = Color.white;
            if (metadata.skin == null || metadata.skin.textureUnits.Count <= 0)
            {
                return newColor;
            }

            for (i = 0; i < metadata.textures.Count; i++)
            {
                if (material.name == metadata.textures[i].mtlName)
                    break;
            }

            for (j = 0; j < metadata.skin.textureUnits.Count; j++)
            {
                if (metadata.skin.textureUnits[j].geosetIndex == i)
                    break;
            }

            if (j < metadata.skin.textureUnits.Count)
                k = (int)metadata.skin.textureUnits[j].colorIndex;
            else
                return newColor;

            if (k < metadata.colors.Count)
            {
                newColor.r = metadata.colors[k].color.values[0][0][0];
                newColor.g = metadata.colors[k].color.values[0][0][1];
                newColor.b = metadata.colors[k].color.values[0][0][2];
                newColor.a = 1;
            }

            return newColor;
        }

        public static Material ProcessADTMaterial(MaterialDescription description, Material material, string modelImportPath)
        {
            material.shader = Shader.Find(ADT_CHUNK_SHADER);

            TexturePropertyDescription textureProperty;
            if (description.TryGetProperty("DiffuseColor", out textureProperty) && textureProperty.texture != null)
            {
                material.SetTexture("_BaseMap", textureProperty.texture);
            }

            LoadMetadataAndConfigureADT(material, modelImportPath);

            return material;
        }

        public static void LoadMetadataAndConfigureADT(Material mat, string assetPath)
        {
            string jsonFilePath = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar + mat.name + ".json";
            var sr = new StreamReader(Application.dataPath.Replace("Assets", "") + jsonFilePath);
            var fileContents = sr.ReadToEnd();
            sr.Close();

            ADTChunk newChunk = JsonUtility.FromJson<ADTChunk>(fileContents);

            Vector4 scaleVector = new Vector4();
            Vector4 heightScaleVector = new Vector4(1, 1, 1, 1);
            Vector4 heightOffsetVector = new Vector4(0, 0, 0, 0);

            Layer currentLayer;

            for (int i = 0; i < newChunk.layers.Count; i++)
            {
                currentLayer = newChunk.layers[i];

                string texturePath = Path.Combine(Path.GetDirectoryName(@assetPath), @currentLayer.file);
                texturePath = Path.GetFullPath(texturePath);
                texturePath = texturePath.Substring(texturePath.IndexOf($"Assets{Path.DirectorySeparatorChar}"));

                Texture2D layerTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D));
                mat.SetTexture("Layer_" + i, layerTexture);

                // If height data is included, we'll add that to the shader as well:
                if(@currentLayer.heightFile != null) {
                    string heightTexturePath = Path.Combine(Path.GetDirectoryName(@assetPath), @currentLayer.heightFile);
                    heightTexturePath = Path.GetFullPath(heightTexturePath);
                    heightTexturePath = heightTexturePath.Substring(heightTexturePath.IndexOf($"Assets{Path.DirectorySeparatorChar}"));

                    // Properly Configure the Height texture data
                    ConfigureDataTexture(heightTexturePath);

                    Texture2D heightLayerTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(heightTexturePath, typeof(Texture2D));
                    mat.SetTexture("Height_" + i, heightLayerTexture);

                    heightScaleVector[i] = currentLayer.heightScale;
                    heightOffsetVector[i] = currentLayer.heightOffset;
                }

                scaleVector[i] = currentLayer.scale;
            }

            mat.SetVector("Scale", scaleVector);
            mat.SetVector("Height_Scale", heightScaleVector);
            mat.SetVector("Height_Offset", heightOffsetVector);
        }

        public static void ExtractMaterialFromAsset(Material material)
        {
            string assetPath = AssetDatabase.GetAssetPath(material);
            string newMaterialPath = "Assets/Materials/" + material.name + ".mat";
            Material newMaterialAsset;

            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
            }
            
            if (!File.Exists(newMaterialPath))
            {
                newMaterialAsset = new Material(material);
                AssetDatabase.CreateAsset(newMaterialAsset, newMaterialPath);
            }
            else
            {
                newMaterialAsset = AssetDatabase.LoadAssetAtPath<Material>(newMaterialPath);
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), newMaterialAsset);

            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        public static void ConfigureDataTexture(string assetPath) {
            TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(assetPath);

            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.filterMode = FilterMode.Bilinear;
            textureImporter.mipmapEnabled = false;
            textureImporter.sRGBTexture = false;
            textureImporter.mipmapEnabled = false;
        }
    }
}
