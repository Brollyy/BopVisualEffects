# Copilot Instructions for BopVisualEffects

## Repository purpose
- This repository contains a BepInEx 5.x mod for Bits & Bops.
- The mod adds additional visual effects for the in-game editor.

## Technology and runtime
- Language: C#
- Target framework: `net472`
- Mod framework: BepInEx 5.x
- Patch framework: Harmony
- Unity game: Bits & Bops

## Project structure
- Main plugin entrypoint: `BopVisualEffects/BopVisualEffectsPlugin.cs`
- Project file: `BopVisualEffects/BopVisualEffects.csproj`
- Features: `BopVisualEffects/Features`
- Patches: `BopVisualEffects/Patches`
- Shared services: `BopVisualEffects/Services`
- Game library list: `BopVisualEffects/GameDependencies.props`
- Local machine config (not committed): `BopVisualEffects/BopVisualEffects.user.props`

## Build and validation commands
- Local build:
  - `dotnet build BopVisualEffects.sln`
- CI-style build (no local game references):
  - `dotnet build BopVisualEffects.sln -c Release -p:SkipGameReferences=true`
- Formatting/style/analyzer checks:
  - `dotnet format whitespace BopVisualEffects.sln --verify-no-changes --exclude BopVisualEffects/obj`
  - `dotnet format style BopVisualEffects.sln --verify-no-changes --exclude BopVisualEffects/obj`
  - `dotnet format analyzers BopVisualEffects.sln --verify-no-changes --exclude BopVisualEffects/obj`

## Formatting and style rules
- Follow `.editorconfig` and `.gitattributes`.
- C# files use tabs.
- `*.csproj`, `*.props`, `*.targets` use 2-space indentation.
- Use LF line endings.

## BepInEx and Unity reference rules
- Do not hardcode machine-specific game paths in committed files.
- Keep `BopVisualEffects/BopVisualEffects.user.props` local-only.
- Keep game-specific assembly names in `BopVisualEffects/GameDependencies.props`.
- Respect `SkipGameReferences` behavior for CI builds.

## Logging and settings conventions
- Main plugin class should use `BaseUnityPlugin.Logger`.
- Use `LogService` for class-scoped logs:
  - `LogService.Initialize(Config, "...")` in plugin startup.
  - `LogService.GetForClass<T>()` in other classes.
- Keep log messages concise and actionable.

## When acting as a standalone implementation agent
- Prefer small, focused changes.
- Keep changes consistent with existing architecture and naming.
- Update docs/workflows when behavior changes.
- Run relevant validation commands after edits.
- Do not introduce unrelated refactors.

## When acting as a code reviewer
- Prioritize findings in this order:
  1. Behavioral bugs/regressions
  2. Broken build/CI/workflow behavior
  3. Unity/BepInEx runtime risks (missing refs, incorrect patching lifecycle)
  4. Maintainability and style issues
- For each finding, include:
  - severity
  - file path
  - concise explanation
  - concrete fix suggestion
- Call out missing tests/check coverage when relevant.

## Pull request and release workflow expectations
- PRs to `master` are expected to pass:
  - build check
  - whitespace/style/analyzer checks
- Version release is handled via GitHub Actions workflow:
  - `.github/workflows/release-new-version.yml`
- Release flow expects environment-gated deployment before tagging/publishing.
