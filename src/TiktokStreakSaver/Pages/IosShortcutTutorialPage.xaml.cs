using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class IosShortcutTutorialPage : ContentPage
{
    private readonly IReadOnlyList<IosShortcutTutorialStep> _steps;
    private readonly List<Button> _tabButtons = [];
    private bool _suppressTabSync;

    public event EventHandler? Completed;

    public IosShortcutTutorialPage()
    {
        InitializeComponent();
        _steps = IosShortcutTutorialCatalog.Steps;
        TutorialCarousel.ItemsSource = _steps;
        BuildTabs();
        UpdateChrome(0);
    }

    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        _tabButtons.Clear();

        foreach (var step in _steps)
        {
            var tab = new Button
            {
                Text = $"{step.StepNumber}. {step.TabTitle}",
                Padding = new Thickness(14, 8),
                CornerRadius = 20,
                FontSize = 13,
                MinimumHeightRequest = 36
            };
            var index = step.StepNumber - 1;
            tab.Clicked += (_, _) => GoToStep(index);
            _tabButtons.Add(tab);
            TabStrip.Children.Add(tab);
        }

        StyleTab(0, selected: true);
    }

    private void GoToStep(int index)
    {
        if (index < 0 || index >= _steps.Count) return;
        _suppressTabSync = true;
        TutorialCarousel.Position = index;
        _suppressTabSync = false;
        UpdateChrome(index);
    }

    private void OnCarouselPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (_suppressTabSync) return;
        UpdateChrome(e.CurrentPosition);
    }

    private void UpdateChrome(int index)
    {
        for (var i = 0; i < _tabButtons.Count; i++)
            StyleTab(i, selected: i == index);

        PreviousButton.IsEnabled = index > 0;
        PreviousButton.Opacity = index > 0 ? 1.0 : 0.4;

        var isLast = index >= _steps.Count - 1;
        NextButton.IsVisible = !isLast;
        DoneButton.IsVisible = isLast;

        if (!isLast)
            NextButton.Text = $"Next ({index + 1}/{_steps.Count})";
    }

    private void StyleTab(int index, bool selected)
    {
        var tab = _tabButtons[index];
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (selected)
        {
            tab.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
            tab.TextColor = Colors.White;
            tab.FontFamily = "InterSemiBold";
        }
        else
        {
            tab.BackgroundColor = isDark
                ? GetThemeColor("Gray800", "#282828")
                : GetThemeColor("Gray200", "#E8E8E8");
            tab.TextColor = isDark
                ? GetThemeColor("Gray200", "#E8E8E8")
                : GetThemeColor("Gray900", "#1A1A1A");
            tab.FontFamily = "InterRegular";
        }
    }

    private Color GetThemeColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Color color)
            return color;
        return Color.FromArgb(fallbackHex);
    }

    private void OnPreviousClicked(object? sender, EventArgs e)
        => GoToStep(TutorialCarousel.Position - 1);

    private void OnNextClicked(object? sender, EventArgs e)
        => GoToStep(TutorialCarousel.Position + 1);

    private async void OnOpenShortcutsClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync("shortcuts://");
        }
        catch
        {
            await DisplayAlert("Shortcuts", "Open the Shortcuts app manually from your home screen.", "OK");
        }
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        Completed?.Invoke(this, EventArgs.Empty);
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
        else if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
    }
}
