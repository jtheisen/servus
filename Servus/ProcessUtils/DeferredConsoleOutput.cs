using System.Reactive.Disposables;

class DeferredConsoleOutput
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  List<String> pending = new();
  Boolean canWrite = true;
  Boolean haveBrokenConnection;

  public IDisposable CreateBlockedScope()
  {
    Assert(canWrite);

    canWrite = false;

    return Disposable.Create(Reenable);
  }

  void Reenable()
  {
    Assert(!canWrite);
    canWrite = true;
    Flush();
  }

  void Flush()
  {
    foreach (var line in pending)
    {
      WriteLineCore(line);
    }

    pending.Clear();
  }

  public void WriteLine(String line)
  {
    if (canWrite)
    {
      WriteLineCore(line);
    }
    else
    {
      pending.Add(line);
    }
  }

  void WriteLineCore(String line)
  {
    if (haveBrokenConnection)
    {
      return;
    }

    try
    {
      Console.WriteLine(line);
    }
    catch (Exception ex)
    {
      logger.Error(ex, "Can't write to parent");

      haveBrokenConnection = true;
    }
  }
}
