using System;
using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness.Toolsmith;

/// <summary>
/// Weakest-component durability for tinkered tools — the part closest to breaking, not just the head.
/// Mirrors TinkeringUtility.FindLowest*DurabilityForBar: independent min(current) and min(max)
/// across head/handle/binding, so the bar matches what standalone Toolsmith shows.
/// Never calls Get*Durability() extensions — several lazily reset/repair attributes as a side effect.
/// On fold-in: replace with direct TinkeringUtility calls.
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

    /// <summary>True when the stack is an assembled tinkered tool with all three component attributes.</summary>
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
    /// Weakest-component ratio. <paramref name="show"/> is false at pristine (matches LibGUI's "no bar
    /// at full"). <paramref name="allComponentsFull"/> is true only when every part is at max — used by
    /// the sharpness bar to detect a freshly-crafted tool (min equality alone doesn't imply all full).
    /// Returns false for non-tinkered stacks.
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
