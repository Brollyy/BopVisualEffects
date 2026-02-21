# BopVisualEffects

BepInEx 5.x mod for Bits & Bops that adds additional visual effects for the in-game editor.

## Available effects

See [this document](docs/effects/README.md) for the full list of effects this mod enables.

## Configuration

Each effect can be individually enabled or disabled in the BepInEx config file (`BepInEx/config/com.brollyy.bopvisualeffects.cfg`), under the `[Effects]` section.

The config file is located in your Bits & Bops game folder. It is generated automatically on first load. To disable an effect, set its entry to `false`:

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
7. Add media for documentation:
   - `docs/effects/<effect-id>/preview.gif`
   - `docs/effects/<effect-id>/demo.mp4`
8. Update `docs/effects/README.md`.

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
