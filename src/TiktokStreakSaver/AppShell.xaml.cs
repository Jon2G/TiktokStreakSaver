namespace TiktokStreakSaver;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
    }
}
