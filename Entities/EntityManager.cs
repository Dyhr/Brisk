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
        
        public NetEntity CreateEntity(AssetManager assets, int assetId, int entityId)
        {
            var gameObject = Object.Instantiate(assets[assetId]);
            if (gameObject == null)
            {
                Debug.LogError("Asset not found for id: "+assetId);
                return null;
            }

            var entity = gameObject.GetComponent<NetEntity>();

            if (entity == null)
            {
                Debug.LogError("Cannot create asset without an Entity for id: "+assetId);
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