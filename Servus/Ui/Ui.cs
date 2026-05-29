using Spectre.Console;
using Spectre.Console.Rendering;

enum GlobalLifecycleState
{
  None,
  Running,
  StoppingSuggested,
  Stopping,
  Stopped
}

record KeyBinding(
  ConsoleKey[] Keys,
  String Description,
  Action<Ui> Execute,
  Boolean ResetsExitRequest = true,
  Char[]? Characters = null)
{
  public Boolean Matches(ConsoleKeyInfo key)
  {
    return Keys.Contains(key.Key) || (Characters?.Contains(key.KeyChar) ?? false);
  }

  public String KeyText => String.Join("/", Keys.Select(FormatKey).Concat((Characters ?? []).Select(FormatCharacter)));

  static String FormatKey(ConsoleKey key)
  {
    return key switch
    {
      ConsoleKey.UpArrow => "Up",
      ConsoleKey.DownArrow => "Down",
      ConsoleKey.Escape => "Esc",
      _ => key.ToString()
    };
  }

  static String FormatCharacter(Char character)
  {
    return character.ToString();
  }
}

class Ui(AppState store)
{
  static Logger logger = LogManager.GetCurrentClassLogger();
  const Int32 HeaderHeight = 3;
  const Int32 FooterHeight = 3;
  const Int32 LoggingWidgetHeight = 7;
  const Int32 TableChromeHeight = 4;

  static readonly IReadOnlyList<KeyBinding> keyBindings =
  [
    new([ConsoleKey.UpArrow], "Previous task", ctx =>
    {
      ctx.selectedIndex = ctx.selectedIndex <= 0 ? ctx.Tasklets.Count - 1 : ctx.selectedIndex - 1;
    }),
    new([ConsoleKey.DownArrow], "Next task", ctx =>
    {
      ctx.selectedIndex = ctx.selectedIndex >= ctx.Tasklets.Count - 1 ? 0 : ctx.selectedIndex + 1;
    }),
    new([ConsoleKey.PageUp], "Previous page", ctx =>
    {
      ctx.selectedIndex = Math.Max(0, ctx.selectedIndex - ctx.VisibleRowCount);
    }),
    new([ConsoleKey.PageDown], "Next page", ctx =>
    {
      ctx.selectedIndex = Math.Min(ctx.Tasklets.Count - 1, ctx.selectedIndex + ctx.VisibleRowCount);
    }),
    new([ConsoleKey.S, ConsoleKey.Enter], "Start/Stop", ctx =>
    {
      ctx.SelectedTask.Toggle();
    }),
    new([ConsoleKey.W], "Start/Stop in Window", ctx =>
    {
      ctx.SelectedTask.Toggle(true);
    }),
    new([ConsoleKey.K, ConsoleKey.X], "Custom shortcut", ctx =>
    {
      ctx.haveCustomKeyCombo = true;
      ctx.statusBarText = "Press custom command key";
    }),
    new([ConsoleKey.B], "Browser command", ctx =>
    {
      if (ctx.Store.Settings.Port is null)
      {
        ctx.statusBarText = "No http is being served, configure port and restart";
      }
      else
      {
        ctx.haveBrowserKeyCombo = true;
        ctx.statusBarText = "Press browser command key";
      }
    }),
    new([ConsoleKey.L], "Toggle log", ctx =>
    {
      ctx.showLoggingWidget = !ctx.showLoggingWidget;
    }, Characters: ['\'']),
    new([ConsoleKey.Escape], "Exit Servus", ctx =>
    {
      ctx.HandleExit();
    }, false),
  ];

  GlobalLifecycleState state = GlobalLifecycleState.None;
  String statusBarText = "";
  Int32 selectedIndex;
  Int32 scrollOffset;
  Boolean haveCustomKeyCombo;
  Boolean haveBrowserKeyCombo;
  Boolean showLoggingWidget;

  IReadOnlyList<Tasklet> Tasklets => store.Tasklets;
  AppState Store => store;
  Tasklet SelectedTask => Tasklets[selectedIndex];
  Int32 VisibleRowCount => GetVisibleRowCount(showLoggingWidget);

  public void Run()
  {
    var tasklets = Tasklets;

    var root = BuildLayout(tasklets, selectedIndex, showLoggingWidget, statusBarText);

    AnsiConsole.Live(root)
      .AutoClear(false)
      .Start(ctx =>
      {
        state = GlobalLifecycleState.Running;

        while (state < GlobalLifecycleState.Stopped)
        {
          var key = default(ConsoleKeyInfo);
          var sw = System.Diagnostics.Stopwatch.StartNew();

          Thread.Sleep(50);

          foreach (var task in tasklets)
          {
            task.Tick();
          }

          if (Console.KeyAvailable)
          {
            //key = Console.ReadKey(intercept: true);

            key = AnsiConsole.Console.Input.ReadKey(intercept: true) ?? default;

            var task = tasklets[selectedIndex];

            if (haveCustomKeyCombo)
            {
              haveCustomKeyCombo = false;

              var match = task.Configuration.Shortcuts.FirstOrDefault(s => key.KeyChar.Equals(s.Key));

              if (match is not null)
              {
                var cargs = match.GetCargs(task.Configuration);

                try
                {
                  ConsoleProcessRunner.RunProcess(cargs);

                  statusBarText = $"Ran shortcut: {match.Name}";
                }
                catch (Exception)
                {
                  statusBarText = $"Failed to run: {cargs.GetCargsDebugString()}";
                }
              }
              else
              {
                statusBarText = $"Unkown custom shortcut '{key.KeyChar}'";
              }
            }
            else if (haveBrowserKeyCombo)
            {
              haveBrowserKeyCombo = false;

              if (key.KeyChar == 'l')
              {
                var url = $"http://127.0.0.1:{store.Settings.Port}/tasks/{Uri.EscapeDataString(task.Name)}/logs";

                try
                {
                  ConsoleProcessRunner.RunProcess([url]);

                  statusBarText = $"Opened logs: {task.Name}";
                }
                catch (Exception)
                {
                  statusBarText = $"Failed to open logs: {url}";
                }
              }
              else
              {
                statusBarText = $"Unkown browser command '{key.KeyChar}'";
              }
            }
            else
            {
              var binding = keyBindings.FirstOrDefault(b => b.Matches(key));

              var resetExitRequest = true;

              statusBarText = "";                

              if (binding is not null)
              {
                binding.Execute(this);

                resetExitRequest = binding.ResetsExitRequest;
              }
              else if (key.KeyChar != 0 && !char.IsControl(key.KeyChar))
              {
                statusBarText = $"Unhandled key: {key.KeyChar}";
              }
              else
              {
                statusBarText = $"Unhandled key: {key.Key}";
              }

              if (resetExitRequest)
              {
                // Only if we press exit twice in a row
                // we actually force an exit.
                state = GlobalLifecycleState.Running;
              }
            }
          }

          CheckPendingStop();

          EnsureSelectedTaskVisible(tasklets.Count);
          UpdateLayout(root, tasklets, selectedIndex, scrollOffset, VisibleRowCount, showLoggingWidget, statusBarText);
          ctx.Refresh();
        }
      });
  }

  void EnsureSelectedTaskVisible(Int32 itemCount)
  {
    if (itemCount == 0)
    {
      selectedIndex = 0;
      scrollOffset = 0;
      return;
    }

    selectedIndex = Math.Clamp(selectedIndex, 0, itemCount - 1);

    var visibleRows = VisibleRowCount;

    if (selectedIndex < scrollOffset)
    {
      scrollOffset = selectedIndex;
    }
    else if (selectedIndex >= scrollOffset + visibleRows)
    {
      scrollOffset = selectedIndex - visibleRows + 1;
    }

    scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, itemCount - visibleRows));
  }

  void CheckPendingStop()
  {
    if (state == GlobalLifecycleState.Stopping)
    {
      logger.Debug("Checking pending stop");

      var running = Tasklets.FirstOrDefault(t => t.State != State.Stopped);

      if (running is null)
      {
        logger.Info("All processes stopped, we're leaving");

        statusBarText = "All processes stopped, exiting...";

        state = GlobalLifecycleState.Stopped;
      }
      else
      {
        logger.Debug("Process '{process}' is still running, won't terminate just yet", running.Name);
      }
    }
  }

  void HandleExit()
  {
    switch (state)
    {
      case GlobalLifecycleState.Running:
        state = GlobalLifecycleState.StoppingSuggested;
        statusBarText = "Press again to stop processes and exit";
        break;
      case GlobalLifecycleState.StoppingSuggested:
        logger.Info("Graceful exit requested by user");
        state = GlobalLifecycleState.Stopping;
        statusBarText = "Stopping processes, repeat one more time to forcefully exit";
        foreach (var tasklet in Tasklets)
        {
          tasklet.Stop();
        }
        break;
      case GlobalLifecycleState.Stopping:
        logger.Info("Forceful exit requested by user");
        statusBarText = "Killing processes and terminating...";
        foreach (var tasklet in Tasklets)
        {
          tasklet.Kill();
        }
        state = GlobalLifecycleState.Stopped;
        break;
    }

  }

  private static Layout BuildLayout(
    IReadOnlyList<Tasklet> items,
    Int32 selectedIndex,
    Boolean showLoggingWidget,
    String lastAction)
  {
    var root = new Layout("Root")
      .SplitRows(
        new Layout("Header").Size(3),
        new Layout("Body"),
        new Layout("Logging").Size(LoggingWidgetHeight),
        new Layout("Footer").Size(3));

    root["Body"].SplitColumns(
      new Layout("TableArea").Ratio(2),
      new Layout("SideBar").Size(GetSideBarWidth() + 1));

    UpdateLayout(root, items, selectedIndex, 0, GetVisibleRowCount(showLoggingWidget), showLoggingWidget, lastAction);
    return root;
  }

  static Int32 GetVisibleRowCount(Boolean showLoggingWidget)
  {
    var loggingHeight = showLoggingWidget ? LoggingWidgetHeight : 0;

    return Math.Max(1, Console.WindowHeight - HeaderHeight - FooterHeight - loggingHeight - TableChromeHeight);
  }

  static Int32 GetSideBarWidth()
  {
    var keyColumnWidth = keyBindings
      .Select(b => b.KeyText.Length)
      .DefaultIfEmpty(0)
      .Max();

    var descriptionColumnWidth = keyBindings
      .Select(b => b.Description.Length)
      .DefaultIfEmpty(0)
      .Max();

    var keyBindingWidth = keyColumnWidth + 2 + descriptionColumnWidth + 6;

    return Math.Max(28, keyBindingWidth);
  }

  private static void UpdateLayout(
    Layout root,
    IReadOnlyList<Tasklet> items,
    Int32 selectedIndex,
    Int32 scrollOffset,
    Int32 visibleRows,
    Boolean showLoggingWidget,
    String lastAction)
  {
    var selected = items.Count > 0 ? items[selectedIndex] : null;

    root["Header"].Update(
      new Panel(
        new Markup("[bold yellow]Servus[/]"))
      .Border(BoxBorder.Rounded)
      .Expand());

    root["TableArea"].Update(
      BuildTable(items, selectedIndex, scrollOffset, visibleRows));

    root["SideBar"].Update(
      new Padder(BuildHelpWidget(keyBindings), new Padding(1, 0, 0, 0)));

    root["Logging"].IsVisible = showLoggingWidget;
    root["Logging"].Update(BuildLoggingWidget(selected));

    root["Footer"].Update(
      new Panel(BuildFooterContent(lastAction))
      .Header("Status")
      .Border(BoxBorder.Rounded)
      .Expand());
  }

  private static Table BuildTable(
    IReadOnlyList<Tasklet> items,
    Int32 selectedIndex,
    Int32 scrollOffset,
    Int32 visibleRows)
  {
    var table = new Table()
      .Border(TableBorder.Rounded)
      .Expand();

    table.AddColumn("[grey]Name[/]");
    table.AddColumn("[grey]Branch[/]");
    table.AddColumn("[grey]Status[/]", c => c.Width(12));
    table.AddColumn("[grey]Port[/]");

    var lastIndex = Math.Min(items.Count, scrollOffset + visibleRows);

    for (var i = scrollOffset; i < lastIndex; i++)
    {
      var item = items[i];
      var selected = i == selectedIndex;

      String Cell(String value)
        => selected
          ? $"[black on deepskyblue1]{Markup.Escape(value)}[/]"
          : Markup.Escape(value);

      String PortCell(String value)
        => item.IsPortConnectable ? Markup.Escape(value) : $"[grey]{Markup.Escape(value)}[/]";

      table.AddRow(
        Cell(item.Name),
        Markup.Escape(item.GitBranch ?? ""),
        item.UiState,
        PortCell(item.Port?.ToString() ?? ""));
    }

    return table;
  }

  private static IRenderable BuildLoggingWidget(Tasklet? selected)
  {
    var text = String.Join("\n", (selected?.Output ?? []).TakeLast(LoggingWidgetHeight - 2));

    return new Panel(
        new Markup($"[bold aqua]{Markup.Escape(text)}[/]"))
      .Header("Log")
      .Border(BoxBorder.Rounded)
      .Expand();
  }

  private static IRenderable BuildHelpWidget(IReadOnlyList<KeyBinding> bindings)
  {
    var keyColumnWidth = bindings
      .Select(b => b.KeyText.Length)
      .DefaultIfEmpty(0)
      .Max();

    var lines = bindings
      .Select(b =>
        $"[grey]{Markup.Escape(b.KeyText.PadRight(keyColumnWidth))}[/]  {Markup.Escape(b.Description)}");

    return new Panel(new Markup("\n" + String.Join("\n", lines)))
      .Header("Keys")
      .Border(BoxBorder.Rounded)
      .Expand();
  }

  private static IRenderable BuildFooterContent(string lastAction)
  {
    var layout = new Layout("FooterContent")
      .SplitColumns(
          new Layout("Left").Ratio(5),
          new Layout("Right").Size(8));

    layout["Left"].Update(new Markup($"[grey]{Markup.Escape(lastAction)}[/]"));
    layout["Right"].Update(Align.Right(new Markup($"[grey]{DateTime.Now:HH:mm:ss}[/]")));

    return layout;
  }

}
