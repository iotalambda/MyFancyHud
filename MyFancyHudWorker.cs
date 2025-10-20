using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyFancyHud;

public class MyFancyHudWorker : BackgroundService
{
    private readonly ILogger<MyFancyHudWorker> logger;
    private readonly IdleDetectionService idleDetectionService;
    private readonly ScheduledMessageService scheduledMessageService;
    private readonly DebugConfiguration debugConfig;
    private MessageController? messageController;

    private Thread? uiThread;
    private ApplicationContext? appContext;
    private SynchronizationContext? uiSyncContext;

    public MyFancyHudWorker(
        ILogger<MyFancyHudWorker> logger,
        IdleDetectionService idleDetectionService,
        ScheduledMessageService scheduledMessageService,
        DebugConfiguration debugConfig)
    {
        this.logger = logger;
        this.idleDetectionService = idleDetectionService;
        this.scheduledMessageService = scheduledMessageService;
        this.debugConfig = debugConfig;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("MyFancyHud service is starting");

        // Start UI thread for Windows Forms
        uiThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Capture the synchronization context from the UI thread
            uiSyncContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(uiSyncContext);

            appContext = new ApplicationContext();
            Application.Run(appContext);
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.IsBackground = false;
        uiThread.Start();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MyFancyHud service is running");

        // Wait for UI thread to initialize
        await Task.Delay(1500, stoppingToken);

        // Ensure application context is ready
        int waitCount = 0;
        while ((appContext == null || uiSyncContext == null) && waitCount < 10)
        {
            await Task.Delay(100, stoppingToken);
            waitCount++;
        }

        // Initialize message controller
        messageController = new MessageController(
            idleDetectionService,
            scheduledMessageService,
            uiSyncContext,
            logger);

        // Handle debug mode - show messages immediately if requested
        if (debugConfig.ShowIdleMessage)
        {
            logger.LogInformation("Debug mode: Showing idle message");
            messageController.ShowIdleMessage();
        }

        if (debugConfig.ShowScheduledMessage)
        {
            logger.LogInformation("Debug mode: Showing scheduled message");
            var debugMessage = new Schedule.Item(
                At: TimeOnly.FromDateTime(DateTime.Now),
                Label: debugConfig.ScheduledMessageText,
                ItemKind: debugConfig.ScheduledMessageKind
            );
            messageController.ShowScheduledMessage(debugMessage);
        }

        // Apply debug idle time if specified
        if (debugConfig.ShowIdleMessage && debugConfig.IdleTimeSeconds > 0)
        {
            idleDetectionService.IdleTimeThreshold = TimeSpan.FromSeconds(debugConfig.IdleTimeSeconds);
            logger.LogInformation($"Debug mode: Idle threshold set to {debugConfig.IdleTimeSeconds} seconds");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check for idle state and scheduled messages
                messageController.CheckIdleState();
                messageController.CheckScheduledMessages();

                // Check at configured interval
                await Task.Delay(Constants.CheckIntervalMs, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in MyFancyHud worker");
            }
        }

        logger.LogInformation("MyFancyHud service is stopping");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("MyFancyHud service is stopping");

        // Clean up windows through message controller
        messageController?.Cleanup();

        if (appContext != null)
        {
            appContext.ExitThread();
        }

        if (uiThread != null && uiThread.IsAlive)
        {
            uiThread.Join(2000); // Wait up to 2 seconds for thread to exit
        }

        await base.StopAsync(cancellationToken);
    }
}
