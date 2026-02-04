using System.Globalization;

namespace TILSOFTAI.Domain.Localization;

/// <summary>
/// Provides centralized culture management for the application.
/// </summary>
public static class Localization
{
    /// <summary>
    /// Sets the current culture and UI culture for the current thread.
    /// </summary>
    /// <param name="cultureName">The culture name (e.g., "en", "vi-VN").</param>
    public static void SetCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    /// <summary>
    /// Gets the current UI culture name.
    /// </summary>
    public static string CurrentCulture => Thread.CurrentThread.CurrentUICulture.Name;
}
