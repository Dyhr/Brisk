using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Brisk.Serialization
{
    internal class StringDictionary
    {
        private readonly Dictionary<int, string> intToString = new Dictionary<int, string>();
        private readonly Dictionary<string, int> stringToInt = new Dictionary<string, int>();

        private byte[] serialized;

        public int Length { get; private set; }

        public void Register(string s)
        {
            if (stringToInt.ContainsKey(s)) return;
            var id = ++Length;
            intToString.Add(id, s);
            stringToInt.Add(s, id);
        }

        public int this[string s] => stringToInt.TryGetValue(s, out var i) ? i : 0;
        public string this[int i] => intToString.TryGetValue(i, out var s) ? s : "";

        private void Register(int i, string s)
        {
            if (stringToInt.ContainsKey(s))
            {
                Debug.LogError($@"Duplicate ID for string: ""{s}""");
                return;
            }
            intToString.Add(i, s);
            stringToInt.Add(s, i);
            
            Cache();
        }

        private void Cache()
        {
            serialized = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Dict
            {
                items = stringToInt.Select(pair => new Mapping{key = pair.Key, value = pair.Value}).ToArray()
            }));
        }

        public byte[] Serialize()
        {
            if(serialized == null) Cache();
            return serialized;
        }

        public static StringDictionary Deserialize(string json)
        {
            if (json == null) return null;
            var dictionary = new StringDictionary();
            var data = JsonUtility.FromJson<Dict>(json);
            foreach (var item in data.items)
                dictionary.Register(item.value, item.key);
            return dictionary;
        }

        [Serializable]
        private struct Dict
        {
            public Mapping[] items;
        }
        [Serializable]
        private struct Mapping
        {
            public string key;
            public int value;
        }
    }
}