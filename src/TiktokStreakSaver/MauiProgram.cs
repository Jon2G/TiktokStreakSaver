using Microsoft.Extensions.Logging;

namespace TiktokStreakSaver;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Entry, Microsoft.Maui.Handlers.EntryHandler>();
                handlers.AddHandler<Editor, Microsoft.Maui.Handlers.EditorHandler>();

                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
                {
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                });

                Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
                {
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                });
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
