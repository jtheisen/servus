using System.Net.Sockets;
using NLog;



class Client : IDisposable
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  TcpClient tcpClient;
  Inout inout;

  public Inout Inout => inout;
  public IObserver<String> Out { get; }
  public IObservable<String> In { get; }
  public Boolean IsConnected => tcpClient.Connected;

  public Client(TcpClient client)
    : this(client, client.GetStream().GetInout())
  {
    logger.Debug("Created {type}", GetType().Name);
  }

  public void WriteLine(String line) => inout.output.WriteLine(line);

  public Client(TcpClient client, Inout inout)
  {
    this.tcpClient = client;
    this.inout = inout;

    Out = Observer.Create<String>(WriteLine);
    In = Read().ToObservable(NewThreadScheduler.Default).Replay().RefCount();
  }

  public void Close()
  {
    tcpClient.Close();
  }

  public void Dispose()
  {
    tcpClient.Dispose();
  }

  IEnumerable<String> Read()
  {
    String? line = null;
    while ((line = inout.input.ReadLine()) is not null)
    {
      yield return line;
    }
  }
}
