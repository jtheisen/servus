using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.VisualBasic;



class ProcessFactory
{
  public static readonly ProcessFactory Instance = new();

  Type defaultProcessType;

  Dictionary<String, Type> processTypesByName = new();

  public ProcessFactory()
  {
    Add<DirectProcess>();
    Add<LegacyProcess>();
    Add<WindowedWrappedProcess>(true);
    Add<WrappedWindowedProcess>();

    Assert(defaultProcessType is not null);
  }

  void Add<TProcess>(Boolean isDefault = false)
    where TProcess : IProcess
  {
    foreach (var name in TProcess.Names)
    {
      processTypesByName.Add(name, typeof(TProcess));
    }

    if (isDefault)
    {
      Assert(defaultProcessType is null);

      defaultProcessType = typeof(TProcess);
    }
  }

  public AbstractProcess Start(FactoryProcessSettings settings)
  {
    var type = Get(settings.ProcessRunner);

    var activatorResult = Activator.CreateInstance(type, settings) as AbstractProcess;

    Assert(activatorResult is not null);

    return activatorResult;
  }

  public Type Get(String? name)
  {
    if (String.IsNullOrWhiteSpace(name))
    {
      return defaultProcessType;
    }
    else if (processTypesByName.TryGetValue(name, out var factory))
    {
      return factory;
    }
    else
    {
      throw new Exception($"Unknown factory {name}");
    }
  }
}

interface IProcess
{
  static abstract IReadOnlyList<String> Names { get; }
}

abstract class AbstractProcess(FactoryProcessSettings settings)
{
  public static String GetName<TProcess>()
    where TProcess : IProcess
  {
    return TProcess.Names.First();
  }

  protected readonly FactoryProcessSettings settings = settings;

  public abstract Boolean HasExited { get; }

  public abstract Int32 ExitCode { get; }

  public abstract void Stop();

  public abstract void Kill();

  protected void OnDataReceived(object? sender, DataReceivedEventArgs e)
  {
    if (e.Data is not null)
    {
      settings.OnOutput?.Invoke(e.Data);
    }
  }
}

abstract class SystemDiagnosticsProcess(FactoryProcessSettings settings) : AbstractProcess(settings)
{
  public override Boolean HasExited => Process.HasExited;

  public override Int32 ExitCode => Process.ExitCode;

  protected abstract Process Process { get; }

  public override void Kill() => Process.Kill(true);

  [Flags]
  protected enum WrappingArgFlags
  {
    NoWindow = 1
  }

  protected String[] GetWrappingRunArgs(WrappingArgFlags flags = default)
  {
    return AddWrappingRunArgs(new List<String>(), flags).ToArray();
  }
  
  protected ICollection<String> AddWrappingRunArgs(ICollection<String> args, WrappingArgFlags flags = default)
  {
    args.Add("run");

    args.Add("--runner");
    args.Add("direct");

    if (!flags.HasFlag(WrappingArgFlags.NoWindow) && settings.WindowStyle is { } windowStyle)
    {
      args.Add("--window");
      args.Add(windowStyle.ToString());
    }

    if (settings.Id is String id)
    {
      args.Add("--id");
      args.Add(id);
    }

    if (settings.RedirectOutput)
    {
      args.Add("--redirect-output");
    }

    if (settings.KeepTerminalOpen)
    {
      args.Add("--keep-terminal-open");
    }

    if (settings.Port is Int32 port)
    {
      args.Add("--port");
      args.Add(port.ToString());
    }

    if (settings.WorkingDirectory is String wd)
    {
      args.Add("-d");
      args.Add(wd);
    }

    if (settings.Cargs.Count == 0)
    {
      throw new FriendlyException("The command arguments list must include an executable.");
    }

    args.Add(settings.Cargs[0]);
    args.Add("--");

    foreach (var arg in settings.Cargs.Skip(1))
    {
      args.Add(arg);
    }

    return args;
  }
}

