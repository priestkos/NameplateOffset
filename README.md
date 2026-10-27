# Nameplate Offset

Dalamud dev plugin that raises (or lowers) the overhead nameplate of a game object.

## Why it exists

FFXIV has no vanilla control for nameplate height. The plate is anchored in world space at the
object's `GetNamePlateWorldPosition()` — bottom-centre of the plate — which is computed from:

    character position + NameplateOffset (rotated by actor rotation) + (0, NameplateOffsetScaleMultiplier * Scale, 0)

`NameplateOffset` is interpolated every frame toward `NameplateOffsetTarget`. Writing
`NameplateOffsetTarget.Y` (and `NameplateOffset.Y` to skip the lerp) is what the client itself does
for mount/transform offsets. `MidoriKami/VanillaPlus` uses the same field in its
`DutyPortalNameplateOffset` feature.

## Behaviour

- `Configuration.Offset` in yalms (1 yalm = 1 m). Slider `/npoffset` or `/npoffset 0.35`.
- Per-object baseline tracking: if `NameplateOffsetTarget.Y` no longer holds what we wrote, the game
  just rewrote it (spawn, mount, transform) — adopt it as the new baseline instead of fighting it.
- Scopes: own character / other players / NPCs & objects, independently.
- `Dispose()` and every config change restores the captured baselines, so unloading leaves plates
  where the game wants them. Restore is guarded by `IObjectTable.CreateObjectReference` so a freed
  object pointer is never dereferenced.

## Build

Requires the Dalamud dev libs (`%APPDATA%\XIVLauncher\addon\Hooks\dev`) and .NET SDK 10.

    dotnet build -c Release

## Install (dev plugin)

1. `%APPDATA%\XIVLauncher\devPlugins\NameplateOffset\` must contain `NameplateOffset.dll` **and**
   `NameplateOffset.json` — manifests are required for dev plugins since Dalamud dropped the
   DLL-only scan; a DLL with no matching-stem JSON is logged as "no longer valid" and ignored.
2. `/xlplugins` (opening the installer runs `ScanDevPluginsAsync`; there is also a
   "Scan Dev Plugins" button) → enable **Nameplate Offset**.
3. `/npoffset` for the slider.

`InternalName` in the manifest must equal the DLL filename stem.
