using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Text;
using Servus.Utils;

enum State
{
	Stopped,
	Running,
	Stopping,
	Restarting,
}

class Tasklet : IDisposable
{
	static Logger logger = LogManager.GetCurrentClassLogger();

	public Configuring.Task Configuration { get; }

	public String Name => Configuration.Name ?? throw new Exception("Task validation should require a name.");
	public String? Wd => Configuration.Wd;
	public String Type => Configuration.Type ?? "";
	public String? ProcessRunner => Configuration.ProcessRunner;
	public Int32? Port => Configuration.Port;
	public Configuring.ConnectionMethod Method => Configuration.GetConnectionMethod();

	public State State { get; set; } = State.Stopped;

	public Boolean IsPortConnectable { get; set; }

	public String? GitBranch { get; set; }

	public String UiState => State.ToString().PadRight(11) + OutputSpinner;

	public Int32? ExitCode => process is { HasExited: true } ? process.ExitCode : null;

	public List<String> Output { get; } = new();

	AbstractProcess? process;

	SerialDisposable processDisposable = new();

	Boolean IsClientConnected => process?.IsClientConnected ?? false;

	String OutputSpinner => IsClientConnected ? $"[grey]{BrailleSpinner.FromNumber(outputSpinnerState)}[/]" : "";

	CompositeDisposable disposables = new();

	Boolean showWindow;

	Int32 outputSpinnerState, pendingSpinningForOutput;

	public Tasklet(Configuring.Task configuration)
	{
		Configuration = configuration;
		disposables.Add(processDisposable);

		if (Port is Int32 port)
		{
			disposables.Add(ConnectionChecker.GetTester(port, Method).Subscribe(v => IsPortConnectable = v));
		}

		if (Wd is String wd && GitBranchTester.HasGitMetadata(wd))
		{
			disposables.Add(GitBranchTester.GetTester(wd, Name).Subscribe(branch => GitBranch = branch));
		}
	}

	public void Tick()
	{
		if (pendingSpinningForOutput > 0)
		{
			--pendingSpinningForOutput;
			++outputSpinnerState;
		}
	}

	public void Toggle(Boolean showWindow = false)
	{
		this.showWindow = showWindow;

		switch (State)
		{
			case State.Stopped:
			case State.Stopping:
				Start();
				break;
			case State.Restarting:
			case State.Running:
				Stop();
				break;
		}
	}

	public void Start()
	{
		if (State == State.Stopping)
		{
			State = State.Restarting;
		}

		if (State != State.Stopped || process is { HasExited: false })
		{
			return;
		}

		Output.Clear();

		try
		{
			State = State.Running;

			Run();
		}
		catch (Exception ex)
		{
			Output.Add(ex.Message);
		}
	}

	public void Stop()
	{
		if (State == State.Restarting)
		{
			State = State.Stopping;

			return;
		}

		if (State != State.Running || process is null)
		{
			return;
		}

		try
		{
			State = State.Stopping;

			logger.Debug($"Stopping '{Name}'");

			Output.Add("Stopping...");

			_ = ShutdownAndEventuallyKill(process);
		}
		catch (Exception ex)
		{
			Output.Add(ex.Message);
		}
	}

	async Task ShutdownAndEventuallyKill(AbstractProcess process)
	{
		var didSignal = process.Shutdown();

		if (didSignal)
		{
			await Task.Delay(TimeSpan.FromSeconds(4));
		}

		if (!process.HasExited)
		{
			process.Kill();
		}
	}

	public async Task StopAndWaitAsync(CancellationToken cancellationToken)
	{
		Stop();

		while (State != State.Stopped)
		{
			Assert(State == State.Stopping, "Stopping was interrupted");

			await Task.Delay(50, cancellationToken);
		}
	}

	public Boolean Kill()
	{
		try
		{
			if (!(process?.HasExited ?? true))
			{
				process.Kill();

				State = State.Stopped;

				return true;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Exception on killing process");
		}

		return false;
	}

	void Run()
	{
		var windowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;

		var port = Server.Instance.Port;

		var settings = new FactoryProcessSettings(
			Configuration.GetCargs(Configuration),
			WorkingDirectory: Wd,
			WindowStyle: windowStyle,
			Port: port,
			Id: Name,
			PosixShutdownSignal: Configuration.PosixShutdownSignal,
			RedirectOutput: !showWindow,
			KeepTerminalOpen: showWindow,
			OnOutput: LogFromSelf,
			OnLog: LogFromSelf,
			OnExit: HandleExit);

		process = ProcessFactory.Instance.Start(settings);
		processDisposable.Disposable = process;
	}

	public void HandleExit(Int32 exitCode)
	{
		var previousState = State;

		State = State.Stopped;

		if (previousState == State.Restarting)
		{
			Start();
		}
	}

	const Int32 MaxPendingSpinningForOutput = 4;

	public void LogFromSelf(String message)
	{
		if (pendingSpinningForOutput < MaxPendingSpinningForOutput)
		{
			++pendingSpinningForOutput;
		}

		Output.Add(message);
	}

	void IDisposable.Dispose()
	{
		disposables.Dispose();
	}
}
