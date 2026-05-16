using System.Linq;

class AppState
{
	public IReadOnlyList<Tasklet> Tasklets { get; }

	public IReadOnlyList<Configuring.Task> Profiles { get; }

	Dictionary<String, Tasklet> taskletsById;

	public static AppState Load(String filePath)
	{
		var configuration = Configuring.Configuration.Load(filePath);
		return new AppState(configuration);
	}

	public AppState(Configuring.Configuration configuration)
	{
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
