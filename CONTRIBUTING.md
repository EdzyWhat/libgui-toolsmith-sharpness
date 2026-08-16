# Contributing

Thanks for looking at this mod. It's a small, single-purpose compatibility patch, so the workflow is
light — but because it Harmony-patches other mods' compiled code, a couple of steps are worth doing
carefully.

## Prerequisites

- **.NET 10 SDK**
- A local **Vintage Story** install (1.22.0+). If it isn't at the macOS default
  `/Applications/Vintage Story.app`, point the `VINTAGE_STORY` environment variable at it.
- **`lib/Gui.dll`** — a compile-time reference that is *not* committed (it's another author's binary).
  Drop the `Gui.dll` from your target `gui` (LibGUI) mod release into `lib/`. See
  [`lib/README.md`](lib/README.md).

## Build & run

```sh
build/restage.sh            # Release build, staged into the local Mods folder for playtesting
build/restage.sh Debug      # Debug build
build/release.sh            # Distributable zip in dist/ (version read from src/modinfo.json)
```

`restage.sh` copies the built DLL + `modinfo.json` + `assets/` into
`~/Library/Application Support/VintagestoryData/Mods/<modid>` (override with `VINTAGESTORY_DATA`), then
restart the game or reload mods to pick up the change.

Only our own assembly and content ship. The game, Harmony, OpenTK, and `Gui.dll` are all `Private=false`
references — the game and the installed `gui` mod provide them at runtime, so we never bundle or
redistribute them.

## Code layout

See [`README.md`](README.md#project-layout) and [`CLAUDE.md`](CLAUDE.md). In short:

- `src/Compat/` — the LibGUI bridge (the Harmony patch + widgets). References `Gui.dll`.
- `src/Toolsmith/` — read-only shims over Toolsmith's data/config, written to need **no** `Toolsmith.dll`.
- `src/SharpnessBarsModSystem.cs` — the client-only entry point.

The dependency is one-directional (`Compat` → `Toolsmith`); please keep it that way. If you're looking to
fold this into Toolsmith, read [`INTEGRATION.md`](INTEGRATION.md).

## The version-sensitive part: re-verifying the patch surface

This mod patches `Gui.Widgets.Inventory.ItemSlotOverlay.Build` and reads Toolsmith attributes/config. If
LibGUI, HudUI, PlayerInvUI, or Toolsmith update, the patch or the reads can break. The ground truth is the
**decompiled** mod source. Decompile with [`ilspycmd`](https://github.com/icsharpcode/ILSpy)
(`dotnet tool install -g ilspycmd`):

```sh
ilspycmd -p -o /tmp/gui "<path-to>/Gui.dll"          # ItemSlotOverlay, DurabilityBar, the widgets
ilspycmd -p -o /tmp/piu "<path-to>/PlayerInvUI.dll"
ilspycmd -p -o /tmp/hud "<path-to>/HudUI.dll"
```

Things to confirm after an update:

- `ItemSlotOverlay.Build` still exists, still returns a flat `ItemSlotOverlayStack`, and the durability
  bar is still keyed by `ItemSlotOverlay.DurabilityBarKey`.
- `MultiChildWidget.Children` is still public; `Theme.Of(context).ItemSlotStyle.Padding` is still the
  slot's edge inset.
- Toolsmith's attribute keys (`toolSharpnessCurrent` / `toolSharpnessMax`, `tinkeredTool*Durability`) and
  its client-config fields (`UseGradientForSharpnessInstead`, `ShowAllSharpnessBarSections`,
  `GradientSelection`) are unchanged.

On startup the mod logs whether the patch attached (`[libguitoolsmithsharpness] Patched ...`) or warns if
`ItemSlotOverlay.Build` wasn't found — check the client log first when a bar goes missing.

## Guardrails (please don't regress these)

- **Never** call Toolsmith's `Get*Sharpness()` / `Get*Durability()` extension helpers from the render
  path — several lazily initialise/reset/repair attributes as a side effect. Read raw values only.
- **Never** add HudUI / PlayerInvUI / Toolsmith assembly references, and never ship their DLLs. The whole
  point is to bridge them without linking them.
- **Preserve the postfix's return type and keys** — rebuild an `ItemSlotOverlayStack` (not a plain
  `Stack`) with the same `ItemStack` / `SlotSize`, and reuse LibGUI's `DurabilityBarKey`, so LibGUI's
  frame-to-frame reconciliation stays stable.

## Reporting bugs

Please include your Vintage Story version and the exact versions of `gui`, `toolsmith`, and (if used)
`hudui` / `playerinvui`, plus the relevant lines from `client-main.log`. See the issue template.
