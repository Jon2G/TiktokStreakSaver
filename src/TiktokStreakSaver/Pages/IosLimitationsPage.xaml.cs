using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class IosLimitationsPage : ContentPage
{
    public IosLimitationsPage()
    {
        InitializeComponent();
    }

    private static Task OpenUrlAsync(string url) => Launcher.OpenAsync(url);

    private void OnOpenAppWhenRunDoc(object? sender, EventArgs e) =>
        _ = OpenUrlAsync(IosPlatformDocLinks.OpenAppWhenRun);

    private void OnSupportedModesDoc(object? sender, EventArgs e) =>
        _ = OpenUrlAsync(IosPlatformDocLinks.SupportedModes);

    private void OnBackgroundTasksDoc(object? sender, EventArgs e) =>
        _ = OpenUrlAsync(IosPlatformDocLinks.BackgroundTasks);

    private void OnWkWebViewDoc(object? sender, EventArgs e) =>
        _ = OpenUrlAsync(IosPlatformDocLinks.WkWebView);

    private void OnShortcutsGuideDoc(object? sender, EventArgs e) =>
        _ = OpenUrlAsync(IosPlatformDocLinks.ShortcutsUserGuide);

    private void OnFullDoc(object? sender, EventArgs e) =>
        _ = OpenUrlAsync(IosPlatformDocLinks.FullLimitationsDoc);
}
