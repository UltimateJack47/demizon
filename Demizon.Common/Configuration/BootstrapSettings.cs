namespace Demizon.Common.Configuration;

/// <summary>
/// Jednorázový bootstrap prvního admina. Nemá <c>[Required]</c> ani
/// <c>ValidateOnStart</c> záměrně — nenastavený token znamená, že je
/// seed endpoint vypnutý, což je správný stav pro běžící instanci.
/// </summary>
public class BootstrapSettings
{
    /// <summary>
    /// Tajemství, kterým se autorizuje <c>POST /api/database/seed</c>.
    /// Předává se přes <c>Bootstrap__SeedToken</c>. Dokud není nastavené,
    /// endpoint vrací 404 a nejde jím vůbec nic založit.
    /// </summary>
    public string? SeedToken { get; set; }

    /// <summary>Minimální délka hesla prvního admina.</summary>
    public const int MinPasswordLength = 12;
}
