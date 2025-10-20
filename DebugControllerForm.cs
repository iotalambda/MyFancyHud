namespace MyFancyHud;

public class DebugControllerForm : Form
{
    private readonly MessageController messageController;
    private System.Windows.Forms.Timer checkTimer = null!;

    public DebugControllerForm(
        IdleDetectionService idleService,
        ScheduledMessageService scheduledService,
        DebugConfiguration debugConfig)
    {

        // Initialize message controller (no SyncContext needed, we're on UI thread)
        messageController = new MessageController(
            idleService,
            scheduledService,
            syncContext: null,
            logger: null);

        InitializeComponent();
        SetupTimer();

        // For scheduled message debug, show it immediately
        if (debugConfig.ShowScheduledMessage)
        {
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
            idleService.IdleTimeThreshold = TimeSpan.FromSeconds(debugConfig.IdleTimeSeconds);
        }
    }

    private void InitializeComponent()
    {
        // Hidden form - just keeps the message pump running
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.WindowState = FormWindowState.Minimized;
        this.Opacity = 0; // Completely invisible
        this.Size = new Size(0, 0);
    }

    private void SetupTimer()
    {
        checkTimer = new System.Windows.Forms.Timer
        {
            Interval = Constants.CheckIntervalMs
        };
        checkTimer.Tick += CheckTimer_Tick;
        checkTimer.Start();
    }

    private void CheckTimer_Tick(object? sender, EventArgs e)
    {
        // Delegate all logic to message controller
        messageController.CheckIdleState();
        messageController.CheckScheduledMessages();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        checkTimer?.Stop();
        checkTimer?.Dispose();

        // Clean up through message controller
        messageController.Cleanup();

        base.OnFormClosing(e);
    }
}
