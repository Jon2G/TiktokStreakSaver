namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>Minimal UI while a Shortcut-triggered run is in progress.</summary>
internal static class IosShortcutRunUi
{
    private static ContentPage? _overlayPage;

    public static void TryShowRunningOverlay()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_overlayPage != null)
                return;

            var host = Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;
            if (host?.Navigation == null)
                return;

            _overlayPage = new ContentPage
            {
                BackgroundColor = Colors.Black.WithAlpha(0.85f),
                Content = new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Spacing = 16,
                    Children =
                    {
                        new ActivityIndicator { IsRunning = true, Color = Colors.White },
                        new Label
                        {
                            Text = "Sending streak messages…",
                            TextColor = Colors.White,
                            FontSize = 18,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "Streak Saver stays open briefly while TikTok runs in an invisible browser — required on iOS.",
                            TextColor = Colors.White.WithAlpha(0.75f),
                            FontSize = 13,
                            HorizontalTextAlignment = TextAlignment.Center,
                            Margin = new Thickness(24, 0)
                        }
                    }
                }
            };

            await host.Navigation.PushModalAsync(_overlayPage, false);
        });
    }

    public static void TryHideRunningOverlay()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_overlayPage == null)
                return;

            var page = _overlayPage;
            _overlayPage = null;

            if (page.Navigation?.ModalStack.Contains(page) == true)
                await page.Navigation.PopModalAsync(false);
        });
    }
}
