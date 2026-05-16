class GitBranchTester
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  public static Boolean HasGitMetadata(String workingDirectory)
  {
    var gitPath = GetGitPath(workingDirectory);

    return Directory.Exists(gitPath) || File.Exists(gitPath);
  }

  public static IObservable<String?> GetTester(String workingDirectory, String taskName)
  {
    return Observable.Create<String?>(async (o, ct) =>
    {
      var loggedError = false;

      while (!ct.IsCancellationRequested)
      {
        try
        {
          o.OnNext(ReadBranch(workingDirectory));
        }
        catch (Exception ex)
        {
          o.OnNext(null);

          if (!loggedError)
          {
            logger.Error(ex, "Could not read git branch for task '{task}' in '{workingDirectory}'", taskName, workingDirectory);
            loggedError = true;
          }
        }

        await Task.Delay(1000, ct);
      }
    });
  }

  static String? ReadBranch(String workingDirectory)
  {
    var gitDirectory = GetGitDirectory(workingDirectory);

    if (gitDirectory is null)
    {
      return null;
    }

    var headPath = Path.Combine(gitDirectory, "HEAD");
    var head = File.ReadAllText(headPath).Trim();

    const String branchPrefix = "ref: refs/heads/";

    if (head.StartsWith(branchPrefix, StringComparison.Ordinal))
    {
      return head[branchPrefix.Length..];
    }

    if (head.Length > 0)
    {
      return head.Length > 7 ? head[..7] : head;
    }

    throw new Exception($"Git HEAD file is empty at '{headPath}'.");
  }

  static String? GetGitDirectory(String workingDirectory)
  {
    var gitPath = GetGitPath(workingDirectory);

    if (Directory.Exists(gitPath))
    {
      return gitPath;
    }

    if (!File.Exists(gitPath))
    {
      return null;
    }

    var gitDirLine = File.ReadAllText(gitPath).Trim();
    const String gitDirPrefix = "gitdir:";

    if (!gitDirLine.StartsWith(gitDirPrefix, StringComparison.OrdinalIgnoreCase))
    {
      throw new Exception($"Git file at '{gitPath}' does not contain a gitdir entry.");
    }

    var gitDirectory = gitDirLine[gitDirPrefix.Length..].Trim();

    if (Path.IsPathRooted(gitDirectory))
    {
      return gitDirectory;
    }

    return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(gitPath) ?? "", gitDirectory));
  }

  static String GetGitPath(String workingDirectory)
    => Path.Combine(Path.GetFullPath(workingDirectory), ".git");
}
