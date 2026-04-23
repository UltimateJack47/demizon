using CommunityToolkit.Maui.Behaviors;
using Demizon.Contracts.Attendances;
using Demizon.Maui.Behaviors;
using Demizon.Maui.ViewModels.Attendance;

namespace Demizon.Maui.Pages.Attendance;

public partial class AllMembersAttendancePage : ContentPage
{
    private AllMembersAttendanceViewModel? _vm;

    // Fixed column widths
    private const double NameColumnWidth = 130;
    private const double CellWidth = 56;
    private const double RowHeight = 40;
    private const double HeaderHeight = 60;

    public AllMembersAttendancePage(AllMembersAttendanceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _vm = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;

        if (_vm is not null && !_vm.IsBusy && !_vm.HasData)
            _vm.LoadCommand.Execute(null);

#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is not null)
        {
            var interceptor = new Demizon.Maui.Platforms.Android.SwipeGestureInterceptor(
                activity,
                onSwipeLeft:  () => MainThread.BeginInvokeOnMainThread(() => _vm?.NextMonthCommand.Execute(null)),
                onSwipeRight: () => MainThread.BeginInvokeOnMainThread(() => _vm?.PreviousMonthCommand.Execute(null)));

            // Touch inside the rendered data-cell area should scroll the inner
            // ScrollView horizontally, not flip months. Exempt TableGrid's bounds
            // minus the name column (tapping the name column is still a month swipe,
            // as is tapping empty space below the last member row).
            interceptor.ShouldIgnoreStart = IsInsideDataArea;
            Demizon.Maui.Platforms.Android.MainActivity.CurrentSwipeInterceptor = interceptor;
        }
#endif
    }

#if ANDROID
    /// <summary>
    /// Returns true when a touch at the given screen coordinates should be left to
    /// the inner ScrollView instead of flipping the month. The exclusion zone is
    /// the rendered data-cell rectangle of <see cref="TableGrid"/>, minus the
    /// "Člen" column width — i.e. only where the user can meaningfully see
    /// horizontally-scrollable content.
    /// </summary>
    private bool IsInsideDataArea(float rawX, float rawY)
    {
        if (TableGrid.Handler?.PlatformView is not Android.Views.View tg) return false;
        if (TableScrollView.Handler?.PlatformView is not Android.Views.View sv) return false;
        if (tg.Width <= 0 || tg.Height <= 0) return false;

        // If the whole table already fits inside the viewport there is nothing to
        // scroll horizontally — don't steal swipes from the month-navigation gesture.
        // This keeps "short months" behaving like the AttendancePage: swipe anywhere.
        if (tg.Width <= sv.Width) return false;

        var density = tg.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var nameColPx = (int)(NameColumnWidth * density);

        var loc = new int[2];
        tg.GetLocationOnScreen(loc);

        // When the ScrollView is already scrolled horizontally, loc[0] goes negative.
        // That naturally widens the exclusion zone to the full visible strip, so the
        // user can scroll back without accidentally flipping the month mid-drag.
        var left = loc[0] + nameColPx;
        var top = loc[1];
        var right = loc[0] + tg.Width;
        var bottom = loc[1] + tg.Height;

        return rawX >= left && rawX <= right && rawY >= top && rawY <= bottom;
    }
#endif

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
#if ANDROID
        Demizon.Maui.Platforms.Android.MainActivity.CurrentSwipeInterceptor = null;
#endif
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AllMembersAttendanceViewModel.Table))
            MainThread.BeginInvokeOnMainThread(BuildTable);
    }

    private void BuildTable()
    {
        TableGrid.Children.Clear();
        TableGrid.ColumnDefinitions.Clear();
        TableGrid.RowDefinitions.Clear();

        var table = _vm?.Table;
        if (table is null || table.Members.Count == 0) return;

        var columns = table.Columns;
        var members = table.Members;

        // Define columns: name + one per attendance column
        TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = NameColumnWidth });
        foreach (var _ in columns)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = CellWidth });

        // Define rows: header + one per member
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = HeaderHeight });
        foreach (var _ in members)
            TableGrid.RowDefinitions.Add(new RowDefinition { Height = RowHeight });

        // Header row: column 0 = "Člen" label
        AddHeaderCell("Člen", 0, 0);

        // Header cells for each column
        for (int c = 0; c < columns.Count; c++)
        {
            var col = columns[c];
            AddColumnHeader(col, c + 1);
        }

        // Member rows
        for (int r = 0; r < members.Count; r++)
        {
            var member = members[r];
            int row = r + 1;

            // Name cell
            AddMemberNameCell(member.FullName, row);

            // Attendance cells
            for (int c = 0; c < columns.Count; c++)
            {
                var cell = member.Cells.Count > c ? member.Cells[c] : null;
                var col = columns[c];
                AddAttendanceCell(cell, col, row, c + 1, member.MemberId, member.FullName);
            }
        }
    }

    private void AddHeaderCell(string text, int col, int row)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#A8845E"),
            StrokeThickness = 0,
            Padding = new Thickness(4, 2),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };
        border.Content = new Label
        {
            Text = text,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(border, col);
        Grid.SetRow(border, row);
        TableGrid.Children.Add(border);
    }

    private void AddColumnHeader(MonthlyColumnDto col, int gridCol)
    {
        var bgColor = col.IsEvent ? Color.FromArgb("#C9A227") : Color.FromArgb("#A8845E");

        var border = new Border
        {
            BackgroundColor = bgColor,
            StrokeThickness = 0,
            Padding = new Thickness(4, 2),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var stack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 0
        };

        stack.Add(new Label
        {
            Text = col.Label,
            TextColor = Colors.White,
            FontSize = 10,
            FontAttributes = col.IsEvent ? FontAttributes.Bold : FontAttributes.None,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaximumWidthRequest = CellWidth - 4
        });

        stack.Add(new Label
        {
            Text = col.Date.ToString("d.M."),
            TextColor = Colors.White,
            FontSize = 9,
            Opacity = 0.85,
            HorizontalTextAlignment = TextAlignment.Center
        });

        border.Content = stack;

        // Tap header → navigate to own event detail (header is not member-specific)
        if (col.EventId.HasValue)
        {
            var eventId = col.EventId.Value;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                try { await _vm!.NavigateToEventCommand.ExecuteAsync(eventId); }
                catch { await DisplayAlert("Chyba", "Nepodařilo se otevřít detail akce.", "OK"); }
            };
            border.GestureRecognizers.Add(tap);
        }

        Grid.SetColumn(border, gridCol);
        Grid.SetRow(border, 0);
        TableGrid.Children.Add(border);
    }

    private void AddMemberNameCell(string name, int row)
    {
        var bg = row % 2 == 0 ? Color.FromArgb("#F5EEE0") : Colors.White;
        var border = new Border
        {
            BackgroundColor = bg,
            StrokeThickness = 0,
            Padding = new Thickness(6, 2),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };
        border.Content = new Label
        {
            Text = name,
            FontSize = 12,
            TextColor = Color.FromArgb("#4A3420"),
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(border, 0);
        Grid.SetRow(border, row);
        TableGrid.Children.Add(border);
    }

    private void AddAttendanceCell(MemberCellDto? cell, MonthlyColumnDto col, int row, int gridCol, int memberId, string memberFullName)
    {
        var bg = row % 2 == 0 ? Color.FromArgb("#F5EEE0") : Colors.White;
        if (col.IsCancelled) bg = Color.FromArgb("#F0E8E0");

        var border = new Border
        {
            BackgroundColor = bg,
            StrokeThickness = 0,
            Padding = new Thickness(2),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        string symbol;
        Color textColor;
        if (col.IsCancelled)
        {
            symbol = "–";
            textColor = Color.FromArgb("#AAAAAA");
        }
        else if (cell?.Status == "yes")
        {
            symbol = "✓";
            textColor = Color.FromArgb("#27AE60");
        }
        else if (cell?.Status == "maybe")
        {
            symbol = "?";
            textColor = Color.FromArgb("#F39C12");
        }
        else if (cell?.Status == "no")
        {
            symbol = "✗";
            textColor = Color.FromArgb("#E74C3C");
        }
        else
        {
            symbol = "·";
            textColor = Color.FromArgb("#BBBBBB");
        }

        border.Content = new Label
        {
            Text = symbol,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = textColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        bool hasComment = !string.IsNullOrWhiteSpace(cell?.Comment);
        if (hasComment)
        {
            var stack = new Grid
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };
            stack.Add(new Label
            {
                Text = symbol,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = textColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            });
            stack.Add(new Label
            {
                Text = "📝",
                FontSize = 8,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Start,
                Margin = new Thickness(0, 2, 2, 0)
            });
            border.Content = stack;
        }

        // Tap to edit — events and rehearsals, own row vs other member
        if (!col.IsCancelled)
        {
            var capturedMemberId = memberId;
            var capturedName = memberFullName;
            var capturedDate = col.Date;
            var tap = new TapGestureRecognizer();

            if (col.EventId.HasValue)
            {
                // Event cell
                var eventId = col.EventId.Value;
                if (_vm!.IsCurrentUser(capturedMemberId))
                {
                    tap.Tapped += async (_, _) =>
                    {
                        if (LongPressTracker.JustFired) return;
                        try { await _vm.NavigateToEventCommand.ExecuteAsync(eventId); }
                        catch { await DisplayAlert("Chyba", "Nepodařilo se otevřít detail akce.", "OK"); }
                    };
                }
                else if (_vm!.IsAdmin)
                {
                    tap.Tapped += async (_, _) =>
                    {
                        if (LongPressTracker.JustFired) return;
                        try { await _vm.NavigateToMemberAttendanceAsync(eventId, capturedMemberId, capturedName); }
                        catch { await DisplayAlert("Chyba", "Nepodařilo se otevřít docházku člena.", "OK"); }
                    };
                }
                else
                {
                    var commentText = cell?.Comment;
                    tap.Tapped += async (_, _) =>
                    {
                        if (LongPressTracker.JustFired) return;
                        if (!string.IsNullOrWhiteSpace(commentText))
                            await DisplayAlert($"Poznámka – {capturedName}", commentText, "OK");
                        else
                            await DisplayAlert("Info", "Editace docházky jiných členů je dostupná pouze pro administrátory.", "OK");
                    };
                }
            }
            else
            {
                // Rehearsal cell (EventId is null)
                if (_vm!.IsCurrentUser(capturedMemberId))
                {
                    tap.Tapped += async (_, _) =>
                    {
                        if (LongPressTracker.JustFired) return;
                        try { await _vm.NavigateToRehearsalAsync(capturedDate); }
                        catch { await DisplayAlert("Chyba", "Nepodařilo se otevřít detail zkoušky.", "OK"); }
                    };
                }
                else if (_vm!.IsAdmin)
                {
                    tap.Tapped += async (_, _) =>
                    {
                        if (LongPressTracker.JustFired) return;
                        try { await _vm.NavigateToMemberRehearsalAsync(capturedDate, capturedMemberId, capturedName); }
                        catch { await DisplayAlert("Chyba", "Nepodařilo se otevřít docházku člena.", "OK"); }
                    };
                }
                else
                {
                    var commentText = cell?.Comment;
                    tap.Tapped += async (_, _) =>
                    {
                        if (LongPressTracker.JustFired) return;
                        if (!string.IsNullOrWhiteSpace(commentText))
                            await DisplayAlert($"Poznámka – {capturedName}", commentText, "OK");
                        else
                            await DisplayAlert("Info", "Editace docházky jiných členů je dostupná pouze pro administrátory.", "OK");
                    };
                }
            }

            border.GestureRecognizers.Add(tap);
        }

        // Long-press on a cell with a comment shows that member's note in a dialog.
        // Uses CommunityToolkit's TouchBehavior because it handles OnTouchListener
        // chaining with the TapGestureRecognizer above — our own LongPressBehavior
        // (which uses view.Touch) would otherwise overwrite the tap recognizer's listener.
        if (hasComment)
        {
            var noteText = cell!.Comment!;
            var noteTitle = $"Poznámka – {memberFullName}";
            border.Behaviors.Add(new TouchBehavior
            {
                LongPressDuration = 500,
                LongPressCommand = new Command(async () =>
                {
                    LongPressTracker.LastFiredUtc = DateTime.UtcNow;
                    try { await DisplayAlert(noteTitle, noteText, "OK"); }
                    catch { /* page torn down */ }
                })
            });
        }

        Grid.SetColumn(border, gridCol);
        Grid.SetRow(border, row);
        TableGrid.Children.Add(border);
    }
}
