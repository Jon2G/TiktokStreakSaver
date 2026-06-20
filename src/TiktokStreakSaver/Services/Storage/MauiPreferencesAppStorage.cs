namespace TiktokStreakSaver.Services.Storage;

public sealed class MauiPreferencesAppStorage : IAppStorage
{
    public string GetString(string key, string defaultValue = "") =>
        Preferences.Get(key, defaultValue);

    public void SetString(string key, string value) => Preferences.Set(key, value);

    public bool GetBool(string key, bool defaultValue = false) =>
        Preferences.Get(key, defaultValue);

    public void SetBool(string key, bool value) => Preferences.Set(key, value);

    public long GetLong(string key, long defaultValue = 0)
    {
#if ANDROID
        return GetLongMigratingLegacyInt(key, defaultValue);
#else
        return Preferences.Get(key, defaultValue);
#endif
    }

    public void SetLong(string key, long value) => Preferences.Set(key, value);

#if ANDROID
    /// <summary>
    /// Older builds stored small numeric settings with Preferences.Get(key, int), which
    /// writes Android SharedPreferences Integers. Reading those keys with GetLong crashes.
    /// </summary>
    private static long GetLongMigratingLegacyInt(string key, long defaultValue)
    {
        try
        {
            return Preferences.Get(key, defaultValue);
        }
        catch (Exception ex) when (IsIntegerLongTypeMismatch(ex))
        {
            var asInt = Preferences.Get(key, 0);
            var asLong = (long)asInt;
            Preferences.Set(key, asLong);
            return asLong;
        }
    }

    private static bool IsIntegerLongTypeMismatch(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Java.Lang.ClassCastException)
                return true;

            if (current.Message.Contains("Integer cannot be cast", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("cannot be cast to java.lang.Long", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
#endif

    public void Remove(string key) => Preferences.Remove(key);

    public void Clear() => Preferences.Clear();
}
