# Effects Guide

This page shows the visual effects currently available in BopVisualEffects and what each one does in-game.

## Camera Shake

**DisplayName:** `Camera Shake`

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

Briefly tilts the camera on its Z-axis with a smooth swing arc. Great for expressive rhythm accents and musical phrases in a cartoonish style.

**Properties**
- `angle`: Maximum tilt angle in degrees (positive = counter-clockwise).
- `length` (event length in editor): How long the tilt lasts, in beats. This event is resizable in the timeline.

**Preview media**

![Camera Tilt preview](camera-tilt/preview.gif)

[Watch video preview](camera-tilt/demo.mp4)

## Screen Flash

**DisplayName:** `Screen Flash`

Draws a brief full-screen colored overlay that fades out to zero opacity over the event duration. Great for hits, beat drops, or any moment requiring a punchy visual accent.

**Properties**
- `r`, `g`, `b`: Color of the flash (0–1 each; default `1, 1, 1` — white).
- `alpha`: Maximum opacity of the flash at the start (0–1; default `0.8`).
- `length` (event length in editor): How long the flash lasts, in beats. This event is resizable in the timeline.

**Preview media**

![Screen Flash preview](screen-flash/preview.gif)

[Watch video preview](screen-flash/demo.mp4)

## Fog

**DisplayName:** `Fog`

Enables Unity scene fog that fades in over the first 20% of the event, holds, then fades out over the last 20%. Adds a dreamy, atmospheric depth to musical passages.

**Properties**
- `r`, `g`, `b`: Color of the fog (0–1 each; default `0.8, 0.8, 0.9` — pale blue-grey).
- `density`: Maximum fog density (default `0.03`).
- `length` (event length in editor): How long the fog lasts, in beats. This event is resizable in the timeline.

**Preview media**

![Fog preview](fog/preview.gif)

[Watch video preview](fog/demo.mp4)

## Zoom Pulse

**DisplayName:** `Zoom Pulse`

Rapidly zooms the camera in and then eases it back out, creating a punchy "push-in" accent. Works with both orthographic and perspective cameras.

**Properties**
- `intensity`: How much to zoom in, expressed as a fraction of the camera's base size (e.g. `0.15` = 15% zoom).
- `length` (event length in editor): How long the pulse lasts, in beats. This event is resizable in the timeline.

**Preview media**

![Zoom Pulse preview](zoom-pulse/preview.gif)

[Watch video preview](zoom-pulse/demo.mp4)

