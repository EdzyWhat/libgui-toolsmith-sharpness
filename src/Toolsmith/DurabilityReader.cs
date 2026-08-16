// [FOLD-IN] DELETE this file when folding into Toolsmith. It re-declares Toolsmith's attribute keys and
// re-implements the weakest-component min logic. Replace with Toolsmith's own
// TinkeringUtility.FindLowestCurrent/MaxDurabilityForBar — INTEGRATION.md L1.
using System;
using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness.Toolsmith;

/// <summary>
/// Computes the durability ratio Toolsmith wants shown for a tinkered (multi-part) tool: the
/// health of the component CLOSEST TO BREAKING, not the tool head.
///
/// A Toolsmith tinkered tool is three parts - head, handle, binding - each with its own
/// current/max durability, and the whole tool breaks when ANY one part hits zero. Vanilla (and
/// therefore LibGUI's <c>ItemSlotOverlay</c>) only knows the head: it draws
/// <c>GetRemainingDurability / GetMaxDurability</c>, which Toolsmith maps to the head, so a tool
/// with a near-full head but a nearly-snapped binding shows a misleadingly full green bar.
///
/// This mirrors Toolsmith's own GUI transpiler (<c>ToolTinkeringGuiElementPatches</c> ->
/// <c>TinkeringUtility.FindLowestCurrentDurabilityForBar</c> /
/// <c>FindLowestMaxDurabilityForBar</c>): it takes the minimum current across the three parts
/// and the minimum max across the three parts, INDEPENDENTLY, then draws minCurrent / minMax.
/// (In practice both minima land on the same about-to-break part, e.g. a chert shovel binding
/// at 5/30 -> ~17%, red.) We reproduce that exactly rather than a true min-of-ratios so the bar
/// matches what standalone Toolsmith shows.
///
/// Like <see cref="SharpnessReader"/>, we read RAW attributes and only ever call the two vanilla
/// collectible methods LibGUI itself already calls on this same stack in the same Build
/// (<c>GetMaxDurability</c> / <c>GetRemainingDurability</c> for the head) - so we add no new side
/// effects and need no Toolsmith.dll reference. We never touch Toolsmith's <c>Get*Durability()</c>
/// extensions, several of which lazily reset/repair attributes as a side effect.
/// </summary>
public static class DurabilityReader
{
    // Toolsmith attribute keys (from Toolsmith.Utils.ItemStackExtensions). The head's current
    // durability is the vanilla "durability" attribute; head max comes from the collectible.
    public const string HandleCurrentKey = "tinkeredToolHandleDurability";
    public const string HandleMaxKey = "tinkeredToolHandleMaxDurability";
    public const string BindingCurrentKey = "tinkeredToolBindingDurability";
    public const string BindingMaxKey = "tinkeredToolBindingMaxDurability";
    public const string HeadCurrentKey = "durability";

    /// <summary>
    /// True when the stack carries the full set of tinkered-tool component attributes, i.e. it is
    /// an assembled Toolsmith tool whose durability bar should reflect the weakest part. Detached
    /// tool parts and plain vanilla tools return false and keep LibGUI's default bar.
    /// </summary>
    public static bool IsTinkeredTool(ItemStack? stack)
    {
        var attributes = stack?.Attributes;
        return attributes != null
            && attributes.HasAttribute(HeadCurrentKey)
            && attributes.HasAttribute(HandleCurrentKey)
            && attributes.HasAttribute(BindingCurrentKey)
            && attributes.HasAttribute(HandleMaxKey)
            && attributes.HasAttribute(BindingMaxKey);
    }

    /// <summary>
    /// For a tinkered tool, computes the 0..1 ratio of the part closest to breaking and whether a
    /// bar should be drawn at all. <paramref name="show"/> is false when the tool is pristine
    /// (minCurrent == minMax), matching LibGUI's "no bar at full durability" behaviour.
    /// <paramref name="allComponentsFull"/> is true only when EVERY part (head, handle, binding) is
    /// at full durability - i.e. the tool has never been used - which the sharpness bar uses to
    /// detect a freshly-crafted tool (min equality alone doesn't imply all parts are full). Returns
    /// false for non-tinkered stacks (leave LibGUI's default durability bar in place).
    /// </summary>
    public static bool TryGetLowestRatio(ItemStack? stack, out float ratio, out bool show, out bool allComponentsFull)
    {
        ratio = 0f;
        show = false;
        allComponentsFull = false;

        if (stack?.Collectible == null || !IsTinkeredTool(stack))
        {
            return false;
        }

        var attributes = stack.Attributes;

        // Head: exactly the values LibGUI reads for its own bar (already invoked this Build).
        int headMax = stack.Collectible.GetMaxDurability(stack);
        int headCurrent = stack.Collectible.GetRemainingDurability(stack);

        // Handle / binding: raw attributes, no side-effecting extension calls.
        int handleMax = attributes.GetInt(HandleMaxKey, 0);
        int bindingMax = attributes.GetInt(BindingMaxKey, 0);
        int handleCurrent = attributes.GetInt(HandleCurrentKey, handleMax);
        int bindingCurrent = attributes.GetInt(BindingCurrentKey, bindingMax);

        // Independent minima across the three parts (mirrors Toolsmith's FindLowest* methods).
        int minMax = Math.Min(headMax, Math.Min(handleMax, bindingMax));
        int minCurrent = Math.Min(headCurrent, Math.Min(handleCurrent, bindingCurrent));

        if (minMax <= 0)
        {
            return false;
        }

        ratio = Math.Clamp((float)minCurrent / minMax, 0f, 1f);
        show = minCurrent != minMax;
        allComponentsFull = headCurrent >= headMax && handleCurrent >= handleMax && bindingCurrent >= bindingMax;
        return true;
    }

    /// <summary>
    /// Whether the stack's vanilla (single-part) durability is untouched - remaining >= max, i.e. the
    /// item has never been used. For a non-tinkered Toolsmith item (a bare tool head, a smithed tool)
    /// this stands in for the tinkered "all components full" check when deciding if a tool is freshly
    /// crafted. Items with no durability concept count as pristine (nothing to have worn down yet).
    /// </summary>
    public static bool IsVanillaDurabilityFull(ItemStack? stack)
    {
        if (stack?.Collectible == null)
        {
            return false;
        }

        int max = stack.Collectible.GetMaxDurability(stack);
        if (max <= 0)
        {
            return true;
        }

        return stack.Collectible.GetRemainingDurability(stack) >= max;
    }
}
