#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Brisk.Config;
using Brisk.Entities;
using UnityEditor;
using UnityEngine;

namespace Brisk.Serialization
{
    public static class SerializerGenerator
    {
        [MenuItem("Brisk/Generate Serializers", false, 5)]
        public static void GenerateClasses()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
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
                var indent = "                ";
                var castLine = $"{indent}var obj = ({serializer.Key.FullName})bhr;";
                var reliableReadLines = new StringBuilder();
                var unreliableReadLines = new StringBuilder();
                var reliableWriteLines = new StringBuilder();
                var unreliableWriteLines = new StringBuilder();
                
                reliableReadLines.AppendLine(castLine);
                unreliableReadLines.AppendLine(castLine);
                reliableWriteLines.AppendLine(castLine);
                unreliableWriteLines.AppendLine(castLine);
                
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
                    switch (type.FullName)
                    {
                        case "System.Byte":
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name});");
                            wLines.AppendLine($"{indent}obj.{info.Name} = msg.ReadByte();");
                            break;
                        case "System.Int16":
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name});");
                            wLines.AppendLine($"{indent}obj.{info.Name} = msg.ReadInt16();");
                            break;
                        case "System.Int32":
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name});");
                            wLines.AppendLine($"{indent}obj.{info.Name} = msg.ReadInt32();");
                            break;
                        case "System.Int64":
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name});");
                            wLines.AppendLine($"{indent}obj.{info.Name} = msg.ReadInt64();");
                            break;
                        case "System.Boolean":
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name});");
                            wLines.AppendLine($"{indent}obj.{info.Name} = msg.ReadBoolean();");
                            break;
                        case "UnityEngine.Vector3":
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name}.x);");
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name}.y);");
                            rLines.AppendLine($"{indent}msg.Write(obj.{info.Name}.z);");
                            wLines.AppendLine($"{indent}obj.{info.Name} = new UnityEngine.Vector3(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());");
                            break;
                        default:
                            Debug.LogError($"Type not supported for serialization: {type.FullName}");
                            break;
                    }
                }

                if(reliableReadLines.Length > castLine.Length+2) 
                    reliableSerializers.Append($"            {{typeof(Brisk.Entities.NetEntity), (bhr, msg) => {{\r\n{reliableReadLines}{indent}}}\r\n            }}");
                if(unreliableReadLines.Length > castLine.Length+2) 
                    unreliableSerializers.Append($"            {{typeof(Brisk.Entities.NetEntity), (bhr, msg) => {{\r\n{unreliableReadLines}{indent}}}\r\n            }}");
                if(reliableWriteLines.Length > castLine.Length+2) 
                    reliableDeserializers.Append($"            {{typeof(Brisk.Entities.NetEntity), (bhr, msg) => {{\r\n{reliableWriteLines}{indent}}}\r\n            }}");
                if(unreliableWriteLines.Length > castLine.Length+2) 
                    unreliableDeserializers.Append($"            {{typeof(Brisk.Entities.NetEntity), (bhr, msg) => {{\r\n{unreliableWriteLines}{indent}}}\r\n            }}");
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

        [MenuItem("Brisk/Save Serializers", false, 6)]
        public static void SaveSerialization()
        {
            var scriptableObject = (Serializer)ScriptableObject.CreateInstance("AutoGenerated_BriskSerialization");
            if (scriptableObject == null) return;
            AssetDatabase.CreateAsset(scriptableObject, "Assets/Resources/AutoGenerated_BriskSerialization.asset");
            foreach (var config in Resources.FindObjectsOfTypeAll<ServerConfig>())
                config.Serializer = scriptableObject;
        }

        private static void AddSerialization(StringBuilder result, bool reliable, bool write, string serializers)
        {
            var dictName = (write ? "deserializers" : "serializers") + (reliable ? "Reliable" : "Unreliable");
            var methodName = (write ? "Deserialize" : "Serialize") + (reliable ? "Reliable" : "Unreliable");
            var messageName = write ? "NetIncomingMessage" : "NetOutgoingMessage";
            
            result.AppendLine($"        private static readonly System.Collections.Generic.Dictionary<System.Type, System.Action<Brisk.Entities.NetBehaviour,Lidgren.Network.{messageName}>> {dictName} = ");
            result.AppendLine($"          new System.Collections.Generic.Dictionary<System.Type, System.Action<Brisk.Entities.NetBehaviour,Lidgren.Network.{messageName}>> {{");
            result.AppendLine(serializers);
            result.AppendLine( "        };");
            
            result.AppendLine($"        public override void {methodName}<T>(T obj, Lidgren.Network.{messageName} msg) {{");
            result.AppendLine($"            {dictName}[typeof(T)](obj, msg);");
            result.AppendLine( "        }");
        }
        
        private static void AddMember(
            Dictionary<Type, List<Tuple<MemberInfo, bool>>> dict, Type type, MemberInfo member, bool reliable)
        {
            if (!dict.ContainsKey(type))
                dict.Add(type, new List<Tuple<MemberInfo, bool>>());
                    
            dict[type].Add(Tuple.Create(member, reliable));
        }
    }
}
#endif