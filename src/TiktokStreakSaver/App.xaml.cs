namespace TiktokStreakSaver
{[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}