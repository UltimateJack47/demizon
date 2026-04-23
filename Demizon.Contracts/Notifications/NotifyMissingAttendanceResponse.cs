namespace Demizon.Contracts.Notifications;

public sealed record NotifyMissingAttendanceResponse(int NotifiedCount, int SkippedWithAttendance, int SkippedWithoutNotifications);
