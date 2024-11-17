using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WowUnity
{
    class WMOUtility
    {
        public static bool AssignVertexColors(WMO.Group group, List<GameObject> gameObjects)
        {
            if (gameObjects.Count != group.renderBatches.Count)
            {
                Debug.LogError("Attempted to assign vertex colors to WMO, but group size did not match object stack!");
                return false;
            }

            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                WMO.RenderBatch renderBatch = group.renderBatches[i];
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.sharedMesh;
                
                if (mesh == null)
                {
                    Debug.LogError("Attempted to assign vertex colors to WMO, but mesh was missing.");
                    return false;
                }

                mesh.colors = GetVertexColorsInRange(group, renderBatch.firstVertex, renderBatch.lastVertex);
            }

            return true;
        }

        static Color[] GetVertexColorsInRange(WMO.Group group, int start, int end)
        {
            List<byte[]> vertexColors = group.vertexColors.GetRange(start, end - start);
            List<Color> parsedColors = new List<Color>();

            for (int i = 0; i < vertexColors.Count; i++)
            {
                Color newColor = new Color();
                byte[] colorData = vertexColors[i];
                newColor.a = (float)colorData[0] / 255f;
                newColor.r = (float)colorData[1] / 255f;
                newColor.b = (float)colorData[2] / 255f;
                newColor.g = (float)colorData[3] / 255f;
            }

            return parsedColors.ToArray();
        }
    }
}
