#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the BopVisualEffects shader AssetBundle for all supported platforms and copies each
/// bundle to the main C# project's <c>Resources/</c> folder so it can be embedded as a managed
/// assembly resource.
/// Run via <b>BopVisualEffects → Build Shader Bundles</b> in the Unity Editor menu.
/// </summary>
public static class BuildAssetBundles
{
	private const string BundleBaseName = "bopvisualeffects_shaders";
	private const string OutputDir = "AssetBundles";
	private const string TargetDir = "../BopVisualEffects/Resources";

	private static readonly (BuildTarget target, string suffix)[] Platforms =
	{
		(BuildTarget.StandaloneWindows64, "win"),
		(BuildTarget.StandaloneOSX, "osx"),
		(BuildTarget.StandaloneLinux64, "linux"),
	};

	[MenuItem("BopVisualEffects/Build Shader Bundles")]
	public static void Build()
	{
		Directory.CreateDirectory(TargetDir);

		// Specify bundle contents explicitly — no need to set AssetBundle labels in the Inspector.
		var builds = new[]
		{
			new AssetBundleBuild
			{
				assetBundleName = BundleBaseName,
				assetNames = new[]
				{
					"Assets/Shaders/BopVisualEffects_HSL.shader",
					"Assets/Shaders/BopVisualEffects_Sepia.shader",
				}
			}
		};

		foreach (var (target, suffix) in Platforms)
		{
			var platformOutputDir = Path.Combine(OutputDir, suffix);
			Directory.CreateDirectory(platformOutputDir);

			BuildPipeline.BuildAssetBundles(
				platformOutputDir,
				builds,
				BuildAssetBundleOptions.None,
				target);

			var bundleSrc = Path.Combine(platformOutputDir, BundleBaseName);
			if (!File.Exists(bundleSrc))
			{
				Debug.LogError($"[BopVisualEffects] Bundle not found at {Path.GetFullPath(bundleSrc)} — build may have failed for {target}.");
				continue;
			}

			var bundleDest = Path.Combine(TargetDir, $"{BundleBaseName}_{suffix}.assetbundle");
			File.Copy(bundleSrc, bundleDest, overwrite: true);
			Debug.Log($"[BopVisualEffects] Shader bundle ({suffix}) written to {Path.GetFullPath(bundleDest)}");
		}
	}
}
#endif
