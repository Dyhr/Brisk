using System.IO;
using Lidgren.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Network.Entities
{
    public sealed class NetEntity : MonoBehaviour
    {
        [SerializeField] private SerializedEntity entity = null;
        [SerializeField] private Client client = null;
        [SerializeField] private UpdateType updateType = UpdateType.None;
        [SerializeField] private bool moving = true;
        [SerializeField] private bool local = false;
        [SerializeField] private int id = 0;

        public bool Local => local;
        public int Id => id;
        
        private NetBehaviour[] behaviours;
        
        
        private void Start()
        {
            if (client == null) client = FindObjectOfType<Client>();
            behaviours = GetComponents<NetBehaviour>();

            if(client == null)
                Debug.LogError("No Client found for NetEntity", this);
            else
                client.Register(this);
        }

        private void OnDestroy()
        {
            if(client != null)
                client.Deregister(this);
        }

        public void Send(NetOutgoingMessage msg)
        {
            msg.Write(id);
            msg.Write(transform.position.x);
            msg.Write(transform.position.y);
            msg.Write(transform.position.z);
        }

        private byte[] Serialize()
        {
            var ms = new MemoryStream();
            using (var writer = new BsonWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, new Entity
                {
                    x = transform.position.x,
                    y = transform.position.y,
                    z = transform.position.z,
                    asset = "Wall_Cross",
                });
                return ms.ToArray();
            }
        }

        public static NetEntity Deserialize(AssetBundle bundle, SerializedEntity entity)
        {
            var gameObject = new GameObject(entity.title, typeof(NetEntity));
            var netEntity = gameObject.GetComponent<NetEntity>();
            netEntity.entity = entity;
            
            var ms = new MemoryStream(entity.data);
            
            using (var reader = new BsonReader(ms))
            {
                var serializer = new JsonSerializer();
                var e = serializer.Deserialize<Entity>(reader);

                netEntity.transform.position = new Vector3(e.x, e.y, e.z);

                if(e.asset != null)
                    Instantiate(bundle.LoadAsset(e.asset), netEntity.transform);
            }

            return netEntity;
        }

#if UNITY_EDITOR
        
        [ContextMenu("Save")]
        private void Save()
        {
            if (entity != null)
            {
                entity.data = Serialize();
                
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = entity;
            }
            else
            {
                // Prepare the asset for saving
            
                var asset = ScriptableObject.CreateInstance<SerializedEntity>();

                asset.title = name;
                asset.data = Serialize();
            
            
                // Actually save the asset
            
                if (!Directory.Exists("Assets/Resources/Entities"))
                    Directory.CreateDirectory("Assets/Resources/Entities");

                int? i = null;
                while (File.Exists($"Assets/Resources/Entities/{(i.HasValue ? name + " " + i : name)}.asset"))
                {
                    if (!i.HasValue)
                        i = 1;
                    else
                        i++;
                }

                AssetDatabase.CreateAsset(
                    asset, $"Assets/Resources/Entities/{(i.HasValue ? name + " " + i : name)}.asset");
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;

                entity = asset;
            }
        }
#endif
    }
}