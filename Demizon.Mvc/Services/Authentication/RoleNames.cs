using Demizon.Dal.Entities;

namespace Demizon.Mvc.Services.Authentication;

/// <summary>
/// Role claims for <c>[Authorize(Roles = ...)]</c> and <c>AuthorizeView</c>, which need compile-time
/// strings. Derived from <see cref="UserRole"/> so a renamed role cannot leave a stale literal behind.
/// </summary>
public static class RoleNames
{
    public const string Admin = nameof(UserRole.Admin);
    public const string ContentManager = nameof(UserRole.ContentManager);
    public const string Leader = nameof(UserRole.Leader);
    public const string CostumeKeeper = nameof(UserRole.CostumeKeeper);

    /// <summary>Who may edit public web content: dances, videos, gallery.</summary>
    public const string ContentEditors = Admin + "," + ContentManager;

    /// <summary>Who may see the whole costume store (catalogue, holders, history). Only the keeper edits it.</summary>
    public const string CostumeViewers = CostumeKeeper + "," + Admin + "," + Leader;

    /// <summary>Who may cancel and restore rehearsals: an admin or an ensemble leader.</summary>
    public const string RehearsalManagers = Admin + "," + Leader;
}
