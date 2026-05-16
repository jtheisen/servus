using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

delegate void ClientListener(String id, AcceptedClient client);

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

  ClientListener? clientListener;

  CancellationToken ct;

  TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

  Dictionary<String, Client> clientsById = new();

  Subject<AcceptedClient> clients = new();

  public Int32 Port => IPEndPoint.Port;

  IPEndPoint IPEndPoint => listener.Server.LocalEndPoint as IPEndPoint ?? throw new Exception("Don't have a local IP endpoint");

  IObservable<AcceptedClient> Clients => clients;

  public void InstallClientListener(ClientListener clientListener)
  {
    this.clientListener = clientListener;
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

    clientsById[greeting.Id] = client;

    clients.OnNext(client);

    clientListener?.Invoke(greeting.Id, client);
  }
}