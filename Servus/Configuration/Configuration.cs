using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Configuring;

class Configuration
{
	const String SampleResourceName = "servus.sample.yaml";

	public Settings Settings { get; set; } = new();
	public List<Task> Tasks { get; set; } = new();
	public List<Task> Profiles { get; set; } = new();

	public static Configuration Load(String filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FriendlyException(
				$"Configuration file '{filePath}' was not found. Run 'servus init' to create a sample.");
		}

		var yaml = File.ReadAllText(filePath);
		var deserializer = new DeserializerBuilder()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.Build();

		try
		{
			return deserializer.Deserialize<Configuration>(yaml) ?? new Configuration();
		}
		catch (YamlException ex)
		{
			throw new FriendlyException(
				$"Could not parse {Path.GetFileName(filePath)}:{Environment.NewLine}{ex.Message}",
				ex);
		}
	}

	public static void WriteSample(String filePath)
	{
		if (File.Exists(filePath))
		{
			throw new FriendlyException($"Configuration file '{filePath}' already exists.");
		}

		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SampleResourceName)
			?? throw new FriendlyException($"Embedded sample configuration '{SampleResourceName}' was not found.");
		using var reader = new StreamReader(stream);

		File.WriteAllText(filePath, reader.ReadToEnd());
	}
}

class Settings
{
	public Int32? Port { get; set; }
	public Char? StartAllKey { get; set; }
}

class WithCommand
{
	public String? Cmd { get; set; }
	public String? Exe { get; set; }
	public String[]? Args { get; set; }

	public IReadOnlyList<String> GetCargs()
		=> GetCargs(null);

	public IReadOnlyList<String> GetCargs(Task? interpolationContext)
	{
		if (!String.IsNullOrWhiteSpace(Cmd))
		{
			var parts = CommandLineArgs.Parse(InterpolateCommandLine(Cmd, interpolationContext));

			if (parts.Length == 0)
			{
				throw new FriendlyException($"The {nameof(Cmd)} property must include a command.");
			}

			return parts;
		}
		else if (!String.IsNullOrEmpty(Exe))
		{
			return [
				InterpolateArgument(Exe, interpolationContext),
					.. (Args ?? []).Select(a => InterpolateArgument(a, interpolationContext))
			];
		}
		else
		{
			throw new FriendlyException($"Either the {nameof(Cmd)} or the {nameof(Exe)} property must be set.");
		}
	}

	static String InterpolateCommandLine(String value, Task? task)
	{
		return Interpolate(value, task, true);
	}

	static String InterpolateArgument(String value, Task? task)
	{
		return Interpolate(value, task, false);
	}

	static String Interpolate(String value, Task? task, Boolean quoteValues)
	{
		if (task is null)
		{
			return value;
		}

		return TaskInterpolation.Interpolate(value, task, quoteValues);
	}
}

static class TaskInterpolation
{
	static readonly Regex tokenPattern = new(@"\{(?<name>[A-Za-z][A-Za-z0-9]*)\}");

	public static String Interpolate(String template, Task task, Boolean quoteValues)
	{
		return tokenPattern.Replace(template, match =>
		{
			var name = match.Groups["name"].Value;
			var property = typeof(Task)
				.GetProperties()
				.FirstOrDefault(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

			if (property is null)
			{
				throw new FriendlyException($"Unknown task field '{name}' in shortcut command.");
			}

			var value = property.GetValue(task);

			var text = value switch
			{
				null => "",
				String textValue => textValue,
				Char character => character.ToString(),
				Int32 number => number.ToString(CultureInfo.InvariantCulture),
				String[] values => String.Join(" ", values),
				IEnumerable<String> values => String.Join(" ", values),
				_ => throw new FriendlyException(
					$"Task field '{name}' cannot be used in shortcut command interpolation.")
			};

			return quoteValues ? QuoteCommandLineArgument(text) : text;
		});
	}

	static String QuoteCommandLineArgument(String value)
	{
		if (value == "")
		{
			return "\"\"";
		}

		if (!value.Any(Char.IsWhiteSpace) && !value.Contains('"'))
		{
			return value;
		}

		return $"\"{value.Replace("\"", "\\\"")}\"";
	}
}

enum ConnectionMethod
{
	Tcp,
	Get,
	Head,
	Options
}

static class ConnectionMethods
{
	public static ConnectionMethod Parse(String value)
	{
		return Enum.TryParse<ConnectionMethod>(value, true, out var method)
			? method
			: throw new FriendlyException($"The {nameof(Task.Method)} property must be one of: tcp, GET, HEAD, OPTIONS.");
	}
}

class Task : WithCommand, IValidatableObject
{
	[Required]
	public String? Name { get; set; }
	public String? Wd { get; set; }
	public String? Type { get; set; }
	public String? ProcessRunner { get; set; }
	public String? Sln { get; set; }
	public String? Proj { get; set; }
	public Int32? Port { get; set; }
	public String? Method { get; set; }
	public List<String> Profiles { get; set; } = new();
	public List<Task> Shortcuts { get; set; } = new();
	public Char? Key { get; set; }

	public Task Combine(Task defaults)
	{
		return new Task
		{
			Name = Name ?? defaults.Name,
			Wd = Wd ?? defaults.Wd,
			Type = Type ?? defaults.Type,
			ProcessRunner = ProcessRunner ?? defaults.ProcessRunner,
			Sln = Sln ?? defaults.Sln,
			Proj = Proj ?? defaults.Proj,
			Port = Port ?? defaults.Port,
			Method = Method ?? defaults.Method,
			Cmd = Cmd ?? defaults.Cmd,
			Exe = Exe ?? defaults.Exe,
			Args = Args ?? defaults.Args,
			Profiles = Profiles.Count > 0 ? Profiles : defaults.Profiles,
			Shortcuts = Shortcuts.Count > 0 ? Shortcuts : defaults.Shortcuts,
			Key = Key ?? defaults.Key,
		};
	}

	public Task WithDefaults(IReadOnlyDictionary<String, Task> profiles)
	{
		var defaults = new Task();

		foreach (var profileName in Profiles)
		{
			if (!profiles.TryGetValue(profileName, out var profile))
			{
				throw new FriendlyException($"Unknown profile '{profileName}' for task '{Name ?? "<unnamed>"}'.");
			}

			defaults = profile.Combine(defaults);
		}

		return Combine(defaults);
	}

	public ConnectionMethod GetConnectionMethod()
	{
		return Method is null ? ConnectionMethod.Tcp : ConnectionMethods.Parse(Method);
	}

	public Task Validate()
	{
		var results = new List<ValidationResult>();
		var context = new ValidationContext(this);

		if (Validator.TryValidateObject(this, context, results, true))
		{
			return this;
		}

		var messages = results
			.Select(r => FormatValidationResult(r));

		throw new FriendlyException(
			$"Invalid task '{Name ?? "<unnamed>"}':{Environment.NewLine}{String.Join(Environment.NewLine, messages)}");
	}

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (String.IsNullOrWhiteSpace(Cmd) && String.IsNullOrWhiteSpace(Exe))
		{
			yield return new ValidationResult(
				$"Either the {nameof(Cmd)} or the {nameof(Exe)} property must be set.",
				[nameof(Cmd), nameof(Exe)]);
		}

		var methodValidationError = default(String);

		try
		{
			if (Method is not null)
			{
				ConnectionMethods.Parse(Method);
			}
		}
		catch (FriendlyException ex)
		{
			methodValidationError = ex.Message;
		}

		if (methodValidationError is not null)
		{
			yield return new ValidationResult(
				methodValidationError,
				[nameof(Method)]);
		}
	}

	static String FormatValidationResult(ValidationResult result)
	{
		var members = String.Join(", ", result.MemberNames);

		if (String.IsNullOrWhiteSpace(members))
		{
			return result.ErrorMessage ?? "Validation failed.";
		}

		return $"{members}: {result.ErrorMessage}";
	}
}
