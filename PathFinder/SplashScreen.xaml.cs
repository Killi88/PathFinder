using System.Windows;

namespace PathFinder;

public partial class SplashScreen : System.Windows.Window
{
    public SplashScreen(bool isDarkMode = true)
    {
        InitializeComponent();
        ApplyTheme(isDarkMode);
    }

    public void SetStatus(string text)
    {
        statusText.Text = text;
    }

    public void UpdateTheme(bool isDarkMode)
    {
        ApplyTheme(isDarkMode);
    }

    private void ApplyTheme(bool dark)
    {
        var themeUri = new Uri(
            dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml",
            UriKind.Relative);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
    }
}
