using System;
using System.Collections.Generic;
using Gui.Core.Layout;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Layout;
using Gui.Widgets.Painting;
using LibGuiToolsmithSharpness.Toolsmith;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness.Compat;

/// <summary>
/// Sharpness bar for a LibGUI item slot. Mirrors LibGUI's DurabilityBar geometry (3px track +
/// ratio fill, same (SlotSize - 8) * ratio width) so the two read as a pair. Fill colour matches
/// the player's Toolsmith display mode via <see cref="SharpnessPalette"/>.
///
/// Always visible, even at 100%. Toolsmith hides the bar when keen, but on a LibGUI slot a missing
/// bar is ambiguous — sharp, or just untracked? When you're doing a batch forging pass and scanning
/// across a row of tools, you need the bar to mean something reliable: sweep = done, fill = in
/// progress, ghost pulse = free hone still available. An absent bar breaks that scan.
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

        // Keen: bar stays visible at 100% as a positive "done, move on" confirmation rather than
        // disappearing. In a batch forging pass, the sweep means this tool is finished.
        if (Ratio >= 1f)
        {
            return BuildKeen(scheme, innerWidth);
        }

        // No min-stub: at zero sharpness we want a clean empty (bordered) track, not a stray sliver.
        float fillWidth = Math.Clamp(innerWidth * Ratio, 0f, innerWidth);
        bool dull = Ratio < DullThreshold;

        var layers = new List<Widget>(3);

        // 1. Track — must be explicit full width, or a zero-fill Stack collapses it to nothing and the
        //    bar visually disappears when blunt (which is exactly when you most need to see it). The
        //    outline makes "fully blunt" a visible, deliberate state rather than an absent one —
        //    sharpening a blunt tool costs significant head durability, so the player should be choosing
        //    it consciously, not discovering it by accident. Escalates to Error when critically dull.
        Vector4 outline = dull ? scheme.Error : WithAlpha(scheme.OutlineVariant, 0.9f);
        layers.Add(new Container(new BoxStyle
        {
            Width = innerWidth,
            Height = BarHeight,
            Color = new Vector4(0f, 0f, 0f, 0.55f),
            CornerRadius = new Vector4(1.5f),
            BorderThickness = dull ? 1f : 0.75f,
            BorderColor = outline
        }));

        // 2. Fresh-tool hint — under the fill so it only shows in the unsharp negative space.
        //    Full width; the opaque fill on top masks the already-sharpened portion.
        if (IsFresh)
        {
            layers.Add(new SharpnessGhostPulse(innerWidth));
        }

        // 3. Fill, coloured to match the player's Toolsmith mode.
        layers.Add(BuildFill(fillWidth, innerWidth));

        return new Stack(layers);
    }

    // Fully-sharp bar: a solid "keen" bar coloured from the player's palette top, with a soft
    // highlight that periodically sweeps across it - a positive "this edge is keen" signal.
    private Widget BuildKeen(ColorScheme scheme, float innerWidth)
    {
        Vector4 baseColor = SharpnessPalette.TopColor(Mode, GradientSelection);
        Vector4 outline = WithAlpha(scheme.OutlineVariant, 0.9f);
        return new SharpnessKeenSweep(innerWidth, baseColor, outline);
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
