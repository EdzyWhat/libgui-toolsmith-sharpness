# Hey JonR

So, you found this. Our preference is to **fold it into Toolsmith** if you're open to it — and it's less work than it looks. About half the code here is a workaround for not having access to Toolsmith internals, and it just deletes itself the moment it moves inside. The actual LibGUI bridge is net new code, not a rewrite of anything you have.

That said, either path works. This doc covers both.

---

## What this does

Sharpness bar + weakest-component durability fix, on every LibGUI slot. One Harmony **postfix** on `ItemSlotOverlay.Build` — the single method HudUI and PlayerInvUI both route their slots through — covers both UIs without touching either of them directly.

Worth flagging: it's a postfix, not a transpiler. We let Build run normally, then modify the returned widget tree. No IL surgery, no instruction-sequence pattern matching — which means a LibGUI refactor that keeps the return type intact won't silently break anything.

---

## Your two options

### Option A: fold it into Toolsmith

**This is probably the move.** Here's why: about half the code here is a workaround for not having access to Toolsmith internals. The reflection on your config fields, the copied colour palettes, the behaviour-name string matching, the re-declared attribute key constants — all of that just disappears and gets replaced with direct calls to your own APIs. The actual LibGUI bridge (the Harmony patch + the bar widgets) is a net new addition, not a rewrite.

Your existing `assets/toolsmith/compatibility/` pattern is a JSON thing; this is C# because it has to patch a compiled method, but the spirit is the same — an optional module that activates only when LibGUI is present.

The new thing you'd take on: a compile-time `Gui.dll` reference and the discipline of keeping the LibGUI code unreachable when `gui` isn't installed (the JIT only resolves a type when a method referencing it is called, so "never call it without confirming LibGUI is loaded" is sufficient). There's already a `toolsmith-integration` branch in this repo that shows exactly what that looks like with `[FOLD-IN]` markers on every file.

### Option B: co-maintain the standalone

Totally valid if you'd rather not touch Toolsmith's build or add a new compile-time dependency. I'll keep the standalone working on new VS/LibGUI/Toolsmith releases. The main ask would be a heads-up when your attribute key names or config fields change, since this mod reads them by reflection and hardcoded string.

---

## Fold-in roadmap

### Step 1 — delete the shim layer (`src/Toolsmith/`)

These four files exist only because we can't reference Toolsmith.dll from the outside. On a fold-in they're gone:

| File | What it does (outside) | What replaces it (inside Toolsmith) |
|------|------------------------|--------------------------------------|
| `ToolsmithSharpnessConfig.cs` | Reflects `ToolsmithModSystem.ClientConfig` and `GradientSelection` by field name | Read those fields directly — collapses to a few lines |
| `SharpnessPalette.cs` | Copies your hex palettes verbatim; re-derives colours via `ColorUtil` | Call `TinkeringUtility`'s colour methods (removes the palette drift risk) |
| `SharpnessReader.cs` | Detects Toolsmith tools by behaviour class-name string; reads raw `toolSharpness*` attrs; guesses the fresh-tool 0.66 ratio | Real `is CollectibleBehaviorToolHead` / `...SmithedTools` type checks + your `ToolsmithAttributes` constants |
| `DurabilityReader.cs` | Re-declares your `tinkeredTool*Durability` key names; re-implements `FindLowest*` | Call `TinkeringUtility.FindLowestCurrentDurabilityForBar` / `FindLowestMaxDurabilityForBar` directly |

One rule that does **not** relax: never trigger Toolsmith's lazy-init extension helpers (`Get*Sharpness()`, `Get*Durability()`) from the render path. Several of them write or repair attributes as a side effect, which is fine from a tick but bad from a `Build` call. The shim layer was careful about this — use a pure read path, or add a `TryPeekSharpness` that peeks without initialising. (Or just init sharpness at craft time and drop the fresh-tool heuristic entirely.)

### Step 2 — keep the bridge layer (`src/Compat/`), but gate it

These files move over as-is. They live cleanly in something like `Toolsmith/Client/LibGui/`:

- `ItemSlotSharpnessPatch.cs` — the Harmony postfix. Already tagged `[HarmonyPatchCategory("toolsmith.libgui.compat")]` so your existing `PatchAll` won't sweep it up accidentally.
- `SharpnessBar.cs`, `SharpnessKeenSweep.cs`, `SharpnessGhostPulse.cs` — the bar widgets. No changes needed; they already call into the shim layer which you'll have replaced with your own APIs.

The gate is a new client `ModSystem` (or just a guard in your existing one) that checks `api.ModLoader.IsModEnabled("gui")` before applying the category patch. That check is the whole thing — if LibGUI isn't installed, none of the `Gui.*` types are ever resolved and Toolsmith loads fine.

```csharp
// Inside your client ModSystem or a new LibGuiCompatModSystem:
if (!api.ModLoader.IsModEnabled("gui")) return;
harmony.PatchCategory("toolsmith.libgui.compat");
// ...log whether ItemSlotOverlay.Build was actually patched
```

Don't add `gui` to your `modinfo.json` dependencies — compile-time reference only (`Private=false`, same as we do in `src/Mod.csproj`).

### One design call to make: always-on vs hide-when-keen

We show the sharpness bar at 100% — Toolsmith hides it. You noted that hiding it mirrors vanilla durability behaviour, and that's a fair default. Our reasoning was that when scanning a row of freshly-forged tools, an absent bar is ambiguous (sharp? untracked?), so we wanted the bar to always mean something: sweep = done, fill = in progress, ghost = free hone. But it's a genuine preference, not an obvious right answer.

If you fold this in and want to give players the choice, a `ShowSharpnessBarWhenKeen` boolean in `ToolsmithClientConfigs` sits naturally next to `UseGradientForSharpnessInstead`. The check in `SharpnessBar.Build` is one `if`.

### The checklist

- [ ] Copy `src/Compat/` into `Toolsmith/Client/LibGui/`
- [ ] Replace `src/Toolsmith/` shim calls with direct Toolsmith API calls (table above)
- [ ] Add a `gui`-gated code path that calls `harmony.PatchCategory("toolsmith.libgui.compat")`
- [ ] Add compile-time `Gui.dll` reference (`Private=false`)
- [ ] Decide always-on vs hide-when-keen (see above); add config toggle if wanted
- [ ] Confirm Toolsmith loads and behaves normally with **no** LibGUI installed
- [ ] Confirm bars appear on HudUI hotbar and PlayerInvUI grids with LibGUI installed

---

## Co-maintain roadmap

Mostly just: point me at breaking changes. The fragile spots are:

- `ItemSlotOverlay.Build` — the patch target. If LibGUI restructures this method, the patch breaks. The mod logs `[libguitoolsmithsharpness] No methods were patched` if it can't find it, so players will know.
- Your config fields (`UseGradientForSharpnessInstead`, `ShowAllSharpnessBarSections`, `GradientSelection`) — read by reflection. If they move or rename, the bar silently falls back to flat-band mode.
- Your attribute keys (`toolSharpnessCurrent`, `toolSharpnessMax`, `tinkeredTool*Durability`) — hardcoded. A rename would need a patch release on our end.

---

## The animations — optional polish

Two of the bars animate. They're nice, but they're optional — the fold-in can ship without them and add them later.

**SharpnessKeenSweep** (the fully-sharp bar): a soft highlight glides left-to-right across the bar on a loop. Signals "this edge is keen" positively, instead of a blank bar that reads as "no info." In LibGUI this is a `StatefulWidget` with an `AnimationController`. On your vanilla Cairo path there's no per-frame hook in `ComposeSlotOverlays` (it bakes to a texture once), so you'd draw the animation layer in a Harmony postfix on `GuiElementItemSlotGridBase.RenderInteractiveElements` — that fires every frame and gets `deltaTime`. The animated layer is a GL quad drawn on top of the baked bar using `capi.Render.Render2DTexturePremultipliedAlpha`, with an x-offset that advances with accumulated time. Not hard, but it's a new pattern if you haven't done per-frame slot overlays before.

**SharpnessGhostPulse** (the fresh-tool hint): a faint bar that slowly breathes in and out in the unsharp space on a newly-crafted tool. Same `RenderInteractiveElements` approach — alpha driven by `sin(accumulatedTime / period * π)`, sized to the unsharpened portion of the bar. Clears once durability drops below pristine (tool has been used).

If you want to ship something simpler first: both can be replaced with a static treatment (a solid keen bar, a static faint ghost) that lives entirely in `ComposeSlotOverlays` with no per-frame code. The LibGUI versions can live in the compat layer unchanged.

---

## A note on the code style

You'll notice the `Toolsmith/` shim layer is documented pretty heavily — all those XML docs exist so the *why* of each decision is obvious before you delete the file. The `Compat/` layer is lighter. The goal was to make the fold-in seam self-evident at a glance rather than requiring a lot of reading. If something isn't clear, ask.

---

*This is CC0 — do whatever you want with it.*

*You mentioned the Toolsmith Discord thread — I'll find you there.*
