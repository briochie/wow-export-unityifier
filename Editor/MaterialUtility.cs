using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class MaterialUtility
    {
        public const string LIT_SHADER = "Universal Render Pipeline/Lit";
        public const string UNLIT_SHADER = "Universal Render Pipeline/Unlit";
        public const string EFFECT_SHADER = "Universal Render Pipeline/Particles/Unlit";
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

        public enum MaterialFor: short
        {
            M2 = 0,
            WMO = 1,
        }

        public static Material GetMaterial(M2Utility.Texture texture, MaterialFor materialFor, short flags, uint blendingMode, int shader, Color materialColor) {
            var colorName = materialColor == Color.white ? "W" : ColorUtility.ToHtmlStringRGBA(materialColor);
            var matName = $"{Path.GetFileNameWithoutExtension(texture.fileNameExternal)}_TF{texture.flag}_F{flags:X}_B{blendingMode:X}_S{shader:X}_C{colorName}";
            var assetMatPath = Path.Join(Path.GetDirectoryName(texture.assetPath), $"{matName}.mat");

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetMatPath);
            if (material != null) {
                return material;
            }

            Debug.Log($"{matName}: material does not exist, creating.");

            material = new Material(Shader.Find(LIT_SHADER));
            material.SetFloat("_WorkflowMode", 0);
            material.SetFloat("_Smoothness", 0);
            if (shader == 1) {
                material.SetFloat("_Smoothness", 1);
                material.SetFloat("_SmoothnessTextureChannel", 1);
            }

            //Flags first
            if ((flags & (short)MaterialFlags.Unlit) != (short)MaterialFlags.None)
            {
                material.shader = Shader.Find(UNLIT_SHADER);
                material.SetFloat("_Cull", 0);
            }

            Texture assetTexture = AssetDatabase.LoadAssetAtPath<Texture>(texture.assetPath);
            material.SetTexture("_BaseMap", assetTexture);
            material.SetColor("_BaseColor", materialColor);

            if ((flags & (short)MaterialFlags.TwoSided) != (short)MaterialFlags.None)
            {
                material.doubleSidedGI = true;
                material.SetFloat("_Cull", 0);
            }

            //Now blend modes
            if (blendingMode == (short)BlendModes.AlphaKey)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_AlphaClip", 1);
            }

            if (blendingMode == (short)BlendModes.Alpha)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Blend", 0);
                material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
            }

            if (blendingMode == (short)BlendModes.Add)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetFloat("_Cutoff", 0);
                material.SetFloat("_Blend", 2);
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 1);
                material.SetFloat("_ZWrite", 0);
                material.SetShaderPassEnabled("ShadowCaster", false);
            }

            if (materialFor == MaterialFor.WMO && (flags & 16) == 16)
            {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                material.SetColor("_EmissionColor", Color.white);
                material.SetTexture("_EmissionMap", assetTexture);
            }

            AssetDatabase.CreateAsset(material, assetMatPath);

            return material;
        }

        public static Dictionary<uint, (Material, bool)> GetSkinMaterials(M2Utility.M2 metadata)
        {
            Dictionary<uint, (Material, bool)> mats = new();

            foreach (var textureUnit in metadata.skin.textureUnits) {
                var texture = metadata.textures[metadata.textureCombos[checked((int)textureUnit.textureComboIndex)]];
                var unitMat = metadata.materials[checked((int)textureUnit.materialIndex)];

                var materialColor = Color.white;
                Debug.Log($"color: {textureUnit.colorIndex}, {textureUnit.colorIndex != 0xffff}");
                if (textureUnit.colorIndex < 0xffff)
                {
                    var colorValue = metadata.colors[(int)textureUnit.colorIndex].color.values[0][0];
                    materialColor = new Color() { r = colorValue[0], g = colorValue[1], b = colorValue[2], a = 1 };
                }

                mats[textureUnit.skinSectionIndex] = (
                    GetMaterial(texture, MaterialFor.M2, unitMat.flags, unitMat.blendingMode, 0, materialColor),
                    (unitMat.flags & (short)MaterialFlags.TwoSided) != (short)MaterialFlags.None); // is two-sided
            }

            AssetDatabase.SaveAssets();
            return mats;
        }

        public static Dictionary<string, Material> GetWMOMaterials(WMOUtility.WMO metadata)
        {
            Dictionary<string, Material> mats = new();

            var texturesById = metadata.textures.ToDictionary((item) => item.fileDataID);

            foreach (var group in metadata.groups) {
                for (var batchIdx = 0; batchIdx < group.renderBatches.Count; batchIdx++) {
                    var batch = group.renderBatches[batchIdx];
                    var batchMat = metadata.materials[checked((int)batch.materialID)];
                    var texture = texturesById[batchMat.texture1];

                    var material = GetMaterial(texture, MaterialFor.WMO, batchMat.flags, batchMat.blendMode, batchMat.shader, Color.white);
                    mats[$"{group.groupName}{batchIdx}"] = material;
                }
            }

            AssetDatabase.SaveAssets();
            return mats;
        }

        public static Material GetTerrainMaterial(string dirName, string chunkName, ADTUtility.Tex metadata)
        {
            var matDir = Path.Join(dirName, "terrain_materials");
            if (!Directory.Exists(matDir))
            {
                Directory.CreateDirectory(matDir);
            }
            var assetMatPath = Path.Join(matDir, $"tex_{chunkName}.mat");

            var assetMat = AssetDatabase.LoadAssetAtPath<Material>(assetMatPath);
            if (assetMat != null)
            {
                return assetMat;
            }

            Debug.Log($"{assetMatPath}: material does not exist, creating.");

            var textures = metadata.layers.Select((item) => AssetDatabase.LoadAssetAtPath<Texture>(item.assetPath)).ToList();
            var mask = AssetDatabase.LoadAssetAtPath<Texture>(Path.Join(dirName, $"tex_{chunkName}.png"));

            assetMat = new Material(Shader.Find(ADT_CHUNK_SHADER));
            assetMat.SetTexture("_BaseMap", mask);

            Vector4 scaleVector = new Vector4();
            ADTUtility.Layer currentLayer;
            for (int i = 0; i < metadata.layers.Count; i++)
            {
                currentLayer = metadata.layers[i];
                var layerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(currentLayer.assetPath);
                assetMat.SetTexture("Layer_" + i, layerTexture);
                scaleVector[i] = currentLayer.scale;
            }

            assetMat.SetVector("Scale", scaleVector);

            AssetDatabase.CreateAsset(assetMat, assetMatPath);

            return assetMat;
        }
    }
}
