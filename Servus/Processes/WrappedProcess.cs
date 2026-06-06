using System.Diagnostics;
using System.Reactive.Disposables;



class WrappedProcess : SystemDiagnosticsProcess, IProcess
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  static IReadOnlyList<String> IProcess.Names => ["wrapped"];

  ControllingClient? client;

  SerialDisposable subscriptionDisposable = new();

  protected override Process Process { get; }

  public WrappedProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    var redirectToParent = settings.RedirectOutput;

    if (settings.Port is Int32 port && settings.Id is String id)
    {
      client = ControllingClient.Create(id, port);

      logger.Debug($"Redirect to parent: {redirectToParent}");

      subscriptionDisposable.Disposable = client.In
        .SubscribeOn(NewThreadScheduler.Default)
        .Subscribe(line =>
      {
        if (line == "shutdown")
        {
          logger.Info($"Got shutdown message from server");

          Shutdown();
        }
        else if (line == "kill")
        {
          logger.Info($"Got kill message from server");

          Kill();
        }
        else
        {
          logger.Debug($"Got message from server: '{line}'");
        }
      });
    }

    var args = new List<String>();

    if (settings.WorkingDirectory is String wd)
    {
      args.Add("-WorkingDirectory");
      args.Add(wd);
    }

    args.Add("-c");
    args.AddRange(settings.Cargs);

    Process = ConsoleProcessRunner.StartProcess(
      new ConsoleProcessSettings(
        ["pwsh", .. args],
        WindowStyle: settings.WindowStyle,
        RedirectOutput: settings.RedirectOutput,
        CreateNoWindow: settings.CreateNoWindow,
        NoShellExecute: settings.NoShellExecute,
        KeepTerminalOpen: settings.KeepTerminalOpen,
        OnOutput: redirectToParent && client is not null ? client.WriteLine : settings.OnOutput,
        OnLog: settings.OnLog,
        OnExit: exitCode =>
        {
          client?.Close();
          subscriptionDisposable.Dispose();
          settings.OnExit?.Invoke(exitCode);
        },
        CreateConsoleBlockedScope: settings.CreateConsoleBlockedScope));
  }

  public override Boolean Shutdown()
  {
    if (OperatingSystem.IsWindows())
    {
      logger.Info("Trying gracefully");

      try
      {
        using var scope = settings.CreateConsoleBlockedScope?.Invoke();

        ConsoleProcessRunner.SendBreak(Process);
        return true;
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Exception on sending a ctrl-c");
        return false;
      }
    }
    else
    {
      logger.Info("Skipping graceful stop on this platform");
      return false;
    }
  }
}
