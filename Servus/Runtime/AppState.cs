using System.Linq;

class AppState
{
	public IReadOnlyList<Tasklet> Tasklets { get; }

	public IReadOnlyList<Configuring.Task> Profiles { get; }

	public Configuring.Settings Settings { get; }

	Dictionary<String, Tasklet> taskletsById;

	public static AppState Load(String filePath)
	{
		var configuration = Configuring.Configuration.Load(filePath);
		return new AppState(configuration);
	}

	public AppState(Configuring.Configuration configuration)
	{
		Settings = configuration.Settings;

		var profiles = configuration.Profiles.ToDictionary(
			p => p.Name ?? throw new FriendlyException("Profiles must have a name."));

		Tasklets = configuration.Tasks
			.Select(t => t.WithDefaults(profiles))
			.Select(t => t.Validate())
			.Select(t => new Tasklet(t))
			.ToList();

		Profiles = configuration.Profiles;
		taskletsById = Tasklets.ToDictionary(t => t.Name);

		Server.Instance.InstallClientListener(SetNewClient);
	}

	public IReadOnlyList<TaskDto> GetTasks()
		=> Tasklets.Select(TaskDto.From).ToList();

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

	void SetNewClient(String id, AcceptedClient client)
	{
		if (taskletsById.TryGetValue(id, out var tasklet))
		{
			tasklet.SetClient(client);
		}
		else
		{
			throw new Exception($"Unkown connection id '{id}' from client.");
		}
	}
}

record TaskDto(
	String Name,
	String? WorkingDirectory,
	String Type,
	String? ProcessRunner,
	Int32? Port,
	String Method,
	String State,
	Boolean IsPortConnectable,
	String? GitBranch,
	Int32? ExitCode,
	IReadOnlyList<String> Cargs,
	IReadOnlyList<String> Logs)
{
	public static TaskDto From(Tasklet tasklet)
		=> new(
			tasklet.Name,
			tasklet.Wd,
			tasklet.Type,
			tasklet.ProcessRunner,
			tasklet.Port,
			tasklet.Method.ToString(),
			tasklet.State.ToString(),
			tasklet.IsPortConnectable,
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
