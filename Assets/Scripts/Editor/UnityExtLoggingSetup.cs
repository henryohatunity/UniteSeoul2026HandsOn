#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;

[InitializeOnLoad]
public class UnityExtLoggingSetup
{
    static UnityExtLoggingSetup()
    {
        // Unity 에디터 시작 시 자동 실행
        // SetupExtendedLogging();
    }

    private static void SetupExtendedLogging()
    {
        // 현재 프로세스에 환경 변수 설정
        Environment.SetEnvironmentVariable("UNITY_EXT_LOGGING", "1");
        
        string value = Environment.GetEnvironmentVariable("UNITY_EXT_LOGGING");
        Debug.Log($"UNITY_EXT_LOGGING is set to: {value}");
        
        if (value == "1")
        {
            Debug.Log("Extended logging is enabled!");
        }
    }

    [MenuItem("Tools/Logging/Enable Extended Logging")]
    private static void EnableExtendedLogging()
    {
        Environment.SetEnvironmentVariable("UNITY_EXT_LOGGING", "1");
        Debug.Log("UNITY_EXT_LOGGING enabled. Restart Unity Editor to take effect.");
        
        EditorUtility.DisplayDialog(
            "Extended Logging Enabled",
            "UNITY_EXT_LOGGING has been set to 1.\n\n" +
            "Please restart Unity Editor for changes to take effect.",
            "OK"
        );
    }

    [MenuItem("Tools/Logging/Disable Extended Logging")]
    private static void DisableExtendedLogging()
    {
        Environment.SetEnvironmentVariable("UNITY_EXT_LOGGING", "0");
        Debug.Log("UNITY_EXT_LOGGING disabled. Restart Unity Editor to take effect.");
    }

    [MenuItem("Tools/Logging/Check Extended Logging Status")]
    private static void CheckExtendedLoggingStatus()
    {
        string value = Environment.GetEnvironmentVariable("UNITY_EXT_LOGGING");
        
        if (value == "1")
        {
            Debug.Log("✓ Extended logging is ENABLED");
            EditorUtility.DisplayDialog("Status", "Extended logging is ENABLED", "OK");
        }
        else
        {
            Debug.Log("✗ Extended logging is DISABLED");
            EditorUtility.DisplayDialog("Status", "Extended logging is DISABLED", "OK");
        }
    }
}
#endif
