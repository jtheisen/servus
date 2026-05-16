using System.Diagnostics;



class LegacyProcess : SystemDiagnosticsProcess, IProcess
{
  static IReadOnlyList<String> IProcess.Names => ["wrapped-legacy"];

  protected override Process Process { get; }

  public LegacyProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    var ownPath = Environment.ProcessPath;

    var info = new ProcessStartInfo
    {
      FileName = ownPath,
      WorkingDirectory = settings.WorkingDirectory,
      // FileName = "cmd.exe",
      // Arguments = $"/c {Cmd}",
      UseShellExecute = false,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };

    AddWrappingRunArgs(info.ArgumentList);

    Process = new Process
    {
      StartInfo = info,
      EnableRaisingEvents = true
    };

    Process.Exited += (s, e) =>
    {
      if (Process == s)
      {
        settings.OnExit?.Invoke(Process.ExitCode);
      }
    };

    Process.OutputDataReceived += OnDataReceived;
    Process.ErrorDataReceived += OnDataReceived;

    Process.Start();

    Process.BeginOutputReadLine();
    Process.BeginErrorReadLine();
  }

  public override void Stop()
  {
    Process.StandardInput.WriteLine();
  }
}
