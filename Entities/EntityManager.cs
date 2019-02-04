using System.Collections;
using System.Collections.Generic;
using Brisk.Assets;
using UnityEngine;

namespace Brisk.Entities
{
    internal class EntityManager : IEnumerable<NetEntity>
    {
        private readonly Dictionary<int, NetEntity> entities = new Dictionary<int, NetEntity>();
        private readonly HashSet<int> deadEntities = new HashSet<int>();

        private int nextEntityId;
        

        public NetEntity CreateEntity(AssetManager assetManager, int assetId)
        {
            return CreateEntity(assetManager, assetId, ++nextEntityId);
        }
        public NetEntity CreateEntity(AssetManager assets, int assetId, int entityId)
        {
            var asset = assets[assetId];
            if (asset == null)
            {
                Debug.LogError("Asset not found for id: "+assetId);
                return null;
            }
            
            var gameObject = Object.Instantiate(asset);
            var entity = gameObject.GetComponent<NetEntity>();

            if (entity == null)
            {
                Debug.LogError("Cannot create asset without an Entity for id: "+assetId);
                Object.DestroyImmediate(gameObject);
                return null;
            }

            entity.Id = entityId;
            entity.AssetId = assetId;
            
            entities.Add(entityId, entity);
            return entity;
        }

        public NetEntity this[int id] => entities.TryGetValue(id, out var e) ? e : null;
        
        public IEnumerator<NetEntity> GetEnumerator()
        {
            foreach (var entity in entities)
            {
                if (entity.Value == null)
                {
                    deadEntities.Add(entity.Key);
                    continue;
                }
                yield return entity.Value;
            }

            foreach (var entity in deadEntities)
                entities.Remove(entity);
            deadEntities.Clear();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}