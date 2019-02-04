using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Brisk.Assets
{
    internal class AssetManager
    {
        public bool Ready => assets != null && stringsLength == stringsProgress;
        public int StringsLength => strings.Length;

        private readonly StringDictionary strings = new StringDictionary();
        private int stringsLength;
        private int stringsProgress;
        private AssetBundle assets;
        private byte[] assetsData;
        private int assetsProgress;

        #region Platforms

        public bool Available(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return assetsData != null;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return assetsData != null;
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return assetsData != null;
                default:
                    return false;
            }
        }
        
        private byte[] BundleData(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return assetsData;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return assetsData;
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return assetsData;
                default:
                    return null;
            }
        }
        
        public int Size(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return assetsData.Length;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return assetsData.Length;
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return assetsData.Length;
                default:
                    Debug.LogErrorFormat("Platform not supported: {0}", platform);
                    return 0;
            }
        }

        #endregion
        
        #region Transmission

        public IEnumerator SendStrings(float interval, Action<int, string> onData)
        {
            for (var i = 0; i < strings.Length; i ++)
            {
                yield return new WaitForSeconds(interval);
                onData.Invoke(i+1, strings[i+1]);
            }
        }
        
        public IEnumerator SendAssetBundle(
            RuntimePlatform platform, int maxPacketSize, float interval, Action<int, int, byte[]> onData)
        {
            var bundle = BundleData(platform);
            if (onData == null) yield break;

            var data = new byte[maxPacketSize];

            for (var i = 0; i < bundle.Length; i += data.Length)
            {
                yield return new WaitForSeconds(interval);
                var size = Mathf.Min(data.Length, bundle.Length - i);
                for (var j = 0; j < size; j++) data[j] = bundle[i + j];
                onData.Invoke(i, size, data);
            }
        }

        public void InitializeStringGet(int length)
        {
            stringsLength = length;
        }

        public void StringGet(int i, string s)
        {
            strings.Register(i, s);
            stringsProgress++;
        }

        public void InitializeDataGet(int length)
        {
            assetsData = new byte[length];
        }

        public void DataGet(int start, int length, byte[] data)
        {
            for (var i = 0; i < length; i++)
                assetsData[i + start] = data[i];
            assetsProgress += length;

            if (assetsProgress != assetsData.Length) return;
            assets = AssetBundle.LoadFromMemory(assetsData);

        }

        public void LoadFromFile(string path)
        {
            assetsData = File.ReadAllBytes(path);
            assetsProgress = assetsData.Length;
            assets = AssetBundle.LoadFromMemory(assetsData);
            InitializeStringDictionary();
        }

        #endregion

        #region Assets

        private void InitializeStringDictionary()
        {
            // TODO check that the assets are the same for all platforms
            foreach (var asset in assets.LoadAllAssets())
                strings.Register(asset.name);
            stringsProgress = stringsLength = strings.Length;
        }
        
        public GameObject this[int id] => id != 0 ? assets.LoadAsset<GameObject>(strings[id]) : null;
        public int this[string asset] => strings[asset];

        #endregion
    }
}