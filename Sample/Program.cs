using System.Diagnostics;
using System.Runtime.InteropServices;

if (args is not [var mode, var nestingText])
{
	Console.Error.WriteLine("Usage: Sample <mode> <nesting>");
	return 1;
}

if (!Int32.TryParse(nestingText, out var nesting) || nesting < 0)
{
	Console.Error.WriteLine("The nesting argument must be a non-negative number.");
	return 1;
}

Console.WriteLine($"Sample process {Environment.ProcessId}: mode={mode}, nesting={nesting}");

if (mode is not ("graceful" or "stubborn"))
{
	throw new Exception($"Unknown mode '{mode}'. Use 'graceful' or 'stubborn'.");
}

if (nesting > 0)
{
	var parentRegistrations = InstallStubbornHandlers(nesting);

	var ownPath = Environment.ProcessPath ?? throw new Exception("Can't see what executable we're running.");
	var startInfo = new ProcessStartInfo
	{
		FileName = ownPath,
		UseShellExecute = false,
		RedirectStandardInput = false,
		RedirectStandardOutput = false,
		RedirectStandardError = false,
	};

	startInfo.ArgumentList.Add(mode);
	startInfo.ArgumentList.Add((nesting - 1).ToString());

	var child = Process.Start(startInfo)
		?? throw new Exception("Could not start child process.");

	child.WaitForExit();

	foreach (var registration in parentRegistrations)
	{
		registration.Dispose();
	}

	return child.ExitCode;
}

var registrations = mode == "stubborn" ? InstallStubbornHandlers(nesting) : [];

await Task.Delay(Timeout.InfiniteTimeSpan);

foreach (var registration in registrations)
{
	registration.Dispose();
}

return 0;

static IDisposable[] InstallStubbornHandlers(Int32 nesting)
{
	Console.CancelKeyPress += (_, e) =>
	{
		Console.WriteLine($"Sample process {Environment.ProcessId}, nesting={nesting}: ignoring {e.SpecialKey}");
		e.Cancel = true;
	};

	return OperatingSystem.IsWindows()
		? []
		:
		[
			Register(PosixSignal.SIGINT),
			Register(PosixSignal.SIGTERM),
			Register(PosixSignal.SIGQUIT),
		];

	PosixSignalRegistration Register(PosixSignal signal)
		=> PosixSignalRegistration.Create(signal, context =>
		{
			Console.WriteLine($"Sample process {Environment.ProcessId}, nesting={nesting}: ignoring {context.Signal}");
			context.Cancel = true;
		});
}
