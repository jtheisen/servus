using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
class ServusMcpTools(AppState state)
{
	[McpServerTool]
	[Description("Returns the configured Servus tasks and allowed command prefixes. Allowed commands are shown with ellipses to indicate that more arguments may follow.")]
	public TaskOverviewDto GetTasks()
		=> state.GetTaskOverview();

	[McpServerTool]
	[Description("Returns a longer tail of recent log output for one Servus task.")]
	public TaskLogTailDto GetTaskLogTail(
		[Description("The task name.")] String task,
		[Description("The maximum number of log lines to return.")] Int32 lines)
		=> state.GetTaskLogTail(task, lines);

	[McpServerTool]
	[Description("Starts one or more Servus tasks by name.")]
	public Task<IReadOnlyList<TaskActionResultDto>> StartTasks(
		[Description("The task names to start.")] String[] tasks,
		CancellationToken cancellationToken)
		=> state.ExecuteActionsAsync(
			tasks.Select(task => new TaskActionDto(task, "start")),
			cancellationToken);

	[McpServerTool]
	[Description("Stops one or more Servus tasks by name and waits until each stop operation has completed.")]
	public Task<IReadOnlyList<TaskActionResultDto>> StopTasks(
		[Description("The task names to stop.")] String[] tasks,
		CancellationToken cancellationToken)
		=> state.ExecuteActionsAsync(
			tasks.Select(task => new TaskActionDto(task, "stop")),
			cancellationToken);

	[McpServerTool]
	[Description("Runs a batch of Servus task actions. Each action must contain a task name and an action of start or stop.")]
	public Task<IReadOnlyList<TaskActionResultDto>> RunTaskActions(
		[Description("The task actions to run.")] TaskActionDto[] actions,
		CancellationToken cancellationToken)
		=> state.ExecuteActionsAsync(actions, cancellationToken);

	[McpServerTool]
	[Description("Runs a command if it starts with one of the configured allowed command prefixes. Use GetTasks first to inspect allowedCommands.")]
	public Task<RunCommandResultDto> RunCommand(
		[Description("The full command line to run. It must start with one of the allowed command prefixes.")] String command,
		CancellationToken cancellationToken)
		=> state.RunCommandAsync(new RunCommandRequestDto(command), cancellationToken);
}
