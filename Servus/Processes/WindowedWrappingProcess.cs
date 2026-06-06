using System.Diagnostics;

class WindowedWrappingProcess : WrappingProcess, IProcess
{
  static IReadOnlyList<String> IProcess.Names => ["windowed-wrapping"];

  protected override Process Process { get; }

  public WindowedWrappingProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    var ownArgs = GetWrappingRunArgs(WrappingArgFlags.NoWindow);

    var ownPath = settings.ServusPath
      ?? Environment.ProcessPath
      ?? throw new Exception("Can't see what executable we're running");

    Process = ConsoleProcessRunner.StartProcess(
      new ConsoleProcessSettings(
        [ownPath, .. ownArgs],
        WorkingDirectory: Environment.CurrentDirectory,
        WindowStyle: settings.WindowStyle,
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
