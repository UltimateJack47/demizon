using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

[QueryProperty(nameof(EventId), "eventId")]
public partial class EditEventViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private int _eventId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private DateTime _dateFromDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _dateFromTime = new(18, 0, 0);

    [ObservableProperty]
    private DateTime _dateToDate = DateTime.Today;

    partial void OnDateFromDateChanged(DateTime value)
    {
        if (DateToDate < value)
            DateToDate = value;
    }

    [ObservableProperty]
    private TimeSpan _dateToTime = new(20, 0, 0);

    [ObservableProperty]
    private string? _place;

    [ObservableProperty]
    private int _notifyBeforeDaysIndex;

    [ObservableProperty]
    private int _recurrenceIndex;

    [ObservableProperty]
    private bool _isPublic;

    [ObservableProperty]
    private bool _isCancelled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public List<string> NotifyOptions { get; } =
    [
        "Bez notifikace",
        "1 den",
        "2 dny",
        "3 dny",
        "7 dní"
    ];

    public List<string> RecurrenceOptions { get; } =
    [
        "Jednorázová",
        "Týdně",
        "Měsíčně"
    ];

    private static readonly int?[] NotifyDaysMap = [null, 1, 2, 3, 7];
    private static readonly string[] RecurrenceMap = ["None", "Weekly", "Monthly"];

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var ev = await apiClient.GetEventAsync(EventId);
            Name = ev.Name;
            DateFromDate = ev.DateFrom.Date;
            DateFromTime = ev.DateFrom.TimeOfDay;
            DateToDate = ev.DateTo.Date;
            DateToTime = ev.DateTo.TimeOfDay;
            Place = ev.Place;
            IsPublic = ev.IsPublic;
            IsCancelled = ev.IsCancelled;

            NotifyBeforeDaysIndex = ev.NotifyBeforeDays switch
            {
                1 => 1,
                2 => 2,
                3 => 3,
                7 => 4,
                _ => 0
            };

            RecurrenceIndex = ev.Recurrence?.ToLowerInvariant() switch
            {
                "weekly" => 1,
                "monthly" => 2,
                _ => 0
            };
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst akci.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Název akce je povinný.";
            return;
        }

        var dateFrom = DateFromDate.Date + DateFromTime;
        var dateTo = DateToDate.Date + DateToTime;

        if (dateFrom >= dateTo)
        {
            ErrorMessage = "Datum začátku musí být před datem konce.";
            return;
        }

        IsBusy = true;
        try
        {
            var request = new UpdateEventRequest(
                Name.Trim(),
                dateFrom,
                dateTo,
                string.IsNullOrWhiteSpace(Place) ? null : Place.Trim(),
                NotifyDaysMap[NotifyBeforeDaysIndex],
                RecurrenceMap[RecurrenceIndex],
                IsPublic,
                IsCancelled);

            await apiClient.UpdateEventAsync(EventId, request);

            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await navigation.GoBackAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se uložit akci.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        var confirm = await Application.Current!.MainPage!
            .DisplayAlert("Smazat akci", "Opravdu chcete smazat tuto akci?", "Smazat", "Zrušit");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            await apiClient.DeleteEventAsync(EventId);
            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await navigation.GoBackAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se smazat akci.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
