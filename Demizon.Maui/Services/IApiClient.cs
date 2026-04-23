using Demizon.Contracts.Auth;
using Demizon.Contracts.Events;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Dances;
using Demizon.Contracts.Members;
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

    [Get("/api/attendances/rehearsal/member/{memberId}")]
    Task<AttendanceDto> GetMemberRehearsalAttendanceAsync(int memberId, [Query] DateTime date);

    [Put("/api/attendances/rehearsal/member/{memberId}")]
    Task<AttendanceDto> UpsertMemberRehearsalAttendanceAsync(int memberId, [Query] DateTime date, [Body] UpsertAttendanceRequest request);

    [Get("/api/attendances/stats")]
    Task<List<MemberAttendanceStatDto>> GetAttendanceStatsAsync([Query] DateTime from, [Query] DateTime to);

    [Get("/api/attendances/table")]
    Task<MonthlyAttendanceTableDto> GetMonthlyAttendanceTableAsync([Query] int year, [Query] int month);
    
    [Delete("/api/attendances/{eventId}")]
    Task DeleteMyAttendanceAsync(int eventId);

    [Delete("/api/attendances/rehearsal")]
    Task DeleteMyRehearsalAttendanceAsync([Query] DateTime date);

    [Get("/api/dances")]
    Task<List<DanceDto>> GetDancesAsync();
    
    [Get("/api/dances/{id}")]
    Task<DanceDto> GetDanceAsync(int id);

    [Get("/api/dances/{id}/photos")]
    Task<List<Demizon.Contracts.Gallery.GalleryPhotoDto>> GetDancePhotosAsync(int id);

    [Get("/api/events/{id}/attendees")]
    Task<EventAttendeesDto> GetEventAttendeesAsync(int id);
    
    [Post("/api/notifications/device")]
    Task RegisterDeviceAsync([Body] RegisterDeviceRequest request);
    
    [Delete("/api/notifications/device")]
    Task UnregisterDeviceAsync([Body] RegisterDeviceRequest request);

    [Post("/api/notifications/test")]
    Task SendTestNotificationAsync();

    // Members
    [Get("/api/members/me")]
    Task<MemberProfileDto> GetMyProfileAsync();

    [Put("/api/members/me")]
    Task UpdateMyProfileAsync([Body] UpdateProfileRequest request);

    [Put("/api/members/me/password")]
    Task ChangePasswordAsync([Body] ChangePasswordRequest request);

    // Event management (admin)
    [Put("/api/events/{id}")]
    Task UpdateEventAsync(int id, [Body] UpdateEventRequest request);

    [Delete("/api/events/{id}")]
    Task DeleteEventAsync(int id);

    [Patch("/api/events/{id}/cancel")]
    Task ToggleEventCancelledAsync(int id);

    [Patch("/api/events/{id}/public")]
    Task ToggleEventPublicAsync(int id);

    [Post("/api/events/{id}/notify-missing-attendance")]
    Task<NotifyMissingAttendanceResponse> NotifyMissingAttendanceAsync(int id);
    
    [Post("/api/events/rehearsals/notify-missing-attendance")]
    Task<NotifyMissingAttendanceResponse> NotifyMissingRehearsalAttendanceAsync([Query] DateTime date);

    // Videos
    [Get("/api/videos")]
    Task<List<VideoLinkDto>> GetVideosAsync();

    [Get("/api/videos/{id}")]
    Task<VideoLinkDto> GetVideoAsync(int id);

    [Post("/api/videos")]
    Task<VideoLinkDto> CreateVideoAsync([Body] CreateVideoLinkRequest request);

    [Put("/api/videos/{id}")]
    Task UpdateVideoAsync(int id, [Body] CreateVideoLinkRequest request);

    [Delete("/api/videos/{id}")]
    Task DeleteVideoAsync(int id);

    // Gallery
    [Get("/api/files/gallery")]
    Task<List<Demizon.Contracts.Gallery.GalleryPhotoDto>> GetGalleryPhotosAsync();
}
