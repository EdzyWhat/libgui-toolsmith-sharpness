using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// Client-side entry point. Applies the single Harmony patch that injects the sharpness bar
/// into LibGUI's item-slot overlay, and cleanly removes it on dispose.
/// </summary>
public class SharpnessBarsModSystem : ModSystem
{
    private const string HarmonyId = "libguitoolsmithsharpness.patches";

    private Harmony? _harmony;

    /// <summary>Client logger, used to report whether the LibGUI patch attached (see StartClientSide).</summary>
    internal static ILogger? Logger;

    // Client-only: we patch the client GUI framework and never touch the server.
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        Logger = api.Logger;

        // [FOLD-IN] Inside Toolsmith this gate is ESSENTIAL: Toolsmith must NOT hard-depend on `gui`, so
        // only ever touch the LibGUI patch (and thus resolve any Gui.* types) when LibGUI is installed.
        // In this standalone mod `gui` is a hard dependency, so the gate is redundant but harmless.
        // See INTEGRATION.md (Layer 2).
        if (!api.ModLoader.IsModEnabled("gui"))
        {
            Logger.Notification("[libguitoolsmithsharpness] LibGUI (gui) not enabled; skipping patch.");
            return;
        }

        _harmony = new Harmony(HarmonyId);
        _harmony.PatchCategory("toolsmith.libgui.compat");

        // Confirm the patch actually attached to LibGUI's ItemSlotOverlay.Build (this is
        // version-sensitive - a LibGUI/HudUI/PlayerInvUI update could move the method).
        int patched = 0;
        foreach (var method in _harmony.GetPatchedMethods())
        {
            patched++;
            Logger.Notification("[libguitoolsmithsharpness] Patched {0}.{1}", method.DeclaringType?.FullName, method.Name);
        }

        if (patched == 0)
        {
            Logger.Warning("[libguitoolsmithsharpness] No methods were patched - ItemSlotOverlay.Build was not found. The sharpness/durability bars will not appear.");
        }
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
        base.Dispose();
    }
}
