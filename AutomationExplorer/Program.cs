using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;

namespace AutomationExplorer;

internal sealed class Program
{
    private const string SingleInstanceMutexName = "Local\\AutomationExplorer.SingleInstance";
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        if (!TryAcquireSingleInstance())
        {
            ShowAlreadyRunningMessage();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            ReleaseSingleInstance();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            return createdNew;
        }
        catch
        {
            return true;
        }
    }

    private static void ReleaseSingleInstance()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        try
        {
            _singleInstanceMutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
    }

    private static void ShowAlreadyRunningMessage()
    {
        const uint okIconWarning = 0x00000030;
        MessageBoxW(IntPtr.Zero,
            "AutomationExplorer is already running. Close the existing instance before starting another one.",
            "AutomationExplorer",
            okIconWarning);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}