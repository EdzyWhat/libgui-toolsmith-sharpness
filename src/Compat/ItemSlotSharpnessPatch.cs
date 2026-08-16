using System.Collections.Generic;
using Gui.Rendering;
using Gui.Widgets.Framework;
using Gui.Widgets.Inventory;
using Gui.Widgets.Layout;
using HarmonyLib;
using LibGuiToolsmithSharpness.Toolsmith;

namespace LibGuiToolsmithSharpness.Compat;

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
// If this patch moves into Toolsmith, register it as its own category and apply it only from
// inside the gui-gated ModSystem — not from Toolsmith's main PatchAll pass. See HANDOFF.md.
[HarmonyPatchCategory("toolsmith.libgui.compat")]
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

        bool hasSharpness = SharpnessReader.TryGetRatio(itemStack, out float sharpnessRatio, out bool sharpnessUninitialized);
        bool isTinkered = DurabilityReader.TryGetLowestRatio(itemStack, out float durabilityRatio, out bool showDurability, out bool allComponentsFull);

        // We draw the sharpness bar for ANY Toolsmith sharpenable, including fully keen (ratio == 1).
        // Standalone Toolsmith hides the bar at 100%, but a missing bar reads as "no info" rather than
        // "sharp" - so we keep it visible and let SharpnessBar render a distinct gleaming keen state.
        bool showSharpness = hasSharpness;

        if (!showSharpness && !isTinkered)
        {
            // Not a Toolsmith tool and nothing to fix - leave the overlay untouched.
            return;
        }

        // A freshly-crafted tool (never used -> durability pristine) that still isn't fully sharp can
        // be sharpened for FREE before first use. Flag that so the sharpness bar shows the "sharpen
        // me" hint. A tool whose sharpness attributes haven't been written yet is fresh by definition
        // (never hovered since crafting). The check self-clears the instant the tool is used.
        bool durabilityPristine = isTinkered ? allComponentsFull : DurabilityReader.IsVanillaDurabilityFull(itemStack);
        bool isFresh = showSharpness && (sharpnessUninitialized || durabilityPristine);

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

        if (showSharpness)
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
