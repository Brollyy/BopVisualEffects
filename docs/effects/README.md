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

**Preview**

https://github.com/user-attachments/assets/7c0a6e5e-4b7b-45ae-9be7-bef0b399b7b1

## Camera Tilt

**DisplayName:** `Camera Tilt`

**Config Key:** `CameraTilt.Enabled`

Briefly tilts the camera on its Z-axis with a smooth swing arc.

**Properties**
- `angle`: Maximum tilt angle in degrees (positive = counter-clockwise).
- `length` (event length in editor): How long the tilt lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/79927ec8-175f-4c43-ab05-3cdffa6b753f

## Color Tint

**DisplayName:** `Color Tint`

**Config Key:** `ColorTint.Enabled`

Applies a sustained full-screen color tint that fades in, holds, then fades out.

**Properties**
- `color`: Tint color (default `#ff000040` — red with slight opacity).
- `ease_in`: Fade in from zero at the event start (default `true`).
- `ease_out`: Fade out to zero at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/434d016e-1cc6-4503-98f6-6affb074c270

## Fog

**DisplayName:** `Fog`

**Config Key:** `Fog.Enabled`

Draws a ground fog gradient overlay - opaque at the bottom of the screen, fading to transparent at the specified height. Fades in and out smoothly.

**Properties**
- `color`: Fog color (default `#CCCCE599` - pale blue-gray).
- `height`: Normalized screen height at which the fog fully fades to transparent (0–1; default `0.5`).
- `ease_in`: Fade in from zero at the event start (default `true`).
- `ease_out`: Fade out to zero at the event end (default `true`).
- `length` (event length in editor): How long the fog lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/d6c128bb-7b45-4654-9265-a0499195218a

## Horizontal Flip

**DisplayName:** `Horizontal Flip`

**Config Key:** `HorizontalFlip.Enabled`

Mirrors the screen left-to-right for the duration of the event. Composes with Vertical Flip and zoom effects.

**Properties**
- `length` (event length in editor): How long the flip lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/a2a1ca7b-ff89-446f-a28d-326a0a86df50

## HSL Filter

**DisplayName:** `HSL Filter`

**Config Key:** `Hsl.Enabled`

Adjusts hue, saturation and lightness of the whole screen for colour grading using a per-pixel shader. Fades in and out smoothly.

**Properties**
- `hue_shift`: Degrees to rotate the hue wheel (-180–180; default `0`). Requires shader bundle; ignored in GL fallback.
- `saturation`: Saturation multiplier (0 = fully greyscale, 1 = unchanged, >1 = boosted; default `1.0`). Boost above 1 requires shader bundle.
- `lightness`: Additive lightness offset (-0.5–0.5; default `0`). Positive values brighten, negative values darken.
- `intensity`: Overall blend strength (0–1; default `1.0`).
- `ease_in`: Fade in from zero at the event start (default `true`).
- `ease_out`: Fade out to zero at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/75390981-e2cb-4c9f-8796-28dbf8b0462e

## Letterbox

**DisplayName:** `Letterbox`

**Config Key:** `Letterbox.Enabled`

Adds cinematic black bars at the top and bottom of the screen for a dramatic widescreen feel. Bars slide in and out smoothly.

**Properties**
- `size`: Height of each bar as a normalized screen fraction (0–0.49; default `0.1`).
- `ease_in`: Slide bars in at the event start (default `true`).
- `ease_out`: Slide bars out at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/cdb0a74d-9a72-4efd-9d15-b83b5d19998b

## Pixel Grid

**DisplayName:** `Pixel Grid`

**Config Key:** `PixelGrid.Enabled`

Pixelates the screen by downsampling the camera's rendered output to a low-resolution render texture and upsampling it with nearest-neighbour (point) filtering — the same method as retro 8-bit displays. Every pixel block averages the colours within it, giving a genuine pixelated look. The block size ramps up and down smoothly at the event boundaries.

**Properties**
- `pixel_size`: Size of each pixel block in screen pixels (2–64; default `4`). Larger values produce a more pronounced 8-bit look.
- `ease_in`: Ramp pixelation in at the event start (default `true`).
- `ease_out`: Ramp pixelation out at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/ea81aebd-35ad-4002-82c5-fab4a258e7d2

## Scanlines

**DisplayName:** `Scanlines`

**Config Key:** `Scanlines.Enabled`

Draws horizontal CRT-style scan lines over the screen for a retro aesthetic. Can optionally scroll up or down continuously. Fades in and out smoothly.

**Properties**
- `alpha`: Darkness of each scan line (0–1; default `0.35`).
- `count`: Number of scan lines (default `60`, clamped 4–2000).
- `scroll_speed`: Speed at which lines scroll upward, in cells per second (default `0` = static; negative = scroll downward).
- `ease_in`: Fade scanlines in at the event start (default `true`).
- `ease_out`: Fade scanlines out at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/9cb4cacc-9497-4f44-94bc-e9b764c68e5e

## Screen Noise

**DisplayName:** `Screen Noise`

**Config Key:** `ScreenNoise.Enabled`

Draws animated TV static noise specks over the screen for a glitchy atmosphere.

**Properties**
- `alpha`: Maximum opacity of the noise specks (0–1; default `0.5`).
- `count`: Number of noise specks drawn per frame (10–2000; default `400`).
- `size`: Physical size of each speck in normalized screen coordinates (0.005–0.1; default `0.01`).
- `ease_in`: Fade noise in at the event start (default `true`).
- `ease_out`: Fade noise out at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/6852cad0-a432-4389-b98e-e251f6e7e212

## Sepia

**DisplayName:** `Sepia`

**Config Key:** `Sepia.Enabled`

Applies a warm vintage sepia-tone filter using a per-pixel shader (standard Adobe/Kodak sepia matrix). Fades in and out smoothly.

**Properties**
- `intensity`: Strength of the sepia toning (0–1; default `0.8`).
- `ease_in`: Fade sepia in at the event start (default `true`).
- `ease_out`: Fade sepia out at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/ff1025b3-1f4b-44c8-982f-6691fe861277

## Speed Lines

**DisplayName:** `Speed Lines`

**Config Key:** `SpeedLines.Enabled`

Draws an animated, noise-driven radial speed-line vignette. Thin colored streak segments are procedurally distributed in polar space and continuously scroll toward the screen edges. Fades in and out smoothly.

**Properties**
- `alpha`: Maximum opacity of the speed lines (0–1; default `0.8`).
- `count`: Angular density of the procedural speed-line field (24–192; default `96`).
- `speed`: Animation rate of the procedural noise in cycles per second (default `3.5`). Higher values produce faster-moving streaks.
- `reach`: How far each speed line reaches toward the screen center as a normalized edge-to-center fraction (0–1; default `0.25`). `0` keeps the effect at the screen edge; `1` reaches the center. The calculation accounts for the screen aspect ratio.
- `color`: Color and base opacity of the speed lines (default white).
- `ease_in`: Fade speed lines in over the first 15% of the event (default `true`).
- `ease_out`: Fade speed lines out over the last 15% of the event (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/edddc3f0-6b81-4b29-9956-da7df65da25b

## Vertical Flip

**DisplayName:** `Vertical Flip`

**Config Key:** `VerticalFlip.Enabled`

Mirrors the screen top-to-bottom for the duration of the event. Composes with Horizontal Flip and zoom effects.

**Properties**
- `length` (event length in editor): How long the flip lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/c155c567-04b0-4fa8-9e83-109681003a1d

## Vignette

**DisplayName:** `Vignette`

**Config Key:** `Vignette.Enabled`

Darkens the screen edges with a smooth radial/elliptical gradient using a 32-segment triangle fan. The inner clear zone is aspect-ratio-corrected to appear circular on screen. Fades in and out smoothly.

**Properties**
- `alpha`: Darkness of the edge (0–1; default `0.7`).
- `size`: How far the darkening extends inward as a fraction of screen half-height (0–0.5; default `0.1`).
- `ease_in`: Fade vignette in at the event start (default `true`).
- `ease_out`: Fade vignette out at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/b2031a79-2d93-46ab-b15b-765fe0f1bd79

## Zoom In

**DisplayName:** `Zoom In`

**Config Key:** `ZoomIn.Enabled`

Eases the camera smoothly into a zoomed-in view, holds at the target level, then eases back out. Composes with other zoom events.

**Properties**
- `intensity`: How far to zoom in, as a fraction of the camera's base size (e.g. `0.2` = 20% closer). Clamped to `[0, 0.99]`.
- `ease_in`: Ease into the zoom at the event start (default `true`).
- `ease_out`: Ease out of the zoom at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/466ba43b-e218-432f-9023-095f3095e138

## Zoom Out

**DisplayName:** `Zoom Out`

**Config Key:** `ZoomOut.Enabled`

Eases the camera smoothly out to a wider view, holds, then eases back in. Composes with other zoom events.

**Properties**
- `intensity`: How far to zoom out, as a fraction of the camera's base size (e.g. `0.2` = 20% further out).
- `ease_in`: Ease into the zoom at the event start (default `true`).
- `ease_out`: Ease out of the zoom at the event end (default `true`).
- `length` (event length in editor): How long the effect lasts, in beats. This event is resizable in the timeline.

**Preview**

https://github.com/user-attachments/assets/bf89e240-a1f7-48b7-90f9-46fc471c3c6c
