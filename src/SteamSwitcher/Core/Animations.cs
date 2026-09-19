using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SteamSwitcher.Core;

/// <summary>
/// Reusable motion helpers.
///
/// Timings follow the Fluent 2 motion guidance: short durations (150-350 ms),
/// decelerating easing for entrances, and staggered offsets for lists so a
/// group of cards resolves in sequence instead of snapping in all at once.
/// </summary>
public static class Anim
{
    // Fluent-style easings.
    public static readonly IEasingFunction EaseOut =
        new CubicEase { EasingMode = EasingMode.EaseOut };

    public static readonly IEasingFunction EaseInOut =
        new CubicEase { EasingMode = EasingMode.EaseInOut };

    public static readonly IEasingFunction EaseBack =
        new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

    private static Duration Ms(double value) => new(TimeSpan.FromMilliseconds(value));

    /// <summary>
    /// Ensures the element has a TranslateTransform we can animate without
    /// clobbering a transform the template already set.
    /// </summary>
    private static TranslateTransform EnsureTranslate(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform existing)
            return existing;

        if (element.RenderTransform is TransformGroup group)
        {
            foreach (var child in group.Children)
                if (child is TranslateTransform found) return found;

            var added = new TranslateTransform();
            group.Children.Add(added);
            return added;
        }

        var translate = new TranslateTransform();
        element.RenderTransform = translate;
        return translate;
    }

    private static ScaleTransform EnsureScale(UIElement element)
    {
        if (element.RenderTransform is ScaleTransform existing) return existing;

        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        if (element.RenderTransform is TransformGroup group)
        {
            group.Children.Add(scale);
            return scale;
        }

        element.RenderTransform = scale;
        return scale;
    }

    /// <summary>Fades an element in while it rises slightly into place.</summary>
    public static void FadeSlideIn(UIElement element, double fromY = 14, double ms = 260, double delayMs = 0)
    {
        if (element is null) return;

        var translate = EnsureTranslate(element);
        element.Opacity = 0;
        translate.Y = fromY;

        var begin = TimeSpan.FromMilliseconds(delayMs);

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = Ms(ms),
            BeginTime = begin,
            EasingFunction = EaseOut
        });

        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = fromY,
            To = 0,
            Duration = Ms(ms),
            BeginTime = begin,
            EasingFunction = EaseOut
        });
    }

    /// <summary>Slides a page in from the right — used for navigation.</summary>
    public static void PageIn(UIElement element, double ms = 300)
    {
        if (element is null) return;

        var translate = EnsureTranslate(element);
        element.Opacity = 0;

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = Ms(ms),
            EasingFunction = EaseOut
        });

        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = 26,
            To = 0,
            Duration = Ms(ms),
            EasingFunction = EaseOut
        });
    }

    /// <summary>
    /// Staggers a fade-slide across a set of elements so a group of cards
    /// resolves in sequence. Offsets stay short so the whole group still
    /// settles quickly.
    /// </summary>
    public static void Stagger(IEnumerable<UIElement> elements, double stepMs = 55, double ms = 280, double fromY = 16)
    {
        var index = 0;
        foreach (var element in elements)
        {
            FadeSlideIn(element, fromY, ms, index * stepMs);
            index++;
        }
    }

    /// <summary>Staggers the direct children of a panel.</summary>
    public static void StaggerChildren(Panel panel, double stepMs = 55, double ms = 280, double fromY = 16)
    {
        if (panel is null) return;
        Stagger(panel.Children.OfType<UIElement>(), stepMs, ms, fromY);
    }

    /// <summary>Pops an element for emphasis, e.g. a value that just changed.</summary>
    public static void Pulse(UIElement element, double peak = 1.06, double ms = 320)
    {
        if (element is null) return;

        var scale = EnsureScale(element);

        var animation = new DoubleAnimationUsingKeyFrames { Duration = Ms(ms) };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromPercent(0.45)) { EasingFunction = EaseOut });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1)) { EasingFunction = EaseBack });

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
    }

    /// <summary>Animates a number counting up — used for the metric tiles.</summary>
    public static void CountTo(TextBlock target, double to, string format = "0", double ms = 650)
    {
        if (target is null) return;

        double from = 0;
        var existing = target.Text?.Replace(",", "") ?? "";
        if (double.TryParse(existing, out var parsed)) from = parsed;

        // A very small change is not worth animating.
        if (Math.Abs(to - from) < 0.5)
        {
            target.Text = to.ToString(format);
            return;
        }

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = Ms(ms),
            EasingFunction = EaseOut
        };

        var clock = animation.CreateClock();
        clock.CurrentTimeInvalidated += (_, _) =>
        {
            if (clock.CurrentProgress is { } progress)
                target.Text = (from + (to - from) * progress).ToString(format);
        };
        clock.Completed += (_, _) => target.Text = to.ToString(format);
        clock.Controller?.Begin();
    }

    /// <summary>Smoothly animates a progress/health bar to a new width ratio.</summary>
    public static void AnimateWidth(FrameworkElement element, double toWidth, double ms = 700)
    {
        if (element is null) return;

        element.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation
        {
            To = toWidth,
            Duration = Ms(ms),
            EasingFunction = EaseOut
        });
    }

    /// <summary>Fades an element out and optionally collapses it afterwards.</summary>
    public static void FadeOut(UIElement element, bool collapse = true, double ms = 200)
    {
        if (element is null) return;

        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = Ms(ms),
            EasingFunction = EaseOut
        };

        if (collapse)
            animation.Completed += (_, _) => element.Visibility = Visibility.Collapsed;

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    /// <summary>Reveals a collapsed element with a fade.</summary>
    public static void FadeIn(UIElement element, double ms = 220)
    {
        if (element is null) return;

        element.Visibility = Visibility.Visible;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = Ms(ms),
            EasingFunction = EaseOut
        });
    }
}
