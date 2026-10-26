import 'dart:io';

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
/// `core/providers.dart` je převádí na kalendářní den `yyyy-MM-dd`,
/// protože ASP.NET u zkoušek bere `.Date` a ISO instant by posunul pátek.
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

  // ------------------------------------------------------------ Krojárka

  @GET('/api/costumes/me')
  Future<MyCostumes> getMyCostumes();

  @GET('/api/costumes/me/transfers')
  Future<List<CostumeTransfer>> getMyCostumeTransfers();

  /// QR z profilu jako PNG — server ho vykreslí, appka nepotřebuje balíček na QR.
  @GET('/api/costumes/me/qr')
  @DioResponseType(ResponseType.bytes)
  Future<HttpResponse<List<int>>> getMyCostumeQr();

  @POST('/api/costumes/me/qr/regenerate')
  Future<void> regenerateMyCostumeQr();

  @GET('/api/costumes/members/by-qr/{token}')
  Future<CostumeMember> getCostumeMemberByQr(@Path('token') String token);

  @GET('/api/costumes/items/by-code/{code}')
  Future<CostumeItemCard> getCostumeItemCard(@Path('code') String code);

  @GET('/api/costumes/transfers/{id}')
  Future<CostumeTransfer> getCostumeTransfer(@Path('id') int id);

  /// Fotky nejsou veřejné, takže jdou přes Dio s tokenem, ne přes `Image.network`.
  @GET('/api/costumes/transfers/{id}/photos/{photoId}')
  @DioResponseType(ResponseType.bytes)
  Future<HttpResponse<List<int>>> getCostumePhoto(
    @Path('id') int id,
    @Path('photoId') int photoId,
    @Query('size') String size,
  );

  @POST('/api/costumes/transfers')
  Future<CostumeTransfer> createCostumeTransfer(@Body() CreateCostumeTransferRequest request);

  @POST('/api/costumes/transfers/{id}/lines')
  Future<void> addCostumeLine(@Path('id') int id, @Body() CostumeLineRequest request);

  @DELETE('/api/costumes/transfers/{id}/lines/{lineId}')
  Future<void> removeCostumeLine(@Path('id') int id, @Path('lineId') int lineId);

  @PUT('/api/costumes/transfers/{id}/lines/{lineId}/note')
  Future<void> setCostumeLineNote(
    @Path('id') int id,
    @Path('lineId') int lineId,
    @Body() CostumeLineNoteRequest request,
  );

  @POST('/api/costumes/transfers/{id}/lines/{lineId}/photos')
  @MultiPart()
  Future<void> addCostumePhoto(
    @Path('id') int id,
    @Path('lineId') int lineId,
    @Part(name: 'file', contentType: 'image/jpeg') File file,
  );

  @DELETE('/api/costumes/transfers/{id}/photos/{photoId}')
  Future<void> removeCostumePhoto(@Path('id') int id, @Path('photoId') int photoId);

  @POST('/api/costumes/transfers/{id}/submit')
  Future<CostumeTransferStatusResponse> submitCostumeTransfer(@Path('id') int id);

  @POST('/api/costumes/transfers/{id}/confirm')
  Future<void> confirmCostumeTransfer(@Path('id') int id);

  @POST('/api/costumes/transfers/{id}/reject')
  Future<void> rejectCostumeTransfer(@Path('id') int id, @Body() RejectCostumeTransferRequest request);

  @POST('/api/costumes/transfers/{id}/cancel')
  Future<void> cancelCostumeTransfer(@Path('id') int id);

  // ------------------------------------------- Krojárka (jen správce / vedení)

  @GET('/api/costumes/holders')
  Future<List<CostumeHolderOverview>> getCostumeHolders();

  @GET('/api/costumes/transfers/open')
  Future<List<CostumeTransfer>> getOpenCostumeTransfers();

  @POST('/api/costumes/reminders')
  Future<CostumeReminderResponse> remindCostumeHolders(@Body() CostumeReminderRequest request);
}
