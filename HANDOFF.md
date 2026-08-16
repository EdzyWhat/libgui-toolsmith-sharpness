# Hey JonR

Fair warning: I vibe-coded most of this over two days and I'm genuinely reading some of it for the first time right now. It works — tested against three versions of Toolsmith on 1.2.x — but I wanted to be upfront about that before handing it over.

Anyway. **Fold it into Toolsmith** if you're open to it. Half this codebase exists purely because I can't reference your DLL from the outside, and it evaporates the moment it moves inside. What's left is the actual LibGUI bridge — new code, nothing you'd be rewriting.

Either path is fine. This doc covers both.

---

## What it does

Sharpness bar + weakest-component durability fix, on every LibGUI slot. One Harmony **postfix** on `ItemSlotOverlay.Build` — the single method HudUI and PlayerInvUI both route their slots through.

Worth flagging: postfix, not transpiler. I let Build finish, grab the returned widget tree, swap in what I need. No IL surgery, no instruction-sequence pattern matching. You mentioned a hardcoded patch breaking things in GloomeClasses — this approach sidesteps exactly that.

---

## Your options

### Option A: fold it in (probably the move)

Half the code here is embarrassing scaffolding:

- Reflecting over your config field names because I can't read them directly
- Copying your hex palettes verbatim (drift risk, I know)
- Detecting your tool types by class-name string match
- Re-declaring your attribute key constants

All of that just... goes away. Replace it with direct calls to `TinkeringUtility`, `ToolsmithAttributes`, and `ClientConfig`. Your existing `assets/toolsmith/compatibility/` pattern is JSON because your other compat is JSON — this is C# because it has to patch a compiled method, but the spirit's the same.

New thing you'd take on: a compile-time `Gui.dll` reference and keeping the LibGUI code unreachable when `gui` isn't installed. The JIT only resolves a type when a method referencing it gets called, so the gate is just `api.ModLoader.IsModEnabled("gui")` before touching anything in `Compat/`. There's a `toolsmith-integration` branch here that annotates every file with exactly what to keep and delete.

### Option B: co-maintain the standalone

Also fine. I'll keep it working across VS/LibGUI/Toolsmith updates. Main ask: heads-up when your attribute key names or config fields change, since I'm reading them by reflection and hardcoded string. (Yes I know. See "embarrassing scaffolding" above.)

---

## Fold-in roadmap

### Step 1 — delete `src/Toolsmith/` (all four files, guilt-free)

| File | What it does right now | What replaces it |
|------|------------------------|------------------|
| `ToolsmithSharpnessConfig.cs` | Reflects `ToolsmithModSystem.ClientConfig` + `GradientSelection` by field name | Just read them directly — collapses to a few lines |
| `SharpnessPalette.cs` | Your hex palettes, copied verbatim, re-derived via `ColorUtil` | Call `TinkeringUtility`'s colour methods |
| `SharpnessReader.cs` | Detects tools by behaviour class-name string; guesses the 0.66 fresh ratio | Real `is CollectibleBehaviorToolHead` type checks + `ToolsmithAttributes` constants |
| `DurabilityReader.cs` | Re-declares your `tinkeredTool*Durability` keys; re-implements `FindLowest*` | `TinkeringUtility.FindLowestCurrentDurabilityForBar` / `FindLowestMaxDurabilityForBar` |

One thing that doesn't change: don't call `Get*Sharpness()` or `Get*Durability()` from the render path. Several of them write or repair attributes as a side effect — fine from a tick, catastrophic from inside `Build`. Raw reads only. (Or init sharpness at craft time and drop the fresh-tool heuristic entirely, which is probably cleaner anyway.)

### Step 2 — move `src/Compat/` to `Toolsmith/Client/LibGui/`

These go over as-is:

- `ItemSlotSharpnessPatch.cs` — already tagged `[HarmonyPatchCategory("toolsmith.libgui.compat")]` so your `PatchAll` won't sweep it up accidentally
- `SharpnessBar.cs`, `SharpnessKeenSweep.cs`, `SharpnessGhostPulse.cs` — the bar widgets, no changes needed

The gate:

```csharp
if (!api.ModLoader.IsModEnabled("gui")) return;
harmony.PatchCategory("toolsmith.libgui.compat");
```

Don't add `gui` to `modinfo.json` dependencies — compile-time reference only (`Private=false`).

### One call to make: always-on vs hide-when-keen

You mentioned hiding the bar at 100% mirrors vanilla durability — you're right, that's a legitimate default. My reasoning was that when scanning a row of freshly-forged tools, an absent bar is ambiguous: sharp, or just not a Toolsmith tool? I wanted the bar to always mean something. It's a preference, not gospel.

If you want to give players the choice: `ShowSharpnessBarWhenKeen` in `ToolsmithClientConfigs` next to `UseGradientForSharpnessInstead`, and it's one `if` in `SharpnessBar.Build`.

### Checklist

- [ ] Copy `src/Compat/` into `Toolsmith/Client/LibGui/`
- [ ] Replace `src/Toolsmith/` shim calls with direct Toolsmith API calls (table above)
- [ ] Add `api.ModLoader.IsModEnabled("gui")` gate + `harmony.PatchCategory("toolsmith.libgui.compat")`
- [ ] Add compile-time `Gui.dll` reference (`Private=false`)
- [ ] Decide always-on vs hide-when-keen; add toggle if wanted
- [ ] Confirm Toolsmith loads normally with **no** LibGUI installed
- [ ] Confirm bars appear on HudUI + PlayerInvUI with LibGUI installed

---

## Co-maintain roadmap

Point me at breaking changes. The fragile bits:

- `ItemSlotOverlay.Build` — if LibGUI restructures this method, the patch breaks. Mod logs `[libguitoolsmithsharpness] No methods were patched` on startup if it can't find it.
- Your config fields (`UseGradientForSharpnessInstead`, `ShowAllSharpnessBarSections`, `GradientSelection`) — read by reflection; silently falls back to flat-band mode if they move or rename.
- Your attribute keys (`toolSharpnessCurrent`, `toolSharpnessMax`, `tinkeredTool*Durability`) — hardcoded strings; a rename needs a patch from me.

---

## The animations — optional, skip them if you want

Two of the bars animate. The fold-in can ship without them and add them later.

**SharpnessKeenSweep** — the fully-sharp bar has a highlight that slowly glides left to right. The intent is "this edge is keen, move on," so it's deliberately calm and slow rather than flashy. In LibGUI it's a `StatefulWidget` with an `AnimationController` — I'm reasonably confident I understand how that works. On your vanilla Cairo path it's different: `ComposeSlotOverlays` bakes to a texture once, so you'd do the animation in a postfix on `GuiElementItemSlotGridBase.RenderInteractiveElements` (fires every frame, gets `deltaTime`), drawing a GL quad on top via `capi.Render.Render2DTexturePremultipliedAlpha` with an x-offset that advances with time.

**SharpnessGhostPulse** — faint bar that slowly breathes in the unsharp space on a newly-crafted tool. Free first hone = worth a nudge, not an alarm. Same `RenderInteractiveElements` approach, alpha driven by sin. Clears once the tool's been used.

Both can be replaced with static treatments (solid keen bar, static ghost) that live entirely in `ComposeSlotOverlays` with no per-frame code, if you'd rather start simple.

---

## On the code

The `Toolsmith/` shim layer has heavy comments explaining why each decision was made. That's partly so the fold-in seam is obvious, and partly because I wasn't always 100% sure why I made some of them. The `Compat/` layer is lighter — that's the part I'm more confident about.

---

*CC0 — no strings.*

*You mentioned the Toolsmith Discord thread — I'll find you there.*
