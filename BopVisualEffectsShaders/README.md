# BopVisualEffectsShaders

Unity project that compiles the post-processing shaders used by the **BopVisualEffects** BepInEx
mod into AssetBundles.  The built bundles are embedded into the mod DLL at compile time and loaded
at runtime via `AssetBundle.LoadFromMemory`.

## Prerequisites

| Requirement | Notes |
|---|---|
| **Unity Editor** | Must match the Unity version used by **Bits & Bops**. This project is configured for **2021.3.45f2**. |

## Supported platforms

Bits & Bops runs on Windows, macOS, and Linux.  A separate AssetBundle is compiled for each
target so the correct shader bytecode is used on every platform:

| Platform | Build target | Output file |
|---|---|---|
| Windows (x64) | `StandaloneWindows64` | `bopvisualeffects_shaders_win.assetbundle` |
| macOS | `StandaloneOSX` | `bopvisualeffects_shaders_osx.assetbundle` |
| Linux (x64) | `StandaloneLinux64` | `bopvisualeffects_shaders_linux.assetbundle` |

At runtime `ShaderBundleLoader` reads `Application.platform` and loads only the bundle that
matches the current OS.  When no matching bundle is embedded the effects automatically fall back
to their GL-based rendering path.

## Shaders

| Asset path | Purpose |
|---|---|
| `Assets/Shaders/BopVisualEffects_HSL.shader` | Per-pixel hue rotation, saturation scaling and lightness offset. |
| `Assets/Shaders/BopVisualEffects_Sepia.shader` | Per-pixel sepia tone using the standard Adobe/Kodak conversion matrix. |

## Build steps

1. Open this folder (`BopVisualEffectsShaders/`) as a Unity project in Unity **2021.3.45f2**.
2. Wait for Unity to import all assets.
3. In the Unity Editor menu bar, click **BopVisualEffects → Build Shader Bundles**.
4. The Editor script will:
   - Compile both shaders into three platform-specific bundles under
     `BopVisualEffectsShaders/AssetBundles/<platform>/`.
   - Copy each bundle to `BopVisualEffects/Resources/bopvisualeffects_shaders_<platform>.assetbundle`.
5. Return to the main solution and rebuild `BopVisualEffects.sln`.  The `.csproj` will automatically
   embed whichever platform bundles are present as managed resources.

> **After rebuilding, commit all three `BopVisualEffects/Resources/bopvisualeffects_shaders_*.assetbundle`
> files to the repository** so that CI and other developers get the compiled shaders without needing
> Unity Editor installed.

## Updating shaders

Edit the `.shader` files in `Assets/Shaders/`, then repeat the build steps above and commit the
updated `bopvisualeffects_shaders_*.assetbundle` files.

## Fallback behaviour

If no matching bundle resource is embedded (e.g., initial checkout before the first build, or the
current platform has no bundle), both effects fall back to GL blend operations automatically at
runtime — no error is thrown and the mod remains functional, albeit with limited colour-grading
fidelity (no true hue rotation or per-pixel sepia).
