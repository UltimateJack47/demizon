namespace Demizon.Maui.Pages;

public partial class GalleryPage : ContentPage
{
    public GalleryPage(ViewModels.GalleryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.GalleryViewModel vm)
        {
            await vm.LoadPhotosCommand.ExecuteAsync(null);
        }
    }
}
