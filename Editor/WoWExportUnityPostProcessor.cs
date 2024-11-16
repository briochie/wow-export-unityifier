using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System;
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

    static private bool ValidAsset(string path)
    {
        if (!path.Contains(".obj"))
            return false;
        if (path.Contains(".phys.obj"))
            return false;

        return true;
    }

    public void OnPreprocessTexture()
    {
        bool match = Regex.IsMatch(assetPath, @"tex_\d{2}_\d{2}_\d{1,3}(?=\.png)");

        if (!match)
        {
            return;
        }

        MaterialUtility.ConfigureDataTexture(assetPath);
    }

    public void OnPreprocessModel()
    {
        if (!ValidAsset(assetPath))
        {
            return;
        }

        ModelImporter modelImporter = assetImporter as ModelImporter;
        modelImporter.bakeAxisConversion = true;
        modelImporter.generateSecondaryUV = true;
        modelImporter.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
        modelImporter.secondaryUVMinLightmapResolution = 16;
        modelImporter.secondaryUVMinObjectScale = 1;

        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        modelImporter.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        modelImporter.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
    }

    public void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] materialAnimation)
    {
        if (!ValidAsset(assetPath))
        {
            return;
        }

        M2Utility.M2 metadata = M2Utility.ReadMetadataFor(assetPath);
        material = MaterialUtility.ConfigureMaterial(description, material, assetPath, metadata);
    }

    public void OnPostprocessModel(GameObject gameObject)
    {
        if (!ValidAsset(assetPath))
        {
            return;
        }

        GameObject physicsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath.Replace(".obj", ".phys.obj"));
        MeshRenderer[] childRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();

        if (physicsPrefab == null || physicsPrefab.GetComponentInChildren<MeshFilter>() == null)
        {
            foreach (MeshRenderer child in childRenderers)
            {
                child.gameObject.AddComponent<MeshCollider>();
            }
        }
        else
        {
            GameObject collider = new GameObject();
            collider.transform.SetParent(gameObject.transform);
            collider.name = "Collision";
            MeshFilter collisionMesh = physicsPrefab.GetComponentInChildren<MeshFilter>();
            MeshCollider parentCollider = collider.AddComponent<MeshCollider>();
            parentCollider.sharedMesh = collisionMesh.sharedMesh;
        }
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        string path;

        for(int i = 0; i < importedAssets.Length; i++)
        {
            path = importedAssets[i];

            //M2 Utility Queue
            if (ValidAsset(path))
            {
                M2Utility.QueueMetadata(path);
            }

            //ADT/WMO Item Collection Queue
            if (Path.GetFileName(path).Contains("_ModelPlacementInformation.csv"))
            {
                ItemCollectionUtility.QueuePlacementData(path);
            }

            //ADT Liquid Volume Queue
            if (Regex.IsMatch(path, @"liquid_\d{2}_\d{2}(?=\.json)"))
            {
                LiquidUtility.QueueLiquidData(path);
            }
        }

        EditorApplication.update += AssetConversionManager.ProcessAssets;
    }
}
