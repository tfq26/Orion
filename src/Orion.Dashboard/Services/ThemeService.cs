using Microsoft.JSInterop;

namespace Orion.Dashboard.Services;

public class ThemeService
{
    private readonly IJSRuntime _js;
    private string _currentTheme = "orion";

    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public string CurrentTheme => _currentTheme;

    public async Task SetThemeAsync(string theme)
    {
        _currentTheme = theme;
        await _js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", theme);
        OnThemeChanged?.Invoke();
    }

    public async Task InitializeAsync()
    {
        // In a real app, we'd load this from local storage
        await SetThemeAsync(_currentTheme);
    }
}
