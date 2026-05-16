using System.Text;

static class Extensions
{
  public static String GetString(this MemoryStream ms)
	{
		return Encoding.UTF8.GetString(ms.ToArray());
	}

  public static String GetCargsDebugString(this IReadOnlyList<String> cargs)
    => String.Join("·", cargs);
}
