using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

class ControllingClient : Client
{
  static Logger logger = LogManager.GetCurrentClassLogger();

  public ControllingClient(TcpClient client, Inout inout)
    : base(client, inout)
  {
  }

  public static ControllingClient Create(String id, Int32 port)
  {
    var tcpClient = new TcpClient();

    tcpClient.Connect(IPAddress.Loopback, port);

    logger.Debug("Connected");

    var inout = tcpClient.GetStream().GetInout();

    var greeting = new Protocol.Greeting(id);

    inout.output.WriteLine(JsonConvert.SerializeObject(greeting));

    logger.Debug("Sent greeting");

    return new ControllingClient(tcpClient, inout);
  }
}
