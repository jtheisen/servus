using System.Diagnostics;

class DirectProcess : SystemDiagnosticsProcess, IProcess
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  static IReadOnlyList<String> IProcess.Names => ["direct"];

  protected override Process Process { get; }

  public DirectProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    Process = ConsoleProcessRunner.StartProcess(
      new ConsoleProcessSettings(
        settings.Cargs,
        WorkingDirectory: settings.WorkingDirectory,
        WindowStyle: null,
        RedirectOutput: true,
        CreateNoWindow: true,
        NoShellExecute: settings.NoShellExecute,
        KeepTerminalOpen: settings.KeepTerminalOpen,
        OnOutput: settings.OnOutput,
        OnLog: settings.OnLog,
        OnExit: settings.OnExit,
        CreateConsoleBlockedScope: settings.CreateConsoleBlockedScope));
  }

  public override Boolean Shutdown()
  {
    try
    {
      if (OperatingSystem.IsWindows())
      {
        ConsoleProcessRunner.SendBreak(Process);
      }
      else
      {
        PlatformPosix.SendSignal(Process, settings.PosixShutdownSignal ?? PlatformPosix.SIGINT);
      }

      return true;
    }
    catch (Exception ex)
    {
      logger.Error(ex, "Exception on sending shutdown signal");
      return false;
    }
  }
}
