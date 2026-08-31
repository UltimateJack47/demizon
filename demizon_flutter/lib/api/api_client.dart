import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/models.dart';

part 'api_client.g.dart';

/// Typovaný klient Demizon API — protějšek `Demizon.Maui/Services/IApiClient.cs`.
///
/// Retrofit zde nahrazuje Refit. Názvy metod jsou převzaté z `IApiClient.cs`
/// bez sufixu `Async` a v camelCase. Cesty a HTTP metody jsou zachovány 1:1.
///
/// Nepřenesené (v MAUI bez volajícího): `GetVideosAsync`, `GetVideoAsync`,
/// `CreateVideoAsync`, `UpdateVideoAsync`, `DeleteVideoAsync`,
/// `ToggleEventCancelledAsync`, `ToggleEventPublicAsync`.
///
/// Obrázky se přes tento klient nestahují — jde o přímé URL, viz
/// `core/formatting.dart` (`imageUrl` / `thumbnailUrl`).
///
/// Pozor na `DateTime` v query parametrech: Dio by na hodnotu zavolal
/// `toString()` (formát `2026-08-31 00:00:00.000`). Interceptor v
/// `core/providers.dart` je proto převádí na ISO 8601, aby odpovídaly tomu,
/// co posílal Refit.
@RestApi()
abstract class ApiClient {
  factory ApiClient(Dio dio, {String baseUrl}) = _ApiClient;

  // ---------------------------------------------------------------- Auth

  @POST('/api/auth/token')
  Future<TokenResponse> login(@Body() TokenRequest request);

  @POST('/api/auth/refresh')
  Future<TokenResponse> refresh(@Body() RefreshRequest request);

  // -------------------------------------------------------------- Events

  @GET('/api/events/upcoming')
  Future<List<Event>> getUpcomingEvents();

  @GET('/api/events/month')
  Future<List<Event>> getEventsByMonth(
    @Query('year') int year,
    @Query('month') int month,
  );

  @GET('/api/events/{id}')
  Future<Event> getEvent(@Path('id') int id);

  @POST('/api/events')
  Future<Event> createEvent(@Body() CreateEventRequest request);

  @GET('/api/events/{id}/attendees')
  Future<EventAttendees> getEventAttendees(@Path('id') int id);

  // --------------------------------------------------------- Attendances

  @GET('/api/attendances/me')
  Future<List<Attendance>> getMyAttendances();

  @PUT('/api/attendances/{eventId}')
  Future<Attendance> upsertAttendance(
    @Path('eventId') int eventId,
    @Body() UpsertAttendanceRequest request,
  );

  @GET('/api/attendances/rehearsal')
  Future<Attendance> getRehearsalAttendance(@Query('date') DateTime date);

  @PUT('/api/attendances/rehearsal')
  Future<Attendance> upsertRehearsalAttendance(
    @Query('date') DateTime date,
    @Body() UpsertAttendanceRequest request,
  );

  @GET('/api/attendances/{eventId}/member/{memberId}')
  Future<Attendance> getMemberAttendance(
    @Path('eventId') int eventId,
    @Path('memberId') int memberId,
  );

  @PUT('/api/attendances/{eventId}/member/{memberId}')
  Future<Attendance> upsertMemberAttendance(
    @Path('eventId') int eventId,
    @Path('memberId') int memberId,
    @Body() UpsertAttendanceRequest request,
  );

  @GET('/api/attendances/rehearsal/member/{memberId}')
  Future<Attendance> getMemberRehearsalAttendance(
    @Path('memberId') int memberId,
    @Query('date') DateTime date,
  );

  @PUT('/api/attendances/rehearsal/member/{memberId}')
  Future<Attendance> upsertMemberRehearsalAttendance(
    @Path('memberId') int memberId,
    @Query('date') DateTime date,
    @Body() UpsertAttendanceRequest request,
  );

  @GET('/api/attendances/stats')
  Future<List<MemberAttendanceStat>> getAttendanceStats(
    @Query('from') DateTime from,
    @Query('to') DateTime to,
  );

  @GET('/api/attendances/table')
  Future<MonthlyAttendanceTable> getMonthlyAttendanceTable(
    @Query('year') int year,
    @Query('month') int month,
  );

  @DELETE('/api/attendances/{eventId}')
  Future<void> deleteMyAttendance(@Path('eventId') int eventId);

  @DELETE('/api/attendances/rehearsal')
  Future<void> deleteMyRehearsalAttendance(@Query('date') DateTime date);

  // -------------------------------------------------------------- Dances

  @GET('/api/dances')
  Future<List<Dance>> getDances();

  @GET('/api/dances/{id}')
  Future<Dance> getDance(@Path('id') int id);

  @GET('/api/dances/{id}/photos')
  Future<List<GalleryPhoto>> getDancePhotos(@Path('id') int id);

  @GET('/api/dances/{id}/documents')
  Future<List<DanceDocument>> getDanceDocuments(@Path('id') int id);

  // --------------------------------------------------------------- Files

  /// Protějšek `DownloadDocumentAsync` (v Refitu vracel `HttpResponseMessage`).
  /// `HttpResponse` dává přístup k hlavičkám (`Content-Disposition`,
  /// `Content-Type`), které volající potřebuje pro uložení souboru.
  @GET('/api/files/{id}/document')
  @DioResponseType(ResponseType.bytes)
  Future<HttpResponse<List<int>>> downloadDocument(@Path('id') int id);

  @GET('/api/files/gallery')
  Future<List<GalleryPhoto>> getGalleryPhotos();

  // ------------------------------------------------------- Notifications

  @POST('/api/notifications/device')
  Future<void> registerDevice(@Body() RegisterDeviceRequest request);

  // TODO(verify): DELETE s tělem požadavku — Dio to umí, ale ověřit, že
  // retrofit_generator tělo skutečně přiloží a server ho přijme.
  @DELETE('/api/notifications/device')
  Future<void> unregisterDevice(@Body() RegisterDeviceRequest request);

  @POST('/api/notifications/test')
  Future<void> sendTestNotification();

  // ------------------------------------------------------------- Members

  @GET('/api/members/me')
  Future<MemberProfile> getMyProfile();

  @PUT('/api/members/me')
  Future<void> updateMyProfile(@Body() UpdateProfileRequest request);

  @PUT('/api/members/me/password')
  Future<void> changePassword(@Body() ChangePasswordRequest request);

  // --------------------------------------------- Správa akcí (jen admin)

  @PUT('/api/events/{id}')
  Future<void> updateEvent(
    @Path('id') int id,
    @Body() UpdateEventRequest request,
  );

  @DELETE('/api/events/{id}')
  Future<void> deleteEvent(@Path('id') int id);

  @POST('/api/events/{id}/notify-missing-attendance')
  Future<NotifyMissingAttendanceResponse> notifyMissingAttendance(
    @Path('id') int id,
  );

  @POST('/api/events/rehearsals/notify-missing-attendance')
  Future<NotifyMissingAttendanceResponse> notifyMissingRehearsalAttendance(
    @Query('date') DateTime date,
  );
}
