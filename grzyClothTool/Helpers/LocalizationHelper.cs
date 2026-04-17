using System;
using System.Globalization;
using System.Windows;

namespace grzyClothTool.Helpers;

public static class LocalizationHelper
{
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var resource = Application.Current?.TryFindResource(key);
        return resource as string ?? $"!{key}!";
    }

    public static string GetFormat(string key, params object[] args)
    {
        var fmt = Get(key);
        if (args == null || args.Length == 0) return fmt;
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, fmt, args);
        }
        catch (FormatException)
        {
            return fmt;
        }
    }
}
