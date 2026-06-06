using System.Diagnostics;
using System.Reflection;

namespace PlatformTests;

record ShutdownTestCase(
	String Name,
	String PlatformName,
	FactoryProcessSettings Settings,
	TimeSpan? Delay = null,
	TimeSpan? Timeout = null)
{
	public override String ToString() => $"{PlatformName}: {Name}";
}

record ShutdownTestCaseDefinition(
	String Name,
	String PlatformName,
	Func<String, String, FactoryProcessSettings> CreateSettings)
{
	public override String ToString() => $"{PlatformName}: {Name}";

	public ShutdownTestCase Create()
	{
		var samplePath = TestExecutables.Find("Sample");
		var servusPath = TestExecutables.Find("Servus");

		return new(Name, PlatformName, CreateSettings(samplePath, servusPath));
	}
}

[TestClass]
public class ShutdownTests
{
	public TestContext TestContext { get; set; } = null!;

	public static IEnumerable<Object[]> TestCases
		=> GetCaseDefinitions().Select(c => new Object[] { c.Name });

	[TestMethod]
	[DynamicData(nameof(TestCases), DynamicDataSourceType.Property, DynamicDataDisplayName = nameof(GetDisplayName))]
	public async Task StopRemovesSampleProcessTree(String name)
	{
		var testCase = GetCaseDefinitions().Single(c => c.Name == name).Create();

		if (!String.Equals(testCase.PlatformName, CurrentPlatformName, StringComparison.OrdinalIgnoreCase))
		{
			Assert.Inconclusive($"Test case is for {testCase.PlatformName}, running on {CurrentPlatformName}.");
		}

		AssertNoSamples("before test");

		var process = ProcessFactory.Instance.Start(testCase.Settings);

		try
		{
			await Task.Delay(testCase.Delay ?? TimeSpan.FromSeconds(4));

			Assert.IsFalse(
				process.HasExited,
				$"Factory process exited before stop for case '{testCase}'. Exit code: {process.ExitCode}.");

			var samplesBeforeStop = GetSampleProcessIds();

			Assert.AreNotEqual(
				0,
				samplesBeforeStop.Count,
				$"No Sample process was running before stop for case '{testCase}'.");

			process.Shutdown();

			var stopwatch = Stopwatch.StartNew();

			while (stopwatch.Elapsed < (testCase.Timeout ?? TimeSpan.FromSeconds(5)) && GetSampleProcessIds().Count > 0)
			{
				await Task.Delay(100);
			}

			var leftovers = GetSampleProcessIds();

			Assert.AreEqual(
				0,
				leftovers.Count,
				$"Sample processes still running: {String.Join(", ", leftovers)}");
		}
		finally
		{
			if (!process.HasExited)
			{
				process.Kill();
			}

			process.Dispose();

			AssertNoSamples("after test");
		}
	}

	public static String GetDisplayName(MethodInfo methodInfo, Object[] data)
		=> data is [String name]
			? GetCaseDefinitions().Single(c => c.Name == name).ToString()
			: methodInfo.Name;

	static IEnumerable<ShutdownTestCaseDefinition> GetCaseDefinitions()
	{
		if (OperatingSystem.IsWindows())
		{
			yield return Case("wrapping graceful nested", (sample, servus) =>
				new FactoryProcessSettings(
					[sample, "graceful", "1"],
					ProcessRunner: "windowed-wrapping",
					ServusPath: servus,
					Port: Server.Instance.Port,
					Id: Guid.NewGuid().ToString(),
					CreateNoWindow: true,
					NoShellExecute: true));

			yield return Case("windowed-wrapping stubborn nested", (sample, servus) =>
				new FactoryProcessSettings(
					[sample, "stubborn", "1"],
					ProcessRunner: "windowed-wrapping",
					ServusPath: servus,
					Port: Server.Instance.Port,
					Id: Guid.NewGuid().ToString(),
					CreateNoWindow: true,
					NoShellExecute: true));
		}
		else if (OperatingSystem.IsLinux())
		{
			yield return Case("wrapping graceful nested", (sample, servus) =>
				new FactoryProcessSettings(
					[sample, "graceful", "1"],
					ProcessRunner: "windowed-wrapping",
					ServusPath: servus,
					Port: Server.Instance.Port,
					Id: Guid.NewGuid().ToString(),
					CreateNoWindow: true,
					NoShellExecute: true));
		}
	}

	static ShutdownTestCaseDefinition Case(
		String name,
		Func<String, String, FactoryProcessSettings> createSettings)
		=> new(name, CurrentPlatformName, createSettings);

	IReadOnlyList<Int32> GetSampleProcessIds()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "pwsh",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		startInfo.ArgumentList.Add("-NoProfile");
		startInfo.ArgumentList.Add("-Command");
		startInfo.ArgumentList.Add(
			"Get-Process Sample -ErrorAction SilentlyContinue | ForEach-Object { $_.Id }; exit 0");

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Could not start pwsh for process discovery.");

		var output = process.StandardOutput.ReadToEnd();
		_ = process.StandardError.ReadToEnd();
		process.WaitForExit();

		return output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(line => Int32.TryParse(line, out _))
			.Select(Int32.Parse)
			.ToList();
	}

	void AssertNoSamples(String phase)
	{
		var samples = GetSampleProcessIds();

		if (samples.Count == 0)
		{
			TestContext.WriteLine($"No Sample processes found {phase}.");
			return;
		}

		TestContext.WriteLine(
			$"Found Sample processes {phase}: {String.Join(", ", samples)}");

		KillSamples(samples);

		var leftovers = GetSampleProcessIds();

		Assert.AreEqual(
			0,
			leftovers.Count,
			$"Sample processes still running {phase} after cleanup: {String.Join(", ", leftovers)}");
	}

	void KillSamples(IReadOnlyList<Int32> samples)
	{
		foreach (var processId in samples)
		{
			try
			{
				TestContext.WriteLine($"Cleanup: Killing Sample process {processId}.");
				using var process = Process.GetProcessById(processId);
				process.Kill(true);
				process.WaitForExit(2000);
			}
			catch (Exception ex)
			{
				TestContext.WriteLine($"Cleanup: Failed to kill Sample process {processId}: {ex}");
			}
		}
	}

	static String CurrentPlatformName
	{
		get
		{
			if (OperatingSystem.IsWindows())
			{
				return "Windows";
			}

			if (OperatingSystem.IsLinux())
			{
				return "Linux";
			}

			if (OperatingSystem.IsMacOS())
			{
				return "macOS";
			}

			return "Unknown";
		}
	}
}

static class TestExecutables
{
	public static String Find(String name)
	{
		var executableName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
		var candidates = Directory
			.EnumerateFiles(AppContext.BaseDirectory, executableName, SearchOption.TopDirectoryOnly)
			.ToList();

		return candidates.Count switch
		{
			1 => candidates[0],
			0 => throw new InvalidOperationException(
				$"Could not find executable '{executableName}' under '{AppContext.BaseDirectory}'."),
			_ => throw new InvalidOperationException(
				$"Found more than one executable '{executableName}': {String.Join(", ", candidates)}")
		};
	}
}
