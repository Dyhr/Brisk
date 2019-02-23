using System;

namespace Brisk.Assets
{
    [Serializable]
    public struct Entity
    {
        public string name;
        public float px, py, pz;
        public float rx, ry, rz;
        public float sx, sy, sz;
    }

    [Serializable]
    public struct EntityArray
    {
        public Entity[] entities;
    }
}