using Demizon.Dal.Entities;

namespace Demizon.Core.Services.Costume;

/// <summary>
/// Kdo s krojárkou pracuje. Role předává volající z claimů — Core autentizaci nezná,
/// ale pravidla „kdo smí co“ jsou doménová, takže žijí tady.
/// </summary>
public readonly record struct CostumeActor(int MemberId, bool IsKeeper);

public sealed record CostumeInput(string Name, string? Description, int SortOrder);

public sealed record CostumePartInput(
    int? CostumeId,
    string Name,
    CostumeGender Gender,
    string? Description,
    CostumeTrackingMode TrackingMode);

/// <param name="Code">Prázdný = vygeneruje se další volný <c>K0001</c>, <c>K0002</c>…</param>
/// <param name="Quantity">U kusu (<see cref="CostumeTrackingMode.Individual"/>) vždy 1.</param>
public sealed record CostumeItemInput(
    int PartId,
    string? Code,
    string? Size,
    string? Color,
    string? Note,
    int? ValueCzk,
    int Quantity = 1);

public sealed record CostumeLineInput(string Code, int Quantity = 1);

/// <summary>Stav zásob položky. V krojárce je <c>Quantity − Held</c>.</summary>
/// <param name="Reserved">Kusy z krojárky v rozpracovaných a čekajících předávkách.</param>
public sealed record CostumeItemStock(int ItemId, int Quantity, int Held, int Reserved)
{
    public int InStore => Quantity - Held;

    public int AvailableInStore => InStore - Reserved;
}

/// <summary>Jméno člena pro přehledy krojárky; zahrnuje i soft-smazané (bývalý člen pořád drží kroj).</summary>
public sealed record CostumeMemberName(int Id, string FullName, bool IsDeleted);
