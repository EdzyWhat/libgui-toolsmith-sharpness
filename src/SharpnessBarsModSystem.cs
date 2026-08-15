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

    // Client-only: we patch the client GUI framework and never touch the server.
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll(typeof(SharpnessBarsModSystem).Assembly);
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
        base.Dispose();
    }
}
