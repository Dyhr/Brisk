using System.Collections.Generic;

namespace Network.Assets
{
    internal class StringDictionary
    {
        private readonly Dictionary<int, string> intToString = new Dictionary<int, string>();
        private readonly Dictionary<string, int> stringToInt = new Dictionary<string, int>();

        private int nextId;
        
        public int Register(string s)
        {
            if (stringToInt.TryGetValue(s, out var id)) return id;
            id = ++nextId;
            intToString.Add(id, s);
            stringToInt.Add(s, id);
            return id;
        }

        public int this[string s] => stringToInt.TryGetValue(s, out var i) ? i : 0;
        public string this[int i] => intToString.TryGetValue(i, out var s) ? s : "";
    }
}