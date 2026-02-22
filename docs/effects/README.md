# Effects Guide

This page shows the visual effects currently available in BopVisualEffects and what each one does in-game.

## Camera Shake

**DisplayName:** `Camera Shake`

**Config Key:** `CameraShake.Enabled`

Adds a temporary camera shake effect for impact, hits, drops, or strong rhythm accents.

**Properties**
- `amplitude`: Strength of the shake.
- `frequency`: Speed of the shake movement.
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview media**

![Camera Shake preview](camera-shake/preview.gif)

[Watch video preview](camera-shake/demo.mp4)

## Camera Tilt

**DisplayName:** `Camera Tilt`

**Config Key:** `CameraTilt.Enabled`

Briefly tilts the camera on its Z-axis with a smooth swing arc. Great for expressive rhythm accents and musical phrases in a cartoonish style.

**Properties**
- `angle`: Maximum tilt angle in degrees (positive = counter-clockwise).
- `length` (event length in editor): How long the tilt lasts, in beats. This event is resizable in the timeline.

## Zoom Pulse

**DisplayName:** `Zoom Pulse`

**Config Key:** `ZoomPulse.Enabled`

Rapidly zooms the camera in and then eases it back out, creating a punchy "push-in" accent. Works with both orthographic and perspective cameras.

**Properties**
- `intensity`: How much to zoom in, expressed as a fraction of the camera's base size (e.g. `0.15` = 15% zoom).
- `length` (event length in editor): How long the pulse lasts, in beats. This event is resizable in the timeline.

## Zoom In

**DisplayName:** `Zoom In`

**Config Key:** `ZoomIn.Enabled`

Eases the camera smoothly into a zoomed-in view, holds at the target level, then eases back out. Great for building tension or drawing attention to a musical phrase.

**Properties**
- `intensity`: How far to zoom in, as a fraction of the camera's base size (e.g. `0.2` = 20% closer). Clamped to `[0, 0.99]`.
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Zoom Out

**DisplayName:** `Zoom Out`

**Config Key:** `ZoomOut.Enabled`

Eases the camera smoothly out to a wider view, holds, then eases back in. Great for revealing the scene or creating a sense of space.

**Properties**
- `intensity`: How far to zoom out, as a fraction of the camera's base size (e.g. `0.2` = 20% further out).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Fog

**DisplayName:** `Fog`

**Config Key:** `Fog.Enabled`

Draws a ground fog gradient overlay — opaque at the bottom of the screen, fading to transparent at the specified height. Works in both 2D and 3D scenes. Fades in and out smoothly.

**Properties**
- `r`, `g`, `b`: Fog color (0–1 each; default `0.8, 0.8, 0.9` — pale blue-grey).
- `alpha`: Maximum opacity at the bottom of the screen (0–1; default `0.6`).
- `height`: Normalized screen height at which the fog fully fades to transparent (0–1; default `0.5`).
- `length` (event length in editor): How long the fog lasts, in beats. This event is resizable in the timeline.

## Scanlines

**DisplayName:** `Scanlines`

**Config Key:** `Scanlines.Enabled`

Draws horizontal CRT-style scan lines over the screen for a retro 8-bit aesthetic. Can optionally scroll up or down continuously. Fades in and out smoothly.

**Properties**
- `alpha`: Darkness of each scan line (0–1; default `0.35`).
- `count`: Number of scan lines (default `60`, clamped 4–2000).
- `scroll_speed`: Speed at which lines scroll upward, in cells per second (default `0` = static; negative = scroll downward).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Vignette

**DisplayName:** `Vignette`

**Config Key:** `Vignette.Enabled`

Darkens the screen edges with a smooth radial/elliptical gradient using a 32-segment triangle fan. The inner clear zone is aspect-ratio-corrected to appear circular on screen. Fades in and out smoothly.

**Properties**
- `alpha`: Darkness of the edge (0–1; default `0.7`).
- `size`: How far the darkening extends inward as a fraction of screen half-height (0–0.5; default `0.1`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Color Tint

**DisplayName:** `Color Tint`

**Config Key:** `ColorTint.Enabled`

Applies a sustained full-screen color tint that fades in, holds, then fades out. Perfect for horror (red), sepia (warm), or supernatural (purple) atmospheres.

**Properties**
- `r`, `g`, `b`: Tint color (0–1 each; default `1, 0, 0` — red).
- `alpha`: Maximum opacity (0–1; default `0.25`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Letterbox

**DisplayName:** `Letterbox`

**Config Key:** `Letterbox.Enabled`

Adds cinematic black bars at the top and bottom of the screen for a dramatic widescreen feel. Bars slide in and out smoothly.

**Properties**
- `size`: Height of each bar as a normalized screen fraction (0–0.49; default `0.1`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Screen Noise

**DisplayName:** `Screen Noise`

**Config Key:** `ScreenNoise.Enabled`

Draws animated TV static noise specks over the screen for a glitchy atmosphere. The noise changes every frame. Each speck uses an independent local RNG so global gameplay randomness is unaffected.

**Properties**
- `alpha`: Maximum opacity of the noise specks (0–1; default `0.5`).
- `count`: Number of noise specks drawn per frame (10–2000; default `400`).
- `size`: Physical size of each speck in normalized screen coordinates (0.005–0.1; default `0.01`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Pixel Grid

**DisplayName:** `Pixel Grid`

**Config Key:** `PixelGrid.Enabled`

Pixelates the screen by downsampling the camera's rendered output to a low-resolution render texture and upsampling it with nearest-neighbour (point) filtering — the same method as retro 8-bit displays. Every pixel block averages the colours within it, giving a genuine pixelated look. The block size ramps up and down smoothly at the event boundaries.

**Properties**
- `pixel_size`: Size of each pixel block in screen pixels (2–64; default `4`). Larger values produce a more pronounced 8-bit look.
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

## Horizontal Flip

**DisplayName:** `Horizontal Flip`

**Config Key:** `HorizontalFlip.Enabled`

Mirrors the screen left-to-right for the duration of the event. Composes correctly with Vertical Flip and zoom effects.
``|

**Properties**
- `length` (event length in editor): How long the flip lasts, in beats. This event is resizable in the timeline.

## Vertical Flip

**DisplayName:** `Vertical Flip`

**Config Key:** `VerticalFlip.Enabled`

Mirrors the screen top-to-bottom for the duration of the event. Composes correctly with Horizontal Flip and zoom effects.

**Properties**
- `length` (event length in editor): How long the flip lasts, in beats. This event is resizable in the timeline.

