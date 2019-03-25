using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using Brisk.Actions;
using Brisk.Serialization;
using UnityEngine;
using YamlDotNet.RepresentationModel;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Brisk
{
    public class Config : ScriptableObject
    {
        [SerializeField] private Data defaults = new Data
        {
            configs = new []{ "config.yaml" },
            port_game = 3553,
            port_web = 3550,
            app_name = "App Name",
            update_rate = 30,
            status_report_time = 2,
        };
        
        [SerializeField] private Serializer serializer = null;
        public Serializer Serializer
        {
            get => serializer;
            internal set => serializer = value;
        }
        [SerializeField] private ActionSet actionSet = null;
        public ActionSet ActionSet
        {
            get => actionSet;
            internal set => actionSet = value;
        }
        
        private readonly IDictionary<string, string> strings = new Dictionary<string, string>();
        private readonly IDictionary<string, int> ints = new Dictionary<string, int>();
        private readonly IDictionary<string, float> floats = new Dictionary<string, float>();
        private readonly IDictionary<string, string[]> stringArrays = new Dictionary<string, string[]>();
        private readonly IDictionary<string, int[]> intArrays = new Dictionary<string, int[]>();
        private readonly IDictionary<string, float[]> floatArrays = new Dictionary<string, float[]>();



        public string GetString(string key)
        {
            return strings.TryGetValue(key, out var s) ? s : "";
        }
        public int GetInt(string key)
        {
            return ints.TryGetValue(key, out var i) ? i : 0;
        }
        public float GetFloat(string key)
        {
            return floats.TryGetValue(key, out var f) ? f : 0f;
        }
        public string[] GetStrings(string key)
        {
            return stringArrays.TryGetValue(key, out var s) ? s : new string[0];
        }
        public int[] GetInts(string key)
        {
            return intArrays.TryGetValue(key, out var i) ? i : new int[0];
        }
        public float[] GetFloats(string key)
        {
            return floatArrays.TryGetValue(key, out var f) ? f : new float[0];
        }

        internal void Load()
        {
            LoadDefaults();
            for (var i = 0; i < GetStrings("configs").Length; i++)
                LoadFromFile(GetStrings("configs")[i]);
            LoadFromArgs();
        }

        private void LoadDefaults()
        {
            foreach (var field in defaults.GetType().GetFields())
            {
                if(field.FieldType == typeof(int))
                    ints.Add(field.Name, (int)field.GetValue(defaults));
                else if(field.FieldType == typeof(float))
                    floats.Add(field.Name, (float)field.GetValue(defaults));
                else if(field.FieldType == typeof(string))
                    strings.Add(field.Name, (string)field.GetValue(defaults));
                else if(field.FieldType == typeof(int[]))
                    intArrays.Add(field.Name, (int[])field.GetValue(defaults));
                else if(field.FieldType == typeof(float[]))
                    floatArrays.Add(field.Name, (float[])field.GetValue(defaults));
                else if(field.FieldType == typeof(string[]))
                    stringArrays.Add(field.Name, (string[])field.GetValue(defaults));
                else
                    Debug.LogWarning($"Unknown config field type: {field.FieldType}");
            }
        }

        private void LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.Log($@"Config file ""{path}"" not found");
                return;
            }

            var file = File.ReadAllText(path);
            var yaml = new YamlStream();
            yaml.Load(new StringReader(file));

            if (yaml.Documents.Count == 0) return;
            
            if (!(yaml.Documents[0].RootNode is YamlMappingNode mapping))
            {
                Debug.LogError($@"Malformed config file ""{path}""");
                return;
            }
            
            ParseNode(mapping);
            
            Debug.Log($@"Loaded config file ""{path}""");
        }

        private void LoadFromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (args[0].Contains("Unity.exe")) return;

            for (var i = 1; i < args.Length; i++)
            {
                var match = Regex.Match(args[i], @"--([\w\d]+)=([\w\d]+)");
                if (!match.Success)
                {
                    Debug.LogWarning($"Malformed command line arg: {args[i]}");
                    continue;
                }

                var varName = match.Groups[1].Value;
                var varValue = match.Groups[2].Value;
                
                if(int.TryParse(varValue, out var r))
                    ints[varName] = r;
                else if(float.TryParse(varValue, out var f))
                    floats[varName] = f;
                else
                    strings[varName] = varValue;
            }
        }

        private void ParseNode(YamlMappingNode mapping, string parent = "")
        {
            foreach (var node in mapping.Children)
            {
                var nodeName = parent == "" ? node.Key.ToString() : $"{parent}:{node.Key}";
                switch (node.Value.NodeType)
                {
                    case YamlNodeType.Mapping:
                        ParseNode((YamlMappingNode) node.Value, nodeName);
                        break;
                    case YamlNodeType.Scalar:
                    {
                        var scalar = (YamlScalarNode) node.Value;
                        
                        if(int.TryParse(scalar.Value, out var i))
                            ints[nodeName] = i;
                        else if(float.TryParse(scalar.Value, out var f))
                            floats[nodeName] = f;
                        else
                            strings[nodeName] = scalar.Value;
                        
                        break;
                    }
                    case YamlNodeType.Sequence:
                    {
                        var sequence = (YamlSequenceNode) node.Value;
                        var offset = 0;
                        for (var index = 0; index < sequence.Children.Count; index++)
                        {
                            if (sequence.Children[index].NodeType != YamlNodeType.Scalar)
                            {
                                Debug.Log($"Malformed yaml node: {mapping}");
                                return;
                            }
                            var scalar = (YamlScalarNode) sequence.Children[index];

                            if (int.TryParse(scalar.Value, out var i))
                            {
                                if(!intArrays.ContainsKey(nodeName)) 
                                    intArrays[nodeName] = new int[sequence.Children.Count];
                                intArrays[nodeName][index] = i;
                            } 
                            else if (float.TryParse(scalar.Value, out var f))
                            {
                                if(!floatArrays.ContainsKey(nodeName)) 
                                    floatArrays[nodeName] = new float[sequence.Children.Count];
                                floatArrays[nodeName][index] = f;
                            }
                            else
                            {
                                if (index == 0 && nodeName == "configs" && stringArrays.ContainsKey(nodeName))
                                {
                                    var oldConfigs = stringArrays[nodeName];
                                    offset = oldConfigs.Length;
                                    stringArrays[nodeName] = new string[oldConfigs.Length+sequence.Children.Count];
                                    oldConfigs.CopyTo(stringArrays[nodeName], 0);
                                }
                                if(!stringArrays.ContainsKey(nodeName)) 
                                    stringArrays[nodeName] = new string[sequence.Children.Count];
                                stringArrays[nodeName][index+offset] = scalar.Value;
                            }
                        }

                        break;
                    }
                    default:
                        Debug.Log($"Malformed yaml node: {mapping}");
                        return;
                }
            }
        }
        
#if UNITY_EDITOR
        [MenuItem("Assets/Create/Network/Server Config")]
        public static void Create()
        {
            var asset = CreateInstance<Config>();
            AssetDatabase.CreateAsset(asset, "Assets/Resources/ServerConfig.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
#endif

        [Serializable]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct Data
        {
            public string[] configs;
            public int port_game;
            public int port_web;
            public string app_name;
            public float update_rate;
            public float status_report_time;
        }
    }
}
