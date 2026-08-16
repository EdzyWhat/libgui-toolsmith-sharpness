using System;
using Gui.Widgets.Animations;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Painting;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness.Compat;

/// <summary>
/// "Sharpen me" hint for a freshly-forged tool: a faint full-width bar drawn UNDER the fill, visible
/// only in the still-unsharp negative space. The first hone on a new tool is free, so this is a
/// nudge, not an alarm — slow enough to feel calm, present enough not to miss on a small bar.
/// Self-clears the moment the tool is used (<see cref="DurabilityReader"/> pristine check).
/// Theme Primary colour so it reads as "opportunity" rather than "danger."
/// </summary>
public class SharpnessGhostPulse : StatefulWidget
{
    /// <summary>Full width of the bar, so the hint spans the whole track (not a collapsed 0px).</summary>
    public float Width { get; }

    public SharpnessGhostPulse(float width, Gui.Widgets.Framework.Key? key = null)
        : base(key)
    {
        Width = width;
    }

    public override State CreateState()
    {
        return new SharpnessGhostPulseState();
    }
}

internal sealed class SharpnessGhostPulseState : State<SharpnessGhostPulse>
{
    // ~1.5s breathe, ~1s rest, 2.5s loop. Short rest so the hint stays visible on a tiny bar.
    private static readonly TimeSpan CycleDuration = TimeSpan.FromMilliseconds(2500);
    private const double BreatheEnd = 1500.0 / 2500.0;

    // Faint at rest so it doesn't compete with the fill; breathe lifts it just enough to register.
    private const float RestAlpha = 0.12f;
    private const float PeakAlpha = 0.42f;

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
        // Lazily create — needs a BuildContext for the ticker provider. Loop by restarting on Completed.
        if (_controller == null)
        {
            _controller = new AnimationController(CycleDuration, context.GetTickerProvider());
            _controller.OnValueChanged += OnValueChanged;
            _controller.OnStatusChanged += OnStatusChanged;
            _controller.Forward();
        }

        float alpha = AlphaFor(_controller.Value);
        Vector4 ghost = Theme.Of(context).ColorScheme.Primary;
        ghost.W = alpha;

        return new Container(new BoxStyle
        {
            Width = Widget.Width,
            Height = SharpnessBar.BarHeight,
            Color = ghost,
            CornerRadius = new Vector4(1.5f)
        });
    }

    private static float AlphaFor(double t)
    {
        if (t >= BreatheEnd)
        {
            return RestAlpha;
        }

        // 0 → 1 → 0 half-sine across the breathe window.
        double phase = t / BreatheEnd;
        double pulse = Math.Sin(phase * Math.PI);
        return (float)(RestAlpha + (PeakAlpha - RestAlpha) * pulse);
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
