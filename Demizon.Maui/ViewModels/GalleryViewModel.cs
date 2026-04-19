using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Gallery;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

public partial class GalleryViewModel(IApiClient apiClient) : ObservableObject
{
    [ObservableProperty] private List<GalleryPhotoItem> _photos = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private int _selectedIndex;

    [RelayCommand]
    private async Task LoadPhotosAsync()
    {
        try
        {
            IsBusy = true;
            var dtos = await apiClient.GetGalleryPhotosAsync();
            Photos = dtos.Select(d => new GalleryPhotoItem
            {
                Id = d.Id,
                DanceName = d.DanceName,
                ThumbnailUrl = $"{ApiConfig.BaseUrl}/api/files/{d.Id}/image?size=thumb",
                FullUrl = $"{ApiConfig.BaseUrl}/api/files/{d.Id}/image?size=full",
            }).ToList();
            IsEmpty = Photos.Count == 0;
        }
        catch
        {
            IsEmpty = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPhotoAsync(GalleryPhotoItem photo)
    {
        SelectedIndex = Photos.IndexOf(photo);
        // Navigate to full-screen photo viewer
        await Shell.Current.GoToAsync(AppRoutes.PhotoViewer, true,
            new Dictionary<string, object> { ["Photos"] = Photos, ["Index"] = SelectedIndex });
    }
}

public class GalleryPhotoItem
{
    public int Id { get; set; }
    public string? DanceName { get; set; }
    public string ThumbnailUrl { get; set; } = "";
    public string FullUrl { get; set; } = "";
}
