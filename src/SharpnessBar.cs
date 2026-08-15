using System;
using System.Collections.Generic;
using Gui.Core.Layout;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Layout;
using Gui.Widgets.Painting;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// A Toolsmith "sharpness" bar for a LibGUI item slot, mirroring the geometry of LibGUI's own
/// <c>Gui.Widgets.Inventory.DurabilityBar</c> (3px tall dark track + a ratio-driven fill, same
/// <c>(SlotSize - 8) * ratio</c> width) so the two read as a matched pair when stacked.
///
/// The fill is coloured to match whatever mode the player has Toolsmith set to
/// (<see cref="ToolsmithSharpnessConfig"/> / <see cref="SharpnessPalette"/>) - flat bands, a gradient
/// ramp, or five flat sections - so this bar looks like Toolsmith's own, just relocated onto the
/// LibGUI slot. Two extra affordances layer on top of that:
///
/// <list type="bullet">
///   <item><b>Always-legible track.</b> The dark track keeps a faint themed outline so the bar's full
///     extent (and therefore an empty/near-empty sharpness) is readable; the outline escalates to the
///     theme's <c>Error</c> colour when the tool is critically dull.</item>
///   <item><b>Fresh-tool hint.</b> When <see cref="IsFresh"/> (a just-crafted tool that can still be
///     sharpened for free), a faint breathing <see cref="SharpnessGhostPulse"/> fills the negative
///     space to nudge the player to sharpen before first use.</item>
/// </list>
///
/// Note we only draw this bar at all when sharpness is below max (see the patch / SharpnessReader),
/// matching Toolsmith's own "no bar when fully sharp" convention - so a missing bar means "keen".
/// </summary>
public class SharpnessBar : StatelessWidget
{
    /// <summary>Bar height in px - matches LibGUI's DurabilityBar.</summary>
    public const float BarHeight = 3f;

    /// <summary>
    /// How far to lift this bar above the durability bar: the durability bar's own height plus a
    /// 2px gap, so the sharpness bar sits cleanly on top of it.
    /// </summary>
    public const float LiftAboveDurability = BarHeight + 2f;

    /// <summary>At/under this ratio the tool is "critically dull" - escalate the track outline.</summary>
    private const float DullThreshold = 0.15f;

    public float Ratio { get; }

    public float SlotSize { get; }

    public ToolsmithSharpnessConfig.SharpnessMode Mode { get; }

    public int GradientSelection { get; }

    /// <summary>Just-crafted tool that can still be sharpened for free - show the breathing hint.</summary>
    public bool IsFresh { get; }

    public SharpnessBar(float ratio, float slotSize, ToolsmithSharpnessConfig.SharpnessMode mode, int gradientSelection, bool isFresh)
    {
        Ratio = Math.Clamp(ratio, 0f, 1f);
        SlotSize = slotSize;
        Mode = mode;
        GradientSelection = gradientSelection;
        IsFresh = isFresh;
    }

    public override Widget Build(BuildContext context)
    {
        ColorScheme scheme = Theme.Of(context).ColorScheme;

        float innerWidth = SlotSize - 8f;
        // Same min-stub as DurabilityBar so a near-zero sharpness still shows a sliver of its band colour.
        float fillWidth = Math.Clamp(Math.Max(innerWidth * Ratio, BarHeight), 0f, innerWidth);
        bool dull = Ratio < DullThreshold;

        var layers = new List<Widget>(3);

        // 1. Track (full width). Matches DurabilityBar's dark track; a faint themed outline keeps the
        //    bar's extent legible even when nearly empty, escalating to Error when critically dull.
        Vector4 outline = dull ? scheme.Error : WithAlpha(scheme.OutlineVariant, 0.9f);
        layers.Add(new Container(new BoxStyle
        {
            Height = BarHeight,
            Color = new Vector4(0f, 0f, 0f, 0.55f),
            CornerRadius = new Vector4(1.5f),
            BorderThickness = dull ? 1f : 0.75f,
            BorderColor = outline
        }));

        // 2. Fresh-tool hint, under the fill so it only shows in the unsharp negative space.
        if (IsFresh)
        {
            layers.Add(new SharpnessGhostPulse());
        }

        // 3. Fill, coloured to match the player's Toolsmith mode.
        layers.Add(BuildFill(fillWidth, innerWidth));

        return new Stack(layers);
    }

    private Widget BuildFill(float fillWidth, float innerWidth)
    {
        if (Mode == ToolsmithSharpnessConfig.SharpnessMode.Sections)
        {
            return BuildSectionsFill(fillWidth, innerWidth);
        }

        Vector4 color = Mode == ToolsmithSharpnessConfig.SharpnessMode.Gradient
            ? SharpnessPalette.GradientColor(Ratio, GradientSelection)
            : SharpnessPalette.BandColor(Ratio);

        return new Container(new BoxStyle
        {
            Width = fillWidth,
            Height = BarHeight,
            Color = color,
            CornerRadius = new Vector4(1.5f)
        });
    }

    // Five flat segments (0.15/0.15/0.3/0.2/0.2 of the inner width), each clipped to the fill,
    // laid left-to-right - mirroring Toolsmith's all-sections rendering.
    private Widget BuildSectionsFill(float fillWidth, float innerWidth)
    {
        var segments = new List<Widget>(SharpnessPalette.SectionFractions.Length);
        float used = 0f;

        for (int i = 0; i < SharpnessPalette.SectionFractions.Length; i++)
        {
            float segWidth = SharpnessPalette.SectionFractions[i] * innerWidth;
            if (used + segWidth > fillWidth)
            {
                segWidth = fillWidth - used;
            }

            if (segWidth <= 0f)
            {
                break;
            }

            segments.Add(new Container(new BoxStyle
            {
                Width = segWidth,
                Height = BarHeight,
                Color = SharpnessPalette.SectionColor(i),
                CornerRadius = new Vector4(1.5f)
            }));

            used += segWidth;
            if (used >= fillWidth)
            {
                break;
            }
        }

        return new Row(mainAxisSize: MainAxisSize.Min, children: segments);
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha)
    {
        color.W = alpha;
        return color;
    }
}
