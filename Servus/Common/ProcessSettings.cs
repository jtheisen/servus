using System.Diagnostics;

record class FactoryProcessSettings(
  IReadOnlyList<String> Cargs,
  String? WorkingDirectory = null,
  String? ProcessRunner = null,
  ProcessWindowStyle? WindowStyle = null,
  Int32? Port = null,
  String? Id = null,
  Boolean RedirectOutput = true,
  Boolean CreateNoWindow = false,
  Boolean NoShellExecute = false,
  Boolean KeepTerminalOpen = false,
  Action<String>? OnOutput = null,
  Action<String>? OnLog = null,
  Action<Int32>? OnExit = null,
  Func<IDisposable>? CreateConsoleBlockedScope = null,
  Action<String>? SendMessageToClient = null
);

record class ConsoleProcessSettings(
  IReadOnlyList<String> Cargs,
  String? WorkingDirectory = null,
  ProcessWindowStyle? WindowStyle = null,
  Boolean RedirectOutput = true,
  Boolean CreateNoWindow = false,
  Boolean NoShellExecute = false,
  Boolean KeepTerminalOpen = false,
  Action<String>? OnOutput = null,
  Action<String>? OnLog = null,
  Action<Int32>? OnExit = null,
  Func<IDisposable>? CreateConsoleBlockedScope = null
);
