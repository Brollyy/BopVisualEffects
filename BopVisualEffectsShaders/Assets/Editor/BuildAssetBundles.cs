#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the BopVisualEffects shader AssetBundle and copies it to the main C# project's
/// <c>Resources/</c> folder so it can be embedded as a managed assembly resource.
/// Run via <b>BopVisualEffects → Build Shader Bundles</b> in the Unity Editor menu.
/// </summary>
public static class BuildAssetBundles
{
    private const string BundleName = "bopvisualeffects_shaders";
    private const string OutputDir = "AssetBundles";
    private const string TargetDir = "../BopVisualEffects/Resources";

    [MenuItem("BopVisualEffects/Build Shader Bundles")]
    public static void Build()
    {
        Directory.CreateDirectory(OutputDir);

        // Specify bundle contents explicitly — no need to set AssetBundle labels in the Inspector.
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[]
                {
                    "Assets/Shaders/BopVisualEffects_HSL.shader",
                    "Assets/Shaders/BopVisualEffects_Sepia.shader",
                }
            }
        };

        BuildPipeline.BuildAssetBundles(
            OutputDir,
            builds,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        var bundleSrc = Path.Combine(OutputDir, BundleName);
        if (!File.Exists(bundleSrc))
        {
            Debug.LogError($"[BopVisualEffects] Bundle not found at {Path.GetFullPath(bundleSrc)} — build may have failed.");
            return;
        }

        Directory.CreateDirectory(TargetDir);
        var bundleDest = Path.Combine(TargetDir, $"{BundleName}.assetbundle");
        File.Copy(bundleSrc, bundleDest, overwrite: true);
        Debug.Log($"[BopVisualEffects] Shader bundle written to {Path.GetFullPath(bundleDest)}");
    }
}
#endif
