using System.ComponentModel;
using System.Runtime.InteropServices;

static class WindowsArgs
{
  [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
  static extern IntPtr CommandLineToArgvW(
    [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
    out int pNumArgs);

  [DllImport("kernel32.dll", SetLastError = false)]
  static extern IntPtr LocalFree(IntPtr hMem);

  public static void Parse(String cmdLine, out String[] args)
  {
    IntPtr ptr = CommandLineToArgvW(cmdLine, out var numArgs);
    if (ptr == IntPtr.Zero)
    {
      throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not parse command line with CommandLineToArgvW");
    }

    try
    {
      args = new String[numArgs];
      IntPtr current = ptr;
      for (int i = 0; i < numArgs; ++i)
      {
        IntPtr pStr = Marshal.ReadIntPtr(current, i * IntPtr.Size);
        args[i] = Marshal.PtrToStringUni(pStr) ?? "";
      }
    }
    finally
    {
      LocalFree(ptr);
    }
  }
}