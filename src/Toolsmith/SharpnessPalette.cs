using System;
using OpenTK.Mathematics;
using Vintagestory.API.MathTools;

namespace LibGuiToolsmithSharpness.Toolsmith;

/// <summary>
/// The exact sharpness-bar colour maths from Toolsmith (<c>Toolsmith.ToolTinkering.TinkeringUtility</c>),
/// reproduced so our LibGUI bar matches what standalone Toolsmith paints for the player's chosen mode.
///
/// The hex palettes are copied verbatim from Toolsmith source (we own no Toolsmith.dll reference); only
/// the player's *mode/selection* is read live from config via <see cref="ToolsmithSharpnessConfig"/>.
/// We feed the same hex strings through the same <see cref="ColorUtil"/> calls Toolsmith uses
/// (<c>Hex2Int</c> -> <c>ColorOverlay</c> -> <c>ToRGBAFloats</c>), so the resulting RGB is identical.
/// </summary>
public static class SharpnessPalette
{
    // --- Toolsmith palettes (verbatim from TinkeringUtility) --------------------------------------

    // GradientSelection 0 - the "sharpness" purple->cyan ramp (Toolsmith's only assigned selection).
    private static readonly int[] SharpnessColors = Hexes(
        "#7e0279", "#7a3299", "#6f4eb6", "#5e67ce", "#457fe2", "#1995f0",
        "#00abf8", "#00bffd", "#00d3fd", "#00e6fb", "#43f8f8");

    // GradientSelection 1 - "unpleasant" ramp.
    private static readonly int[] UnpleasantGradient = Hexes(
        "#9e5400", "#c34523", "#e5274a", "#fe007c", "#ff00b8", "#fa35fd",
        "#ff5fa3", "#ff746a", "#fb8e00", "#aebb00", "#23d726");

    // GradientSelection 2 - Monster-Hunter ramp.
    private static readonly int[] MonhunGradient = Hexes(
        "#ff0f00", "#ff4b00", "#ff6b00", "#ffb300", "#fff700", "#b4fd00",
        "#24ff00", "#009aab", "#0000FF", "#8080ff", "#ffffff");

    // Flat band / section colours (indices 0-4 used by the bar; 5-6 unused here).
    private static readonly int[] FlatLevelColors = Hexes(
        "#ff0f00", "#ff6b00", "#fff700", "#24ff00", "#50b0ff", "#ffffff", "#c050ff");

    /// <summary>Section fractions of the fill width, left to right (Toolsmith's 5-segment layout).</summary>
    public static readonly float[] SectionFractions = { 0.15f, 0.15f, 0.3f, 0.2f, 0.2f };

    // --- Public colour selectors -----------------------------------------------------------------

    /// <summary>Flat-band colour for the default mode: red/orange/yellow/green/light-blue by band.</summary>
    public static Vector4 BandColor(float ratio)
    {
        int index = ratio < 0.15f ? 0
            : ratio < 0.3f ? 1
            : ratio < 0.6f ? 2
            : ratio < 0.8f ? 3
            : 4;
        return ToVec(FlatLevelColors[index]);
    }

    /// <summary>Colour of section <paramref name="index"/> (0-4) in all-sections mode.</summary>
    public static Vector4 SectionColor(int index)
    {
        return ToVec(FlatLevelColors[Math.Clamp(index, 0, 4)]);
    }

    /// <summary>
    /// The "keen" colour for a given mode - i.e. what the fill would be at 100% sharp - so a
    /// fully-sharp bar is coloured from the player's own palette (the top band/section, or the top
    /// of the gradient ramp). For the default flat mode this is Toolsmith's light-blue top band.
    /// </summary>
    public static Vector4 TopColor(ToolsmithSharpnessConfig.SharpnessMode mode, int selection)
    {
        return mode switch
        {
            ToolsmithSharpnessConfig.SharpnessMode.Gradient => GradientColor(1f, selection),
            ToolsmithSharpnessConfig.SharpnessMode.Sections => SectionColor(SectionFractions.Length - 1),
            _ => BandColor(1f),
        };
    }

    /// <summary>
    /// Smooth-gradient colour, mirroring Toolsmith's <c>InitializeSharpnessColorGradient</c> +
    /// <c>GetItemSharpnessColor</c>: index = clamp(100*ratio, 0, 99); the gradient interpolates
    /// between palette stops i and i+1 by the tenths digit.
    /// </summary>
    public static Vector4 GradientColor(float ratio, int selection)
    {
        int[] palette = selection switch
        {
            1 => UnpleasantGradient,
            2 => MonhunGradient,
            _ => SharpnessColors,
        };

        int index = Math.Clamp((int)(100f * ratio), 0, 99);
        int stop = index / 10;      // 0..9
        int tenths = index % 10;    // 0..9
        int color = ColorUtil.ColorOverlay(palette[stop], palette[stop + 1], tenths / 10f);
        return ToVec(color);
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static int[] Hexes(params string[] hex)
    {
        var ints = new int[hex.Length];
        for (int i = 0; i < hex.Length; i++)
        {
            ints[i] = ColorUtil.Hex2Int(hex[i]);
        }

        return ints;
    }

    private static Vector4 ToVec(int color)
    {
        // Same channel order Toolsmith reads (RGB from ToRGBAFloats); bar is fully opaque.
        float[] rgba = ColorUtil.ToRGBAFloats(color);
        return new Vector4(rgba[0], rgba[1], rgba[2], 1f);
    }
}
