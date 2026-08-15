# Vendored reference DLLs

## `Gui.dll` (LibGUI, modid `gui`)

Compile-time reference only. This is the LibGUI assembly, extracted from the `gui` mod
(`gui_3.1.0.zip`). It is **not** shipped with this mod: the installed `gui` mod provides it
at runtime, so the reference is `Private=false` in `src/Mod.csproj` and the build never copies
it into our output. (LibGUI depends on game version `1.22.0`; this mod requires `gui >= 3.0.0`.)

**Not committed to git.** `lib/*.dll` is git-ignored - it's another author's binary and not ours
to redistribute. To build this mod, drop the `Gui.dll` from your target `gui` mod release here
(see below); the file must be present locally but will never be tracked.

To update: replace this file with the `Gui.dll` from the target `gui` mod release and rebuild.
Do not vendor HudUI / PlayerInvUI / Toolsmith DLLs - this mod references none of their types
(it patches LibGUI's shared `ItemSlotOverlay` and reads Toolsmith's sharpness from raw stack
attributes).
