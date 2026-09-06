# BopVisualEffects

BepInEx 5.x mod for Bits & Bops that adds additional visual effects for the in-game editor.

## Installation

1. Install [BepInEx 5.x](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for Bits & Bops.
2. Download `BopVisualEffects.dll` from the [latest release](https://github.com/Brollyy/BopVisualEffects/releases/latest).
3. Place `BopVisualEffects.dll` in `<GameRoot>/BepInEx/plugins/`.
4. Launch the game — effects will be available in the in-game editor.

The mod currently targets Bits & Bops 1.13.0 (Steam BuildID 24990737, released August 29, 2026).
The CI workflow downloads the current Steam build and uses a versioned managed-DLL
cache so updates cannot silently compile against stale game references.

## Available effects

See [this document](docs/effects/README.md) for the full list of effects this mod enables.

## Configuration

Each effect can be individually enabled or disabled in the BepInEx config file (`<GameRoot>/BepInEx/config/BopVisualEffects.cfg`), under the `[Effects]` section.

The config file is at `<GameRoot>/BepInEx/config/BopVisualEffects.cfg`. It's generated automatically on first load. To disable an effect, set its entry to `false`:

```ini
[Effects]

## Whether the Camera Shake effect is active.
# Setting type: Boolean
# Default value: true
CameraShake.Enabled = false
```

See the [effects documentation](docs/effects/README.md) for all available config keys.

Disabled effects are removed from the in-game editor and will not run during playback.

## Contributing

### Local setup

1. Copy `BopVisualEffects/BopVisualEffects.user.props.example` to `BopVisualEffects/BopVisualEffects.user.props`.
2. Set `GameRoot` in `BopVisualEffects/BopVisualEffects.user.props` to the path where Bits & Bops is installed.
3. Build the solution with `dotnet build BopVisualEffects.sln`

Derived paths:
- `BepInExPluginsDir = <GameRoot>/BepInEx/plugins`
- `UnityManagedDir = <GameRoot>/Bits & Bops_Data/Managed`

If `BepInExPluginsDir` exists, build output is copied there automatically.

### Adding a new effect

1. Create a class in `BopVisualEffects/Effects/<EffectName>/` that implements `IVisualEffectDefinition`.
2. Fill in `Id` (lowercase, with spaces), `DisplayName` (human-readable), `ConfigKey` (PascalCase, used for config file entries) and `Description`.
3. Implement `CreateTemplate` so the effect appears in the editor template list.
4. Add a dedicated runtime runner `MonoBehaviour` for the effect (in the same file or a sibling file).
5. Implement `TrySchedule` to read entity properties and spawn your runner through `EffectRuntimeController.Instance.SpawnRunner<T>(...)`.
6. Register the effect once in `EffectDefinitionRegistry.Initialize(...)` using `Register(new YourEffect())`.
7. Record a short `.mp4` preview video showing the event selected with its properties and the effect being applied in the editor. Upload it to GitHub (e.g. by attaching it to a PR comment or issue) and copy the resulting link.
8. Update `docs/effects/README.md` and include the uploaded video link under the **Preview** section for the new effect.

### Pull requests

Formatting, code style rules and analyzer rules are enforced on Pull Requests.
To enable auto-format on commit locally, run the attached script once after cloning the repository.

Windows:
```cmd
scripts/setup-git-hooks.bat
```

Linux/macOS:
```bash
./scripts/setup-git-hooks.sh
```
