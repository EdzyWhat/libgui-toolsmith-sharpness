using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness.Toolsmith;

/// <summary>
/// Mirrors Toolsmith's ItemStackExtensions.IsCraftableMetal — whether a collectible's "metal" or
/// "material" variant names an ingot that actually exists (metal tools vs. stone/bone/etc). Pure
/// vanilla API (RegistryObject.Variant, IWorldAccessor.GetItem); no Toolsmith.dll reference needed.
/// On fold-in: replace with a direct call to Toolsmith's own IsCraftableMetal extension.
/// </summary>
public static class ToolMaterialReader
{
    /// <summary>
    /// True if <paramref name="collectible"/>'s metal/material variant resolves to a real
    /// "game:ingot-&lt;material&gt;" item. False (fails soft) if the API isn't available yet.
    /// </summary>
    public static bool IsCraftableMetal(CollectibleObject? collectible)
    {
        string? material = collectible?.Variant["metal"] ?? collectible?.Variant["material"];
        if (material == null)
        {
            return false;
        }

        var world = SharpnessBarsModSystem.Api?.World;
        if (world == null)
        {
            return false;
        }

        Item? ingot = world.GetItem(new AssetLocation("game:ingot-" + material));
        return ingot != null && (ingot.Variant["metal"] != null || ingot.Variant["material"] != null);
    }
}
