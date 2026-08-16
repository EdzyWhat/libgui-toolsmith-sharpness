// [FOLD-IN] DELETE this file when folding into Toolsmith. It exists only because this is an external
// mod (behaviour detected by class-name string match; raw attribute reads). Replace its callers with
// real `is CollectibleBehaviorTool*` checks + Toolsmith's own attribute constants — INTEGRATION.md L1.
using Vintagestory.API.Common;

namespace LibGuiToolsmithSharpness.Toolsmith;

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

    // Toolsmith writes sharpness lazily: a freshly-crafted tool head has NO sharpness attributes
    // until something calls its getter (e.g. hovering it for the tooltip), which is why the bar
    // used to appear only after a hover. We detect that not-yet-initialised state by the Toolsmith
    // behaviours the collectible carries (matched by class name so we need no Toolsmith.dll ref).
    // A fresh head's ratio is deterministic: current = max * (IsCraftableMetal ? 0.85 : 0.66); we
    // use the non-metal 0.66 as the pre-hover default (it self-corrects to the exact value once the
    // real attributes exist). Bindings/handles are NOT sharpenable, so their behaviours are excluded.
    private const float FreshDefaultRatio = 0.66f;
    private static readonly string[] SharpenableBehaviors =
    {
        "CollectibleBehaviorTinkeredTools",
        "CollectibleBehaviorSmithedTools",
        "CollectibleBehaviorToolHead"
    };
    private const string BluntBehavior = "CollectibleBehaviorToolBlunt";

    /// <summary>
    /// Attempts to read a 0..1 sharpness ratio for the stack. Returns false (and leaves the ratio at
    /// 0) when the stack isn't a Toolsmith sharpenable tool, or its max is non-positive.
    ///
    /// <paramref name="uninitialized"/> is true when the item is a Toolsmith sharpenable whose
    /// sharpness attributes have not been written yet (freshly crafted, never hovered): in that case
    /// we report the deterministic fresh ratio so the bar shows immediately, and the caller can treat
    /// it as a fresh tool. We deliberately do NOT call Toolsmith's getters, which would lazily write
    /// attributes (a forbidden side effect in a render path).
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

    /// <summary>
    /// Whether the collectible is a Toolsmith item that carries a sharpness stat (tinkered tool,
    /// smithed tool, or a detached tool head) and isn't explicitly blunt. Matched by behaviour class
    /// name so we need no reference to Toolsmith.dll.
    /// </summary>
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
