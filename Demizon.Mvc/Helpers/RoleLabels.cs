using Demizon.Dal.Entities;

namespace Demizon.Mvc.Helpers;

public static class RoleLabels
{
    public static string For(UserRole role) => role switch
    {
        UserRole.Admin => "Admin",
        UserRole.ContentManager => "Správce obsahu webu",
        _ => role.ToString()
    };
}
