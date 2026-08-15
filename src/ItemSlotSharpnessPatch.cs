using System.Collections.Generic;
using Gui.Rendering;
using Gui.Widgets.Framework;
using Gui.Widgets.Inventory;
using Gui.Widgets.Layout;
using HarmonyLib;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// Postfix on LibGUI's <see cref="ItemSlotOverlay.Build"/>. That single method builds the
/// overlay (including the durability bar) for EVERY LibGUI item slot, so patching it here
/// covers HudUI's hotbar and PlayerInvUI's inventory/creative/crafting grids at once - all of
/// them render their slots via <c>Gui.Widgets.Inventory.FlatItemSlot</c> -> <c>ItemSlotOverlay</c>.
///
/// The overlay's <c>Build</c> returns an <see cref="ItemSlotOverlayStack"/> - a flat
/// <see cref="Stack"/> of positioned children (item, count text, durability bar, ...). For a
/// Toolsmith tool we do two things in one rebuild of that stack:
///
/// 1. <b>Fix the durability bar.</b> LibGUI draws <c>GetRemainingDurability / GetMaxDurability</c>,
///    which for a tinkered tool is only the HEAD - so a near-full head hides a nearly-snapped
///    binding. We drop LibGUI's head bar and re-add one driven by the weakest component
///    (<see cref="DurabilityReader"/>), matching what standalone Toolsmith shows (and letting
///    LibGUI's <see cref="DurabilityBar"/> ramp colour it red as it nears zero).
/// 2. <b>Add the sharpness bar.</b> A <see cref="SharpnessBar"/> aligned bottom-centre like the
///    durability bar but lifted a few pixels so it sits just above it.
///
/// We preserve the exact <see cref="ItemSlotOverlayStack"/> type, key and metadata
/// (ItemStack / SlotSize) - and reuse LibGUI's own <see cref="ItemSlotOverlay.DurabilityBarKey"/>
/// for the replacement bar - so the framework's reconciliation stays stable frame to frame.
/// </summary>
[HarmonyPatch(typeof(ItemSlotOverlay), "Build")]
public static class ItemSlotSharpnessPatch
{
    // A stable key for our injected sharpness bar, distinct from ItemSlotOverlay.DurabilityBarKey,
    // so it reconciles by identity among the stack's siblings.
    // Fully-qualified: LibGUI's input `Key` (keyboard) shadows the widget-tree
    // `Gui.Widgets.Framework.Key` when both namespaces are in scope.
    private static readonly Gui.Widgets.Framework.Key SharpnessBarKey =
        new ValueKey<string>("libguitoolsmithsharpness.sharpness_bar");

    [HarmonyPostfix]
    public static void Postfix(ItemSlotOverlay __instance, BuildContext context, ref Widget __result)
    {
        // Only the real slot overlay stack carries the metadata we need; bail on anything else.
        if (__result is not ItemSlotOverlayStack stack)
        {
            return;
        }

        var itemStack = __instance.Slot?.Itemstack;

        bool hasSharpness = SharpnessReader.TryGetRatio(itemStack, out float sharpnessRatio);
        bool isTinkered = DurabilityReader.TryGetLowestRatio(itemStack, out float durabilityRatio, out bool showDurability, out bool allComponentsFull);

        if (!hasSharpness && !isTinkered)
        {
            // Not a Toolsmith tool (or no relevant stats) - leave the overlay untouched.
            return;
        }

        // A freshly-crafted tool (never used -> durability pristine) that still isn't fully sharp can
        // be sharpened for FREE before first use. Flag that so the sharpness bar shows the "sharpen
        // me" hint. The check self-clears the instant the tool is used and loses any durability.
        bool durabilityPristine = isTinkered ? allComponentsFull : DurabilityReader.IsVanillaDurabilityFull(itemStack);
        bool isFresh = hasSharpness && sharpnessRatio < 1f && durabilityPristine;

        // Mirror ItemSlotOverlay.Build's own padding resolution so our bars line up with LibGUI's.
        var theme = Theme.Of(context);
        EdgeInsets edge = __instance.Padding ?? theme.ItemSlotStyle.Padding ?? EdgeInsets.Zero;

        List<Widget> children;
        if (isTinkered)
        {
            // Drop LibGUI's head-only durability bar; re-add one driven by the weakest component.
            children = new List<Widget>(stack.Children.Count);
            foreach (Widget child in stack.Children)
            {
                if (Equals(child.Key, ItemSlotOverlay.DurabilityBarKey))
                {
                    continue;
                }

                children.Add(child);
            }

            if (showDurability)
            {
                Widget durabilityBar = new Padding(edge, new Align(Alignment.BottomCenter, new DurabilityBar(durabilityRatio, __instance.Size)));
                children.Add(new Positioned(child: durabilityBar, key: ItemSlotOverlay.DurabilityBarKey));
            }
        }
        else
        {
            children = new List<Widget>(stack.Children);
        }

        if (hasSharpness)
        {
            EdgeInsets lifted = EdgeInsets.Only(
                edge.Left,
                edge.Top,
                edge.Right,
                edge.Bottom + SharpnessBar.LiftAboveDurability);

            // Colour the fill to match the player's Toolsmith display config (bands / gradient / sections).
            var (mode, gradientSelection) = ToolsmithSharpnessConfig.Read();

            Widget sharpnessBar = new Padding(lifted, new Align(Alignment.BottomCenter,
                new SharpnessBar(sharpnessRatio, __instance.Size, mode, gradientSelection, isFresh)));
            children.Add(new Positioned(child: sharpnessBar, key: SharpnessBarKey));
        }

        __result = new ItemSlotOverlayStack(children, stack.ItemStack, stack.SlotSize);
    }
}
