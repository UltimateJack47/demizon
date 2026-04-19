using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

public partial class CreateEventViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
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
    private int _recurrenceIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public List<string> RecurrenceOptions { get; } =
    [
        "Jednorázová",
        "Týdně",
        "Měsíčně"
    ];

    private static readonly string[] RecurrenceMap = ["None", "Weekly", "Monthly"];

    [RelayCommand]
    public async Task CreateAsync()
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
            var request = new CreateEventRequest(
                Name.Trim(),
                dateFrom,
                dateTo,
                string.IsNullOrWhiteSpace(Place) ? null : Place.Trim(),
                RecurrenceMap[RecurrenceIndex]);

            await apiClient.CreateEventAsync(request);

            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await navigation.GoBackAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se vytvořit akci.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
