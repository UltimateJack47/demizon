namespace Demizon.Contracts.Events;

public sealed record EventAttendeeDto(int MemberId, string FullName, string? ActivityRole);

public sealed record EventAttendeesDto(
    List<EventAttendeeDto> Attendees,
    int DancerCount,
    int MusicianCount,
    int TotalCount);
