using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BopVisualEffects.Services;

/// <summary>
/// Log levels used by the mod logger to filter messages per class.
/// </summary>
public enum ModLogLevel
{
	Debug = 0,
	Info = 1,
	Warning = 2,
	Error = 3,
	Fatal = 4
}

/// <summary>
/// Singleton logging service that creates and caches class-specific loggers.
/// </summary>
public sealed class LogService
{
	/// <summary>
	/// Global logger service instance initialized at plugin startup.
	/// </summary>
	public static LogService? Instance { get; private set; }

	private readonly ConfigFile? _config;
	private readonly string? _sourcePrefix;
	private readonly Dictionary<string, LogService>? _cache;
	private readonly ManualLogSource? _logSource;
	private readonly ConfigEntry<ModLogLevel>? _minimumLevel;
	private readonly string? _className;

	/// <summary>
	/// Initializes the singleton logger manager used by the mod.
	/// </summary>
	/// <param name="config">BepInEx config file used for per-class log settings.</param>
	/// <param name="sourcePrefix">Short source label shown in log messages.</param>
	public static void Initialize(ConfigFile config, string sourcePrefix)
	{
		Instance = new LogService(config, sourcePrefix);
	}

	/// <summary>
	/// Gets a class logger for the provided generic type.
	/// </summary>
	/// <typeparam name="T">Type that will own the logger.</typeparam>
	/// <returns>A logger configured for <typeparamref name="T"/>.</returns>
	public static LogService GetForClass<T>()
	{
		return GetForClass(typeof(T));
	}

	/// <summary>
	/// Gets a class logger for a runtime type.
	/// </summary>
	/// <param name="type">Type that will own the logger.</param>
	/// <returns>A logger configured for <paramref name="type"/>.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the singleton is not initialized.</exception>
	public static LogService GetForClass(Type type)
	{
		return Instance is null
			? throw new InvalidOperationException(
				"LogService is not initialized. Call LogService.Initialize(...) first.")
			: Instance.For(type);
	}

	private LogService(ConfigFile config, string sourcePrefix)
	{
		_config = config;
		_sourcePrefix = sourcePrefix;
		_cache = [];
	}

	private LogService(ManualLogSource logSource, ConfigEntry<ModLogLevel> minimumLevel, string className)
	{
		_logSource = logSource;
		_minimumLevel = minimumLevel;
		_className = className;
	}

	/// <summary>
	/// Logs a debug message when allowed by the class log level setting.
	/// </summary>
	/// <param name="message">Message text.</param>
	public void Debug(string message) => Log(ModLogLevel.Debug, message);

	/// <summary>
	/// Logs an informational message when allowed by the class log level setting.
	/// </summary>
	/// <param name="message">Message text.</param>
	public void Info(string message) => Log(ModLogLevel.Info, message);

	/// <summary>
	/// Logs a warning message when allowed by the class log level setting.
	/// </summary>
	/// <param name="message">Message text.</param>
	public void Warning(string message) => Log(ModLogLevel.Warning, message);

	/// <summary>
	/// Logs an error message when allowed by the class log level setting.
	/// </summary>
	/// <param name="message">Message text.</param>
	public void Error(string message) => Log(ModLogLevel.Error, message);

	/// <summary>
	/// Logs a fatal message when allowed by the class log level setting.
	/// </summary>
	/// <param name="message">Message text.</param>
	public void Fatal(string message) => Log(ModLogLevel.Fatal, message);

	/// <summary>
	/// Logs a message using the mapped BepInEx log method for the provided level.
	/// </summary>
	/// <param name="level">Requested log level.</param>
	/// <param name="message">Message text.</param>
	/// <exception cref="InvalidOperationException">Thrown when called on the singleton manager instead of a class logger.</exception>
	private void Log(ModLogLevel level, string message)
	{
		if (_logSource is null || _minimumLevel is null || _className is null || _sourcePrefix is null)
			throw new InvalidOperationException("Use LogService.GetForClass<T>() to obtain a class logger instance.");

		if (level < _minimumLevel.Value)
			return;

		var formattedMessage = $"[{_sourcePrefix}] [{_className}] {message}";

		switch (level)
		{
			case ModLogLevel.Debug:
				_logSource.LogDebug(formattedMessage);
				break;
			case ModLogLevel.Info:
				_logSource.LogInfo(formattedMessage);
				break;
			case ModLogLevel.Warning:
				_logSource.LogWarning(formattedMessage);
				break;
			case ModLogLevel.Error:
				_logSource.LogError(formattedMessage);
				break;
			case ModLogLevel.Fatal:
				_logSource.LogFatal(formattedMessage);
				break;
			default:
				_logSource.LogInfo(formattedMessage);
				break;
			}
	}

	/// <summary>
	/// Resolves a class logger for the provided generic type.
	/// </summary>
	/// <typeparam name="T">Type that will own the logger.</typeparam>
	/// <returns>A logger configured for <typeparamref name="T"/>.</returns>
	public LogService For<T>()
	{
		return For(typeof(T));
	}

	/// <summary>
	/// Resolves and caches a class logger for the provided type.
	/// </summary>
	/// <param name="type">Type that will own the logger.</param>
	/// <returns>A logger configured for <paramref name="type"/>.</returns>
	/// <exception cref="InvalidOperationException">Thrown when called on a class logger instead of the singleton manager.</exception>
	public LogService For(Type type)
	{
		if (_cache is null || _config is null || _sourcePrefix is null)
			throw new InvalidOperationException("Use the singleton manager instance to resolve class loggers.");

		string className = type.Name;

		if (_cache.TryGetValue(className, out var existing))
			return existing;

		ManualLogSource source = Logger.CreateLogSource(_sourcePrefix);
		ConfigEntry<ModLogLevel> minimumLevel = _config.Bind(
			"Logging",
			$"{className}.MinLevel",
			ModLogLevel.Info,
			$"Minimum log level for {className}.");

		var classLogger = new LogService(source, minimumLevel, className);
		_cache[className] = classLogger;
		return classLogger;
	}
}
