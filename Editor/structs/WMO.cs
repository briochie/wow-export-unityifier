using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WowUnity
{
    [Serializable]
    public class WMO
    {
        public uint fileDataID;
        public string fileName;
        public uint version;
        public byte[] ambientColor;
        public uint areaTableID;
        public BitArray flags;
        public List<Group> groups;
        public List<string> groupNames;
        public List<M2.Texture> textures;

        [Serializable]
        public class Group
        {
            public string groupName;
            public bool enabled;
            public uint version;
            public uint groupID;
            public List<RenderBatch> renderBatches;
            public List<byte[]> vertexColors;
        }

        [Serializable]
        public class RenderBatch
        {
            public ushort firstVertex;
            public ushort lastVertex;
            public BitArray flags;
            public uint materialID;
        }
    }

    
}
