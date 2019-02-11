using System.Collections.Generic;
using UnityEngine;

namespace Brisk.Assets
{
    internal class StringDictionary
    {
        private readonly Dictionary<int, string> intToString = new Dictionary<int, string>();
        private readonly Dictionary<string, int> stringToInt = new Dictionary<string, int>();

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

        public void Register(int i, string s)
        {
            if (stringToInt.ContainsKey(s))
            {
                Debug.LogError($@"Duplicate ID for string: ""{s}""");
                return;
            }
            intToString.Add(i, s);
            stringToInt.Add(s, i);
        }
    }
}