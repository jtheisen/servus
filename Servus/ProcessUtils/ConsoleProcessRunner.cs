using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;



static class ConsoleProcessRunner
{
    static Logger logger = LogManager.GetCurrentClassLogger();

    private const uint CTRL_C_EVENT = 0;
    private const uint CTRL_BREAK_EVENT = 1;
    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handler, bool add);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetStdHandle(int nStdHandle); // -10 = STD_INPUT_HANDLE

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint GetConsoleProcessList(uint[] lpdwProcessList, uint nLength);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

    private delegate bool ConsoleCtrlDelegate(uint ctrlType);

    public static Boolean SayHelloIfAppropriate(String[] args)
    {
        if (args is not ["say-hello", ..])
        {
            return false;
        }

        Console.WriteLine("Hello, the current path is:");

        foreach (var e in Environment.GetEnvironmentVariables())
        {
            Console.WriteLine($"{e}");
        }

        Thread.Sleep(1000);

        return true;
    }

    public static Int32 RunProcessAndStopOnInput(ConsoleProcessSettings settings)
    {
        var process = StartProcess(settings);

        LogProcessesOnThisConsole(settings.OnOutput ?? Console.WriteLine);

        Console.Read();

        StopProcess(process, settings);

        return process.ExitCode;
    }

    public static void RunProcess(IReadOnlyList<String> cargs)
    {
        StartProcess(new ConsoleProcessSettings(cargs), overrideUseShellExecute: true);
    }

    static void StopProcess(Process process, ConsoleProcessSettings settings)
    {
        settings.OnOutput?.Invoke("Stopping service");

        using var scope = settings.CreateConsoleBlockedScope?.Invoke();

        SendBreak(process);

        if (!process.HasExited)
        {
            settings.OnOutput?.Invoke("Waiting");
        }

        var didExit = process.WaitForExit(4000);

        if (didExit)
        {
            settings.OnOutput?.Invoke("Service stopped");
        }
        else
        {
            process.Kill();

            settings.OnOutput?.Invoke("Service killed");
        }
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
            StandardErrorEncoding = outputEnconding,
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

        process.Start();

        if (redirected)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.StandardInput.Close();
        }

        settings.OnOutput?.Invoke($"Started process {process.Id}");

        return process;
    }

    public static Boolean SendBreak(Process process, Int32 millis = 400, Int32 retries = 10)
    {
        if (process.HasExited)
        {
            return true;
        }

        var attached = false;
        var ignoreCtrl = false;

        try
        {
            Boolean HandleConsoleCtrl(uint ctrlType)
            {
                return true;
            }

            FreeConsole();

            // Important: We can't use Console.WriteLine after this point,
            // this will lead to the process terminating immediatly.

            if (!AttachConsole((uint)process.Id))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AttachConsole failed.");

            attached = true;

            if (!SetConsoleCtrlHandler(HandleConsoleCtrl, true))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetConsoleCtrlHandler(enable ignore) failed.");

            ignoreCtrl = true;

            // There's a danger that we get killed by our own
            // signal if we re-attach the parent console to soon.
            for (var i = 0; i < retries; ++i)
            {
                if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0)) // (uint)process.Id
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GenerateConsoleCtrlEvent failed.");

                Thread.Sleep(millis);

                if (process.HasExited)
                {
                    return true;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error sending break");

            throw;
        }
        finally
        {
            if (attached)
            {
                FreeConsole();
                AttachConsole(ATTACH_PARENT_PROCESS);
            }

            if (ignoreCtrl)
            {
                SetConsoleCtrlHandler(null, false);
            }
        }
    }

    public static async Task StopAsync(
        Process process,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        // var exited = await SendBreak(process, gracefulTimeout, cancellationToken);

        // if (!exited && !process.HasExited)
        // {
        //     process.Kill(entireProcessTree: true);
        //     await process.WaitForExitAsync(cancellationToken);
        // }
    }

    public static void LogProcessesOnThisConsole(Action<String> writeLine)
    {
        var processesOnConsole = new uint[8];
        var count = GetConsoleProcessList(processesOnConsole, (uint)processesOnConsole.Length);
        writeLine($"Processes on this console ({count}):");
        for (int i = 0; i < count; i++)
            writeLine($"  PID {processesOnConsole[i]}");

        var stdin = GetStdHandle(-10);
        GetConsoleMode(stdin, out var mode);
        writeLine($"Console mode after AttachConsole: 0x{mode:X8}");
    }
}
