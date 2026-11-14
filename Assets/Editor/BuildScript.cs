using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CI 构建脚本 —— 供 GitHub Actions（GameCI unity-builder）调用。
/// 目标：编译出「未签名」的 iOS .app，再由流水线用 ldid 伪签名打包成 TrollStore 可用的 .ipa。
/// </summary>
public static class BuildScript
{
    // GameCI 默认会调用名为 PerformBuild 的静态方法（可通过 buildMethod 指定）
    public static void PerformBuild()
    {
        BuildIOS();
    }

    [MenuItem("Build/iOS (TrollStore)")]
    public static void BuildIOS()
    {
        // 输出目录：项目根下的 build/ iOS 文件夹
        string outputDir = "build/iOS";
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, true);
        Directory.CreateDirectory(outputDir);

        // 取 BuildSettings 里启用过的场景；若为空则退回扫描所有场景
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes == null || scenes.Length == 0)
        {
            Debug.LogWarning("BuildSettings 没有启用场景，改为扫描 Assets 下所有 .unity 场景");
            scenes = AssetDatabase.FindAssets("t:Scene")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();
        }

        Debug.Log("参与构建的场景: " + string.Join(", ", scenes));

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDir, "Unity-iPhone.xcodeproj"),
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        // 关键：iOS 在 CI 上做「未签名」构建，签名交给后面的 ldid
        EditorUserBuildSettings.iOSBuildType = iOSTargetType.Release;
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError("iOS 构建失败: " + report.summary.result);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"iOS 构建成功，输出: {outputDir}，大小 {report.summary.totalSize} 字节");
        EditorApplication.Exit(0);
    }
}