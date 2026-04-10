namespace Demizon.Common.Services;

/// <summary>
/// Poskytuje přístup k přihlášenému uživateli napříč vrstvami bez závislosti na ASP.NET Core.
/// </summary>
public interface ICurrentUserAccessor
{
    string? GetCurrentUserLogin();
}
