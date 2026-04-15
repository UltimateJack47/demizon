using Demizon.Contracts.Attendances;
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
        _vm?.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
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
                AddAttendanceCell(cell, col, row, c + 1);
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

        // Tap to navigate to event detail
        if (col.EventId.HasValue)
        {
            var eventId = col.EventId.Value;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await _vm!.NavigateToEventCommand.ExecuteAsync(eventId);
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

    private void AddAttendanceCell(MemberCellDto? cell, MonthlyColumnDto col, int row, int gridCol)
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
        else if (cell?.Attends == true)
        {
            symbol = "✓";
            textColor = Color.FromArgb("#27AE60");
        }
        else if (cell?.Attends == false)
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

        // Tap to edit: navigate to event detail if eventId available
        if (col.EventId.HasValue && !col.IsCancelled)
        {
            var eventId = col.EventId.Value;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await _vm!.NavigateToEventCommand.ExecuteAsync(eventId);
            border.GestureRecognizers.Add(tap);
        }

        Grid.SetColumn(border, gridCol);
        Grid.SetRow(border, row);
        TableGrid.Children.Add(border);
    }
}
