using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BopVisualEffects.Services;

public enum ModLogLevel
{
	Debug = 0,
	Info = 1,
	Warning = 2,
	Error = 3,
	Fatal = 4
}

public sealed class ClassLogger(ManualLogSource logSource, ConfigEntry<ModLogLevel> minimumLevel)
{
	public static ClassLogger GetForClass<T>()
	{
		return GetForClass(typeof(T));
	}

	public static ClassLogger GetForClass(Type type)
	{
		if (LogService.Instance is null)
			throw new InvalidOperationException("LogService is not initialized. Call LogService.Initialize(...) first.");

		return LogService.Instance.For(type);
	}

	public void Debug(string message) => Log(ModLogLevel.Debug, message);
	public void Info(string message) => Log(ModLogLevel.Info, message);
	public void Warning(string message) => Log(ModLogLevel.Warning, message);
	public void Error(string message) => Log(ModLogLevel.Error, message);
	public void Fatal(string message) => Log(ModLogLevel.Fatal, message);

	private void Log(ModLogLevel level, string message)
	{
		if (level < minimumLevel.Value)
			return;

		switch (level)
		{
			case ModLogLevel.Debug:
				logSource.LogDebug(message);
				break;
			case ModLogLevel.Info:
				logSource.LogInfo(message);
				break;
			case ModLogLevel.Warning:
				logSource.LogWarning(message);
				break;
			case ModLogLevel.Error:
				logSource.LogError(message);
				break;
			case ModLogLevel.Fatal:
				logSource.LogFatal(message);
				break;
			default:
				logSource.LogInfo(message);
				break;
		}
	}
}

public sealed class LogService
{
	public static LogService? Instance { get; private set; }

	private readonly ConfigFile _config;
	private readonly string _sourcePrefix;
	private readonly Dictionary<string, ClassLogger> _cache = [];

	public static void Initialize(ConfigFile config, string sourcePrefix)
	{
		Instance = new LogService(config, sourcePrefix);
	}

	private LogService(ConfigFile config, string sourcePrefix)
	{
		_config = config;
		_sourcePrefix = sourcePrefix;
	}

	public ClassLogger For<T>()
	{
		return For(typeof(T));
	}

	public ClassLogger For(Type type)
	{
		var className = type.FullName ?? type.Name;

		if (_cache.TryGetValue(className, out var existing))
			return existing;

		var source = Logger.CreateLogSource($"{_sourcePrefix}.{className}");
		var minimumLevel = _config.Bind(
			"Logging",
			$"{className}.MinLevel",
			ModLogLevel.Info,
			$"Minimum log level for {className}.");

		var classLogger = new ClassLogger(source, minimumLevel);
		_cache[className] = classLogger;
		return classLogger;
	}
}
