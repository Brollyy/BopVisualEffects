using System;
using System.Collections.Generic;
using System.Linq;
using BopVisualEffects.Effects.CameraTilt;
using BopVisualEffects.Effects.CameraShake;
using BopVisualEffects.Effects.ColorTint;
using BopVisualEffects.Effects.Fog;
using BopVisualEffects.Effects.Letterbox;
using BopVisualEffects.Effects.PixelGrid;
using BopVisualEffects.Effects.Scanlines;
using BopVisualEffects.Effects.ScreenNoise;
using BopVisualEffects.Effects.Vignette;
using BopVisualEffects.Effects.HorizontalFlip;
using BopVisualEffects.Effects.VerticalFlip;
using BopVisualEffects.Effects.ZoomIn;
using BopVisualEffects.Effects.ZoomOut;
using BopVisualEffects.Effects.ZoomPulse;

namespace BopVisualEffects.Core;

/// <summary>
/// Global registry of visual effect definitions.
/// Drives both editor template generation and runtime event dispatch.
/// </summary>
public sealed class EffectDefinitionRegistry
{
	private static EffectDefinitionRegistry? _instance;

	private readonly string _pluginGuid;
	private readonly ClassLogger _log;
	private readonly Dictionary<string, IVisualEffectDefinition> _byDataModel;
	private readonly List<IVisualEffectDefinition> _effects;

	private EffectDefinitionRegistry(string pluginGuid, ClassLogger log)
	{
		_pluginGuid = pluginGuid;
		_log = log;
		_byDataModel = [];
		_effects = [];
	}

	/// <summary>
	/// Initializes and populates the singleton registry.
	/// </summary>
	/// <param name="pluginGuid">Plugin GUID namespace.</param>
	/// <param name="log">Logger used for registry diagnostics.</param>
	public static void Initialize(string pluginGuid, ClassLogger log)
	{
		var registry = new EffectDefinitionRegistry(pluginGuid, log);
		registry.Register(new CameraShakeEffect());
		registry.Register(new CameraTiltEffect());
		registry.Register(new ZoomPulseEffect());
		registry.Register(new ZoomInEffect());
		registry.Register(new ZoomOutEffect());
		registry.Register(new FogEffect());
		registry.Register(new ScanlinesEffect());
		registry.Register(new VignetteEffect());
		registry.Register(new ColorTintEffect());
		registry.Register(new LetterboxEffect());
		registry.Register(new ScreenNoiseEffect());
		registry.Register(new PixelGridEffect());
		registry.Register(new HorizontalFlipEffect());
		registry.Register(new VerticalFlipEffect());
		_instance = registry;
	}

	/// <summary>
	/// Returns the initialized singleton instance.
	/// </summary>
	public static EffectDefinitionRegistry Get()
	{
		return _instance ?? throw new InvalidOperationException("EffectDefinitionRegistry is not initialized.");
	}

	/// <summary>
	/// All registered effect definitions.
	/// </summary>
	public IReadOnlyList<IVisualEffectDefinition> Effects => _effects;

	/// <summary>
	/// Register a new effect definition.
	/// </summary>
	/// <param name="definition">Effect definition implementation.</param>
	public void Register(IVisualEffectDefinition definition)
	{
		var dataModel = $"{_pluginGuid}/{definition.Id}";
		_byDataModel[dataModel] = definition;

		if (!_effects.Contains(definition))
		{
			_effects.Add(definition);
		}

		_log.Info($"Registered visual effect definition '{definition.DisplayName}' as '{dataModel}'.");
	}

	/// <summary>
	/// Builds editor templates for all registered effects.
	/// </summary>
	public IReadOnlyList<MixtapeEventTemplate> BuildTemplates()
	{
		return _effects.Select(e => e.CreateTemplate(_pluginGuid)).ToArray();
	}

	/// <summary>
	/// Dispatches a single entity to its mapped effect definition, if any.
	/// </summary>
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		if (!_byDataModel.TryGetValue(entity.dataModel, out var definition))
			return false;

		return definition.TrySchedule(entity, loader);
	}
}
