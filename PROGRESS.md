# SenealCapeOnLogin — Valheim 1.0.21 performance pass

## Goal
- Confirm compatibility with latest Valheim (1.0.21).
- Reduce reflection/retry cost on login without changing cape restore behavior.

## Status
- **Completed** (compile/API verification; not an in-game FPS capture)

## Acceptance
- Patches match 1.0.21: `Player.Load(ZPackage)`, `Player.OnSpawned(bool)`, `Player.OnRespawn()`, `Humanoid.m_shoulderItem`.
- Release build 1.0.4, Thunderstore zip in `/dist`.

## Files changed
- `src/Plugin.cs` — cached SeneaL delegates/field refs, generation-token retry, `OnSpawned(bool)`, `CapeSlot.IsCape`.
- Version/docs: csproj, Package/manifest, changelogs, READMEs.

## Commands
- `dotnet build -c Release` — succeeded, 0 warnings, 0 errors.
- Installed `assembly_valheim.dll` last write: 2026-10-28 (matches 1.0.21).

## Output
- DLL: `D:\AI-Agent-Projects\cjaycraft\Cjayride.SenealCapeOnLogin\bin\Release\net481\Cjayride.SenealCapeOnLogin.dll` (FileVersion 1.0.4.0)
- Zip: `D:\AI-Agent-Projects\cjaycraft\Cjayride.SenealCapeOnLogin\dist\cjayride-SenealCapeOnLogin-1.0.4.zip`

## Next
- Optional in-game login check with SeneaL UI + Adventure Backpacks.
