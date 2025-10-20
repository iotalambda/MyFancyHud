using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyFancyHud;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // First argument (if not a flag) should be the data folder path
        string? dataFolderPath = null;
        if (args.Length > 0 && !args[0].StartsWith("--"))
        {
            dataFolderPath = args[0];
        }

        // Validate and set data folder path
        if (string.IsNullOrWhiteSpace(dataFolderPath))
        {
            var errorMsg = "ERROR: Data folder path is required as the first argument.\n\n" +
                          "Usage: MyFancyHud.exe <data-folder-path> [options]\n" +
                          "Example: MyFancyHud.exe \"C:\\dev\\mfh\\\"\n\n" +
                          $"Received {args.Length} arguments: {string.Join(", ", args)}";
            Console.Error.WriteLine(errorMsg);
            MessageBox.Show(errorMsg, "MyFancyHud - Missing Data Folder Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
            return;
        }

        // Ensure the path ends with a backslash
        if (!dataFolderPath.EndsWith("\\"))
        {
            dataFolderPath += "\\";
        }

        // Set the data folder path (assume directory already exists)
        Constants.DataFolderPath = dataFolderPath;

        // Parse debug arguments (skip first arg which is the folder path)
        var debugConfig = new DebugConfiguration();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--debug-idle":
                    debugConfig.ShowIdleMessage = true;
                    break;
                case "--debug-scheduled-alert":
                    debugConfig.ShowScheduledMessage = true;
                    debugConfig.ScheduledMessageKind = Schedule.Item.Kind.StartTracking;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        debugConfig.ScheduledMessageText = args[i + 1];
                        i++;
                    }
                    break;
                case "--debug-scheduled-success":
                    debugConfig.ShowScheduledMessage = true;
                    debugConfig.ScheduledMessageKind = Schedule.Item.Kind.EndTracking;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        debugConfig.ScheduledMessageText = args[i + 1];
                        i++;
                    }
                    break;
                case "--debug-idle-time":
                    if (i + 1 < args.Length)
                    {
                        if (int.TryParse(args[i + 1], out int seconds))
                        {
                            debugConfig.IdleTimeSeconds = seconds;
                        }
                        i++;
                    }
                    break;
            }
        }

        // If in debug mode, run as a desktop app instead of service
        if (debugConfig.ShowIdleMessage || debugConfig.ShowScheduledMessage)
        {
            RunAsDesktopApp(debugConfig);
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "MyFancyHud";
        });

        builder.Services.AddSingleton(debugConfig);
        builder.Services.AddSingleton<IdleDetectionService>();
        builder.Services.AddSingleton<ScheduledMessageService>();
        builder.Services.AddHostedService<MyFancyHudWorker>();

        var host = builder.Build();
        host.Run();
    }

    static void RunAsDesktopApp(DebugConfiguration debugConfig)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var idleService = new IdleDetectionService();
        var scheduledService = new ScheduledMessageService();

        // Apply debug idle time if specified
        if (debugConfig.IdleTimeSeconds > 0)
        {
            idleService.IdleTimeThreshold = TimeSpan.FromSeconds(debugConfig.IdleTimeSeconds);
        }

        // Create a debug controller form
        var debugController = new DebugControllerForm(idleService, scheduledService, debugConfig);

        // Keep the application running
        Application.Run(debugController);
    }
}

public class DebugConfiguration
{
    public bool ShowIdleMessage { get; set; }
    public bool ShowScheduledMessage { get; set; }
    public string ScheduledMessageText { get; set; } = "Debug scheduled message";
    public Schedule.Item.Kind ScheduledMessageKind { get; set; } = Schedule.Item.Kind.StartTracking;
    public int IdleTimeSeconds { get; set; } = Constants.DebugIdleTimeoutSeconds;
}
