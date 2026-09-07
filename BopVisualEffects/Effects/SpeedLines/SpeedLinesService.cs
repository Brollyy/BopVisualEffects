using System.Collections.Generic;
using BopVisualEffects.Core;
using UnityEngine;

namespace BopVisualEffects.Effects.SpeedLines;

internal sealed class SpeedLinesRequest
{
	public float Alpha;
	public float Count;
	public float Speed;
	public float Reach;
	public Color Color = Color.white;
}

internal static class SpeedLinesService
{
	private sealed class CameraState
	{
		public readonly List<SpeedLinesRequest> Requests = [];
		public SpeedLinesOverlay? Overlay;
	}

	private static readonly Dictionary<Camera, CameraState> States = [];

	public static SpeedLinesRequest AddRequest(Camera camera)
	{
		if (!States.TryGetValue(camera, out var state))
		{
			state = new CameraState();
			States[camera] = state;
		}

		if (!state.Overlay)
		{
			state.Overlay = camera.gameObject.AddComponent<SpeedLinesOverlay>();
			state.Overlay.Requests = state.Requests;
		}

		var request = new SpeedLinesRequest();
		state.Requests.Add(request);
		return request;
	}

	public static void RemoveRequest(Camera camera, SpeedLinesRequest request)
	{
		if (!States.TryGetValue(camera, out var state))
			return;

		state.Requests.Remove(request);
		if (state.Requests.Count == 0)
		{
			if (state.Overlay)
				Object.Destroy(state.Overlay);
			States.Remove(camera);
		}
	}

	internal sealed class SpeedLinesOverlay : MonoBehaviour
	{
		private const string ShaderAssetPath = "Assets/Shaders/BopVisualEffects_SpeedLines.shader";
		private static Material? _material;
		private static bool _shaderUnavailable;
		internal List<SpeedLinesRequest>? Requests;

		private static Material? GetMaterial()
		{
			if (_material)
				return _material;
			if (_shaderUnavailable)
				return null;

			var bundle = ShaderBundleLoader.GetBundle();
			var shader = bundle?.LoadAsset<Shader>(ShaderAssetPath);
			if (shader is null || !shader.isSupported)
			{
				_shaderUnavailable = true;
				ClassLogger.GetForClass<SpeedLinesOverlay>().Warning("[Speed Lines] Shader bundle unavailable or shader unsupported.");
				return null;
			}

			_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			return _material;
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if (Requests is null)
			{
				Graphics.Blit(source, destination);
				return;
			}

			SpeedLinesRequest? active = null;
			foreach (var request in Requests)
			{
				if (request.Alpha > 0f && (active is null || request.Alpha > active.Alpha))
					active = request;
			}

			var material = GetMaterial();
			if (active is null || material is null)
			{
				Graphics.Blit(source, destination);
				return;
			}

			material.SetColor("_Colour", active.Color);
			material.SetFloat("_SpeedLinesTiling", Mathf.Clamp(active.Count * 2f, 24f, 384f));
			material.SetFloat("_SpeedLinesAnimation", Mathf.Max(0.1f, active.Speed));
			material.SetFloat("_Reach", Mathf.Clamp01(active.Reach));
			material.SetFloat("_Intensity", Mathf.Clamp01(active.Alpha));
			Graphics.Blit(source, destination, material);
		}
	}
}
