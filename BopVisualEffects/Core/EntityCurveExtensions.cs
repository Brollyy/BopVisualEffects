using UnityEngine;

namespace BopVisualEffects.Core;

/// <summary>
/// Helpers for reading animation curves from event properties.
/// </summary>
internal static class EntityCurveExtensions
{
	/// <summary>
	/// Returns the curve stored under <paramref name="key"/>, or <paramref name="fallback"/>
	/// when the property is absent or has an unexpected type.
	/// </summary>
	internal static AnimationCurve GetAnimationCurve(this Entity entity, string key, AnimationCurve fallback)
	{
		if (entity.dynamicData.TryGetValue(key, out var value) && value is AnimationCurve curve)
			return curve;

		return fallback;
	}
}
