using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Demizon.Mvc.Services;

public class FcmService(ILogger<FcmService> logger)
{
    private bool IsInitialized => FirebaseApp.DefaultInstance is not null;

    public static void Initialize(IConfiguration configuration, ILogger logger)
    {
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

        if (FirebaseApp.DefaultInstance is null)
        {
            using var stream = File.OpenRead(credentialFile);
#pragma warning disable CS0618 // FirebaseAdmin SDK zatím nepřešlo na CredentialFactory
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromStream(stream) });
#pragma warning restore CS0618
            logger.LogInformation("Firebase initialized from {Path}.", credentialFile);
        }
    }

    public async Task<bool> SendAsync(string deviceToken, string title, string body,
        Dictionary<string, string>? data = null)
    {
        if (!IsInitialized)
        {
            logger.LogWarning("Firebase not initialized. Skipping push notification.");
            return false;
        }

        try
        {
            var message = new Message
            {
                Token = deviceToken,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = data
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send FCM notification to token {Token}.", deviceToken);
            return false;
        }
    }
}
