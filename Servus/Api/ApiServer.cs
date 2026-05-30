using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

static class ApiServer
{
	static Logger logger = LogManager.GetCurrentClassLogger();

	public static async Task<WebApplication> StartAsync(
		AppState state,
		Int32 port,
		CancellationToken cancellationToken)
	{
		var builder = WebApplication.CreateSlimBuilder();

		builder.Logging.ClearProviders();
		builder.Logging.AddProvider(new NLogApiLoggerProvider());

		builder.Services.Configure<JsonOptions>(options =>
		{
			options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
		});

		builder.Services.AddSingleton(state);
		builder.Services
			.AddMcpServer()
			.WithHttpTransport(options => options.Stateless = true)
			.WithTools<ServusMcpTools>();

		builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

		var app = builder.Build();

		MapEndpoints(app, state);
		app.MapMcp("/mcp");

		await app.StartAsync(cancellationToken);

		logger.Info($"API server listening on http://127.0.0.1:{port}; MCP endpoint is http://127.0.0.1:{port}/mcp");

		return app;
	}

	static void MapEndpoints(IEndpointRouteBuilder app, AppState state)
	{
		app.MapGet("/tasks", () => Results.Ok(state.GetTaskOverview()));

		app.MapGet("/tasks/{name}/logs", (String name, Int32? lines, HttpRequest request) =>
		{
			try
			{
				var tail = state.GetTaskLogTail(name, lines);

				if (request.Headers.Accept.Any(value => value?.Contains("application/json") == true))
				{
					return Results.Ok(tail);
				}

				return Results.Text(String.Join(Environment.NewLine, tail.LogsTail), "text/plain");
			}
			catch (FriendlyException ex)
			{
				return Results.NotFound(new { message = ex.Message });
			}
		});

		app.MapPost("/tasks/actions", async (
			TaskActionsRequestDto request,
			CancellationToken cancellationToken) =>
		{
			var results = await state.ExecuteActionsAsync(request.Actions, cancellationToken);

			return Results.Ok(new { results });
		});

		app.MapPost("/commands/run", async (
			RunCommandRequestDto request,
			CancellationToken cancellationToken) =>
		{
			try
			{
				return Results.Ok(await state.RunCommandAsync(request, cancellationToken));
			}
			catch (FriendlyException ex)
			{
				return Results.BadRequest(new { message = ex.Message });
			}
		});
	}
}
