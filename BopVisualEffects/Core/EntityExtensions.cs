using System.Collections.Generic;
using UnityEngine;

namespace BopVisualEffects.Core;

/// <summary>
/// Extension methods for reading typed values from <see cref="Entity"/> properties.
/// </summary>
internal static class EntityExtensions
{
	/// <summary>
	/// Reads a <see cref="float"/> from an entity property by <paramref name="key"/>.
	/// Returns <paramref name="fallback"/> when the property is absent.
	/// </summary>
	/// <param name="entity">The entity to read from.</param>
	/// <param name="key">Property key holding the float value.</param>
	/// <param name="fallback">Value returned when the property is absent. Defaults to 0.</param>
	public static float GetFloat(this Entity entity, string key, float fallback)
	{
		if (!entity.dynamicData.TryGetValue(key, out var value) || value is null)
			return fallback;
		return value switch
		{
			float f => f,
			double d => (float)d,
			int i => i,
			_ => fallback
		};
	}

	/// <summary>
	/// Reads a <see cref="Color"/> from an entity property by <paramref name="key"/>.
	/// </summary>
	/// <remarks>
	/// Handles two cases, in order:
	/// <list type="number">
	/// <item>The value is already a <see cref="Color"/> struct (in-memory, before a save/load cycle).</item>
	/// <item>
	/// The value is a <see cref="Dictionary{TKey,TValue}"/> with r/g/b/a sub-keys
	/// (produced when the game JSON-deserializes a stored <see cref="Color"/> without type info).
	/// </item>
	/// </list>
	/// Returns <paramref name="fallback"/> (default: <see cref="Color.white"/>) when neither applies.
	/// </remarks>
	/// <param name="entity">The entity to read from.</param>
	/// <param name="key">Property key holding the color value.</param>
	/// <param name="fallback">Value returned when the property is absent or unrecognised.</param>
	public static Color GetColor(this Entity entity, string key, Color? fallback = null)
	{
		if (!entity.dynamicData.TryGetValue(key, out var value) || value is null) return fallback ?? Color.white;
		return value switch
		{
			Color color => color,
			Dictionary<string, object> dict => new Color(GetSubFloat(dict, "r"), GetSubFloat(dict, "g"),
				GetSubFloat(dict, "b"), GetSubFloat(dict, "a", 1f)),
			_ => fallback ?? Color.white
		};
	}

	private static float GetSubFloat(Dictionary<string, object> dict, string key, float defaultValue = 0f)
	{
		if (!dict.TryGetValue(key, out object? value) || value is null)
			return defaultValue;

		return value switch
		{
			float f => f,
			double d => (float)d,
			int i => i,
			_ => defaultValue
		};
	}
}
