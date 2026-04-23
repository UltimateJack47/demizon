namespace Demizon.Contracts.Dances;
public sealed record DanceDto(int Id, string Name, string? Region, string? Description, string? InternalDescription, string? Lyrics, List<VideoLinkDto> Videos);
