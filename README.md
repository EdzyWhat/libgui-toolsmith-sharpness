# LibGUI Toolsmith Sharpness

> **Toolsmith author (JonR):** [`HANDOFF.md`](HANDOFF.md) is written for you.

You installed [Toolsmith](https://mods.vintagestory.at/toolsmith). You installed [HudUI](https://mods.vintagestory.at/hudui) or [PlayerInvUI](https://mods.vintagestory.at/playerinvui). The sharpness bar vanished. This mod puts it back.

It does two things on every [LibGUI](https://mods.vintagestory.at/libgui) item slot:

1. **Draws the sharpness bar** — just above the durability bar, coloured to match whatever Toolsmith display mode you're using (flat bands, gradient, or five sections). Newly forged tools get a faint "hey, sharpen this before you use it" hint since that first hone is free.
2. **Fixes the durability bar for tinkered tools** — shows the part closest to breaking instead of just the tool head. That shovel isn't fine if the binding is at 5/30.

Because HudUI and PlayerInvUI both route their slots through the same LibGUI code, one patch covers the hotbar *and* the inventory.

## Why the bars disappear without this mod

LibGUI replaces Vintage Story's default slot rendering entirely — which means Toolsmith's bars, which hook into the vanilla slots, stop working. This mod bridges the gap by patching LibGUI's slots directly.

## Dependencies

**Required:**

| Mod | Min version |
|-----|-------------|
| Vintage Story | 1.22.0 |
| [LibGUI](https://mods.vintagestory.at/libgui) (`gui`) | 3.0.0 |
| [Toolsmith](https://mods.vintagestory.at/toolsmith) | 1.2.18 |

**Optional** — install whichever UI mods you want; the bars show up on all of them:

| Mod | Effect |
|-----|--------|
| [HudUI](https://mods.vintagestory.at/hudui) | Bars show on the hotbar |
| [PlayerInvUI](https://mods.vintagestory.at/playerinvui) | Bars show in the inventory |

This is a **client-side** mod. You don't need it on the server.

## Compatibility

This mod patches another mod's compiled code, which means a LibGUI, HudUI, PlayerInvUI, or Toolsmith update can break it. If the bars stop showing up after an update, check your client log for a `[libguitoolsmithsharpness] No methods were patched` warning — that's the tell. A fix usually just means recompiling against the new version.

## For developers

Want to build or test? See [`CONTRIBUTING.md`](CONTRIBUTING.md). Thinking about folding this into Toolsmith? [`HANDOFF.md`](HANDOFF.md) is for you — it covers both options and has the fold-in roadmap.

## Credits

- **[Toolsmith](https://mods.vintagestory.at/toolsmith)** by JonR — the sharpness/tinkering system this mod bridges. The colour palettes and weakest-component durability logic are reproduced from Toolsmith to match its look exactly.
- **[LibGUI](https://mods.vintagestory.at/libgui)** — the widget framework this mod patches.
- This mod by **RaptorKhan** — [CC0](LICENSE) (public domain).
