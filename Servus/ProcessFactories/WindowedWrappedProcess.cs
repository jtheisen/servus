using System.Diagnostics;
using System.Security.Cryptography;



class WindowedWrappedProcess : SystemDiagnosticsProcess, IProcess
{
  static IReadOnlyList<String> IProcess.Names => ["windowed-wrapped"];

  protected override Process Process { get; }

  public WindowedWrappedProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    var ownArgs = GetWrappingRunArgs(WrappingArgFlags.NoWindow);

    var ownPath = Environment.ProcessPath ?? throw new Exception("Can't see what executable we're running");

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

  public override void Stop()
  {
    settings.SendMessageToClient?.Invoke("terminate");
  }
}
