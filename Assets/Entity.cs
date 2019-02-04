using System;

namespace Brisk.Assets
{
    [Serializable]
    public struct Entity
    {
        public string name;
        public float x, y, z;
        public float rx, ry, rz, rw;
    }

    [Serializable]
    public struct EntityArray
    {
        public Entity[] entities;
    }
}