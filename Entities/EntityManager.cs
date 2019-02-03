using Network.Assets;
using UnityEngine;

namespace Network.Entities
{
    internal class EntityManager
    {
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
            
            return entity;
        }
    }
}