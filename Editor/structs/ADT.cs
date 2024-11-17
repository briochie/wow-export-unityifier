using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WowUnity
{
    public class ADT
    {
        [Serializable]
        public struct Chunk
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
    }
}