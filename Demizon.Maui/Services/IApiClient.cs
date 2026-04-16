using Demizon.Contracts.Auth;
using Demizon.Contracts.Events;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Dances;
using Demizon.Contracts.Notifications;
using Refit;

namespace Demizon.Maui.Services;

public interface IApiClient
{
    [Post("/api/auth/token")]
    Task<TokenResponse> LoginAsync([Body] TokenRequest request);
    
    [Post("/api/auth/refresh")]
    Task<TokenResponse> RefreshAsync([Body] RefreshRequest request);
    
    [Get("/api/events/upcoming")]
    Task<List<EventDto>> GetUpcomingEventsAsync();

    [Get("/api/events/month")]
    Task<List<EventDto>> GetEventsByMonthAsync([Query] int year, [Query] int month);
    
    [Get("/api/events/{id}")]
    Task<EventDto> GetEventAsync(int id);
    
    [Post("/api/events")]
    Task<EventDto> CreateEventAsync([Body] CreateEventRequest request);
    
    [Get("/api/attendances/me")]
    Task<List<AttendanceDto>> GetMyAttendancesAsync();
    
    [Put("/api/attendances/{eventId}")]
    Task<AttendanceDto> UpsertAttendanceAsync(int eventId, [Body] UpsertAttendanceRequest request);

    [Get("/api/attendances/rehearsal")]
    Task<AttendanceDto> GetRehearsalAttendanceAsync([Query] DateTime date);

    [Put("/api/attendances/rehearsal")]
    Task<AttendanceDto> UpsertRehearsalAttendanceAsync([Query] DateTime date, [Body] UpsertAttendanceRequest request);

    [Get("/api/attendances/{eventId}/member/{memberId}")]
    Task<AttendanceDto> GetMemberAttendanceAsync(int eventId, int memberId);

    [Put("/api/attendances/{eventId}/member/{memberId}")]
    Task<AttendanceDto> UpsertMemberAttendanceAsync(int eventId, int memberId, [Body] UpsertAttendanceRequest request);

    [Get("/api/attendances/stats")]
    Task<List<MemberAttendanceStatDto>> GetAttendanceStatsAsync([Query] DateTime from, [Query] DateTime to);

    [Get("/api/attendances/table")]
    Task<MonthlyAttendanceTableDto> GetMonthlyAttendanceTableAsync([Query] int year, [Query] int month);
    
    [Get("/api/dances")]
    Task<List<DanceDto>> GetDancesAsync();
    
    [Get("/api/dances/{id}")]
    Task<DanceDto> GetDanceAsync(int id);
    
    [Post("/api/notifications/device")]
    Task RegisterDeviceAsync([Body] RegisterDeviceRequest request);
    
    [Delete("/api/notifications/device")]
    Task UnregisterDeviceAsync([Body] RegisterDeviceRequest request);

    [Post("/api/notifications/test")]
    Task SendTestNotificationAsync();
}
