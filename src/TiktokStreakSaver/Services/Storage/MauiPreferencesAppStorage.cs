namespace TiktokStreakSaver.Services.Storage;

public sealed class MauiPreferencesAppStorage : IAppStorage
{
    public string GetString(string key, string defaultValue = "") =>
        Preferences.Get(key, defaultValue);

    public void SetString(string key, string value) => Preferences.Set(key, value);

    public bool GetBool(string key, bool defaultValue = false) =>
        Preferences.Get(key, defaultValue);

    public void SetBool(string key, bool value) => Preferences.Set(key, value);

    public long GetLong(string key, long defaultValue = 0) =>
        Preferences.Get(key, defaultValue);

    public void SetLong(string key, long value) => Preferences.Set(key, value);

    public void Remove(string key) => Preferences.Remove(key);

    public void Clear() => Preferences.Clear();
}
