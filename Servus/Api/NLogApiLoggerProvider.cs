using Microsoft.Extensions.Logging;
using MLogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

class NLogApiLoggerProvider : ILoggerProvider
{
	public Microsoft.Extensions.Logging.ILogger CreateLogger(String categoryName)
		=> new NLogApiLogger(LogManager.GetLogger(categoryName));

	public void Dispose()
	{
	}
}

class NLogApiLogger(Logger logger) : Microsoft.Extensions.Logging.ILogger
{
	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull
		=> null;

	public Boolean IsEnabled(MLogLevel logLevel)
		=> logLevel != MLogLevel.None && logger.IsEnabled(ToNLogLevel(logLevel));

	public void Log<TState>(
		MLogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, String> formatter)
	{
		if (!IsEnabled(logLevel))
		{
			return;
		}

		logger.Log(ToNLogLevel(logLevel), exception, formatter(state, exception));
	}

	static NLogLevel ToNLogLevel(MLogLevel logLevel)
		=> logLevel switch
		{
			MLogLevel.Trace => NLogLevel.Trace,
			MLogLevel.Debug => NLogLevel.Debug,
			MLogLevel.Information => NLogLevel.Info,
			MLogLevel.Warning => NLogLevel.Warn,
			MLogLevel.Error => NLogLevel.Error,
			MLogLevel.Critical => NLogLevel.Fatal,
			_ => NLogLevel.Off
		};
}
