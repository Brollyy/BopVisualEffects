using UnityEngine;

namespace BopVisualEffects.Core;

/// <summary>
/// Extension helper for reading <see cref="AnimationCurve"/> values from <see cref="Entity"/> properties.
/// </summary>
internal static class EntityCurveExtensions
{
	/// <summary>
	/// Returns the <see cref="AnimationCurve"/> stored under <paramref name="key"/>, or
	/// <paramref name="fallback"/> when the key is absent or its value is not an
	/// <see cref="AnimationCurve"/>.
	/// </summary>
	internal static AnimationCurve GetAnimationCurve(this Entity entity, string key, AnimationCurve fallback)
	{
		if (entity.dynamicData.TryGetValue(key, out var value) && value is AnimationCurve curve)
			return curve;

		return fallback;
	}
}
