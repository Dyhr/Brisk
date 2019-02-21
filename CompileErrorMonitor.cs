#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Brisk
{
    /// <summary>
    /// Script that recompiles serializers automatically whenever scripts are compiled
    /// 
    /// Base on https://answers.unity.com/questions/482765/detect-compilation-errors-in-editor-script.html
    /// </summary>
    [InitializeOnLoad]
    public class CompileErrorMonitor
    {
        private static float lastCompileTime;
        private static bool isTrackingTime;
        private static double startTime;
        private static bool compilationSucceeded = true;

        static CompileErrorMonitor()
        {
            EditorApplication.update += Update;
            startTime = PlayerPrefs.GetFloat("CompileStartTime", 0);
            if (startTime > 0)
                isTrackingTime = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling && isTrackingTime == false)
            {
                isTrackingTime = true;
                StartRecordingCompileTime();
            }
            else if (EditorApplication.isCompiling == false && isTrackingTime)
            {
                isTrackingTime = false;
                StopRecordingCompileTime();
                LogTimes();
            }
        }

        private static void UnityDebugLog(string message, string stackTrace, LogType logType)
        {
            if (logType == LogType.Error) compilationSucceeded = false;
        }

        private static void StartRecordingCompileTime()
        {
            Application.logMessageReceived += UnityDebugLog;
            startTime = EditorApplication.timeSinceStartup;
            PlayerPrefs.SetFloat("CompileStartTime", (float) startTime);
        }

        private static void StopRecordingCompileTime()
        {
            Application.logMessageReceived -= UnityDebugLog;
            var finishTime = EditorApplication.timeSinceStartup;
            lastCompileTime = (float) (finishTime - startTime);
            PlayerPrefs.DeleteKey("CompileStartTime");
        }

        private static void LogTimes()
        {
            if (lastCompileTime <= 0f)
                return;

            if (compilationSucceeded)
            {
                Debug.Log("Compilation Success. Congratulations, you are awesome!");
                Debug.Log("It took " + lastCompileTime + "s to compile scripts.");
            }
            else
            {
                Debug.LogError("Compilation FAILED. #$%*&§!!!!");
            }
        }
    }
}
#endif