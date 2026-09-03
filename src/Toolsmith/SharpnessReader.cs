using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness.Toolsmith;

/// <summary>
/// Reads sharpness off the itemstack's raw attributes (toolSharpnessCurrent / toolSharpnessMax).
/// Never calls Toolsmith's GetToolCurrentSharpness() — that lazily initialises sharpness as a
/// side effect, which must never happen from a render path. Raw reads also mean no Toolsmith.dll ref.
/// On fold-in: replace with direct attribute access via ToolsmithAttributes constants.
/// </summary>
public static class SharpnessReader
{
    // Attribute keys defined in Toolsmith's ToolsmithAttributes.
    public const string CurrentKey = "toolSharpnessCurrent";
    public const string MaxKey = "toolSharpnessMax";

    // Toolsmith writes sharpness lazily — no attributes until first hover (or, per ResetSharpness,
    // ever, for a creative-spawned stack). Detect that by checking behaviour class names (no
    // Toolsmith.dll ref needed). Mirrors ResetSharpness's exact split: metal tools start at 85%,
    // everything else (stone, bone, ...) at 66%. Self-corrects to the real value once written.
    private const float MetalFreshRatio = 0.85f;
    private const float NonMetalFreshRatio = 0.66f;
    private static readonly string[] SharpenableBehaviors =
    {
        "CollectibleBehaviorTinkeredTools",
        "CollectibleBehaviorSmithedTools",
        "CollectibleBehaviorToolHead"
    };
    private const string BluntBehavior = "CollectibleBehaviorToolBlunt";

    /// <summary>
    /// Returns the 0..1 sharpness ratio. False if not a Toolsmith sharpenable or max is zero.
    /// <paramref name="uninitialized"/> is true when the attributes haven't been written yet
    /// (freshly crafted, never hovered) — ratio is the metal/non-metal default in that case.
    /// </summary>
    public static bool TryGetRatio(ItemStack? stack, out float ratio, out bool uninitialized)
    {
        ratio = 0f;
        uninitialized = false;

        var attributes = stack?.Attributes;
        if (attributes == null)
        {
            return false;
        }

        // Toolsmith's CollectibleBehaviorSmithedTools.OnCreatedByCrafting writes sharpness
        // attributes to every single-part tool at craft time, blunt ones included - it never
        // checks ToolBlunt there. Toolsmith itself then ignores that value for blunt tools
        // (skips the tooltip line, freezes it in OnDamageItem). We must reject blunt tools
        // before the attribute check below, or we'd surface that frozen, meaningless value.
        if (IsBlunt(stack!.Collectible))
        {
            return false;
        }

        if (attributes.HasAttribute(MaxKey))
        {
            int max = attributes.GetInt(MaxKey, 0);
            if (max <= 0)
            {
                return false;
            }

            int current = attributes.GetInt(CurrentKey, 0);
            ratio = System.Math.Clamp((float)current / max, 0f, 1f);
            return true;
        }

        // No sharpness attribute yet - if this is a Toolsmith sharpenable, it's freshly crafted.
        if (IsSharpenableBehaviorPresent(stack.Collectible))
        {
            ratio = ToolMaterialReader.IsCraftableMetal(stack.Collectible) ? MetalFreshRatio : NonMetalFreshRatio;
            uninitialized = true;
            return true;
        }

        return false;
    }

    // Matched by class name so we need no Toolsmith.dll ref.
    private static bool IsBlunt(CollectibleObject? collectible)
    {
        var behaviors = collectible?.CollectibleBehaviors;
        if (behaviors == null)
        {
            return false;
        }

        foreach (CollectibleBehavior behavior in behaviors)
        {
            if (behavior.GetType().Name == BluntBehavior)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSharpenableBehaviorPresent(CollectibleObject? collectible)
    {
        var behaviors = collectible?.CollectibleBehaviors;
        if (behaviors == null)
        {
            return false;
        }

        foreach (CollectibleBehavior behavior in behaviors)
        {
            string name = behavior.GetType().Name;
            for (int i = 0; i < SharpenableBehaviors.Length; i++)
            {
                if (name == SharpenableBehaviors[i])
                {
                    return true;
                }
            }
        }

        return false;
    }
}
