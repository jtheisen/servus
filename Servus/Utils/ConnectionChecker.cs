using System.Net.Sockets;

class ConnectionChecker
{
  static HttpClient client = new HttpClient();

  public static IObservable<Boolean> GetTester(Int32 port, Configuring.ConnectionMethod method)
  {
    return Observable.Create<Boolean>(async (o, ct) =>
    {
      while (!ct.IsCancellationRequested)
      {
        try
        {
          o.OnNext(await Test(port, method, ct));

          await Task.Delay(1000, ct);
        }
        catch (Exception)
        {
          o.OnNext(false);

          await Task.Delay(1000, ct);
        }
      }
    });
  }

  static async Task<Boolean> Test(Int32 port, Configuring.ConnectionMethod method, CancellationToken cancellationToken)
  {
    if (method == Configuring.ConnectionMethod.Tcp)
    {
      using var tcpClient = new TcpClient();

      await tcpClient.ConnectAsync("localhost", port, cancellationToken);

      return true;
    }

    using var request = new HttpRequestMessage(GetHttpMethod(method), $"http://localhost:{port}");
    var response = await client.SendAsync(request, cancellationToken);

    return (Int32)response.StatusCode < 500;
  }

  static HttpMethod GetHttpMethod(Configuring.ConnectionMethod method)
  {
    return method switch
    {
      Configuring.ConnectionMethod.Get => HttpMethod.Get,
      Configuring.ConnectionMethod.Head => HttpMethod.Head,
      Configuring.ConnectionMethod.Options => HttpMethod.Options,
      _ => throw new FriendlyException($"The connection check method '{method}' is not an HTTP method.")
    };
  }
}
