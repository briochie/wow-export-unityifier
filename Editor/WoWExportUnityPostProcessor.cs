using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using WowUnity;

public class WoWExportUnityPostprocessor : AssetPostprocessor
{
    public override int GetPostprocessOrder()
    {
        return 9001; // must be after unitys post processor so it doesn't overwrite our own stuff
    }

    public override uint GetVersion()
    {
        return 1;
    }

    static public bool ValidAsset(string path)
    {
        if (!path.EndsWith(".obj"))
            return false;
        if (path.EndsWith(".phys.obj"))
            return false;

        return (
            File.Exists(Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + ".json") ||
            ADTUtility.IsAdtObj(path)
        );
    }

    public void OnPreprocessTexture()
    {
        bool match = Regex.IsMatch(assetPath, @"tex_\d{2}_\d{2}_\d{1,3}(?=\.png)");

        if (!match)
        {
            return;
        }

        TextureImporter textureImporter = (TextureImporter)assetImporter;
        textureImporter.textureType = TextureImporterType.Default;
        textureImporter.wrapMode = TextureWrapMode.Clamp;
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        textureImporter.filterMode = FilterMode.Bilinear;
        textureImporter.mipmapEnabled = false;
        textureImporter.sRGBTexture = false;
    }

    public void OnPreprocessModel()
    {
        ModelImporter modelImporter = assetImporter as ModelImporter;

        if (!ValidAsset(assetPath))
        {
            if (assetPath.EndsWith(".phys.obj"))
            {
                modelImporter.bakeAxisConversion = true;
            }
            return;
        }

        Debug.Log($"{assetPath}: processing wow model");

        modelImporter.bakeAxisConversion = true;
        modelImporter.generateSecondaryUV = true;
        modelImporter.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
        modelImporter.secondaryUVMinLightmapResolution = 16;
        modelImporter.secondaryUVMinObjectScale = 1;

        modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    public void OnPostprocessModel(GameObject gameObject)
    {
        if (!ValidAsset(assetPath))
        {
            return;
        }

        if (!File.Exists(assetPath.Replace(".obj", ".phys.obj")))
        {
            MeshRenderer[] childRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer child in childRenderers)
            {
                child.gameObject.AddComponent<MeshCollider>();
            }
        }
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        var hasWow = false;
        foreach (string path in importedAssets)
        {
            if (ValidAsset(path))
            {
                AssetConversionManager.QueuePostprocess(path);
                hasWow = true;
            }
        }

        if (hasWow && !AssetConversionManager.IsBusy())
        {
            var processNow = EditorUtility.DisplayDialog("WoW assets imported", "There were WoW assets imported, they need to be processed to work properly. Do you want to process them now? They can also be processed later by opening menu bar Window > wow.unity and clicking Process under All Assets.", "Process now", "Do it later");
            if (processNow)
            {
                AssetConversionManager.JobPostprocessAllAssets();
            }
        }
    }
}
