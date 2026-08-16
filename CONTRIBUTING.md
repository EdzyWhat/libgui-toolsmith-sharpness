# Building & running

Requires the **.NET 10 SDK** and a local Vintage Story install (1.22.0+). Set `VINTAGE_STORY` if it isn't at the macOS default (`/Applications/Vintage Story.app`).

You also need `lib/Gui.dll` present locally — it's git-ignored because it's another author's binary. Drop the `Gui.dll` from your target `gui` (LibGUI) mod release into `lib/`. See [`lib/README.md`](lib/README.md).

```sh
build/restage.sh            # build + stage into local Mods folder (Release)
build/restage.sh Debug      # same, Debug config
build/release.sh            # build a distributable zip into dist/
```

`restage.sh` copies the DLL + `modinfo.json` + assets into `~/Library/Application Support/VintagestoryData/Mods/<modid>`. Restart the game or reload mods to pick it up.

## Re-verifying the patch surface after a mod update

This mod patches `Gui.Widgets.Inventory.ItemSlotOverlay.Build` and reads Toolsmith attributes by reflection. Both can break on a mod update. The ground truth is the decompiled source — use [`ilspycmd`](https://github.com/icsharpcode/ILSpy):

```sh
dotnet tool install -g ilspycmd

ilspycmd -p -o /tmp/gui "<path-to>/Gui.dll"
ilspycmd -p -o /tmp/hud "<path-to>/HudUI.dll"
ilspycmd -p -o /tmp/piu "<path-to>/PlayerInvUI.dll"
```

Check that `ItemSlotOverlay.Build` still exists, still returns `ItemSlotOverlayStack`, and `DurabilityBarKey` is still the key used for the durability bar. On the Toolsmith side, check that the attribute key names and config field names in `src/Toolsmith/` still match.

The mod logs `[libguitoolsmithsharpness] Patched ...` on startup when the patch attaches, or warns loudly if it doesn't find the target. That's the first thing to check when something goes wrong.

## If you're JonR

See [`HANDOFF.md`](HANDOFF.md).
