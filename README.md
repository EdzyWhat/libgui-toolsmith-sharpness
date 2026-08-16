# LibGUI Toolsmith Sharpness

A small, **client-side** [Vintage Story](https://www.vintagestory.at/) mod that teaches
[LibGUI](https://mods.vintagestory.at/libgui) item slots to show two
[Toolsmith](https://mods.vintagestory.at/toolsmith) stats they'd otherwise drop:

1. **Sharpness** — draws each tool's Toolsmith sharpness as a thin bar **just above the durability
   bar**, coloured to match whatever sharpness-display mode the player has Toolsmith set to. A
   freshly-forged tool (which can be honed for **free** before first use) gets a gentle "sharpen me"
   nudge, and a dull edge gets a legible, escalating warning treatment.
2. **Weakest-component durability** — fixes the durability bar for multi-part (tinkered) tools so it
   reflects the part **closest to breaking** (head / handle / binding) instead of just the tool head.
   A chert shovel with a near-full head but a binding at 5/30 now reads ~17% and red — a warning the
   tool is about to snap — instead of a misleadingly full green bar.

Both work on the HudUI hotbar **and** the PlayerInvUI inventory, creative, and crafting grids — from a
single patch. Read on for how one patch reaches all of them.

> **Status:** functional and in use (see the [ModDB page](https://mods.vintagestory.at/show/mod/64207)).
> This mod Harmony-patches another mod's compiled UI method, so it is inherently version-sensitive —
> see [Compatibility](#compatibility--version-sensitivity) before updating LibGUI, HudUI, PlayerInvUI,
> or Toolsmith.

## Why this mod exists

Toolsmith draws its own sharpness bar and multi-part durability bar on **vanilla** item slots, which
Vintage Story paints directly with Cairo. LibGUI (and the HUD/inventory reskins built on it, HudUI and
PlayerInvUI) *replaces* that slot rendering wholesale with its own widget system — and it knows nothing
about Toolsmith. So the moment you install a LibGUI-based UI, Toolsmith's sharpness bar disappears and
the durability bar silently reverts to showing only the tool head. This mod is the bridge that puts
both back.

## The one-patch design (and why it's dependency-agnostic)

The core idea is that **every LibGUI slot UI renders through one method**:
`Gui.Widgets.Inventory.ItemSlotOverlay.Build`. HudUI's hotbar and PlayerInvUI's grids both build their
slots as `FlatItemSlot` → `ItemSlotOverlay`. So a single Harmony postfix on that one method makes the
sharpness and durability changes appear **everywhere at once** — we never patch HudUI or PlayerInvUI
directly, and we never have to care which of them (or which future LibGUI UI) is installed.

That "don't care what else is installed" principle runs all the way through the design. The mod is
deliberately **agnostic about its neighbours**:

- **No reference to `Toolsmith.dll`.** Sharpness and component durability are read straight off the
  itemstack's tree attributes (`toolSharpnessCurrent` / `toolSharpnessMax`, `tinkeredTool*Durability`),
  which Toolsmith keeps server-authoritative but syncs to the client. Toolsmith tools are detected by
  their behaviour **class names**, and Toolsmith's live display config is read by **reflection** — all
  so the mod compiles and ships without linking Toolsmith's assembly.
- **No side effects in the render path.** We never call Toolsmith's `Get*Sharpness()` / `Get*Durability()`
  extension helpers: several of them *lazily initialise or repair* attributes as a side effect, which
  must never happen while drawing a frame. We only read raw values (and the two vanilla durability calls
  LibGUI already makes on the same stack).
- **No reference to HudUI / PlayerInvUI, and no bundled DLLs.** The only compiled dependency is
  LibGUI's `Gui.dll`, referenced at compile time only (`Private=false`) — the installed `gui` mod
  provides it at runtime, and we never redistribute it.
- **Optional deps handled by omission.** The bar shows on whichever LibGUI slot UIs happen to be
  installed. With none installed, the patched method is simply never reached and vanilla rendering is
  untouched (vanilla slots are Cairo-drawn — this patch can't affect them).

The upshot: the mod sits cleanly *between* LibGUI and Toolsmith without being entangled in either's
internals. That same decoupling is what makes it straightforward to hand off — or to fold directly
into Toolsmith (see [Incorporating into Toolsmith](#incorporating-into-toolsmith)).

## How it works

- **Reading the stats** (`src/Toolsmith/`). `SharpnessReader` pulls the sharpness ratio off the stack;
  `DurabilityReader` computes the weakest-component durability ratio, mirroring Toolsmith's own GUI
  logic (`TinkeringUtility.FindLowestCurrent/MaxDurabilityForBar` — the independent minimum current and
  minimum max across the three parts). A freshly-crafted tool has no sharpness attributes yet (Toolsmith
  writes them lazily on first hover), so the reader recognises that state and reports a deterministic
  fresh ratio, letting the bar appear immediately and self-correct once the real value is written.
- **The bar** (`src/Compat/SharpnessBar.cs`). Mirrors LibGUI's own `DurabilityBar` geometry (a 3px dark
  track + a ratio-driven coloured fill) so the two read as a matched pair when stacked. It colours the
  fill to match the player's Toolsmith display mode — flat bands, a gradient ramp, or five flat sections
  — by reflecting Toolsmith's config (`src/Toolsmith/ToolsmithSharpnessConfig.cs`) and reproducing
  Toolsmith's exact palettes (`src/Toolsmith/SharpnessPalette.cs`). It reads like Toolsmith's own bar,
  just relocated onto the LibGUI slot.
- **Two extra affordances** on top of that Toolsmith-faithful fill:
  - *Always legible when dull.* The track keeps a faint themed outline so a near-empty sharpness is
    still readable, escalating to the theme's error colour when the tool is critically dull.
  - *Fresh-tool nudge.* A just-crafted tool (still at full durability) that isn't fully sharp gets a
    faint, slowly-breathing hint (`src/Compat/SharpnessGhostPulse.cs`) in the unsharp space — a reminder
    to use the free first hone. It self-clears the instant the tool is used.
- **The "keen" state.** Unlike standalone Toolsmith, which *hides* the bar at 100% sharp, this mod keeps
  the bar visible and renders a distinct sweeping-shimmer "keen" treatment (`src/Compat/SharpnessKeenSweep.cs`)
  at full sharpness. Rationale: on a LibGUI slot a *missing* bar reads as "no info", not "sharp" — so the
  always-present bar means the player never has to hover a tool just to confirm its edge still holds.
- **The durability fix.** LibGUI's stock bar only knows the tool head, so a full head hides a dying
  binding. `DurabilityReader` reports `minCurrent / minMax` across all three parts instead.
- **Injection** (`src/Compat/ItemSlotSharpnessPatch.cs`). A Harmony **postfix** on `ItemSlotOverlay.Build`.
  For a Toolsmith tool it rebuilds the overlay's `ItemSlotOverlayStack`, swapping LibGUI's head-only
  durability bar for the weakest-component one (reusing LibGUI's own `DurabilityBarKey` and colour ramp)
  and appending the sharpness bar lifted a few pixels above it. Non-Toolsmith items are left untouched.

## Dependencies

**Required** (declared in `src/modinfo.json`):

| Mod | Min version | Why |
|-----|-------------|-----|
| Vintage Story | 1.22.0 | Base game |
| `gui` (LibGUI) | 3.0.0 | The widget framework this mod patches and builds against |
| `toolsmith` | 1.2.18 | Source of the sharpness / multi-part durability stats |

**Optional** (intentionally *not* declared — the bar appears on whichever slot UIs are installed):

| Mod | Effect when present |
|-----|---------------------|
| [`hudui`](https://mods.vintagestory.at/hudui) | Sharpness/durability bars show on the hotbar |
| [`playerinvui`](https://mods.vintagestory.at/playerinvui) | ...and in the inventory / creative / crafting grids |

Vintage Story's `modinfo` has no "optional dependency" flag, so optional deps are handled by simply not
listing them. No code guard is needed: the patched `ItemSlotOverlay` is a LibGUI type that vanilla never
uses, so with neither UI mod installed the patch is inert.

## Project layout

The source is split along the seam that matters for reuse:

```
src/
├── Compat/        The LibGUI bridge — the irreducible part (the Harmony patch + the widgets).
│                  Namespace LibGuiToolsmithSharpness.Compat.
├── Toolsmith/     Read-only shims over Toolsmith's data + config, written to need NO Toolsmith.dll.
│                  Namespace LibGuiToolsmithSharpness.Toolsmith. Deleted on a fold-in (see below).
└── SharpnessBarsModSystem.cs   Client-only entry point (applies / removes the patch).
```

`Compat` depends on `Toolsmith`, never the reverse — so folding this into Toolsmith means deleting the
`Toolsmith/` layer and pointing its call sites at Toolsmith's own APIs, while `Compat/` ports over
unchanged. See [`INTEGRATION.md`](INTEGRATION.md).

## Build & playtest

Requires the **.NET 10 SDK** and a local Vintage Story install. Point `VINTAGE_STORY` at the install if
it isn't at the macOS default (`/Applications/Vintage Story.app`). You also need `lib/Gui.dll` present
locally as a compile-time reference — it is **not** committed (it's another author's binary); see
[`lib/README.md`](lib/README.md) for how to obtain it.

```sh
# Build + stage into the local Mods folder, then restart the game / reload mods.
build/restage.sh            # Release
build/restage.sh Debug      # Debug

# Build a distributable release zip (version read from modinfo.json).
build/release.sh
```

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the full developer workflow, including how to decompile the
patched mod DLLs to re-verify the patch surface.

## Compatibility & version-sensitivity

This mod Harmony-patches **another mod's compiled `Build` method**, which is inherently fragile: a
LibGUI, HudUI, or PlayerInvUI update that restructures `ItemSlotOverlay` — or a Toolsmith update that
changes its attribute keys or config fields — can break the patch. Two safety nets are built in:

- On startup the mod logs whether the patch actually attached, and warns loudly if
  `ItemSlotOverlay.Build` wasn't found (`[libguitoolsmithsharpness] No methods were patched...`).
- The Toolsmith reflection layer fails soft: if a config field moves or is renamed, the bar falls back
  to Toolsmith's own defaults rather than throwing.

If a bar stops appearing after an update, check the client log for that warning first, then re-verify the
patch surface against the freshly decompiled DLLs (see `CONTRIBUTING.md`).

## Incorporating into Toolsmith

Toolsmith's author is welcome to fold this in. Because the `Toolsmith/` shim layer exists *only* because
this is an external mod, a fold-in actually **deletes about half the code** and replaces it with direct
Toolsmith API calls — while the `Compat/` LibGUI bridge moves over as an optional, `gui`-gated submodule.
[`INTEGRATION.md`](INTEGRATION.md) walks through exactly what to keep, what to delete, and the pattern for
keeping the LibGUI code optional so Toolsmith users without LibGUI are unaffected.

## Credits & license

- **[Toolsmith](https://mods.vintagestory.at/toolsmith)** by JonR — the sharpness/tinkering system this
  mod surfaces. Sharpness palettes and the weakest-component durability logic are reproduced from
  Toolsmith to match its look exactly.
- **[LibGUI](https://mods.vintagestory.at/libgui)** — the widget framework this mod patches and builds on.
- This mod by **RaptorKhan**.

Licensed under the [MIT License](LICENSE). Toolsmith and LibGUI are the property of their respective
authors and are not redistributed here.
