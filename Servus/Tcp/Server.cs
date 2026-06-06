using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using Newtonsoft.Json;

class ServerInstance
{
  static Lazy<Server> server = new Lazy<Server>(Create);

  public static Server Server => server.Value;

  static Server Create() => new();
}

class Server
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  public static Server Instance => ServerInstance.Server;

  CancellationToken ct;

  TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

  Dictionary<String, ClientSlot> clientsById = new();

  Subject<AcceptedClient> clients = new();

  public Int32 Port => IPEndPoint.Port;

  IPEndPoint IPEndPoint => listener.Server.LocalEndPoint as IPEndPoint ?? throw new Exception("Don't have a local IP endpoint");

  public Boolean IsClientConnected(String id)
    => clientsById.TryGetValue(id, out var slot) && (slot.Client?.IsConnected ?? false);

  public IObservable<String> ObserveClientOutput(String id)
    => GetOutput(id);

  public Boolean TrySend(String id, String line)
  {
    if (!clientsById.TryGetValue(id, out var slot) || slot.Client is not { } client)
    {
      logger.Warn("Can't send message to client {id}; client is not connected", id);
      return false;
    }

    client.WriteLine(line);
    return true;
  }

  public Server(CancellationToken ct = default)
  {
    this.ct = ct;

    Start();
  }

  public async void Start()
  {
    listener.Start();

    logger.Info("Starting to listen for clients");

    await foreach (var client in AcceptClients())
    {
      try
      {
        HandleClient(client);
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Error greeting client");
      }
    }
  }

  async IAsyncEnumerable<TcpClient> AcceptClients()
  {
    while (!ct.IsCancellationRequested)
    {
      yield return await listener.AcceptTcpClientAsync(ct);
    }
  }

  async void HandleClient(TcpClient tcpClient)
  {
    logger.Info("Accepted client");

    var stream = tcpClient.GetStream();

    var inout = stream.GetInout();

    var (reader, _) = inout;

    var greetingLine = reader.ReadLine();

    Assert(greetingLine is not null);

    var greeting = JsonConvert.DeserializeObject<Protocol.Greeting>(greetingLine);

    Assert(greeting is not null);

    logger.Info("Client reported {id}", greeting.Id);

    var client = new AcceptedClient(tcpClient, inout, greeting);

    var slot = GetSlot(greeting.Id);

    slot.SetClient(client);

    slot.Subscription.Disposable = client.In.Subscribe(
      slot.Output.OnNext,
      ex => logger.Error(ex, "Client output stream failed for {id}", greeting.Id),
      () =>
      {
        logger.Info("Client {id} disconnected", greeting.Id);
        slot.Client = null;
      });

    clients.OnNext(client);
  }

  Subject<String> GetOutput(String id)
    => GetSlot(id).Output;

  ClientSlot GetSlot(String id)
  {
    if (!clientsById.TryGetValue(id, out var slot))
    {
      slot = new ClientSlot();
      clientsById[id] = slot;
    }

    return slot;
  }
}

class ClientSlot
{
  public AcceptedClient? Client { get; set; }
  public Subject<String> Output { get; } = new();
  public SerialDisposable Subscription { get; } = new();

  public void SetClient(AcceptedClient client)
  {
    Client = client;
  }
}
