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
		app.MapMcp();

		await app.StartAsync(cancellationToken);

		logger.Info($"API server listening on http://127.0.0.1:{port}");

		return app;
	}

	static void MapEndpoints(IEndpointRouteBuilder app, AppState state)
	{
		app.MapGet("/tasks", () => Results.Ok(state.GetTasks()));

		app.MapGet("/tasks/{name}/logs", (String name, Int32? lines) =>
		{
			try
			{
				return Results.Ok(state.GetTaskLogTail(name, lines));
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
	}
}
