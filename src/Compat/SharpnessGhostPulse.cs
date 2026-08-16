// [FOLD-IN] KEEP this file (LibGUI bridge widget: the fresh-tool hint). Port unchanged — INTEGRATION.md L2.
using System;
using Gui.Widgets.Animations;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Painting;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness.Compat;

/// <summary>
/// The "sharpen me" hint for a freshly-forged tool: a faint, full-width bar drawn UNDER the fill so
/// it shows only in the still-unsharp negative space, gently breathing to draw the eye without
/// nagging. A newly crafted tool head can be sharpened for FREE before its first use, so this cue
/// exists purely to nudge the player to top it off - and it self-clears the moment the tool is used
/// (see <see cref="DurabilityReader"/>'s pristine check), so the animation only ever runs on tools
/// that genuinely still want sharpening.
///
/// Cadence: one ~1.5s breathe (ease in/out via a sine half-wave) followed by a short ~1s rest, on a
/// ~2.5s loop - so it's almost continuously pulsing with only a brief pause between breaths, since
/// the bar's on-screen area is tiny and a long rest made the hint easy to miss. Even at rest the
/// ghost stays faintly present so the unsharp negative space is always marked. The colour is the
/// theme's <c>Primary</c> accent (thematic, not a hard-coded call-to-action red).
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
    // One full loop; the breathe occupies the first slice, the remainder is a short rest. Kept
    // almost-continuous (~1.5s breathe + ~1s rest) so the tiny bar's hint is hard to miss.
    private static readonly TimeSpan CycleDuration = TimeSpan.FromMilliseconds(2500);
    private const double BreatheEnd = 1500.0 / 2500.0; // fraction of the loop spent breathing

    // Faint by design - a hint in the negative space, never a solid bar. Even at rest the ghost
    // stays faintly present so the unsharp space is always marked; the breathe lifts it briefly.
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
        // Lazily create the controller once we have a BuildContext (and thus a ticker provider).
        // AnimationController has no built-in repeat, so we relaunch it on Completed for a loop.
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
            return RestAlpha; // resting
        }

        // 0 -> 1 -> 0 smooth half-sine across the breathe window.
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
            // Loop: restart from the beginning of the breathe.
            _controller?.Forward(0.0);
        }
    }
}
