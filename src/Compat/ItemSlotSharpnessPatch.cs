using System.Collections.Generic;
using Gui.Rendering;
using Gui.Widgets.Framework;
using Gui.Widgets.Inventory;
using Gui.Widgets.Layout;
using HarmonyLib;
using LibGuiToolsmithSharpness.Toolsmith;

namespace LibGuiToolsmithSharpness.Compat;

/// <summary>
/// Postfix on LibGUI's ItemSlotOverlay.Build — the single method every LibGUI slot UI calls, so one
/// patch covers HudUI's hotbar and PlayerInvUI's grids. For a Toolsmith tool: swaps the durability
/// bar for a weakest-component one and appends the sharpness bar above it.
///
/// Must rebuild as ItemSlotOverlayStack (not a plain Stack) with the same ItemStack/SlotSize, and
/// reuse DurabilityBarKey for the replacement bar, so LibGUI's reconciliation stays stable per frame.
/// </summary>
// When folding into Toolsmith: apply only from a gui-gated code path, not from Toolsmith's main
// PatchAll. The [HarmonyPatchCategory] is already set for that — see HANDOFF.md.
[HarmonyPatchCategory("toolsmith.libgui.compat")]
[HarmonyPatch(typeof(ItemSlotOverlay), "Build")]
public static class ItemSlotSharpnessPatch
{
    // Fully-qualified because LibGUI's input Key shadows Gui.Widgets.Framework.Key when both are in scope.
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

        // Show the bar even at ratio == 1. Toolsmith hides it when keen, but on a LibGUI slot a missing
        // bar is ambiguous — is it sharp, or just untracked? When scanning across a batch of tools, you
        // need the bar to always mean something: sweep = done, fill = in progress, ghost = free hone.
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
