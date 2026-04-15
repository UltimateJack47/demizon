namespace Demizon.Maui.Platforms.Android;

/// <summary>
/// Template for Firebase credentials. Copy this file to FirebaseConfig.cs (gitignored)
/// and fill in values from google-services.json (Firebase Console → Project settings → Your apps).
/// </summary>
internal static class FirebaseConfig
{
    internal const string ApplicationId  = "YOUR_MOBILESDK_APP_ID";   // e.g. 1:123456:android:abcdef
    internal const string ApiKey         = "YOUR_CURRENT_KEY";         // current_key from google-services.json
    internal const string ProjectId      = "YOUR_PROJECT_ID";
    internal const string StorageBucket  = "YOUR_PROJECT.firebasestorage.app";
    internal const string GcmSenderId    = "YOUR_PROJECT_NUMBER";
}
