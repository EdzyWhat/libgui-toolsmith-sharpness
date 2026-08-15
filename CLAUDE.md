# LibGUI Toolsmith Sharpness

Client-side Vintage Story 1.22.x mod (targets .NET 10). A Toolsmith<->LibGUI compatibility patch
that does two things on LibGUI item slots (HudUI hotbar + PlayerInvUI inventory) via ONE Harmony
patch: (1) draws a "sharpness" bar above the durability bar (coloured to match the player's Toolsmith
mode, with a fresh-tool "sharpen me" nudge and a legible dull-state treatment), and (2) fixes the durability
bar for tinkered tools to show the weakest component (closest to breaking) instead of just the
tool head. See `README.md` for the full picture.

## Architecture (one project)

- `src/SharpnessReader.cs` — reads Toolsmith's `toolSharpnessCurrent` / `toolSharpnessMax` raw
  stack attributes. NO Toolsmith.dll reference; NEVER call Toolsmith's `Get*Sharpness()`
  extensions (they lazily initialise sharpness as a side effect - forbidden in a render path).
- `src/DurabilityReader.cs` — computes the weakest-component durability ratio for tinkered tools,
  mirroring Toolsmith's `FindLowestCurrent/MaxDurabilityForBar` (min current & min max across
  head/handle/binding, taken independently). Reads raw `tinkeredTool*Durability` attributes and
  only the two vanilla collectible calls LibGUI already makes on the same stack (`GetMaxDurability`
  / `GetRemainingDurability` for the head) — no side effects, no Toolsmith.dll. NEVER call
  Toolsmith's `Get*Durability()` extensions (several lazily reset/repair attributes).
- `src/SharpnessBar.cs` — a `StatelessWidget` mirroring LibGUI's `DurabilityBar` geometry. Colours
  the fill to match the player's Toolsmith mode (flat bands / gradient / 5 sections), keeps a faint
  themed track outline for legibility at near-zero sharpness (escalating to `ColorScheme.Error` when
  critically dull), and hosts the fresh-tool hint. Only drawn when sharpness < max (Toolsmith's own
  "no bar when keen" convention), so a missing bar means fully sharp.
- `src/SharpnessPalette.cs` — Toolsmith's exact sharpness colour maths (hex palettes copied verbatim,
  fed through VS `ColorUtil.Hex2Int`/`ColorOverlay`/`ToRGBAFloats`). No Toolsmith.dll reference.
- `src/ToolsmithSharpnessConfig.cs` — reflects the player's live Toolsmith display config
  (`ToolsmithModSystem.ClientConfig.UseGradientForSharpnessInstead` / `ShowAllSharpnessBarSections`
  + `GradientSelection`) to pick the render mode. Fails soft to Toolsmith's defaults (flat bands).
- `src/SharpnessGhostPulse.cs` — a `StatefulWidget` that draws the "sharpen me" hint on a
  freshly-crafted tool: a faint `ColorScheme.Primary` bar in the unsharp negative space, breathing
  ~1.8s then resting ~3s (via an `AnimationController` looped on `Completed`, ticker from
  `context.GetTickerProvider()`). Fresh = sharpness < max AND durability pristine; self-clears on use.
- `src/ItemSlotSharpnessPatch.cs` — Harmony postfix on `Gui.Widgets.Inventory.ItemSlotOverlay.Build`;
  swaps the head-only durability bar for a weakest-component one AND appends the sharpness bar.
- `src/SharpnessBarsModSystem.cs` — client-only entry point; `PatchAll` / `UnpatchAll`.

## Guardrails

- **All slot UIs route through LibGUI's `ItemSlotOverlay`.** Patch that one method, not each
  mod. HudUI/PlayerInvUI expose slots via `FlatItemSlot` -> `ItemSlotOverlay`.
- **Compile-time references are only** the game (`VintagestoryAPI`/`Lib`), `0Harmony`,
  `OpenTK.Mathematics` (all `Private=false`, from the install's root + `Lib/`), and vendored
  `lib/Gui.dll` (`Private=false` — the installed `gui` mod provides it at runtime). Do NOT add
  HudUI/PlayerInvUI/Toolsmith references or ship any of those DLLs.
- **Preserve the return type/key in the postfix.** Rebuild an `ItemSlotOverlayStack` (not a plain
  `Stack`) with the same `ItemStack`/`SlotSize` so LibGUI reconciliation stays stable per frame.
- **Version-sensitive.** This Harmony-patches other mods' compiled `Build`. Re-verify against the
  decompiled `Gui.dll` / `HudUI.dll` / `PlayerInvUI.dll` after any of those mods update.

## Reference: decompiling the mod DLLs

The ground truth for the patch surface is the decompiled mod source. Decompile with
`ilspycmd` (`~/.dotnet/tools/ilspycmd`, add to PATH):

```sh
ilspycmd -p -o /tmp/gui  <path-to>/Gui.dll        # ItemSlotOverlay, DurabilityBar, widgets
ilspycmd -p -o /tmp/piu  <path-to>/PlayerInvUI.dll
ilspycmd -p -o /tmp/hud  <path-to>/HudUI.dll
```

Key facts already established: durability + sharpness bars live in `ItemSlotOverlay.Build`
(LibGUI), which returns a flat `ItemSlotOverlayStack`; `MultiChildWidget.Children` is public;
`Theme.Of(context).ItemSlotStyle.Padding` is the slot's edge inset.
