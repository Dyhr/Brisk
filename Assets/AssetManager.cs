using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Network.Assets
{
    internal class AssetManager
    {
        public readonly StringDictionary strings = new StringDictionary();
        
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

        public IEnumerator SendAssetBundle(
            RuntimePlatform platform, int maxPacketSize, float interval, Action<int, int, byte[]> onData)
        {
            var bundle = assetsData;
            if (onData == null) yield break;

            var data = new byte[maxPacketSize];

            for (var i = 0; i < bundle.Length; i += data.Length)
            {
                yield return new WaitForSeconds(interval);

                var size = Mathf.Min(data.Length, bundle.Length - i);
                
                for (var j = 0; j < size; j++)
                {
                    data[j] = bundle[i + j];
                }

                onData.Invoke(i, size, data);
            }
        }

        public void InitializeDataGet(int length)
        {
            assetsData = new byte[length];
        }

        public bool DataGet(int start, int length, byte[] data)
        {
            for (var i = 0; i < length; i++)
                assetsData[i + start] = data[i];
            assetsProgress += length;

            if (assetsProgress != assetsData.Length) return false;
            
            assets = AssetBundle.LoadFromMemory(assetsData);
            return true;

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
        }

        public GameObject Get(int id)
        {
            return assets.LoadAsset<GameObject>(strings[id]);
        }

        #endregion
    }
}