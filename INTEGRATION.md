# Folding this into Toolsmith

This guide is for the Toolsmith author (or anyone) who wants to absorb this LibGUI compatibility patch
directly into Toolsmith instead of shipping it as a separate mod. It explains what changes, what stays,
and — most importantly — how to keep the LibGUI code **optional** so Toolsmith users who don't run LibGUI
are completely unaffected.

There is a companion `toolsmith-integration` branch that annotates the source with `// [FOLD-IN]` markers
matching the steps below.

## TL;DR

- This mod is deliberately split into two layers: `src/Compat/` (the LibGUI bridge) and `src/Toolsmith/`
  (read-only shims that only exist because this is an *external* mod).
- **Folding in deletes the `src/Toolsmith/` layer entirely** and replaces its call sites with direct
  Toolsmith API calls. That's roughly half the code, gone — along with a real drift/version-fragility
  risk (copied palettes, re-declared attribute keys, reflection).
- **`src/Compat/` ports over almost verbatim.** It's the irreducible part: the Harmony patch on LibGUI's
  `ItemSlotOverlay.Build`, plus the bar widgets. It must live behind a `gui`-gated `ModSystem`.
- The **one new burden** Toolsmith takes on is a compile-time reference to `Gui.dll` and the discipline
  of keeping every LibGUI-touching type isolated so it's never reached when `gui` isn't installed.

## Layer 1 — `src/Toolsmith/` → delete and replace with direct calls

These four files reimplement, from the outside, things Toolsmith already has internally. Inside Toolsmith
you delete them and call the real thing:

| File / member | What it does now (external) | Replace with (inside Toolsmith) |
|---------------|-----------------------------|----------------------------------|
| `ToolsmithSharpnessConfig.Read()` | Reflects `ToolsmithModSystem.ClientConfig.UseGradientForSharpnessInstead` / `ShowAllSharpnessBarSections` + `GradientSelection` | Read those fields directly — collapses to a few lines |
| `SharpnessPalette` | Copies Toolsmith's hex palettes verbatim and re-derives colours via `ColorUtil` | Call `TinkeringUtility`'s colour methods directly (the real source of truth — removes the copied-hex drift risk) |
| `SharpnessReader` | Detects Toolsmith tools by behaviour **class-name string match**; reads raw `toolSharpness*` attributes; guesses the fresh-tool ratio | Use real `is CollectibleBehaviorToolHead` / `...SmithedTools` / `...TinkeredTools` type checks and Toolsmith's own `ToolsmithAttributes` constants |
| `DurabilityReader` | Re-declares `tinkeredTool*Durability` keys; re-implements the weakest-component min logic | Call Toolsmith's own `TinkeringUtility.FindLowestCurrentDurabilityForBar` / `FindLowestMaxDurabilityForBar` |

### The one rule that does NOT relax on fold-in

Even inside Toolsmith you **must not trigger lazy initialisation from the render path**. The external
readers avoid Toolsmith's `Get*Sharpness()` / `Get*Durability()` extension helpers precisely because
several of them lazily *write* / *reset* / *repair* attributes as a side effect — fine from a tooltip or
tick, fatal from a per-frame widget `Build`. When you replace the shims with real calls, use (or add) a
**pure read** path:

- For sharpness, either read the raw `toolSharpness*` attributes as now, or expose a `TryPeekSharpness`
  that returns without initialising. Keep the fresh-tool handling (a just-crafted tool has no sharpness
  attribute until first hover) — inside Toolsmith you may prefer to initialise sharpness at craft time and
  drop the `0.66` fallback heuristic entirely.
- For durability, `FindLowest*` are pure reads; the head still comes from the vanilla
  `GetMaxDurability` / `GetRemainingDurability` that LibGUI already calls on the same stack.

## Layer 2 — `src/Compat/` → keep, behind a `gui`-gated ModSystem

`ItemSlotSharpnessPatch`, `SharpnessBar`, `SharpnessKeenSweep`, `SharpnessGhostPulse`, and the
`SharpnessBarsModSystem` entry point port over essentially unchanged. The only structural change is that
inside Toolsmith they become an **optional submodule** rather than the whole mod.

### Keeping LibGUI optional (the crucial part)

Toolsmith today has **no** LibGUI dependency and most Toolsmith users don't run LibGUI. Absorbing this code
must not change that. The .NET runtime resolves a type only when a method that references it is **JIT-compiled**
(i.e. first called) — so as long as every `Gui.*`-referencing type is reached *only* when `gui` is actually
loaded, a LibGUI-less client never trips over the missing assembly.

Concretely:

1. **Isolate.** Keep all `Gui.*`-referencing code in its own namespace/folder (the `Compat/` layer as-is).
   Never reference those types from Toolsmith's core code paths.
2. **Gate the ModSystem.** Give the compat submodule its own `ModSystem` whose `ShouldLoad` returns false
   unless LibGUI is present:

   ```csharp
   public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

   public override void StartClientSide(ICoreClientAPI api)
   {
       if (!api.ModLoader.IsModEnabled("gui")) return;   // no LibGUI -> never touch Gui.* types
       _harmony = new Harmony(HarmonyId);
       _harmony.PatchAll(typeof(ThisModSystem).Assembly);
       // ...log whether ItemSlotOverlay.Build was actually patched (see SharpnessBarsModSystem)
   }
   ```

   > If Toolsmith ships everything in one assembly, prefer `Harmony.CreateAndPatchAll(type)` /
   > per-method `Patch(...)` targeting only the LibGUI patch class, rather than `PatchAll(assembly)`, so
   > Harmony doesn't scan/JIT the LibGUI patch attributes on a `gui`-less client.

3. **Do NOT hard-depend on `gui`.** Leave `gui` out of Toolsmith's `modinfo.json` dependencies. Add it
   only as a compile-time reference in the build (`Private=false`, exactly as `src/Mod.csproj` does now).
4. **Guard the Harmony target.** `[HarmonyPatch(typeof(ItemSlotOverlay), "Build")]` is only ever applied
   from inside the gated `StartClientSide`, so the type is resolved only when `gui` is loaded.

### Two renderers, one stat

After the fold-in Toolsmith owns **two** sharpness renderers for the same stat: its existing vanilla
(Cairo) bar and this LibGUI widget bar. Keep them loosely coupled — they share the *data* (`FindLowest*`,
the sharpness ratio, the palettes) but not their drawing code. This is also where you'd diverge the look
if you want a more vanilla-flavoured treatment on the Cairo path vs. the widget path.

## Build changes for Toolsmith

- Add a compile-time `Gui.dll` reference (`Private=false`) — see `src/Mod.csproj` and `lib/README.md`.
- No new runtime dependency and no new shipped DLLs: `gui` provides `Gui.dll` at runtime, and it's only
  ever touched when installed.
- Drop the `SkiaSharp` reference concern entirely — this mod uses none (the fancy shader-based keen effect
  was removed; the keen state is a plain animated gradient).

## Checklist

- [ ] Copy `src/Compat/` into Toolsmith under an isolated namespace.
- [ ] Replace `src/Toolsmith/` shim calls with direct Toolsmith API calls (table above); delete the shims.
- [ ] Add a `gui`-gated client `ModSystem` (`IsModEnabled("gui")`) that applies the Harmony patch.
- [ ] Keep the fresh-tool + no-side-effect-in-render behaviour (use a pure read path).
- [ ] Add the compile-time `Gui.dll` reference; do **not** add `gui` to `modinfo` dependencies.
- [ ] Verify Toolsmith still loads and behaves identically on a client with **no** LibGUI installed.
- [ ] Verify the bars appear on the HudUI hotbar and PlayerInvUI grids with LibGUI installed.
