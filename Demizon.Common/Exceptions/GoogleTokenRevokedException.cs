namespace Demizon.Common.Exceptions;

/// <summary>
/// Thrown when a Google OAuth refresh token has been expired or revoked.
/// The caller should clear the stored credentials and prompt the user to reconnect.
/// </summary>
public class GoogleTokenRevokedException(string? message = null) : Exception(message ?? "Google OAuth token has been expired or revoked.");
