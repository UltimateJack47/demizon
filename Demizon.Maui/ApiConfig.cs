namespace Demizon.Maui;

/// <summary>
/// Centrální konfigurace API URL.
/// Pro fyzický telefon přes Wi-Fi změň IP na aktuální adresu PC (ipconfig).
/// </summary>
internal static class ApiConfig
{
#if ANDROID
    public const string BaseUrl = "http://192.168.1.100:5272";
#else
    public const string BaseUrl = "http://localhost:5272";
#endif
}
