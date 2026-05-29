namespace TiktokStreakSaver.Services.Storage;

/// <summary>
/// Cross-platform key-value storage. On iOS uses App Group UserDefaults for extension access.
/// </summary>
public interface IAppStorage
{
    string GetString(string key, string defaultValue = "");
    void SetString(string key, string value);
    bool GetBool(string key, bool defaultValue = false);
    void SetBool(string key, bool value);
    long GetLong(string key, long defaultValue = 0);
    void SetLong(string key, long value);
    void Remove(string key);
    void Clear();
}
