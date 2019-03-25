using System;
using System.Collections;
using System.IO;
using System.Net;
using Brisk.Serialization;
using UnityEngine;
using UnityEngine.Networking;

namespace Brisk.Assets
{
    internal class AssetManager
    {
        public bool Ready => assets != null && strings != null;
        public float AssetLoadingProgress { get; private set; }

        private StringDictionary strings;
        private AssetBundle assets;
        private byte[] assetsData;

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

        #endregion
        
        #region Transmission
        
        public void DownloadAssetBundleHandler(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.OutputStream.Write(assetsData, 0, assetsData.Length);
        }
        public void DownloadStringsHandler(HttpListenerRequest req, HttpListenerResponse res)
        {
            var bytes = strings.Serialize();
            res.OutputStream.Write(bytes, 0, bytes.Length);
        }

        public IEnumerator DownloadAssetBundle(string url, Action<string> callback)
        {
            using (var req = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                req.SendWebRequest();
                while (!req.isDone)
                {
                    AssetLoadingProgress = req.downloadProgress;
                    yield return null;
                }
                AssetLoadingProgress = 1f;

                if (req.isNetworkError || req.isHttpError)
                {
                    callback?.Invoke(req.error);
                }
                else
                {
                    assets = DownloadHandlerAssetBundle.GetContent(req);
                    callback.Invoke(null);
                }
            }
        }

        public IEnumerator DownloadStrings(string url, Action<string> callback)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.SendWebRequest();
                yield return new WaitUntil(() => req.isDone);

                if (req.isNetworkError || req.isHttpError)
                {
                    callback?.Invoke(req.error);
                }
                else
                {
                    strings = StringDictionary.Deserialize(DownloadHandlerBuffer.GetContent(req));
                    callback.Invoke(null);
                }
            }
        }

        public void LoadFromFile(string path)
        {
            assetsData = File.ReadAllBytes(path);
            assets = AssetBundle.LoadFromMemory(assetsData);
            InitializeStringDictionary();
        }

        #endregion

        #region Assets

        private void InitializeStringDictionary()
        {
            strings = new StringDictionary();
            // TODO check that the assets are the same for all platforms
            foreach (var asset in assets.LoadAllAssets())
                strings.Register(asset.name);
        }
        
        public GameObject this[int id] => id != 0 ? assets.LoadAsset<GameObject>(strings[id]) : null;
        public int this[string asset] => strings[asset];

        #endregion
    }
}