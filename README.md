# LibGUI Toolsmith Sharpness

A small **client-side** Vintage Story mod that makes [LibGUI](https://mods.vintagestory.at/libgui)
item slots respect two [Toolsmith](https://mods.vintagestory.at/toolsmith) stats:

1. It draws the per-tool **sharpness** stat as a **bar just above the durability bar**, coloured to
   match the player's Toolsmith sharpness-display mode, with an extra nudge to sharpen freshly-forged
   tools (free before first use) and a legible treatment when a tool is dull.
2. It fixes the **durability bar** for multi-part (tinkered) tools so it shows the component
   **closest to breaking** (head / handle / binding) instead of just the tool head — matching what
   standalone Toolsmith shows. A chert shovel with a near-full head but a binding at 5/30 now reads
   ~17% (red), warning you the tool is about to break, instead of a misleadingly full green bar.

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
  ratio-driven coloured fill, same 3px geometry), sitting just above it. It colours the fill to
  match **whatever sharpness display mode the player has Toolsmith set to** — flat colour bands, a
  gradient ramp, or five flat sections — by reflecting Toolsmith's client config
  (`src/ToolsmithSharpnessConfig.cs`) and reproducing Toolsmith's exact colour palettes
  (`src/SharpnessPalette.cs`), so the bar reads like Toolsmith's own, just relocated onto the LibGUI
  slot. Following Toolsmith's convention, the bar only appears when a tool is **not** fully sharp;
  a missing bar means "keen".
- **Two extra affordances** layered on top of that Toolsmith-faithful fill:
  - *Always legible when dull.* The dark track keeps a faint themed outline (`ColorScheme.OutlineVariant`)
    so an empty / near-empty sharpness is still readable, escalating to the theme's `Error` colour
    when the tool is critically dull.
  - *Fresh-tool nudge.* A freshly-crafted tool head can be sharpened for **free** before its first
    use, so `src/SharpnessGhostPulse.cs` fills the unsharp negative space with a faint, slowly
    breathing `ColorScheme.Primary` hint. It shows only while the tool is still pristine (full
    durability) and below full sharpness, and self-clears the instant the tool is used.
- **Durability fix:** `src/DurabilityReader.cs` mirrors Toolsmith's own GUI logic
  (`TinkeringUtility.FindLowestCurrent/MaxDurabilityForBar`): it takes the minimum current and
  minimum max durability across the three tool parts (head from the collectible, handle/binding
  from raw `tinkeredTool*Durability` attributes) and reports `minCurrent / minMax`. LibGUI's stock
  bar only knows the head, so a full head hides a dying binding.
- **Injection:** `src/ItemSlotSharpnessPatch.cs` is a Harmony **postfix** on
  `ItemSlotOverlay.Build`. For a Toolsmith tool it rebuilds the overlay's `ItemSlotOverlayStack`:
  it swaps LibGUI's head-only durability bar for one driven by the weakest component (reusing
  LibGUI's `DurabilityBarKey` and colour ramp) and appends the blue sharpness bar lifted a few
  pixels above it. Non-Toolsmith items are left untouched.

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
