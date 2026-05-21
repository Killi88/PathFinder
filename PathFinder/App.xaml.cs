using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PathFinder;

public partial class App : Application
{
    private SplashScreen? _splash;
    private Mutex? _singleInstanceMutex;
    private const string MutexName = "PathFinder_SingleInstance_Mutex";
    private const string PipeName = "PathFinder_SingleInstance_Pipe";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var fileArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

        // Try to become the single instance
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // Another instance is running — send file paths to it and exit
            foreach (var arg in fileArgs)
            {
                if (File.Exists(arg))
                    SendFileToRunningInstance(arg);
            }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        // Show splash immediately with default dark theme — before any JSON/settings I/O
        // so the user sees feedback as fast as possible on slow single-file .exe startup.
        _splash = new SplashScreen(isDarkMode: true);
        _splash.Show();
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

        // Now load settings and update theme if needed
        var savedSettings = PathFinder.MainWindow.LoadWindowSettings();
        if (savedSettings?.IsDarkMode == false)
            _splash.UpdateTheme(isDarkMode: false);

        var main = new MainWindow();
        main.Loaded += (s, _) =>
        {
            _splash.Close();
            _splash = null;

            // Open files passed as command-line arguments (e.g. from Explorer context menu)
            foreach (var arg in fileArgs)
            {
                if (File.Exists(arg))
                    main.OpenFileFromCommandLine(arg);
            }
        };

        MainWindow = main;
        main.Show();

        // Start listening for file paths from other instances
        StartPipeListener(main);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
        base.OnExit(e);
    }

    private static void SendFileToRunningInstance(string filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000); // 2-second timeout
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(filePath);
        }
        catch
        {
            // If the pipe is unavailable, fall through silently
        }
    }

    private void StartPipeListener(MainWindow main)
    {
        var thread = new Thread(() => ListenForPipeConnections(main))
        {
            IsBackground = true,
            Name = "PathFinder_PipeListener"
        };
        thread.Start();
    }

    private void ListenForPipeConnections(MainWindow main)
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                server.WaitForConnection();
                using var reader = new StreamReader(server);
                while (reader.ReadLine() is { } filePath)
                {
                    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    {
                        var path = filePath; // capture for closure
                        main.Dispatcher.Invoke(() =>
                        {
                            main.OpenFileFromCommandLine(path);

                            // Bring the window to front
                            if (main.WindowState == WindowState.Minimized)
                                main.WindowState = WindowState.Normal;
                            main.Activate();
                        });
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                break; // App is shutting down
            }
            catch
            {
                // Transient pipe error — retry
            }
        }
    }
}
