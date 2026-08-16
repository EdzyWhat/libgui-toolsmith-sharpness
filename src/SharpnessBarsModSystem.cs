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
