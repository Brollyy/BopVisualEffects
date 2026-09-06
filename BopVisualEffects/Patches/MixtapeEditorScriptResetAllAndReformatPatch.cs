using BopVisualEffects.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BopVisualEffects.Patches;

[HarmonyPatch(typeof(MixtapeEditorScript), "ResetAllAndReformat")]
public static class MixtapeEditorScriptResetAllAndReformatPatch
{
	private const string MetaButtonsPath = "Canvas/MinigamesMeta/Buttons";
	private const string CategoryMarkerPrefix = "BopVisualEffects_MetaCategory_";
	private static readonly Dictionary<int, float> BaseContainerX = new();
	private static readonly Dictionary<int, float> BaseButtonY = new();

	public static void Postfix(MixtapeEditorScript __instance)
	{
		var logger = ClassLogger.GetForClass(typeof(MixtapeEditorScriptResetAllAndReformatPatch));
		EffectTemplateManager.RefreshTemplates(MyPluginInfo.PLUGIN_GUID, logger);
		EnsureMetaCategoryButtons(__instance, logger);

		var formatLevels = typeof(MixtapeEditorScript).GetMethod(
			"FormatLevels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		formatLevels?.Invoke(__instance, null);
	}

	private static void EnsureMetaCategoryButtons(MixtapeEditorScript editor, ClassLogger logger)
	{
		var root = GameObject.Find(MetaButtonsPath);
		if (root is null)
			return;

		var categoryKeys = GetGenericCategoryKeys(editor);
		var buttons = GetDirectButtons(root.transform);
		if (buttons.Count < 3)
			return;

		var onSelectCategory = AccessTools.Method(typeof(MixtapeEditorScript), "OnSelectCategory");
		if (onSelectCategory is null)
			return;

		var source = buttons[2];
		var builtInCategories = new HashSet<string>(StringComparer.Ordinal)
		{
			"_",
			"gameManager",
			"effects",
			"accessibility",
			"debug"
		};

		for (var index = 0; index < categoryKeys.Count; index++)
		{
			var category = categoryKeys[index];
			if (builtInCategories.Contains(category))
				continue;

			var marker = CategoryMarkerPrefix + MakeSafeName(category);
			if (root.transform.Find(marker) is not null)
				continue;

			// If another mod already supplied a button for this category, the
			// available row capacity accounts for it. Do not duplicate it.
			if (buttons.Count >= categoryKeys.Count)
				break;

			var cloneObject = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
			cloneObject.name = marker;
			cloneObject.transform.SetSiblingIndex(index);

			var cloneButton = cloneObject.GetComponent<Button>();
			if (cloneButton is null)
			{
				UnityEngine.Object.Destroy(cloneObject);
				continue;
			}

			for (var listener = 0; listener < cloneButton.onClick.GetPersistentEventCount(); listener++)
				cloneButton.onClick.SetPersistentListenerState(listener, UnityEventCallState.Off);

			var selectedCategory = category;
			cloneButton.onClick.AddListener(new UnityAction(() =>
				onSelectCategory.Invoke(editor, new object[] { selectedCategory })));
			buttons = GetDirectButtons(root.transform);
		}

		var customButton = buttons.FirstOrDefault(button =>
			button.gameObject.name == CategoryMarkerPrefix + MakeSafeName("BopVisualEffects"));
		if (customButton is not null)
			ApplyBveIcon(customButton, logger);
		ArrangeMetaButtons(root, buttons, logger);
		logger.Info($"Configured generic category row: categories={string.Join(",", categoryKeys)}, buttons={buttons.Count}");
	}

	private static void ApplyBveIcon(Button categoryButton, ClassLogger logger)
	{
		var icon = categoryButton.GetComponentsInChildren<Image>(true)
			.FirstOrDefault(image => image.gameObject.name == "Icon");
		if (icon is null || icon.sprite is null || icon.gameObject.GetComponent<MarkerComponent>() is not null)
			return;

		var source = icon.sprite;
		var texture = CopySpriteTexture(source);
		if (texture is null)
			return;

		DrawBveBadge(texture);
		var rect = new Rect(0f, 0f, texture.width, texture.height);
		var pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
		icon.sprite = Sprite.Create(texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
		icon.gameObject.AddComponent<MarkerComponent>();
		logger.Info("Applied rasterized BVE badge to the custom category icon.");
	}

	private static Texture2D? CopySpriteTexture(Sprite source)
	{
		var sourceTexture = source.texture;
		var sourceRect = source.textureRect;
		var width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
		var height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
		var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			filterMode = sourceTexture.filterMode,
			wrapMode = TextureWrapMode.Clamp,
			name = "BopVisualEffects_BveIconTexture"
		};
		var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
		var previous = RenderTexture.active;
		try
		{
			var scale = new Vector2(sourceRect.width / sourceTexture.width, sourceRect.height / sourceTexture.height);
			var offset = new Vector2(sourceRect.x / sourceTexture.width, sourceRect.y / sourceTexture.height);
			Graphics.Blit(sourceTexture, renderTexture, scale, offset);
			RenderTexture.active = renderTexture;
			copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
			copy.Apply(false, false);
			return copy;
		}
		catch
		{
			UnityEngine.Object.Destroy(copy);
			return null;
		}
		finally
		{
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(renderTexture);
		}
	}

	private static void DrawBveBadge(Texture2D texture)
	{
		var pixels = texture.GetPixels32();
		var glyphs = new[]
		{
			new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
			new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
			new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" }
		};
		var scale = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(texture.width / 22f, texture.height / 10f) * 1.5f));
		var glyphWidth = 5 * scale;
		var gap = scale;
		var totalWidth = glyphWidth * glyphs.Length + gap * (glyphs.Length - 1);
		while (totalWidth + 2 * scale > texture.width - 2 && scale > 1)
		{
			scale--;
			glyphWidth = 5 * scale;
			gap = scale;
			totalWidth = glyphWidth * glyphs.Length + gap * (glyphs.Length - 1);
		}
		var startX = Mathf.Max(1, (texture.width - totalWidth) / 2);
		var startY = Mathf.Max(1, texture.height - 8 * scale - 1);
		for (var y = 0; y < 7 * scale + 2 * scale; y++)
		{
			for (var x = 0; x < totalWidth + 2 * scale; x++)
				SetPixel(pixels, texture.width, texture.height, startX - scale + x, startY - scale + y, new Color32(0, 0, 0, 210));
		}
		for (var glyph = 0; glyph < glyphs.Length; glyph++)
		{
			for (var row = 0; row < 7; row++)
			{
				for (var column = 0; column < 5; column++)
				{
					if (glyphs[glyph][row][column] != '1')
						continue;
					for (var dy = 0; dy < scale; dy++)
						for (var dx = 0; dx < scale; dx++)
							SetPixel(pixels, texture.width, texture.height,
								startX + glyph * (glyphWidth + gap) + column * scale + dx,
								startY + (6 - row) * scale + dy,
								new Color32(255, 255, 255, 255));
				}
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, false);
	}

	private static void SetPixel(Color32[] pixels, int width, int height, int x, int y, Color32 color)
	{
		if ((uint)x < width && (uint)y < height)
			pixels[y * width + x] = color;
	}

	private sealed class MarkerComponent : MonoBehaviour
	{
	}

	private static List<string> GetGenericCategoryKeys(MixtapeEditorScript editor)
	{
		var categories = AccessTools.Field(typeof(MixtapeEventTemplates), "categories")?.GetValue(null) as List<string>;
		if (categories is null || categories.Count == 0)
			return new List<string>();

		var minigameButtons = AccessTools.Field(typeof(MixtapeEditorScript), "minigameButtons")
			?.GetValue(editor) as MixtapeEditorMinigameButton[];
		var firstMinigame = minigameButtons?.FirstOrDefault()?.minigame;
		var boundary = firstMinigame is null ? Math.Min(5, categories.Count) : categories.IndexOf(firstMinigame);
		if (boundary < 1)
			boundary = Math.Min(5, categories.Count);
		return categories.Take(boundary).ToList();
	}

	private static List<Button> GetDirectButtons(Transform root)
	{
		var buttons = new List<Button>();
		for (var index = 0; index < root.childCount; index++)
		{
			var button = root.GetChild(index).GetComponent<Button>();
			if (button is not null)
				buttons.Add(button);
		}
		return buttons;
	}

	private static void ArrangeMetaButtons(GameObject root, List<Button> buttons, ClassLogger logger)
	{
		if (buttons.Count == 0)
			return;

		var sourceRect = buttons[Math.Min(2, buttons.Count - 1)].GetComponent<RectTransform>();
		if (sourceRect is null || sourceRect.rect.width <= 0f)
			return;

		var rootRect = root.GetComponent<RectTransform>();
		var baseWidth = sourceRect.rect.width;
		var baseHeight = sourceRect.rect.height;
		var spacing = 10f;
		var rowSpacing = 8f;
		const int maxColumns = 6;
		var availableWidth = rootRect is not null && rootRect.rect.width > 0f
			? rootRect.rect.width : baseWidth * 5f + spacing * 4f;
		var scale = Mathf.Min(1f, (availableWidth - spacing * (maxColumns - 1)) /
			(baseWidth * maxColumns));
		if (scale <= 0f)
			scale = Mathf.Clamp(availableWidth / (baseWidth * maxColumns), 0.5f, 1f);

		// The serialized row uses a layout component that only supports one
		// line. Disable that component and place the buttons directly so extra
		// categories can form centered rows without forcing a layout rebuild.
		foreach (var layout in root.GetComponents<LayoutGroup>())
			layout.enabled = false;

		var rowCount = (buttons.Count + maxColumns - 1) / maxColumns;
		var itemsPerRow = buttons.Count / rowCount;
		var rowsWithExtraItem = buttons.Count % rowCount;
		var rowSizes = Enumerable.Range(0, rowCount)
			.Select(row => itemsPerRow + (row < rowsWithExtraItem ? 1 : 0))
			.ToArray();
		if (rowCount == 1)
		{
			// Preserve the game's original Y positions and vertical margin, but
			// center the row horizontally after the layout group has positioned it.
			foreach (var layout in root.GetComponents<LayoutGroup>())
				layout.enabled = true;
			CenterSingleRowHorizontally(root, buttons, logger);
			return;
		}
		var rowStep = baseHeight * scale + rowSpacing;
		// Use the container's local center as the horizontal origin. The cloned
		// buttons inherit anchors from the serialized layout, so anchoredPosition
		// is not a stable coordinate system for manual placement.
		var originX = rootRect is null ? 0f : rootRect.rect.center.x;
		var baselineY = GetBaseButtonY(root, sourceRect);
		// Positive local Y moves upward in this canvas. The two-row layout needs
		// to move upward slightly to reduce the top margin.
		var verticalOffset = rowCount > 1 ? 7f : 0f;
		var firstRowY = baselineY + (rowCount - 1) * rowStep * 0.5f + verticalOffset;
		foreach (var pair in buttons.Select((button, index) => (button, index)))
		{
			var row = 0;
			var rowStart = 0;
			while (row + 1 < rowSizes.Length && pair.index >= rowStart + rowSizes[row])
			{
				rowStart += rowSizes[row];
				row++;
			}
			var rowCountForLine = rowSizes[row];
			var column = pair.index - rowStart;
			var rect = pair.button.GetComponent<RectTransform>();
			if (rect is null)
				continue;

			var x = originX + (column - (rowCountForLine - 1) * 0.5f) * (baseWidth * scale + spacing);
			var y = firstRowY - row * rowStep;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.localPosition = new Vector3(x, y, rect.localPosition.z);
			rect.localScale = Vector3.one * scale;
		}

		ApplyLeftMargin(rootRect);
		logger.Info($"Arranged generic category row: count={buttons.Count}, rows={rowCount}, scale={scale:0.###}, width={availableWidth:0.##}");
	}

	private static void CenterSingleRowHorizontally(GameObject root, List<Button> buttons, ClassLogger logger)
	{
		var rootRect = root.GetComponent<RectTransform>();
		if (rootRect is null)
			return;

		LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
		var minX = float.PositiveInfinity;
		var maxX = float.NegativeInfinity;
		foreach (var button in buttons)
		{
			var rect = button.GetComponent<RectTransform>();
			if (rect is null)
				continue;

			var left = rect.localPosition.x - rect.rect.width * rect.pivot.x * rect.localScale.x;
			var right = rect.localPosition.x + rect.rect.width * (1f - rect.pivot.x) * rect.localScale.x;
			minX = Mathf.Min(minX, left);
			maxX = Mathf.Max(maxX, right);
		}

		if (float.IsInfinity(minX) || float.IsInfinity(maxX))
			return;

		var deltaX = rootRect.rect.center.x - (minX + maxX) * 0.5f;
		foreach (var button in buttons)
		{
			var rect = button.GetComponent<RectTransform>();
			if (rect is null)
				continue;

			var position = rect.localPosition;
			rect.localPosition = new Vector3(position.x + deltaX, position.y, position.z);
		}

		foreach (var layout in root.GetComponents<LayoutGroup>())
			layout.enabled = false;
		logger.Info($"Centered single-row generic category layout horizontally: delta={deltaX:0.##}");
	}

	private static void ApplyLeftMargin(RectTransform? container)
	{
		if (container is null)
			return;

		var instanceId = container.gameObject.GetInstanceID();
		if (!BaseContainerX.TryGetValue(instanceId, out var baseX))
		{
			baseX = container.anchoredPosition.x;
			BaseContainerX[instanceId] = baseX;
		}

		// The serialized layout has a small rightward margin. Keep the original
		// scene placement and correct only that margin; do not move in Canvas
		// world space, which is not the same coordinate system here.
		container.anchoredPosition = new Vector2(baseX - 10f, container.anchoredPosition.y);
	}

	private static float GetBaseButtonY(GameObject root, RectTransform sourceRect)
	{
		var instanceId = root.GetInstanceID();
		if (!BaseButtonY.TryGetValue(instanceId, out var baselineY))
		{
			baselineY = sourceRect.localPosition.y;
			BaseButtonY[instanceId] = baselineY;
		}

		return baselineY;
	}

	private static string MakeSafeName(string category)
	{
		return new string(category.Select(character =>
			char.IsLetterOrDigit(character) ? character : '_').ToArray());
	}
}
