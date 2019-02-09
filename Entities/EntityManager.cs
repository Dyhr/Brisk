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
        private readonly List<NetEntity> ownedEntities = new List<NetEntity>();

        private int nextEntityId;
        

        public NetEntity CreateEntity(AssetManager assetManager, int assetId)
        {
            return CreateEntity(assetManager, assetId, ++nextEntityId, true);
        }
        public NetEntity CreateEntity(AssetManager assets, int assetId, int entityId, bool mine)
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
            entity.Owner = mine;
            
            if (entity.Owner) ownedEntities.Add(entity);
            entities.Add(entityId, entity);
            return entity;
        }

        private void ClearDead()
        {
            foreach (var entity in deadEntities)
            {
                ownedEntities.Remove(entities[entity]);
                entities.Remove(entity);
            }
            deadEntities.Clear();
        }

        public NetEntity this[int id] => entities.TryGetValue(id, out var e) ? e : null;
        
        public IEnumerable<NetEntity> AllEntities()
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

            ClearDead();
        }
        
        public IEnumerator<NetEntity> GetEnumerator()
        {
            foreach (var entity in ownedEntities)
            {
                if (entity == null)
                {
                    deadEntities.Add(entity.Id);
                    continue;
                }
                yield return entity;
            }

            ClearDead();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}