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
/// <see cref="Stack"/> of positioned children (item, count text, durability bar, ...). When the
/// slot holds a Toolsmith tool, we rebuild that stack with one extra sibling: a
/// <see cref="SharpnessBar"/> aligned bottom-centre like the durability bar but lifted a few
/// pixels so it sits just above it. We preserve the exact <see cref="ItemSlotOverlayStack"/>
/// type, key and metadata (ItemStack / SlotSize) so the framework's reconciliation stays stable
/// frame to frame.
/// </summary>
[HarmonyPatch(typeof(ItemSlotOverlay), "Build")]
public static class ItemSlotSharpnessPatch
{
    // A stable key for our injected bar, distinct from ItemSlotOverlay.DurabilityBarKey, so it
    // reconciles by identity among the stack's siblings.
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
        if (!SharpnessReader.TryGetRatio(itemStack, out float ratio))
        {
            // Not a Toolsmith tool (or no sharpness stat) - leave the overlay untouched.
            return;
        }

        // Mirror ItemSlotOverlay.Build's own padding resolution so the bar lines up with the
        // durability bar, then add extra bottom inset to lift it above.
        var theme = Theme.Of(context);
        EdgeInsets edge = __instance.Padding ?? theme.ItemSlotStyle.Padding ?? EdgeInsets.Zero;
        EdgeInsets lifted = EdgeInsets.Only(
            edge.Left,
            edge.Top,
            edge.Right,
            edge.Bottom + SharpnessBar.LiftAboveDurability);

        Widget bar = new Padding(lifted, new Align(Alignment.BottomCenter, new SharpnessBar(ratio, __instance.Size)));
        var positioned = new Positioned(child: bar, key: SharpnessBarKey);

        var children = new List<Widget>(stack.Children) { positioned };
        __result = new ItemSlotOverlayStack(children, stack.ItemStack, stack.SlotSize);
    }
}
