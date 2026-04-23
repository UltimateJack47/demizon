using Android.Content;
using Android.Views;

namespace Demizon.Maui.Platforms.Android;

/// <summary>
/// Activity-level horizontal-swipe detector. Installed via
/// <see cref="MainActivity.CurrentSwipeInterceptor"/> so it sees every touch
/// before the view tree — this avoids RefreshView / CollectionView consuming
/// events before we can evaluate them.
///
/// Only swallows touches once motion is unambiguously horizontal
/// (|dx| &gt; slop AND |dx| &gt; |dy| * 1.5). Otherwise all events pass through,
/// so pull-to-refresh and vertical scrolling keep working normally.
/// </summary>
internal sealed class SwipeGestureInterceptor
{
    // Thresholds expressed in dp, scaled by screen density at construction.
    private const float HorizontalCommitDp = 40f;
    private const float DominanceRatio = 1.5f;

    private readonly Action _onSwipeLeft;
    private readonly Action _onSwipeRight;
    private readonly float _horizontalCommitPx;

    private float _startX;
    private float _startY;
    private bool _tracking;
    private bool _committedHorizontal;

    public SwipeGestureInterceptor(Context context, Action onSwipeLeft, Action onSwipeRight)
    {
        _onSwipeLeft = onSwipeLeft;
        _onSwipeRight = onSwipeRight;
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        _horizontalCommitPx = HorizontalCommitDp * density;
    }

    /// <summary>
    /// Returns <c>true</c> if this interceptor has taken ownership of the gesture
    /// and the event should NOT be dispatched to the view tree.
    /// </summary>
    public bool OnDispatchTouch(MotionEvent ev)
    {
        switch (ev.ActionMasked)
        {
            case MotionEventActions.Down:
                _startX = ev.GetX();
                _startY = ev.GetY();
                _tracking = true;
                _committedHorizontal = false;
                return false;

            case MotionEventActions.Move:
                if (!_tracking) return false;
                float dx = ev.GetX() - _startX;
                float dy = ev.GetY() - _startY;
                if (!_committedHorizontal
                    && Math.Abs(dx) > _horizontalCommitPx
                    && Math.Abs(dx) > Math.Abs(dy) * DominanceRatio)
                {
                    _committedHorizontal = true;
                }
                return _committedHorizontal;

            case MotionEventActions.Up:
                if (!_tracking) return false;
                _tracking = false;
                if (_committedHorizontal)
                {
                    var totalDx = ev.GetX() - _startX;
                    _committedHorizontal = false;
                    if (totalDx < 0) _onSwipeLeft();
                    else _onSwipeRight();
                    return true;
                }
                return false;

            case MotionEventActions.Cancel:
                _tracking = false;
                var wasCommitted = _committedHorizontal;
                _committedHorizontal = false;
                return wasCommitted;
        }
        return false;
    }
}
