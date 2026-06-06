using System.Diagnostics;
using System.Text;

static class ConsoleProcessRunner
{
    static Logger logger = LogManager.GetCurrentClassLogger();

    public static void RunProcess(IReadOnlyList<String> cargs)
    {
        StartProcess(new ConsoleProcessSettings(cargs), overrideUseShellExecute: true);
    }

    public static Process StartProcess(ConsoleProcessSettings settings, Boolean overrideUseShellExecute = false)
    {
        if (settings.Cargs.Count == 0)
        {
            throw new FriendlyException("The command arguments list must include an executable.");
        }

        var useShellExecute = overrideUseShellExecute || (settings.WindowStyle is not null && !settings.NoShellExecute);

        var redirected = settings.RedirectOutput && !useShellExecute;

        var outputEnconding = redirected ? Encoding.UTF8 : null;

        var info = new ProcessStartInfo
        {
            FileName = settings.Cargs[0],
            UseShellExecute = useShellExecute,
            RedirectStandardInput = redirected,
            RedirectStandardOutput = redirected,
            RedirectStandardError = redirected,
            CreateNoWindow = settings.CreateNoWindow,
            WindowStyle = settings.WindowStyle ?? ProcessWindowStyle.Hidden,
            StandardOutputEncoding = outputEnconding,
            StandardErrorEncoding = outputEnconding
        };

        if (settings.WorkingDirectory is { } wd)
        {
            Assert<FriendlyException>(!useShellExecute, "With shell execute you can't set a working directory");

            info.WorkingDirectory = wd;
        }

        foreach (var arg in settings.Cargs.Skip(1))
        {
            info.ArgumentList.Add(arg);
        }

        var process = new Process
        {
            StartInfo = info,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            Thread.Sleep(100);

            settings.OnOutput?.Invoke($"Process exited with exit code {process.ExitCode}");

            if (settings.KeepTerminalOpen)
            {
                settings.OnOutput?.Invoke($"Waiting for user input");

                Console.Read();
            }

            settings.OnExit?.Invoke(process.ExitCode);
        };

        if (redirected)
        {
            DataReceivedEventHandler dataReceived = (_, e) =>
            {
                if (e.Data is not null)
                {
                    settings.OnOutput?.Invoke(e.Data);
                }
            };

            process.OutputDataReceived += dataReceived;
            process.ErrorDataReceived += dataReceived;
        }

        {
            var line = $"{info.FileName} {String.Join(" ", info.ArgumentList)}";

            settings.OnLog?.Invoke($"Starting process: {line}");
        }

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Process failed to start");

            settings.OnOutput?.Invoke($"Process failed to start: {ex.Message}");

            settings.OnExit?.Invoke(1);

            throw;
        }

        if (redirected)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.StandardInput.Close();
        }

        settings.OnOutput?.Invoke($"Started process {process.Id}");

        return process;
    }

    public static Boolean SendBreak(Process process, Int32 millis = 400)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return PlatformWindows.SendBreak(process, millis);
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error sending break");
            throw;
        }
    }

    public static void LogProcessesOnThisConsole(Action<String> writeLine)
    {
        if (OperatingSystem.IsWindows())
        {
            PlatformWindows.LogProcessesOnThisConsole(writeLine);
        }
    }

}
