using System;
using Gui.Widgets.Animations;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Painting;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness.Compat;

/// <summary>
/// The fully-sharp ("keen") indicator: a solid bar in the palette top colour with a soft highlight
/// that glides left-to-right, then loops. The pace is deliberately slow and settled — this is the
/// "done, move on" signal in a batch forging pass, so it shouldn't feel active or demanding. It just
/// needs to read as a positive resting state, distinct from an empty or filling bar.
/// Plain animated gradient, no shader.
/// </summary>
public class SharpnessKeenSweep : StatefulWidget
{
    public float Width { get; }
    public Vector4 BaseColor { get; }
    public Vector4 BorderColor { get; }

    public SharpnessKeenSweep(float width, Vector4 baseColor, Vector4 borderColor,
        Gui.Widgets.Framework.Key? key = null)
        : base(key)
    {
        Width = width;
        BaseColor = baseColor;
        BorderColor = borderColor;
    }

    public override State CreateState()
    {
        return new SharpnessKeenSweepState();
    }
}

internal sealed class SharpnessKeenSweepState : State<SharpnessKeenSweep>
{
    // 3s continuous glide, no rest gap. Slow enough to read as "settled" rather than animated.
    private static readonly TimeSpan CycleDuration = TimeSpan.FromMilliseconds(3000);
    private const double SweepFraction = 3000.0 / 3000.0; // 1.0 = rest branch never fires

    private const float BandHalf = 0.16f;  // half-width of the bright band in gradient-position space
    private const float GleamLift = 0.65f; // how far the band lightens toward white

    private AnimationController? _controller;

    public override void Dispose()
    {
        if (_controller != null)
        {
            _controller.OnValueChanged -= OnValueChanged;
            _controller.OnStatusChanged -= OnStatusChanged;
            _controller.Dispose();
            _controller = null;
        }

        base.Dispose();
    }

    public override Widget Build(BuildContext context)
    {
        if (_controller == null)
        {
            _controller = new AnimationController(CycleDuration, context.GetTickerProvider());
            _controller.OnValueChanged += OnValueChanged;
            _controller.OnStatusChanged += OnStatusChanged;
            _controller.Forward();
        }

        double t = _controller.Value;
        if (t >= SweepFraction)
        {
            return KeenBar(Widget.BaseColor, gradient: null);
        }

        double p = t / SweepFraction;
        double eased = p * p * (3.0 - 2.0 * p); // smoothstep
        float center = (float)(-BandHalf + eased * (1.0 + 2.0 * BandHalf));

        float lo = Math.Clamp(center - BandHalf, 0f, 1f);
        float mid = Math.Clamp(center, 0f, 1f);
        float hi = Math.Clamp(center + BandHalf, 0f, 1f);

        Vector4 baseColor = Widget.BaseColor;
        Vector4 bright = Lighten(baseColor, GleamLift);

        var gradient = new LinearGradient(0f,
            new GradientStop(baseColor, 0f),
            new GradientStop(baseColor, lo),
            new GradientStop(bright, mid),
            new GradientStop(baseColor, hi),
            new GradientStop(baseColor, 1f));

        return KeenBar(baseColor, gradient);
    }

    private Widget KeenBar(Vector4 fill, Gradient? gradient)
    {
        return new Container(new BoxStyle
        {
            Width = Widget.Width,
            Height = SharpnessBar.BarHeight,
            Color = fill,
            Gradient = gradient,
            CornerRadius = new Vector4(1.5f),
            BorderThickness = 0.75f,
            BorderColor = Widget.BorderColor
        });
    }

    private static Vector4 Lighten(Vector4 color, float amount)
    {
        return new Vector4(
            color.X + (1f - color.X) * amount,
            color.Y + (1f - color.Y) * amount,
            color.Z + (1f - color.Z) * amount,
            color.W);
    }

    private void OnValueChanged(double value)
    {
        SetState(() => { });
    }

    private void OnStatusChanged(AnimationStatus status)
    {
        if (status == AnimationStatus.Completed)
        {
            _controller?.Forward(0.0);
        }
    }
}
