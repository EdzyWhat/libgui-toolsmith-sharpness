# LibGUI Toolsmith Sharpness

Client-side Vintage Story 1.22.x mod (targets .NET 10). A compatibility patch: draws a blue
"sharpness" bar (from the Toolsmith mod's per-tool stat) above the durability bar on LibGUI
item slots, covering both the HudUI hotbar and the PlayerInvUI inventory via one Harmony patch.
See `README.md` for the full picture.

## Architecture (one project)

- `src/SharpnessReader.cs` — reads Toolsmith's `toolSharpnessCurrent` / `toolSharpnessMax` raw
  stack attributes. NO Toolsmith.dll reference; NEVER call Toolsmith's `Get*Sharpness()`
  extensions (they lazily initialise sharpness as a side effect - forbidden in a render path).
- `src/SharpnessBar.cs` — a `StatelessWidget` mirroring LibGUI's `DurabilityBar` geometry.
- `src/ItemSlotSharpnessPatch.cs` — Harmony postfix on `Gui.Widgets.Inventory.ItemSlotOverlay.Build`.
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
