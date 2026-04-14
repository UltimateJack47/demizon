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
    
    [Get("/api/events/{id}")]
    Task<EventDto> GetEventAsync(int id);
    
    [Get("/api/attendances/me")]
    Task<List<AttendanceDto>> GetMyAttendancesAsync();
    
    [Put("/api/attendances/{eventId}")]
    Task<AttendanceDto> UpsertAttendanceAsync(int eventId, [Body] UpsertAttendanceRequest request);
    
    [Get("/api/dances")]
    Task<List<DanceDto>> GetDancesAsync();
    
    [Get("/api/dances/{id}")]
    Task<DanceDto> GetDanceAsync(int id);
    
    [Post("/api/notifications/device")]
    Task RegisterDeviceAsync([Body] RegisterDeviceRequest request);
    
    [Delete("/api/notifications/device")]
    Task UnregisterDeviceAsync([Body] RegisterDeviceRequest request);
}
