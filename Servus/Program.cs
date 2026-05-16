using NLog;
using Spectre.Console;
using Spectre.Console.Cli;

static class Program
{
	static Logger logger = LogManager.GetCurrentClassLogger();

	public static void SetName(String? name)
	{
		logger.Debug($"Setting name to {name}");
		var nlogConfig = LogManager.Configuration;
		Assert(nlogConfig is not null);
		nlogConfig.Variables["name"] = (name ?? "???").Substring(0, 3);
		LogManager.ReconfigExistingLoggers();		
	}

	private static Int32 Main(String[] args)
	{
		logger.Info($"Program start: servus {String.Join(" ", args)}");

		var app = new CommandApp();

		app.SetDefaultCommand<UiCommand>();

		app.Configure(config =>
		{
			config.AddCommand<InitCommand>("init");
			config.AddCommand<RunCommand>("run");

			config.SetExceptionHandler((ex, resolver) =>
			{
				switch(ex)
				{
					case FriendlyException f:
						AnsiConsole.WriteLine(f.Message);
						break;
					default:
						logger.Error(ex, "Uncaught exception");
						AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
						break;
				}

				return 1;
			});
		});

		return app.Run(args);

	}
}
