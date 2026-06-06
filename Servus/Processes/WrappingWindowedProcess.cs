using System.Diagnostics;



class WrappingWindowedProcess : WrappingProcess, IProcess
{
  static IReadOnlyList<String> IProcess.Names => ["wrapping-windowed"];

  protected override Process Process { get; }

  public WrappingWindowedProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    var ownArgs = GetWrappingRunArgs();

    var ownPath = settings.ServusPath
      ?? Environment.ProcessPath
      ?? throw new Exception("Can't see what executable we're running");

    Process = ConsoleProcessRunner.StartProcess(
      new ConsoleProcessSettings(
        [ownPath, .. ownArgs],
        WorkingDirectory: Environment.CurrentDirectory,
        RedirectOutput: settings.RedirectOutput,
        CreateNoWindow: settings.CreateNoWindow,
        NoShellExecute: settings.NoShellExecute,
        KeepTerminalOpen: false,
        OnOutput: settings.OnOutput,
        OnLog: settings.OnLog,
        OnExit: settings.OnExit,
        CreateConsoleBlockedScope: settings.CreateConsoleBlockedScope));
  }

}
