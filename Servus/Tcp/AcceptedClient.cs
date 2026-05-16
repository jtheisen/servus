using System.Net.Sockets;



class AcceptedClient : Client
{
  public String Id { get; }

  public AcceptedClient(TcpClient client, Inout inout, Protocol.Greeting greeting)
    : base(client, inout)
  {
    Id = greeting.Id;
  }
}