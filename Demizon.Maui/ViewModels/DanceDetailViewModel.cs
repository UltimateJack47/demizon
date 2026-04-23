using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Dances;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

[QueryProperty(nameof(DanceId), "danceId")]
public partial class DanceDetailViewModel(IApiClient apiClient, IDocumentService documentService) : ObservableObject
{
    [ObservableProperty]
    private int _danceId;

    [ObservableProperty]
    private DanceDto? _dance;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLyricsExpanded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhotos))]
    private List<GalleryPhotoItem> _photos = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocuments))]
    private List<DanceDocumentDto> _documents = [];

    public bool HasPhotos => Photos.Count > 0;
    public bool HasDocuments => Documents.Count > 0;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Dance = await apiClient.GetDanceAsync(DanceId);

            try
            {
                var dtos = await apiClient.GetDancePhotosAsync(DanceId);
                Photos = dtos.Select(d => new GalleryPhotoItem
                {
                    Id = d.Id,
                    DanceName = Dance?.Name,
                    ThumbnailUrl = $"{ApiConfig.BaseUrl}/api/files/{d.Id}/image?size=thumb",
                    FullUrl = $"{ApiConfig.BaseUrl}/api/files/{d.Id}/image?size=full",
                }).ToList();
            }
            catch
            {
                Photos = [];
            }

            try
            {
                var docs = await apiClient.GetDanceDocumentsAsync(DanceId);
                Documents = docs;
            }
            catch
            {
                Documents = [];
            }
        }
        catch (Exception) { ErrorMessage = "Nepodařilo se načíst tanec."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ToggleLyrics() => IsLyricsExpanded = !IsLyricsExpanded;

    [RelayCommand]
    private async Task OpenVideoAsync(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            try
            {
                await Launcher.OpenAsync(uri);
            }
            catch
            {
                await Shell.Current.DisplayAlert("Chyba", "Nepodařilo se otevřít video.", "OK");
            }
        }
        else
        {
            await Shell.Current.DisplayAlert("Chyba", "Neplatný odkaz na video.", "OK");
        }
    }

    [RelayCommand]
    private async Task OpenPhotoAsync(GalleryPhotoItem photo)
    {
        var index = Photos.IndexOf(photo);
        await Shell.Current.GoToAsync(AppRoutes.PhotoViewer, true,
            new Dictionary<string, object> { ["Photos"] = Photos, ["Index"] = index });
    }

    [RelayCommand]
    private async Task OpenDocumentAsync(DanceDocumentDto document)
    {
        if (document is null) return;
        IsBusy = true;
        try
        {
            var ok = await documentService.DownloadAndOpenAsync(document.Id, document.FileName, document.ContentType);
            if (!ok)
            {
                await Shell.Current.DisplayAlert("Chyba", "Nepodařilo se otevřít dokument.", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
