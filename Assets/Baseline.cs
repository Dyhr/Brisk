using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Brisk.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Brisk.Assets
{
    public static class Baseline
    {
#if UNITY_EDITOR
        private static readonly string[] PropertyCompareBlackList =
        {
            "transform", "gameObject", "renderer", "collider", "collider2D", "rigidbody", "rigidbody2D", "light",
            "camera", "name", "hideFlags", "particleSystem", "networkView", "guiElement", "guiTexture", "root",
            "parent", "childCount", "hasChanged", "up", "forward", "right", "position", "rotation", "lossyScale",
            "eulerAngles", "worldToLocalMatrix", "localToWorldMatrix", "localEulerAngles", "hierarchyCapacity",
            "hierarchyCount", "audio", "guiText", "hingeJoint", "animation", "constantForce", "mesh", "material",
            "materials", "sharedMaterial", "localPosition", "localRotation", "localScale", "tag", "bounds"
        };
        private static readonly string[] ChangedPropertyBlackList =
        {
            "m_Name", "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
            "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", 
            "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z", "m_RootOrder",
            "m_LocalEulerAnglesHint.x", "m_LocalEulerAnglesHint.y", "m_LocalEulerAnglesHint.z",
        };
        
        [MenuItem("Brisk/Make Map", false, 1)]
        public static void UpdateBaseline()
        {
            // Get the data
            var gameObjects = Object.FindObjectsOfType<GameObject>();
            var prefabs = GetActualPrefabs(GetPrefabs(gameObjects)).ToArray();
            var nonPrefabs = GetNonPrefabs(gameObjects).ToArray();
            var entities = GetEntities(prefabs.Where(g => g.Item2 != null && g.Item2.GetComponent<NetEntity>() != null)).ToArray();
            
            // Save the data
            if (!Directory.Exists("Assets/Resources"))Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText($"Assets/Resources/{SceneManager.GetActiveScene().name}.json", GetJson(entities));
            AssetDatabase.Refresh();
            
            // Inform about objects that didn't look right
            var brokenPrefabs = prefabs.Where(g => g.Item2 == null).ToArray();
            if(brokenPrefabs.Length > 0)
                Debug.Log($"{brokenPrefabs.Length} game objects have missing prefabs{brokenPrefabs.Aggregate("", (s, g) => $"{s}\n - {g.Item1.name}")}");
            
            if(nonPrefabs.Length > 0)
                Debug.Log($"{nonPrefabs.Length} game objects are not prefabs{nonPrefabs.Aggregate("", (s, g) => $"{s}\n - {g.name}")}");
            
            var missingEntityPrefabs = prefabs.Select(g => g.Item2).Distinct().Where(g => g != null && g.GetComponent<NetEntity>() == null).ToArray();
            if(missingEntityPrefabs.Length > 0)
                Debug.Log($"{missingEntityPrefabs.Length} prefabs do not have a NetEntity component{missingEntityPrefabs.Aggregate("", (s, g) => $"{s}\n - {g.name}")}");
            
            var changedPrefabs = prefabs.Where(g => g.Item2 != null && Changed(g.Item1)).ToArray();
            if(changedPrefabs.Length > 0)
                Debug.Log($"{changedPrefabs.Length} game object has changes that are not applied{changedPrefabs.Aggregate("", (s, g) => $"{s}\n - {g.Item1.name}")}");
            
            var nonBundledPrefabs = prefabs.Select(g => g.Item2).Distinct().Where(HasBundle).ToArray();
            if(nonBundledPrefabs.Length > 0)
                Debug.Log($"{nonBundledPrefabs.Length} prefabs are not in any asset bundle{nonBundledPrefabs.Aggregate("", (s, g) => $"{s}\n - {g.name}")}");
        }

        private static bool HasBundle(GameObject gameObject)
        {
            return AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(gameObject))?.assetBundleName == "";
        }

        private static bool Changed(GameObject instance)
        {
            if (instance == null) return false;
            var mods = PrefabUtility.GetPropertyModifications(instance);
            var modifications = mods != null 
                ? mods.Where(m => m?.propertyPath != null && !ChangedPropertyBlackList.Contains(m.propertyPath))
                : new PropertyModification[0];
            return PrefabUtility.GetAddedComponents(instance).Count > 0 ||
                   PrefabUtility.GetAddedGameObjects(instance).Count > 0 ||
                   PrefabUtility.GetRemovedComponents(instance).Count > 0 ||
                   PrefabUtility.GetObjectOverrides(instance).Count > 0 ||
                   modifications.Any();
        }

        private static string GetJson(Entity[] entities)
        {
            return JsonUtility.ToJson(new EntityArray{entities = entities});
        }

        private static IEnumerable<GameObject> GetPrefabs(IEnumerable<GameObject> gameObjects)
        {
            return gameObjects
                .Select(PrefabUtility.GetOutermostPrefabInstanceRoot)
                .Where(g => g != null)
                .Where(g => !PrefabUtility.IsPartOfModelPrefab(g))
                .Distinct();
        }
        
        private static IEnumerable<GameObject> GetNonPrefabsRoot(IEnumerable<GameObject> gameObjects)
        {
            return gameObjects
                .Where(g => PrefabUtility.GetOutermostPrefabInstanceRoot(g) == null || 
                            PrefabUtility.IsPartOfModelPrefab(g))
                .Select(g => g.transform.root != null ? g.transform.root.gameObject : g)
                .Distinct()
                .Where(g => g != null);
        }
        private static IEnumerable<GameObject> GetNonPrefabs(IEnumerable<GameObject> gameObjects)
        {
            return gameObjects
                .Where(g => PrefabUtility.GetOutermostPrefabInstanceRoot(g) == null || 
                            PrefabUtility.IsPartOfModelPrefab(g))
                .Distinct()
                .Where(g => g != null);
        }

        private static IEnumerable<Tuple<GameObject, GameObject>> GetActualPrefabs(IEnumerable<GameObject> gameObjects)
        {
            return gameObjects.Select(g => Tuple.Create(g, PrefabUtility.GetCorrespondingObjectFromSource(g)));
        }
        private static IEnumerable<Entity> GetEntities(IEnumerable<Tuple<GameObject, GameObject>> gameObjects)
        {
            return gameObjects
                .Where(g => g.Item2 != null)
                .Select(g =>
                {
                    var (gameObject, prefab) = g;
                    var position = gameObject.transform.position;
                    var eulerAngles = gameObject.transform.eulerAngles;
                    var localScale = gameObject.transform.localScale;
                    return new Entity
                    {
                        name = prefab.name,
                        px = position.x,
                        py = position.y,
                        pz = position.z,
                        rx = eulerAngles.x,
                        ry = eulerAngles.y,
                        rz = eulerAngles.z,
                        sx = localScale.x,
                        sy = localScale.y,
                        sz = localScale.z,
                    };
                });
        }
        [MenuItem("Brisk/Create Prefab", false, 102)]
        public static void CreatePrefab()
        {
            foreach (var gameObject in Selection.gameObjects) 
            {
                CreatePrefab(gameObject);
                if(gameObject.GetComponent<NetEntity>() == null)
                    AddNetEntity(gameObject);
            }
        }

        private static void CreatePrefab(GameObject gameObject)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (root != null)
            {
                Debug.LogError("Game object is already a prefab");
                return;
            }

            var archetype = CreateArchetype(gameObject);
            var others = GetNonPrefabs(Object.FindObjectsOfType<GameObject>())
                .Where(nonPrefab => nonPrefab != gameObject && CreateArchetype(nonPrefab) == archetype)
                .ToList();
            
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                gameObject, 
                Directory.Exists("Assets/Prefabs") ? $"Assets/Prefabs/{gameObject.name}.prefab" : $"Assets/{gameObject.name}.prefab", 
                InteractionMode.UserAction, 
                out var success);
            if (success)
            {
                foreach (var other in others)
                {
                    var otherInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    otherInstance.transform.parent = other.transform.parent;
                    otherInstance.transform.localPosition = other.transform.localPosition;
                    otherInstance.transform.localRotation = other.transform.localRotation;
                    otherInstance.transform.localScale = other.transform.localScale;
                    otherInstance.transform.SetSiblingIndex(other.transform.GetSiblingIndex());
                    otherInstance.name = other.name;
                    Object.DestroyImmediate(other);
                }
            }
        }

        private static Archetype CreateArchetype(GameObject gameObject)
        {
            var children = new List<Archetype>();
            foreach (Transform child in gameObject.transform)
                children.Add(CreateArchetype(child.gameObject));
            return new Archetype(
                gameObject.GetComponents<Component>().Select(c => c.GetType()).ToArray(),
                gameObject.GetComponents<Component>(),
                children.ToArray());
        }

        [MenuItem("Brisk/Add NetEntity", false, 104)]
        public static void AddNetEntity()
        {
            foreach (var gameObject in Selection.gameObjects)
                AddNetEntity(gameObject);
        }

        private static void AddNetEntity(GameObject gameObject)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (root == null)
            {
                Debug.LogError("Game object is not a prefab");
                return;
            }

            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab == null)
            {
                Debug.LogError("Prefab instance is broken");
                return;
            }

            prefab.AddComponent<NetEntity>();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private struct Archetype
        {
            private readonly Type[] componentTypes;
            private readonly Component[] components;
            private readonly Archetype[] children;

            public Archetype(Type[] componentTypes, Component[] components, Archetype[] children)
            {
                this.componentTypes = componentTypes;
                this.components = components;
                this.children = children;
            }
            
            public bool Equals(Archetype other)
            {
                if (componentTypes.Length != other.componentTypes.Length) return false;
                if (components.Length != other.components.Length) return false;
                if (children.Length != other.children.Length) return false;
                
                for (var i = 0; i < componentTypes.Length; i++)
                    if (componentTypes[i] != other.componentTypes[i]) return false;
                for (var i = 0; i < children.Length; i++)
                    if (children[i] != other.children[i]) return false;
                for (var i = 0; i < components.Length; i++)
                {
                    var type = components[i].GetType();
                    if (type.Namespace != "UnityEngine") continue;
                    foreach (var field in type.GetProperties())
                    {
                        if (PropertyCompareBlackList.Contains(field.Name)) continue;
                        if (!CompareProperties(field, components[i], other.components[i])) return false;
                    }
                }
                return true;
            }

            private bool CompareProperties(PropertyInfo property, object a, object b)
            {
                var valueA = property.GetMethod.Invoke(a, new object[0]);
                var valueB = property.GetMethod.Invoke(b, new object[0]);
                if (valueA == null && valueB == null) return true;
                if (valueA == null) return false;

                if (property.PropertyType.IsArray)
                {
                    var arrayA = (Array) valueA;
                    var arrayB = (Array) valueB;
                    if (arrayA.Length != arrayB.Length) return false;
                    for (var i = 0; i < arrayA.Length; i++)
                    {
                        valueA = arrayA.GetValue(i);
                        valueB = arrayB.GetValue(i);
                        if (valueA == null && valueB == null) continue;
                        if (valueA == null) return false;
                        if (!valueA.Equals(valueB)) return false;
                    }
                }
                else
                {
                    if (!valueA.Equals(valueB)) return false;
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is Archetype other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (componentTypes.GetHashCode() * 397) ^ children.GetHashCode();
                }
            }

            public static bool operator ==(Archetype left, Archetype right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Archetype left, Archetype right)
            {
                return !left.Equals(right);
            }
        }
#endif

        internal static void Load(Peer peer, AssetManager assetManager, TextAsset level)
        {
            var entities = JsonUtility.FromJson<EntityArray>(level.text).entities;

            foreach (var entity in entities)
            {
                var e = NetEntity.Create(peer, assetManager, assetManager[entity.name]);
                if (e == null) continue;
                
                e.transform.position = new Vector3(entity.px, entity.py, entity.pz);
                e.transform.eulerAngles = new Vector3(entity.rx, entity.ry, entity.rz);
                e.transform.position = new Vector3(entity.sx, entity.sy, entity.sz);
            }
        }
    }
}