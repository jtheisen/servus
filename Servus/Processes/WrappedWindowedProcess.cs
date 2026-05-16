using System.Diagnostics;



class WrappedWindowedProcess : SystemDiagnosticsProcess, IProcess
{
  static IReadOnlyList<String> IProcess.Names => ["wrapped-windowed"];

  protected override Process Process { get; }

  public WrappedWindowedProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    var ownArgs = GetWrappingRunArgs();

    var ownPath = Environment.ProcessPath ?? throw new Exception("Can't see what executable we're running");

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

  public override void Stop()
  {
    Process.StandardInput.WriteLine("terminate");
  }
}
