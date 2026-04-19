namespace Demizon.Maui.Pages;

[QueryProperty(nameof(Photos), "Photos")]
[QueryProperty(nameof(StartIndex), "Index")]
public partial class PhotoViewerPage : ContentPage
{
    public PhotoViewerPage(ViewModels.PhotoViewerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public List<ViewModels.GalleryPhotoItem>? Photos
    {
        set
        {
            if (BindingContext is ViewModels.PhotoViewerViewModel vm && value != null)
                vm.Photos = value;
        }
    }

    public int StartIndex
    {
        set
        {
            if (BindingContext is ViewModels.PhotoViewerViewModel vm)
                vm.CurrentIndex = value;
        }
    }
}
