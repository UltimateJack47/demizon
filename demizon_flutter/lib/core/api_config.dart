/// Centrální konfigurace API URL.
///
/// Přebíráno z `Demizon.Maui/ApiConfig.cs`. Hodnotu lze přebít při buildu:
/// `flutter run --dart-define=DEMIZON_API_BASE_URL=http://192.168.0.10:8083`
class ApiConfig {
  const ApiConfig._();

  static const String baseUrl = String.fromEnvironment(
    'DEMIZON_API_BASE_URL',
    defaultValue: 'https://demizon-production.up.railway.app',
  );
}
