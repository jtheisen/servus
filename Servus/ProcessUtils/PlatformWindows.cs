using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

static class PlatformWindows
{
  const uint CTRL_C_EVENT = 0;
  const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern Boolean FreeConsole();

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern Boolean AttachConsole(uint dwProcessId);

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern Boolean GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern Boolean SetConsoleCtrlHandler(ConsoleCtrlDelegate? handler, Boolean add);

  [DllImport("kernel32.dll")]
  static extern IntPtr GetStdHandle(Int32 nStdHandle);

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern uint GetConsoleProcessList(uint[] lpdwProcessList, uint nLength);

  [DllImport("kernel32.dll")]
  static extern Boolean GetConsoleMode(IntPtr hConsoleHandle, out Int32 lpMode);

  delegate Boolean ConsoleCtrlDelegate(uint ctrlType);

  public static Boolean SendBreak(Process process, Int32 millis = 400)
  {
    if (process.HasExited)
    {
      return true;
    }

    var attached = false;
    var ignoreCtrl = false;

    try
    {
      Boolean HandleConsoleCtrl(uint ctrlType)
      {
        return true;
      }

      FreeConsole();

      // Important: We can't use Console.WriteLine after this point,
      // this will lead to the process terminating immediatly.

      if (!AttachConsole((uint)process.Id))
        throw new Win32Exception(Marshal.GetLastWin32Error(), "AttachConsole failed.");

      attached = true;

      if (!SetConsoleCtrlHandler(HandleConsoleCtrl, true))
        throw new Win32Exception(Marshal.GetLastWin32Error(), "SetConsoleCtrlHandler(enable ignore) failed.");

      ignoreCtrl = true;

      if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
        throw new Win32Exception(Marshal.GetLastWin32Error(), "GenerateConsoleCtrlEvent failed.");

      // There's a danger that we get killed by our own
      // signal if we re-attach the parent console too soon.
      Thread.Sleep(millis);
      return true;
    }
    finally
    {
      if (attached)
      {
        FreeConsole();
        AttachConsole(ATTACH_PARENT_PROCESS);
      }

      if (ignoreCtrl)
      {
        SetConsoleCtrlHandler(null, false);
      }
    }
  }

  public static void LogProcessesOnThisConsole(Action<String> writeLine)
  {
    var processesOnConsole = new uint[8];
    var count = GetConsoleProcessList(processesOnConsole, (uint)processesOnConsole.Length);
    writeLine($"Processes on this console ({count}):");
    for (var i = 0; i < count; i++)
      writeLine($"  PID {processesOnConsole[i]}");

    var stdin = GetStdHandle(-10);
    GetConsoleMode(stdin, out var mode);
    writeLine($"Console mode after AttachConsole: 0x{mode:X8}");
  }
}
