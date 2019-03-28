#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Brisk.Entities;
using UnityEditor;
using UnityEngine;

namespace Brisk.Serialization
{
    public static class SerializerGenerator
    {
        private static string[] assemblyExceptions = new[]
        {
            "Unity", "Microsoft", "Lidgren", "System", "JetBrains", "mscorlib", "Mono", "nunit",
            "Cinemachine", "com.unity", "YamlDotNet", "ExCSS.Unity"
        };
        
        [MenuItem("Brisk/Generate Serializers", false, 6)]
        public static void GenerateClasses()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => assemblyExceptions.Any(e => a.FullName.StartsWith(e)) ? new Type[0] : a.GetTypes())
                .Where(t => t.IsClass && t.IsSubclassOf(typeof(NetBehaviour)));


            var serializers = new Dictionary<Type, List<Tuple<MemberInfo, bool>>>();
            foreach (var type in types)
            {
                foreach (var property in type.GetProperties())
                {
                    var reliable = Attribute.GetCustomAttribute(property, typeof(SyncReliable)) != null;
                    var unreliable = Attribute.GetCustomAttribute(property, typeof(SyncUnreliable)) != null;

                    if (reliable)
                        AddMember(serializers, type, property, true);
                    if (unreliable)
                        AddMember(serializers, type, property, false);
                }
                foreach (var field in type.GetFields())
                {
                    var reliable = Attribute.GetCustomAttribute(field, typeof(SyncReliable)) != null;
                    var unreliable = Attribute.GetCustomAttribute(field, typeof(SyncUnreliable)) != null;

                    if (reliable)
                        AddMember(serializers, type, field, true);
                    if (unreliable)
                        AddMember(serializers, type, field, false);
                }
            }
            
            var reliableSerializers = new StringBuilder();
            var unreliableSerializers = new StringBuilder();
            var reliableDeserializers = new StringBuilder();
            var unreliableDeserializers = new StringBuilder();
            
            foreach (var serializer in serializers)
            {
                var name = serializer.Key.Name.ToLower();
                const string indent = "                ";
                var reliableReadLines = new StringBuilder();
                var unreliableReadLines = new StringBuilder();
                var reliableWriteLines = new StringBuilder();
                var unreliableWriteLines = new StringBuilder();
                
                foreach (var (info, reliable) in serializer.Value)
                {
                    var field = info as FieldInfo;
                    var property = info as PropertyInfo;
                    Type type;

                    if (field != null)
                    {
                        type = field.FieldType;
                    }
                    else if (property != null)
                    {
                        if (!property.CanRead || !property.CanWrite)
                        {
                            Debug.LogError(
                                $"Field {info.Name} on {serializer.Key} does not have both getter and setter");
                            continue;
                        }

                        type = property.PropertyType;
                    }
                    else
                    {
                        Debug.LogError($"Member type not supported: {info.MemberType}");
                        continue;
                    }

                    var rLines = reliable ? reliableReadLines : unreliableReadLines;
                    var wLines = reliable ? reliableWriteLines : unreliableWriteLines;
                    wLines.AppendLine($"{indent}{name}.{info.Name} = {ReadMessageData(type.FullName)};");
                    switch (type.FullName)
                    {
                        case "System.Byte":
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name});");
                            break;
                        case "System.Int16":
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name});");
                            break;
                        case "System.Int32":
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name});");
                            break;
                        case "System.Int64":
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name});");
                            break;
                        case "System.Boolean":
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name});");
                            break;
                        case "UnityEngine.Vector3":
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name}.x);");
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name}.y);");
                            rLines.AppendLine($"{indent}msg.Write({name}.{info.Name}.z);");
                            break;
                        default:
                            Debug.LogError($"Type not supported for serialization: {type.FullName}");
                            break;
                    }
                }

                if(reliableReadLines.Length > 0) 
                    reliableSerializers.Append($"            case {serializer.Key.FullName} {name}:\r\n{reliableReadLines}{indent}break;");
                if(unreliableReadLines.Length > 0) 
                    unreliableSerializers.Append($"            case {serializer.Key.FullName} {name}:\r\n{unreliableReadLines}{indent}break;");
                if(reliableWriteLines.Length > 0) 
                    reliableDeserializers.Append($"            case {serializer.Key.FullName} {name}:\r\n{reliableWriteLines}{indent}break;");
                if(unreliableWriteLines.Length > 0) 
                    unreliableDeserializers.Append($"            case {serializer.Key.FullName} {name}:\r\n{unreliableWriteLines}{indent}break;");
            }

            var result = new StringBuilder();
            result.AppendLine("// #### AUTO-GENERATED CODE ####");
            result.AppendLine("// Please avoid editing");
            result.AppendLine("// Copyright © Brisk Technologies");
            result.AppendLine("");
            result.AppendLine("namespace Brisk.Serialization {");
            result.AppendLine("    public sealed class AutoGenerated_BriskSerialization : Brisk.Serialization.Serializer {");
            
            AddSerialization(result, true, false, reliableSerializers.ToString());
            AddSerialization(result, true, true, reliableDeserializers.ToString());
            AddSerialization(result, false, false, unreliableSerializers.ToString());
            AddSerialization(result, false, true, unreliableDeserializers.ToString());
            
            result.AppendLine("    }");
            result.AppendLine("}");
            
            File.WriteAllText("Assets/Resources/AutoGenerated_BriskSerialization.cs", result.ToString());
            AssetDatabase.Refresh();
        }

        [MenuItem("Brisk/Save Serializers", false, 7)]
        public static void SaveSerialization()
        {
            var scriptableObject = (Serializer)ScriptableObject.CreateInstance("AutoGenerated_BriskSerialization");
            if (scriptableObject == null) return;
            AssetDatabase.CreateAsset(scriptableObject, "Assets/Resources/AutoGenerated_BriskSerialization.asset");
            foreach (var config in Resources.FindObjectsOfTypeAll<Config>())
                config.Serializer = scriptableObject;
        }

        private static void AddSerialization(StringBuilder result, bool reliable, bool write, string serializers)
        {
            var methodName = (write ? "Deserialize" : "Serialize") + (reliable ? "Reliable" : "Unreliable");
            var messageName = write ? "NetIncomingMessage" : "NetOutgoingMessage";
            
            result.AppendLine($"        public override void {methodName}<T>(T obj, Lidgren.Network.{messageName} msg) {{");
            result.AppendLine($"            switch (obj) {{");
            result.AppendLine(serializers);
            result.AppendLine($"            }}");
            result.AppendLine( "        }");
        }
        
        private static void AddMember(
            Dictionary<Type, List<Tuple<MemberInfo, bool>>> dict, Type type, MemberInfo member, bool reliable)
        {
            if (!dict.ContainsKey(type))
                dict.Add(type, new List<Tuple<MemberInfo, bool>>());
                    
            dict[type].Add(Tuple.Create(member, reliable));
        }

        internal static string ReadMessageData(string type)
        {
            switch (type)
            {
                case "System.Byte":
                    return "msg.ReadByte()";
                case "System.Int16":
                    return "msg.ReadInt16()";
                case "System.Int32":
                    return "msg.ReadInt32()";
                case "System.Int64":
                    return "msg.ReadInt64()";
                case "System.Boolean":
                    return "msg.ReadBoolean()";
                case "UnityEngine.Vector3":
                    return "new UnityEngine.Vector3(msg.ReadSingle(), msg.ReadSingle(), msg.ReadSingle())";
                default:
                    Debug.LogError($"Type not supported for serialization: {type}");
                    return "(object)null";
            }
        }
    }
}
#endif
