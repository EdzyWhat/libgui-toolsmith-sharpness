using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// Reads the Toolsmith "sharpness" stat straight off an itemstack's tree attributes.
///
/// Toolsmith stores sharpness as two flat integer attributes on the stack
/// (<c>Toolsmith.Utils.ToolsmithAttributes</c>): <c>toolSharpnessCurrent</c> and
/// <c>toolSharpnessMax</c>. Only Toolsmith's tinkerable / single-part tools (and detached
/// tool heads) carry them. The values are server-authoritative but synced to the client as
/// ordinary stack attributes (Toolsmith's own client-side tooltip reads them the same way),
/// so a client-only HUD reads them with no server round-trip.
///
/// We deliberately read the RAW attributes rather than calling Toolsmith's
/// <c>ItemStack.GetToolCurrentSharpness()</c> extension: that helper lazily *initialises*
/// sharpness as a side effect (writing attributes and touching the world), which must never
/// happen from a render path. Reading the raw ints also means we need no reference to
/// Toolsmith.dll.
/// </summary>
public static class SharpnessReader
{
    // Attribute keys defined in Toolsmith's ToolsmithAttributes.
    public const string CurrentKey = "toolSharpnessCurrent";
    public const string MaxKey = "toolSharpnessMax";

    /// <summary>
    /// Attempts to read a 0..1 sharpness ratio for the stack. Returns false (and leaves the
    /// ratio at 0) when the stack has no sharpness stat - i.e. it isn't a Toolsmith tool -
    /// or when the max is non-positive (guards the divide-by-zero that Toolsmith's own
    /// GetToolSharpnessPercent does not).
    /// </summary>
    public static bool TryGetRatio(ItemStack? stack, out float ratio)
    {
        ratio = 0f;

        var attributes = stack?.Attributes;
        if (attributes == null || !attributes.HasAttribute(MaxKey))
        {
            return false;
        }

        int max = attributes.GetInt(MaxKey, 0);
        if (max <= 0)
        {
            return false;
        }

        int current = attributes.GetInt(CurrentKey, 0);
        ratio = System.Math.Clamp((float)current / max, 0f, 1f);
        return true;
    }
}
