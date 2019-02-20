using System.IO;
using System.Linq;
using Brisk.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Brisk.Assets
{
    public static class Baseline
    {
#if UNITY_EDITOR
        [MenuItem("Brisk/Make Map", false, 1)]
        public static void UpdateBaseline()
        {
            var name = SceneManager.GetActiveScene().name;

            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");

            File.WriteAllText($"Assets/Resources/{name}.json", GetJson());
            AssetDatabase.Refresh();
        }

        private static string GetJson()
        {
            return JsonUtility.ToJson(new EntityArray{entities = GetEntities()});
        }

        private static Entity[] GetEntities()
        {
            return Object.FindObjectsOfType<GameObject>()
                .Select(PrefabUtility.GetOutermostPrefabInstanceRoot)
                .Distinct()
                .Where(g => g != null)
                .Select(g =>
                {
                    var prefab = PrefabUtility.GetCorrespondingObjectFromSource(g);
                    return new Entity
                    {
                        name = prefab.name,
                        x = g.transform.position.x,
                        y = g.transform.position.y,
                        z = g.transform.position.z,
                        rx = g.transform.rotation.x,
                        ry = g.transform.rotation.y,
                        rz = g.transform.rotation.z,
                        rw = g.transform.rotation.w,
                    };
                })
                .ToArray();
        }
#endif

        internal static void Load(Peer peer, AssetManager assetManager, TextAsset level)
        {
            var entities = JsonUtility.FromJson<EntityArray>(level.text).entities;

            foreach (var entity in entities)
            {
                var e = NetEntity.Create(peer, assetManager, assetManager[entity.name]);
                if (e == null) continue;
                
                e.transform.position = new Vector3(entity.x, entity.y, entity.z);
                e.transform.rotation = new Quaternion(entity.rx, entity.ry, entity.rz, entity.rw);
            }
        }
    }
}