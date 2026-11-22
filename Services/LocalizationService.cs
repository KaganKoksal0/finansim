using System.Globalization;
using Microsoft.JSInterop;

namespace Finansim.Services;

/// <summary>
/// Stores the selected interface language and provides small inline translations.
/// The preference is stored separately from the financial backup data.
/// </summary>
public sealed class LocalizationService
{
    private readonly IJSRuntime _js;
    private bool _initialized;

    public LocalizationService(IJSRuntime js)
    {
        _js = js;
    }

    public string Language { get; private set; } = "tr";
    public bool IsEnglish => Language == "en";

    public string T(string turkish, string english) => IsEnglish ? english : turkish;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            var savedLanguage = await _js.InvokeAsync<string?>("financeStorage.getLanguage");
            Language = savedLanguage == "en" ? "en" : "tr";
            await _js.InvokeVoidAsync("financeStorage.setLanguage", Language);
        }
        catch (JSException)
        {
            // Keep the app usable in Turkish if browser storage is unavailable.
            Language = "tr";
        }
        catch (InvalidOperationException)
        {
            Language = "tr";
        }

        ApplyCulture();
        _initialized = true;
    }

    public async Task SetLanguageAsync(string? language)
    {
        Language = language == "en" ? "en" : "tr";
        ApplyCulture();

        try
        {
            await _js.InvokeVoidAsync("financeStorage.setLanguage", Language);
        }
        catch (JSException)
        {
            // The current page still switches language; only persistence is unavailable.
        }
        catch (InvalidOperationException)
        {
            // The current page still switches language; only persistence is unavailable.
        }
    }

    private void ApplyCulture()
    {
        var culture = CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
