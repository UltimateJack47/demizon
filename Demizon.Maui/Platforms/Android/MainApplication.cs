using Android.App;
using Android.Runtime;

namespace Demizon.Maui.Platforms.Android;

// google-services.json musí být umístěn v Platforms/Android/ před buildem
[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
