using System.Text;

static class CommandLineArgs
{
	public static String[] Parse(String commandLine)
	{
		if (OperatingSystem.IsWindows())
		{
			WindowsArgs.Parse(commandLine, out var args);
			return args;
		}

		return PosixArgs.Parse(commandLine);
	}
}

static class PosixArgs
{
	public static String[] Parse(String commandLine)
	{
		var args = new List<String>();
		var current = new StringBuilder();
		var quote = default(Char?);
		var haveToken = false;

		for (var i = 0; i < commandLine.Length; ++i)
		{
			var c = commandLine[i];

			if (quote is null && Char.IsWhiteSpace(c))
			{
				Flush();
				continue;
			}

			haveToken = true;

			if (quote is null && (c == '\'' || c == '"'))
			{
				quote = c;
			}
			else if (quote == c)
			{
				quote = null;
			}
			else if (c == '\\' && quote != '\'')
			{
				if (++i == commandLine.Length)
				{
					current.Append('\\');
				}
				else
				{
					current.Append(commandLine[i]);
				}
			}
			else
			{
				current.Append(c);
			}
		}

		if (quote is not null)
		{
			throw new FriendlyException($"Unterminated {FormatQuote(quote.Value)} quote in command line.");
		}

		Flush();
		return args.ToArray();

		void Flush()
		{
			if (!haveToken)
			{
				return;
			}

			args.Add(current.ToString());
			current.Clear();
			haveToken = false;
		}
	}

	static String FormatQuote(Char quote)
	{
		return quote == '\'' ? "single" : "double";
	}
}
