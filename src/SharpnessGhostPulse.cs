using System;
using Gui.Widgets.Animations;
using Gui.Widgets.Basic;
using Gui.Widgets.Framework;
using Gui.Widgets.Painting;
using OpenTK.Mathematics;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// The "sharpen me" hint for a freshly-forged tool: a faint, full-width bar drawn UNDER the fill so
/// it shows only in the still-unsharp negative space, gently breathing to draw the eye without
/// nagging. A newly crafted tool head can be sharpened for FREE before its first use, so this cue
/// exists purely to nudge the player to top it off - and it self-clears the moment the tool is used
/// (see <see cref="DurabilityReader"/>'s pristine check), so the animation only ever runs on tools
/// that genuinely still want sharpening.
///
/// Cadence: one slow ~1.8s breathe (ease in/out via a sine half-wave) followed by a ~3s rest, on a
/// ~4.8s loop. It's an "attend to this soon", not an "act now", so the long rest keeps it calm.
/// The colour is the theme's <c>Primary</c> accent (thematic, not a hard-coded call-to-action red).
/// </summary>
public class SharpnessGhostPulse : StatefulWidget
{
    public SharpnessGhostPulse(Gui.Widgets.Framework.Key? key = null)
        : base(key)
    {
    }

    public override State CreateState()
    {
        return new SharpnessGhostPulseState();
    }
}

internal sealed class SharpnessGhostPulseState : State
{
    // One full loop; the breathe occupies the first slice, the remainder is a calm rest.
    private static readonly TimeSpan CycleDuration = TimeSpan.FromMilliseconds(4800);
    private const double BreatheEnd = 1800.0 / 4800.0; // fraction of the loop spent breathing

    // Faint by design - a hint in the negative space, never a solid bar.
    private const float RestAlpha = 0.10f;
    private const float PeakAlpha = 0.34f;

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
