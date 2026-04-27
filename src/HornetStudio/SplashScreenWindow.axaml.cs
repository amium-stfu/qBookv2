using Avalonia.Controls;

namespace HornetStudio;

/// <summary>
/// Displays the startup splash screen while the main window is being prepared.
/// </summary>
public partial class SplashScreenWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplashScreenWindow"/> class.
    /// </summary>
    public SplashScreenWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the visible startup progress.
    /// </summary>
    /// <param name="value">The progress value between 0 and 100.</param>
    public void UpdateProgress(double value)
    {
        StartupProgressBar.Value = value;
    }
}
