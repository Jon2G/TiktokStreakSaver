using Foundation;
using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver.Platforms.iOS.Services;

public sealed class AppGroupAppStorage : IAppStorage
{
    private readonly NSUserDefaults _defaults;

    public AppGroupAppStorage()
    {
        _defaults = AppGroupPaths.IsAppGroupAvailable
            ? new NSUserDefaults(AppConstants.AppGroupId, NSUserDefaultsType.SuiteName)
              ?? NSUserDefaults.StandardUserDefaults
            : NSUserDefaults.StandardUserDefaults;
        MigrateFromStandardIfNeeded();
    }

    /// <summary>Copies a non-empty string from standard UserDefaults into target storage when missing/empty.</summary>
    internal static void TryCopyNonEmptyStringFromStandard(string key, IAppStorage target)
    {
        if (!AppGroupPaths.IsAppGroupAvailable)
            return;

        var existing = target.GetString(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
            return;

        var standard = NSUserDefaults.StandardUserDefaults.StringForKey(key);
        if (string.IsNullOrWhiteSpace(standard))
            return;

        target.SetString(key, standard);
    }

    private static bool ShouldSkipMigrationKey(NSUserDefaults suite, string name, NSObject? standardValue)
    {
        var existing = suite.ValueForKey(new NSString(name));
        if (existing == null)
            return false;

        if (existing is NSString existingString)
            return !string.IsNullOrWhiteSpace(existingString.ToString());

        return true;
    }

    private void MigrateFromStandardIfNeeded()
    {
        if (!AppGroupPaths.IsAppGroupAvailable)
            return;

        const string migratedKey = "app_group_migrated_v1";
        if (_defaults.BoolForKey(migratedKey))
            return;

        var standard = NSUserDefaults.StandardUserDefaults;
        var dict = standard.ToDictionary();
        if (dict != null)
        {
            foreach (var kv in dict)
            {
                if (kv.Key is not NSString key)
                    continue;
                var name = key.ToString();
                if (string.IsNullOrEmpty(name) || name == migratedKey)
                    continue;
                if (ShouldSkipMigrationKey(_defaults, name, kv.Value))
                    continue;
                if (kv.Value is NSString s)
                    _defaults.SetString(s.ToString(), name);
                else if (kv.Value is NSNumber n)
                    _defaults.SetDouble(n.DoubleValue, name);
            }
        }

        _defaults.SetBool(true, migratedKey);
        _defaults.Synchronize();
    }

    public string GetString(string key, string defaultValue = "")
    {
        var v = _defaults.StringForKey(key);
        return string.IsNullOrEmpty(v) ? defaultValue : v;
    }

    public void SetString(string key, string value)
    {
        _defaults.SetString(value ?? string.Empty, key);
        _defaults.Synchronize();
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (_defaults.ValueForKey(new NSString(key)) == null)
            return defaultValue;
        return _defaults.BoolForKey(key);
    }

    public void SetBool(string key, bool value)
    {
        _defaults.SetBool(value, key);
        _defaults.Synchronize();
    }

    public long GetLong(string key, long defaultValue = 0)
    {
        if (_defaults.ValueForKey(new NSString(key)) == null)
            return defaultValue;
        return (long)_defaults.DoubleForKey(key);
    }

    public void SetLong(string key, long value)
    {
        _defaults.SetDouble(value, key);
        _defaults.Synchronize();
    }

    public void Remove(string key)
    {
        _defaults.RemoveObject(key);
        _defaults.Synchronize();
    }

    public void Clear()
    {
        var dict = _defaults.ToDictionary();
        if (dict == null)
            return;
        foreach (var kv in dict)
        {
            if (kv.Key is NSString key)
                _defaults.RemoveObject(key.ToString());
        }
        _defaults.Synchronize();
    }
}
