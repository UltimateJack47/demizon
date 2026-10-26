using Demizon.Dal.Entities;

namespace Demizon.Mvc.Helpers;

public static class RoleLabels
{
    public static string For(UserRole role) => role switch
    {
        UserRole.Admin => "Admin",
        UserRole.ContentManager => "Správce obsahu webu",
        UserRole.Leader => "Vůdce souboru",
        UserRole.CostumeKeeper => "Správce krojárky",
        _ => role.ToString()
    };
}
