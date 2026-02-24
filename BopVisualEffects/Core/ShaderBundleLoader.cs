using System.IO;
using System.Reflection;
using UnityEngine;

namespace BopVisualEffects.Core;

/// <summary>
/// Loads the platform-appropriate <c>bopvisualeffects_shaders</c> AssetBundle from the embedded
/// assembly resource and caches it for the lifetime of the session.
/// <para>
/// A separate bundle is built and embedded for each supported OS (Windows, macOS, Linux).
/// At runtime the loader selects the bundle matching <see cref="Application.platform"/>.
/// </para>
/// <para>
/// The bundle is only present when it has been built from the <c>BopVisualEffectsShaders</c>
/// Unity project (see <c>BopVisualEffectsShaders/README.md</c>) and committed to the repository.
/// When the resource is absent the loader returns <see langword="null"/> and callers fall back to
/// their GL-based rendering path automatically.
/// </para>
/// </summary>
internal static class ShaderBundleLoader
{
	private const string ResourcePrefix = "BopVisualEffects.Resources.bopvisualeffects_shaders_";
	private const string ResourceSuffix = ".assetbundle";

	private static AssetBundle? _bundle;
	private static bool _loadAttempted;

	/// <summary>
	/// Returns the loaded <see cref="AssetBundle"/>, or <see langword="null"/> if the bundle
	/// resource is not embedded or failed to load. The result is cached after the first call.
	/// </summary>
	internal static AssetBundle? GetBundle()
	{
		if (_loadAttempted)
			return _bundle;

		_loadAttempted = true;

		var platformSuffix = GetPlatformSuffix();
		if (platformSuffix is null)
			return null;

		var resourceName = ResourcePrefix + platformSuffix + ResourceSuffix;
		using var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(resourceName);
		if (stream is null)
			return null;

		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		_bundle = AssetBundle.LoadFromMemory(ms.ToArray());
		return _bundle;
	}

	private static string? GetPlatformSuffix() => Application.platform switch
	{
		RuntimePlatform.WindowsPlayer => "win",
		RuntimePlatform.OSXPlayer => "osx",
		RuntimePlatform.LinuxPlayer => "linux",
		_ => null,
	};
}
