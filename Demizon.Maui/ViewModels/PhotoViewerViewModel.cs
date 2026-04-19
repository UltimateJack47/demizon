using CommunityToolkit.Mvvm.ComponentModel;

namespace Demizon.Maui.ViewModels;

public partial class PhotoViewerViewModel : ObservableObject
{
    [ObservableProperty] private List<GalleryPhotoItem> _photos = [];
    [ObservableProperty] private int _currentIndex;
}
