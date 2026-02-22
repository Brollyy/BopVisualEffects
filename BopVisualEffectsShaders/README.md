# BopVisualEffectsShaders

Unity project that compiles the post-processing shaders used by the **BopVisualEffects** BepInEx
mod into an AssetBundle.  The built bundle is embedded into the mod DLL at compile time and loaded
at runtime via `AssetBundle.LoadFromMemory`.

## Prerequisites

| Requirement | Notes |
|---|---|
| **Unity Editor** | Must match (or be compatible with) the Unity version used by **Bits & Bops**. Open the game's `Bits & Bops_Data/` folder and read the `app.info` or `globalgamemanagers` Unity version string to confirm. This project was scaffolded with **2022.3 LTS**. |
| **Target platform** | Build for **Windows x86-64** (StandaloneWindows64). The game runs on Windows only. |

## Shaders

| Asset path | Purpose |
|---|---|
| `Assets/Shaders/BopVisualEffects_HSL.shader` | Per-pixel hue rotation, saturation scaling and lightness offset. |
| `Assets/Shaders/BopVisualEffects_Sepia.shader` | Per-pixel sepia tone using the standard Adobe/Kodak conversion matrix. |

## Build steps

1. Open this folder (`BopVisualEffectsShaders/`) as a Unity project.
2. Wait for Unity to import all assets.
3. In the Unity Editor menu bar, click **BopVisualEffects → Build Shader Bundles**.
4. The Editor script will:
   - Compile both shaders into `BopVisualEffectsShaders/AssetBundles/bopvisualeffects_shaders`.
   - Copy the bundle to `BopVisualEffects/Resources/bopvisualeffects_shaders.assetbundle`.
5. Return to the main solution and rebuild `BopVisualEffects.sln`.  The `.csproj` will automatically
   embed the bundle file as a managed resource when it is present.

> **After rebuilding, commit `BopVisualEffects/Resources/bopvisualeffects_shaders.assetbundle` to the
> repository** so that CI and other developers get the compiled shaders without needing Unity Editor.

## Updating shaders

Edit the `.shader` files in `Assets/Shaders/`, then repeat the build steps above and commit the
updated `bopvisualeffects_shaders.assetbundle`.

## Fallback behaviour

If the bundle resource is absent (e.g., initial checkout before the first build), both effects fall
back to GL blend operations automatically at runtime — no error is thrown and the mod remains
functional, albeit with limited colour-grading fidelity (no true hue rotation or per-pixel sepia).
