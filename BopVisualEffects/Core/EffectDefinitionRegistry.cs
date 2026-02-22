using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BopVisualEffects.Effects.CameraShake;
using BopVisualEffects.Effects.CameraTilt;
using BopVisualEffects.Effects.ColorTint;
using BopVisualEffects.Effects.Fog;
using BopVisualEffects.Effects.HorizontalFlip;
using BopVisualEffects.Effects.Letterbox;
using BopVisualEffects.Effects.PixelGrid;
using BopVisualEffects.Effects.Scanlines;
using BopVisualEffects.Effects.ScreenNoise;
using BopVisualEffects.Effects.VerticalFlip;
using BopVisualEffects.Effects.Vignette;
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
	private readonly ConfigFile? _config;
	private readonly Dictionary<string, IVisualEffectDefinition> _byDataModel;
	private readonly List<IVisualEffectDefinition> _effects;
	private readonly Dictionary<IVisualEffectDefinition, ConfigEntry<bool>> _enabledByDefinition;

	private EffectDefinitionRegistry(string pluginGuid, ClassLogger log, ConfigFile? config)
	{
		_pluginGuid = pluginGuid;
		_log = log;
		_config = config;
		_byDataModel = [];
		_effects = [];
		_enabledByDefinition = [];
	}

	/// <summary>
	/// Initializes and populates the singleton registry.
	/// </summary>
	/// <param name="pluginGuid">Plugin GUID namespace.</param>
	/// <param name="log">Logger used for registry diagnostics.</param>
	/// <param name="config">BepInEx config file used for per-effect enabled settings.</param>
	public static void Initialize(string pluginGuid, ClassLogger log, ConfigFile config)
	{
		var registry = new EffectDefinitionRegistry(pluginGuid, log, config);
		registry.Register(new CameraShakeEffect());
		registry.Register(new CameraTiltEffect());
		registry.Register(new ColorTintEffect());
		registry.Register(new FogEffect());
		registry.Register(new HorizontalFlipEffect());
		registry.Register(new LetterboxEffect());
		registry.Register(new PixelGridEffect());
		registry.Register(new ScanlinesEffect());
		registry.Register(new ScreenNoiseEffect());
		registry.Register(new VerticalFlipEffect());
		registry.Register(new VignetteEffect());
		registry.Register(new ZoomInEffect());
		registry.Register(new ZoomOutEffect());
		registry.Register(new ZoomPulseEffect());
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

		if (_config is not null)
		{
			_enabledByDefinition[definition] = _config.Bind(
				"Effects",
				$"{definition.ConfigKey}.Enabled",
				true,
				$"Whether the {definition.DisplayName} effect is active.");
		}

		_log.Info($"Registered visual effect definition '{definition.DisplayName}' as '{dataModel}'.");
	}

	/// <summary>
	/// Builds editor templates for all registered effects.
	/// </summary>
	public IReadOnlyList<MixtapeEventTemplate> BuildTemplates()
	{
		return _effects
			.Where(IsEnabled)
			.Select(e => e.CreateTemplate(_pluginGuid))
			.ToArray();
	}

	/// <summary>
	/// Dispatches a single entity to its mapped effect definition, if any.
	/// </summary>
	public bool TrySchedule(Entity entity, MixtapeLoaderCustom loader)
	{
		if (!_byDataModel.TryGetValue(entity.dataModel, out var definition))
			return false;

		if (!IsEnabled(definition))
			return false;

		return definition.TrySchedule(entity, loader);
	}

	private bool IsEnabled(IVisualEffectDefinition definition)
	{
		// When no config entry was bound (e.g. config was not provided), treat the effect as enabled.
		return !_enabledByDefinition.TryGetValue(definition, out var entry) || entry.Value;
	}
}
