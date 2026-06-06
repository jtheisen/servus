using System.Reactive.Disposables;

abstract class WrappingProcess : SystemDiagnosticsProcess
{
  CompositeDisposable disposables = new();

  protected WrappingProcess(FactoryProcessSettings settings)
    : base(settings)
  {
    if (settings.Id is String id)
    {
      disposables.Add(Server.Instance.ObserveClientOutput(id).Subscribe(line => settings.OnOutput?.Invoke(line)));
    }
  }

  public override Boolean IsClientConnected
    => settings.Id is String id && Server.Instance.IsClientConnected(id);

  public override Boolean Shutdown()
  {
    if (settings.Id is String id)
    {
      return Server.Instance.TrySend(id, "shutdown");
    }

    return false;
  }

  public override void Kill()
  {
    if (settings.Id is String id)
    {
      Server.Instance.TrySend(id, "kill");
    }
  }

  public override void Dispose()
  {
    disposables.Dispose();
  }
}
