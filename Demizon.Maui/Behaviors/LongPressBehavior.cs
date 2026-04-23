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
/// Handles BOTH tap and long-press on the associated View by attaching Android's
/// native click/long-click listeners (<c>setOnClickListener</c> /
/// <c>setOnLongClickListener</c>) to the PlatformView.
///
/// Why both: as soon as we set <c>LongClickable = true</c> for long-press,
/// Android's <c>View.onTouchEvent</c> starts consuming touch events for that
/// view — which means parent containers (e.g. CollectionView's selection
/// plumbing) no longer see the tap. To keep tap working we have to handle it
/// on the same native view, via <c>Clickable = true</c> + <see cref="TapCommand"/>.
///
/// Long-press timing uses Android's built-in <c>CheckForLongPress</c> in
/// <c>onTouchEvent</c>, so no custom timer is needed. After a long-press fires
/// and we return <c>Handled = true</c>, Android suppresses the subsequent
/// <c>performClick</c> on finger-release automatically.
/// </summary>
public sealed class LongPressBehavior : Behavior<View>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    /// <summary>Fired on long-press (~500 ms hold).</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty TapCommandProperty =
        BindableProperty.Create(nameof(TapCommand), typeof(ICommand), typeof(LongPressBehavior));

    /// <summary>Fired on a normal tap (released within tap timeout).</summary>
    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LongPressBehavior));

    /// <summary>Parameter passed to both <see cref="Command"/> and <see cref="TapCommand"/>.
    /// When null, the associated View's BindingContext is used at gesture time —
    /// which matters for CollectionView items where the BindingContext changes
    /// as RecyclerView recycles the row.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private View? _associated;

#if ANDROID
    private global::Android.Views.View? _nativeView;
    private EventHandler<global::Android.Views.View.LongClickEventArgs>? _longClickHandler;
    private EventHandler? _clickHandler;
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
            // Both flags must be true: LongClickable schedules CheckForLongPress on DOWN;
            // Clickable lets ACTION_UP within tap-timeout fire performClick.
            view.LongClickable = true;
            view.Clickable = true;

            _longClickHandler = OnNativeLongClick;
            _clickHandler = OnNativeClick;
            view.LongClick += _longClickHandler;
            view.Click += _clickHandler;

            _nativeView = view;
        }
#endif
    }

    private void DetachNative()
    {
#if ANDROID
        if (_nativeView is not null)
        {
            if (_longClickHandler is not null) _nativeView.LongClick -= _longClickHandler;
            if (_clickHandler is not null) _nativeView.Click -= _clickHandler;
        }
        _nativeView = null;
        _longClickHandler = null;
        _clickHandler = null;
#endif
    }

#if ANDROID
    private void OnNativeLongClick(object? sender, global::Android.Views.View.LongClickEventArgs args)
    {
        // Consume so the corresponding ACTION_UP on this view does NOT also fire performClick.
        args.Handled = true;
        Fire(Command);
    }

    private void OnNativeClick(object? sender, EventArgs e)
    {
        // Defense in depth: Android already suppresses performClick after a consumed
        // long-press via mHasPerformedLongPress. The tracker check covers the rare case
        // where a click slips in from a different code path during the alert.
        if (LongPressTracker.JustFired) return;
        Fire(TapCommand);
    }
#endif

    private void Fire(ICommand? cmd)
    {
        if (cmd is null) return;
        var parameter = CommandParameter ?? _associated?.BindingContext;
        if (!cmd.CanExecute(parameter)) return;

        if (cmd == Command) LongPressTracker.LastFiredUtc = DateTime.UtcNow;
        MainThread.BeginInvokeOnMainThread(() => cmd.Execute(parameter));
    }
}
