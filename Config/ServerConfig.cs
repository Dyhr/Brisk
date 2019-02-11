using Brisk.Serialization;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Brisk.Config
{
    public class ServerConfig : ScriptableObject
    {
        [SerializeField] private int port = 3553;
        public int Port => port;
        [SerializeField] private string appName = "Space Station";
        public string AppName => appName;
        [SerializeField] private Serializer serializer = null;
        public Serializer Serializer
        {
            get => serializer;
            internal set => serializer = value;
        }
        [SerializeField] private float updateRate = 30;
        public float UpdateRate => updateRate;
        [SerializeField] private float statusReportTime = 2;
        public float StatusReportTime => statusReportTime;

#if UNITY_EDITOR
        [MenuItem("Assets/Create/Network/Server Config")]
        public static void Create()
        {
            var asset = CreateInstance<ServerConfig>();
            AssetDatabase.CreateAsset(asset, "Assets/Resources/ServerConfig.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
#endif
    }
}
