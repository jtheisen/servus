using System.ComponentModel;
using System.Diagnostics;

using Spectre.Console;
using Spectre.Console.Cli;

class UiCommand : Command<UiCommand.Settings>
{
  const String ConfigurationFile = "servus.yaml";

  public class Settings : CommandSettings
  {
    [CommandOption("--config")]
    [Description("The YAML configuration file to load")]
    [DefaultValue(ConfigurationFile)]
    public String Config { get; init; } = ConfigurationFile;
  }

  protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
  {
    Program.SetName("app");

    var store = AppState.Load(settings.Config);

    var ui = new Ui(store);

    ui.Run();

    return 0;
  }
}

class InitCommand : Command
{
  const String ConfigurationFile = "servus.yaml";

  protected override int Execute(CommandContext context, CancellationToken cancellation)
  {
    Configuring.Configuration.WriteSample(ConfigurationFile);
    AnsiConsole.WriteLine($"Created {ConfigurationFile}.");
    return 0;
  }
}

class RunCommand : AsyncCommand<RunCommand.Settings>
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  public class Settings : CommandSettings
  {
    [CommandArgument(0, "<executable>")]
    [Description("The executable to run")]
    public String Cmd { get; init; } = "";

    public IReadOnlyList<String> Cargs { get; set; } = [];

    [CommandOption("-r|--runner")]
    [Description("The process runner to use")]
    public String? ProcessRunner { get; init; }

    [CommandOption("-d|--working-directory")]
    [Description("The working directory for the process to run in")]
    public String? WorkingDirectory { get; init; }

    [CommandOption("-w|--window")]
    [Description("Run in new terminal window")]
    [DefaultValue(null)]
    public ProcessWindowStyle? WindowStyle { get; init; }

    [CommandOption("-p|--port")]
    [Description("The port of the parent process to connect to")]
    public Int32? Port { get; set; }

    [CommandOption("--id")]
    [Description("The id the child process should identify as")]
    public String? Id { get; set; }

    [CommandOption("--redirect-output")]
    [Description("Redirected child process output to the parent process")]
    [DefaultValue(false)]
    public Boolean RedirectOutput { get; init; }

    [CommandOption("--keep-terminal-open")]
    [Description("Keep the new terminal open until the user presses a key")]
    [DefaultValue(false)]
    public Boolean KeepTerminalOpen { get; init; }

    [CommandOption("--create-no-window")]
    [Description("Dont allocate a terminal even if the runner wants it")]
    [DefaultValue(false)]
    public Boolean CreateNoWindow { get; init; }

    [CommandOption("--no-shell-execute")]
    [Description("Suppress shell execution even if the runner wants it")]
    [DefaultValue(false)]
    public Boolean NoShellExecute { get; init; }

    [CommandOption("--test")]
    [Description("Dont listen to stdin for process termination")]
    [DefaultValue(false)]
    public Boolean Test { get; init; }
  }

  protected override async Task<Int32> ExecuteAsync(CommandContext context, Settings s, CancellationToken cancellationToken)
  {
    Program.SetName(Guid.NewGuid().ToString());

    AcceptedClient? client = null;

    void SetNewClient(String id, AcceptedClient newClient)
    {
      client = newClient;

      Console.WriteLine($"Accepted client with id {id}");
    }

    if (s.Test)
    {
      s.Port ??= Server.Instance.Port;
      s.Id ??= "test";
      Server.Instance.InstallClientListener(SetNewClient);
    }

    s.Cargs = [s.Cmd, .. context.Remaining.Raw];

    try
    {
      var process = Run();

      // We're exiting with Environment.Exit.

      // If the process runner is null, we assume the default runner is
      // WindowedWrappedProcess and we're in a test scenario - so we
      // allow termination with a 'q' input line.

      if (!s.Test)
      {
        while (true)
        {
          Thread.Sleep(10000);
        }
      }
      else
      {
        while (Console.ReadLine() is String line)
        {
          if (line.Equals("q", StringComparison.InvariantCultureIgnoreCase))
          {
            process.Stop();
          }
        }

        return 0;
      }
    }
    catch (Exception ex)
    {
      logger.Error(ex, "Unhandled exception");

      return 1;
    }

    AbstractProcess Run()
    {
      var output = new DeferredConsoleOutput();

      var settings = new FactoryProcessSettings(
        s.Cargs,
        WorkingDirectory: s.WorkingDirectory,
        ProcessRunner: s.ProcessRunner,
        WindowStyle: s.WindowStyle,
        Port: s.Port,
        Id: s.Id,
        RedirectOutput: s.RedirectOutput,
        CreateNoWindow: s.CreateNoWindow,
        NoShellExecute: s.NoShellExecute,
        KeepTerminalOpen: s.KeepTerminalOpen,
        OnOutput: output.WriteLine,
        OnLog: output.WriteLine,
        OnExit: _ => Environment.Exit(0),
        SendMessageToClient: l => client?.Inout.output.WriteLine(l),
        CreateConsoleBlockedScope: output.CreateBlockedScope);

      var process = ProcessFactory.Instance.Start(settings);

      //Console.CancelKeyPress += (_, e) => e.Cancel = true;

      output.WriteLine("Waiting for console input or the process to terminate on its own");

      return process;
    }
  }

}
