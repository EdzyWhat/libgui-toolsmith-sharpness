using System;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Layout;
using Gui.Widgets.Painting;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// A blue "sharpness" bar for an item slot, mirroring LibGUI's own
/// <c>Gui.Widgets.Inventory.DurabilityBar</c> (a <see cref="Stack"/> of a dark track plus a
/// coloured fill whose width tracks the ratio). Kept deliberately identical in geometry to
/// the durability bar (same 3px height, 1.5px corner radius, <c>(SlotSize - 8) * ratio</c>
/// fill width) so the two read as a matched pair when stacked.
/// </summary>
public class SharpnessBar : StatelessWidget
{
    /// <summary>Bar height in px - matches LibGUI's DurabilityBar.</summary>
    public const float BarHeight = 3f;

    /// <summary>
    /// How far to lift this bar above the durability bar: the durability bar's own height
    /// plus a 2px gap, so the sharpness bar sits cleanly on top of it.
    /// </summary>
    public const float LiftAboveDurability = BarHeight + 2f;

    public float Ratio { get; }

    public float SlotSize { get; }

    public SharpnessBar(float ratio, float slotSize)
    {
        Ratio = Math.Clamp(ratio, 0f, 1f);
        SlotSize = slotSize;
    }

    public override Widget Build(BuildContext context)
    {
        float fillWidth = Math.Max((SlotSize - 8f) * Ratio, BarHeight);

        // Dull edge -> keen edge: deep blue when blunt, bright cyan-blue when sharp. Distinct
        // from the durability bar's red->amber->green ramp so the two never read as the same stat.
        Vector4 color = Vector4.Lerp(
            new Vector4(0.10f, 0.28f, 0.60f, 1f),
            new Vector4(0.32f, 0.72f, 1.00f, 1f),
            Ratio);

        return new Stack(new Widget[]
        {
            // Dark track (full width).
            new Container(new BoxStyle
            {
                Height = BarHeight,
                Color = new Vector4(0f, 0f, 0f, 0.55f),
                CornerRadius = new Vector4(1.5f)
            }),
            // Coloured fill (ratio-driven width).
            new Container(new BoxStyle
            {
                Width = fillWidth,
                Height = BarHeight,
                Color = color,
                CornerRadius = new Vector4(1.5f)
            })
        });
    }
}
