using System.Linq;

using System.Text.Json.Serialization;

class AppState
{
	public IReadOnlyList<Tasklet> Tasklets { get; }

	public IReadOnlyList<Configuring.Task> Profiles { get; }

	public Configuring.Settings Settings { get; }

	Dictionary<String, Tasklet> taskletsById;

	public static AppState Load(String filePath)
	{
		var fullPath = Path.GetFullPath(filePath);
		var configDirectory = Path.GetDirectoryName(fullPath)
			?? throw new FriendlyException($"Could not determine configuration directory for '{filePath}'.");
		var configuration = Configuring.Configuration.Load(filePath);
		return new AppState(configuration, configDirectory);
	}

	public AppState(Configuring.Configuration configuration, String configDirectory)
	{
		Settings = configuration.Settings;

		var profiles = configuration.Profiles.ToDictionary(
			p => p.Name ?? throw new FriendlyException("Profiles must have a name."));

		Tasklets = configuration.Tasks
			.Select(t => t.WithDefaults(profiles))
			.Select(t => t.Validate())
			.Select(t => new Tasklet(t, configDirectory))
			.ToList();

		Profiles = configuration.Profiles;
		taskletsById = Tasklets.ToDictionary(t => t.Name);

	}

	public IReadOnlyList<TaskDto> GetTasks()
		=> Tasklets.Select(TaskDto.From).ToList();

	public TaskOverviewDto GetTaskOverview()
		=> new(GetTasks(), GetAllowedCommandPrefixes());

	public IReadOnlyList<String> GetAllowedCommandPrefixes()
		=> Settings.AllowedCommands
			.Select(c => $"{c} ...")
			.ToList();

	public TaskLogTailDto GetTaskLogTail(String task, Int32? lines = null)
	{
		if (!taskletsById.TryGetValue(task, out var tasklet))
		{
			throw new FriendlyException($"Unknown task '{task}'.");
		}

		var lineCount = lines is Int32 value
			? Math.Clamp(value, 0, 1000)
			: tasklet.Output.Count;

		return new TaskLogTailDto(tasklet.Name, tasklet.Output.TakeLast(lineCount).ToList());
	}

	public async Task<IReadOnlyList<TaskActionResultDto>> ExecuteActionsAsync(
		IEnumerable<TaskActionDto> actions,
		CancellationToken cancellationToken)
	{
		var results = new List<TaskActionResultDto>();

		foreach (var action in actions)
		{
			results.Add(await ExecuteActionAsync(action, cancellationToken));
		}

		return results;
	}

	async Task<TaskActionResultDto> ExecuteActionAsync(TaskActionDto action, CancellationToken cancellationToken)
	{
		if (!taskletsById.TryGetValue(action.Task, out var tasklet))
		{
			return new TaskActionResultDto(
				action.Task,
				action.Action,
				false,
				null,
				$"Unknown task '{action.Task}'.");
		}

		try
		{
			switch (action.Action.ToLowerInvariant())
			{
				case "start":
					tasklet.Start();
					return new TaskActionResultDto(tasklet.Name, action.Action, true, tasklet.State.ToString(), null);
				case "stop":
					await tasklet.StopAndWaitAsync(cancellationToken);
					return new TaskActionResultDto(tasklet.Name, action.Action, true, tasklet.State.ToString(), null);
				default:
					return new TaskActionResultDto(
						tasklet.Name,
						action.Action,
						false,
						tasklet.State.ToString(),
						$"Unknown action '{action.Action}'.");
			}
		}
		catch (Exception ex)
		{
			return new TaskActionResultDto(tasklet.Name, action.Action, false, tasklet.State.ToString(), ex.Message);
		}
	}

	public async Task<RunCommandResultDto> RunCommandAsync(
		RunCommandRequestDto request,
		CancellationToken cancellationToken)
	{
		var workingDirectory = default(String);

		if (request.Task is { } task)
		{
			if (!taskletsById.TryGetValue(task, out var tasklet))
			{
				throw new FriendlyException($"Unknown task '{task}'.");
			}

			workingDirectory = tasklet.Wd;
		}

		var cargs = CommandLineArgs.Parse(request.Command);

		if (cargs.Length == 0)
		{
			throw new FriendlyException("The command must not be empty.");
		}

		AssertCommandAllowed(cargs);

		var output = new List<String>();

		var process = ConsoleProcessRunner.StartProcess(new ConsoleProcessSettings(
			cargs,
			WorkingDirectory: workingDirectory,
			CreateNoWindow: true,
			NoShellExecute: true,
			OnOutput: output.Add,
			OnLog: output.Add));

		try
		{
			await process.WaitForExitAsync(cancellationToken);
		}
		catch
		{
			if (!process.HasExited)
			{
				process.Kill(true);
			}

			throw;
		}

		return new RunCommandResultDto(request.Command, process.ExitCode, output);
	}

	void AssertCommandAllowed(IReadOnlyList<String> cargs)
	{
		foreach (var allowedCommand in Settings.AllowedCommands)
		{
			var prefix = CommandLineArgs.Parse(allowedCommand);

			if (prefix.Length <= cargs.Count && prefix.Zip(cargs).All(IsMatch))
			{
				return;
			}
		}

		throw new FriendlyException(
			$"Command is not allowed. Allowed prefixes are: {String.Join(", ", GetAllowedCommandPrefixes())}");

		static Boolean IsMatch((String Allowed, String Actual) pair)
			=> String.Equals(
				pair.Allowed,
				pair.Actual,
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
	}

}

record TaskOverviewDto(
	IReadOnlyList<TaskDto> Tasks,
	IReadOnlyList<String> AllowedCommands);

record TaskDto(
	String Name,
	String? WorkingDirectory,
	String State,
	String? GitBranch,
	Int32? ExitCode,
	IReadOnlyList<String> Cargs,
	[property: JsonPropertyName("logs-tail")]
	IReadOnlyList<String> LogsTail)
{
	public static TaskDto From(Tasklet tasklet)
		=> new(
			tasklet.Name,
			tasklet.Wd,
			tasklet.State.ToString(),
			tasklet.GitBranch,
			tasklet.ExitCode,
			tasklet.Configuration.GetCargs(tasklet.Configuration),
			tasklet.Output.TakeLast(20).ToList());
}

record TaskActionDto(String Task, String Action);

record TaskActionsRequestDto(IReadOnlyList<TaskActionDto> Actions);

record TaskActionResultDto(
	String Task,
	String Action,
	Boolean Success,
	String? State,
	String? Message);

record RunCommandRequestDto(String Command, String? Task = null);

record RunCommandResultDto(
	String Command,
	Int32 ExitCode,
	IReadOnlyList<String> Output);

record TaskLogTailDto(
	String Task,
	[property: JsonPropertyName("logs-tail")]
	IReadOnlyList<String> LogsTail);
