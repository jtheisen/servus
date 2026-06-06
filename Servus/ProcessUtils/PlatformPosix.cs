using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

static class PlatformPosix
{
  public const Int32 SIGINT = 2;

  [DllImport("libc", SetLastError = true)]
  static extern Int32 kill(Int32 pid, Int32 sig);

  public static Boolean SendSignal(Process process, Int32 signal)
  {
    if (process.HasExited)
    {
      return true;
    }

    var result = kill(process.Id, signal);

    if (result != 0)
    {
      throw new Win32Exception(Marshal.GetLastWin32Error(), $"kill({signal}) failed.");
    }

    return true;
  }

  public static Boolean SendSigInt(Process process)
    => SendSignal(process, SIGINT);
}
