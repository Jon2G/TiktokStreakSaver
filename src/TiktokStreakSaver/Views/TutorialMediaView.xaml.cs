using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Views;

public partial class TutorialMediaView : ContentView
{
    public static readonly BindableProperty MediaFileNameProperty = BindableProperty.Create(
        nameof(MediaFileName),
        typeof(string),
        typeof(TutorialMediaView),
        propertyChanged: OnMediaFileNameChanged);

    public string? MediaFileName
    {
        get => (string?)GetValue(MediaFileNameProperty);
        set => SetValue(MediaFileNameProperty, value);
    }

    public TutorialMediaView()
    {
        InitializeComponent();
    }

    private static void OnMediaFileNameChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TutorialMediaView view)
            _ = view.LoadMediaAsync((string?)newValue);
    }

    private async Task LoadMediaAsync(string? fileName)
    {
        MediaImage.IsVisible = false;
        MediaWebView.IsVisible = false;
        PlaceholderLayout.IsVisible = true;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            PlaceholderTitleLabel.Text = "No media for this step";
            PlaceholderHintLabel.Text = string.Empty;
            return;
        }

        var assetPath = IosShortcutTutorialCatalog.MediaAssetPath(fileName);
        PlaceholderHintLabel.Text =
            $"Place your file at:\nResources/Raw/{assetPath}";

        if (!await AssetExistsAsync(assetPath))
        {
            PlaceholderTitleLabel.Text = "Media not bundled yet";
            return;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".mp4" or ".mov" or ".m4v" or ".webm")
        {
            await ShowVideoAsync(assetPath);
            return;
        }

        if (ext is ".gif" or ".png" or ".jpg" or ".jpeg" or ".webp")
        {
            ShowImage(assetPath);
            return;
        }

        PlaceholderTitleLabel.Text = "Unsupported media type";
        PlaceholderHintLabel.Text = $"Use .gif, .png, .jpg, or .mp4 for {fileName}";
    }

    private void ShowImage(string assetPath)
    {
        MediaImage.Source = ImageSource.FromFile(assetPath);
        MediaImage.IsVisible = true;
        PlaceholderLayout.IsVisible = false;
    }

    private async Task ShowVideoAsync(string assetPath)
    {
        try
        {
            var cachePath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(assetPath));
            if (!File.Exists(cachePath))
            {
                await using var input = await FileSystem.OpenAppPackageFileAsync(assetPath);
                await using var output = File.Create(cachePath);
                await input.CopyToAsync(output);
            }

            var fileUrl = new Uri(cachePath).AbsoluteUri;
            var html =
                "<!DOCTYPE html><html><head>" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, maximum-scale=1\">" +
                "<style>*{margin:0;padding:0}html,body{width:100%;height:100%;background:#000}" +
                "video{width:100%;height:100%;object-fit:contain}</style></head><body>" +
                $"<video src=\"{fileUrl}\" autoplay loop muted playsinline controls></video>" +
                "</body></html>";

            MediaWebView.Source = new HtmlWebViewSource { Html = html };
            MediaWebView.IsVisible = true;
            PlaceholderLayout.IsVisible = false;
        }
        catch
        {
            PlaceholderTitleLabel.Text = "Could not load video";
        }
    }

    private static async Task<bool> AssetExistsAsync(string assetPath)
    {
        try
        {
            await using var _ = await FileSystem.OpenAppPackageFileAsync(assetPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
