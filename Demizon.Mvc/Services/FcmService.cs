using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Demizon.Mvc.Services;

public class FcmService(ILogger<FcmService> logger)
{
    private bool IsInitialized => FirebaseApp.DefaultInstance is not null;

    public static void Initialize(IConfiguration configuration, ILogger logger)
    {
        if (FirebaseApp.DefaultInstance is not null)
            return;

        // 1) Zkus env proměnnou FIREBASE_CREDENTIAL_JSON (Railway)
        var credentialJson = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIAL_JSON");
        if (!string.IsNullOrWhiteSpace(credentialJson))
        {
            try
            {
#pragma warning disable CS0618
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(credentialJson)
                });
#pragma warning restore CS0618
                logger.LogInformation("Firebase initialized from FIREBASE_CREDENTIAL_JSON env variable.");
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Firebase from FIREBASE_CREDENTIAL_JSON.");
            }
        }

        // 2) Fallback: soubor z konfigurace
        var credentialFile = configuration["Firebase:CredentialFile"];
        if (string.IsNullOrWhiteSpace(credentialFile))
        {
            logger.LogWarning("Firebase CredentialFile is not configured. FCM push notifications are disabled.");
            return;
        }

        if (!File.Exists(credentialFile))
        {
            logger.LogWarning("Firebase credential file not found at {Path}. FCM disabled.", credentialFile);
            return;
        }

        using var stream = File.OpenRead(credentialFile);
#pragma warning disable CS0618
        FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromStream(stream) });
#pragma warning restore CS0618
        logger.LogInformation("Firebase initialized from {Path}.", credentialFile);
    }

    public async Task<FcmSendResult> SendAsync(string deviceToken, string title, string body,
        Dictionary<string, string>? data = null)
    {
        if (!IsInitialized)
        {
            logger.LogWarning("Firebase not initialized. Skipping push notification.");
            return FcmSendResult.Failed;
        }

        try
        {
            var message = new Message
            {
                Token = deviceToken,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = data,
                Android = new AndroidConfig
                {
                    // Must match the channel created in MainActivity.EnsureNotificationChannel()
                    Notification = new AndroidNotification
                    {
                        ChannelId = "demizon_channel",
                        ClickAction = "OPEN_EVENT_DETAIL"
                    }
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
            return FcmSendResult.Success;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                                                  ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument ||
                                                  ex.MessagingErrorCode == MessagingErrorCode.SenderIdMismatch)
        {
            logger.LogWarning(ex, "FCM token {Token} is invalid or unregistered (ErrorCode: {ErrorCode}). Should be removed.", deviceToken, ex.MessagingErrorCode);
            return FcmSendResult.InvalidToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send FCM notification to token {Token}.", deviceToken);
            return FcmSendResult.Failed;
        }
    }
}

public enum FcmSendResult
{
    Success,
    InvalidToken,
    Failed
}
