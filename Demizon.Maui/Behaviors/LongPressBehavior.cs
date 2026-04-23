using System.Windows.Input;

namespace Demizon.Maui.Behaviors;

/// <summary>
/// Global coordination between <see cref="LongPressBehavior"/> and tap handlers:
/// after a long-press fires, tap handlers should check <see cref="JustFired"/> and bail,
/// so a single gesture doesn't trigger BOTH the long-press alert AND the tap navigation.
/// </summary>
public static class LongPressTracker
{
    private const int SuppressWindowMs = 600;

    public static DateTime LastFiredUtc { get; set; } = DateTime.MinValue;

    public static bool JustFired =>
        (DateTime.UtcNow - LastFiredUtc).TotalMilliseconds < SuppressWindowMs;
}

/// <summary>
/// Detects long-press (≥500 ms hold) by timing ACTION_DOWN to ACTION_UP on the
/// associated View's native Touch event. Critically it always returns
/// <c>Handled = false</c> and never makes the View Clickable — so tap events
/// still bubble up to parent containers (e.g. RecyclerView selection on
/// CollectionView items).
///
/// Use this for views that DON'T already have a MAUI TapGestureRecognizer —
/// for those, use CommunityToolkit.Maui.Behaviors.TouchBehavior, which has its
/// own OnTouchListener chaining.
/// </summary>
public sealed class LongPressBehavior : Behavior<View>
{
    private const int LongPressMs = 500;

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LongPressBehavior));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private View? _associated;

#if ANDROID
    private global::Android.Views.View? _nativeView;
    private EventHandler<global::Android.Views.View.TouchEventArgs>? _touchHandler;
    private DateTime _pressStart;
#endif

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _associated = bindable;
        bindable.HandlerChanged += OnHandlerChanged;
        if (bindable.Handler is not null) OnHandlerChanged(bindable, EventArgs.Empty);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        DetachNative();
        _associated = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        DetachNative();

#if ANDROID
        if (_associated?.Handler?.PlatformView is global::Android.Views.View view)
        {
            _touchHandler = OnNativeTouch;
            view.Touch += _touchHandler;
            _nativeView = view;
        }
#endif
    }

    private void DetachNative()
    {
#if ANDROID
        if (_nativeView is not null && _touchHandler is not null)
            _nativeView.Touch -= _touchHandler;
        _nativeView = null;
        _touchHandler = null;
#endif
    }

#if ANDROID
    private void OnNativeTouch(object? sender, global::Android.Views.View.TouchEventArgs args)
    {
        // Never consume — we only observe, so RecyclerView selection etc. keep working.
        args.Handled = false;

        var evt = args.Event;
        if (evt is null) return;

        switch (evt.ActionMasked)
        {
            case global::Android.Views.MotionEventActions.Down:
                _pressStart = DateTime.UtcNow;
                break;

            case global::Android.Views.MotionEventActions.Up:
                var duration = (DateTime.UtcNow - _pressStart).TotalMilliseconds;
                if (duration >= LongPressMs)
                {
                    FireCommand();
                }
                break;
        }
    }
#endif

    private void FireCommand()
    {
        var parameter = CommandParameter ?? _associated?.BindingContext;
        var cmd = Command;
        if (cmd is null || !cmd.CanExecute(parameter)) return;

        LongPressTracker.LastFiredUtc = DateTime.UtcNow;
        MainThread.BeginInvokeOnMainThread(() => cmd.Execute(parameter));
    }
}
