using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver.Services;

/// <summary>
/// Single source of truth for whether the user can run streaks (session + cookies).
/// </summary>
public static class SessionRefreshHelper
{
    public static Task<bool> RefreshAndGetRunReadyAsync(SessionService session)
    {
#if IOS
        return RefreshIosAsync(session);
#else
        return Task.FromResult(RefreshFromCookies(session));
#endif
    }

    private static bool RefreshFromCookies(SessionService session)
    {
        var previous = session.IsSessionValid();
        bool ready = TikTokWebViewHelper.HasValidSessionCookie();
        session.SetSessionValid(ready);

        if (ready != previous)
            SessionState.NotifyChanged();

        return ready;
    }

#if IOS
    private static async Task<bool> RefreshIosAsync(SessionService session)
    {
        var previous = session.IsSessionValid();

        bool ready = await Platforms.iOS.Services.CookieSyncService.RefreshSessionCookiesPreservingExportAsync();
        session.SetSessionValid(ready);

        if (ready)
            AppStorageProvider.Current.SetBool(AppConstants.AuthRequiredKey, false);

        if (ready != previous)
            SessionState.NotifyChanged();

        return ready;
    }
#endif
}
