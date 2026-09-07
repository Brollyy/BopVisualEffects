using UnityEngine;

namespace BopVisualEffects.Core;

/// <summary>
/// Shared envelope math for effects whose strength fades at event boundaries.
/// </summary>
internal static class EffectEnvelope
{
	/// <summary>
	/// Evaluates a fade envelope while allowing either boundary fade to be disabled.
	/// </summary>
	/// <param name="progress">Normalized event progress in the range 0..1.</param>
	/// <param name="easeIn">Whether to fade in from zero at the start.</param>
	/// <param name="easeOut">Whether to fade out to zero at the end.</param>
	/// <param name="fadeInEnd">End of the fade-in segment.</param>
	/// <param name="fadeOutStart">Start of the fade-out segment.</param>
	public static float Evaluate(float progress, bool easeIn, bool easeOut, float fadeInEnd = 0.2f, float fadeOutStart = 0.8f)
	{
		progress = Mathf.Clamp01(progress);

		if (easeIn && progress < fadeInEnd)
			return Mathf.InverseLerp(0f, fadeInEnd, progress);

		if (easeOut && progress > fadeOutStart)
			return 1f - Mathf.InverseLerp(fadeOutStart, 1f, progress);

		return 1f;
	}
}
