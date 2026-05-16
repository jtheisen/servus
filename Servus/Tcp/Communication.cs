readonly record struct Inout(TextReader input, TextWriter output)
{
  public static Inout Console => new(System.Console.In, System.Console.Out);
}

static class TcpExtensions
{
  public static Inout GetInout(this Stream stream)
    => new(new StreamReader(stream), new StreamWriter(stream) { AutoFlush = true });
}
