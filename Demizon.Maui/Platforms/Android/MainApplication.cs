using Android.App;
using Android.Runtime;

[assembly: Android.App.UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: Android.App.UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
[assembly: Android.App.UsesPermission("android.permission.POST_NOTIFICATIONS")]

namespace Demizon.Maui.Platforms.Android;

// google-services.json musí být umístěn v Platforms/Android/ před buildem
[Application(Label = "Demižón", UsesCleartextTraffic = true, Icon = "@mipmap/appicon", RoundIcon = "@mipmap/appicon_round")]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    public override void OnCreate()
    {
        base.OnCreate();
        // Initialize Firebase manually only if not already auto-initialized by google-services.json MSBuild plugin
        if (!Firebase.FirebaseApp.GetApps(this).Any())
        {
            var options = new Firebase.FirebaseOptions.Builder()
                .SetApplicationId(FirebaseConfig.ApplicationId)
                .SetApiKey(FirebaseConfig.ApiKey)
                .SetProjectId(FirebaseConfig.ProjectId)
                .SetStorageBucket(FirebaseConfig.StorageBucket)
                .SetGcmSenderId(FirebaseConfig.GcmSenderId)
                .Build();
            Firebase.FirebaseApp.InitializeApp(this, options);
        }
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
