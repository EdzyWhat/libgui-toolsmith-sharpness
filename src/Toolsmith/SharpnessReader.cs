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

    // Toolsmith writes sharpness lazily — no attributes until first hover. Detect that by checking
    // behaviour class names (no Toolsmith.dll ref needed). Report 0.66 as the pre-hover default
    // (non-metal fresh ratio; self-corrects once real attributes exist). Bindings/handles excluded.
    private const float FreshDefaultRatio = 0.66f;
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
    /// (freshly crafted, never hovered) — ratio is the 0.66 default in that case.
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
        if (IsToolsmithSharpenable(stack!.Collectible))
        {
            ratio = FreshDefaultRatio;
            uninitialized = true;
            return true;
        }

        return false;
    }

    // Matched by class name so we need no Toolsmith.dll ref. Blunt overrides all.
    private static bool IsToolsmithSharpenable(CollectibleObject? collectible)
    {
        var behaviors = collectible?.CollectibleBehaviors;
        if (behaviors == null || behaviors.Length == 0)
        {
            return false;
        }

        bool sharpenable = false;
        foreach (CollectibleBehavior behavior in behaviors)
        {
            string name = behavior.GetType().Name;
            if (name == BluntBehavior)
            {
                return false;
            }

            for (int i = 0; i < SharpenableBehaviors.Length; i++)
            {
                if (name == SharpenableBehaviors[i])
                {
                    sharpenable = true;
                    break;
                }
            }
        }

        return sharpenable;
    }
}
