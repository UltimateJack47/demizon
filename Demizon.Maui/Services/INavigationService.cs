namespace Demizon.Maui.Services;

/// <summary>
/// Abstrakce navigace – umožňuje testování ViewModelů bez závislosti na Shell.Current.
/// </summary>
public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}
