# LibGUI Toolsmith Sharpness

A small **client-side** Vintage Story mod that visualises the [Toolsmith](https://mods.vintagestory.at/toolsmith)
mod's per-tool **sharpness** stat as a **blue bar just above the green durability bar** on item slots.

Because [HudUI](https://mods.vintagestory.at/hudui) and [PlayerInvUI](https://mods.vintagestory.at/playerinvui)
both render their slots through [LibGUI](https://mods.vintagestory.at/libgui)'s
`Gui.Widgets.Inventory.ItemSlotOverlay`, a single Harmony patch on that one method makes the
sharpness bar appear **everywhere**: the HudUI hotbar *and* the PlayerInvUI inventory / creative
/ crafting grids.

## How it works

- **Data:** Toolsmith stores sharpness as two flat integer stack attributes,
  `toolSharpnessCurrent` / `toolSharpnessMax`. They are server-authoritative but synced to the
  client, so this mod reads them straight off the itemstack on the client with no server
  round-trip and no reference to Toolsmith.dll. See `src/SharpnessReader.cs`.
- **Bar:** `src/SharpnessBar.cs` mirrors LibGUI's own `DurabilityBar` (a dark track + a
  ratio-driven coloured fill, same 3px geometry) in a distinct blue ramp.
- **Injection:** `src/ItemSlotSharpnessPatch.cs` is a Harmony **postfix** on
  `ItemSlotOverlay.Build`. When the slot holds a Toolsmith tool it rebuilds the overlay's
  `ItemSlotOverlayStack` with one extra bottom-aligned bar, lifted a few pixels above the
  durability bar. Non-Toolsmith items are left untouched.

## Dependencies

**Required** (declared in `modinfo.json`):

| Mod | Min version | Why |
|-----|-------------|-----|
| Vintage Story | 1.22.0 | Base game |
| `gui` (LibGUI) | 3.0.0 | The widget framework this mod patches / builds against (its types are loaded at startup) |
| `toolsmith` | 1.2.18 | Source of the sharpness stat |

**Optional** (not declared — the bar appears on whichever slot UIs are installed):

| Mod | Why optional |
|-----|--------------|
| `hudui` | Renders the hotbar via LibGUI slots. Present → bar shows on the hotbar. |
| `playerinvui` | Renders the inventory via LibGUI slots. Present → bar shows in the inventory. |

The patch targets LibGUI's shared `ItemSlotOverlay`, which **vanilla never uses** (vanilla slots
are Cairo-drawn), so with neither UI mod installed the patch is simply dormant and never touches
vanilla rendering. Vintage Story's `modinfo` has no "optional dependency" flag, so optional deps
are handled by *not* listing them; no code guard is needed here because the patch is inert
without a LibGUI slot surface.

## Build & playtest

Requires the .NET 10 SDK and a local Vintage Story install. Point `VINTAGE_STORY` at the
install if it isn't at the macOS default (`/Applications/Vintage Story.app`).

```sh
# Build + stage into the local Mods folder, then restart the game / reload mods.
build/restage.sh            # Release
build/restage.sh Debug      # Debug
```

`Gui.dll` under `lib/` is a compile-time reference only; the installed `gui` mod provides it at
runtime (never shipped from here). See `lib/README.md`.

## Status

Scaffolded and compiling against the shipped mod assemblies. In-game pixel alignment of the bar
(the lift above the durability bar) may want a tuning pass. Patching another mod's compiled
`Build` method is inherently version-sensitive: a HudUI / LibGUI / PlayerInvUI update that
restructures `ItemSlotOverlay` could require re-checking the patch.
