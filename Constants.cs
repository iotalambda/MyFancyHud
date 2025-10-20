namespace MyFancyHud;

public static class Constants
{
    // Timing Configuration
    public static readonly int CheckIntervalMs = 300; // How often to poll for idle state (milliseconds)
    public static readonly int ClockUpdateIntervalMs = 1000; // How often to update the clock display (milliseconds)

    // Idle Detection
    public static readonly int DefaultIdleTimeoutSeconds = 30; // Default idle timeout in minutes
    public static readonly int DebugIdleTimeoutSeconds = 5; // Default idle timeout for debug mode in seconds

    // Idle Message Background Fade
    public static readonly int IdleBackgroundFadeDurationMs = 30000; // Duration to fade background from 0% to target opacity (milliseconds)
    public static readonly double IdleBackgroundTargetOpacity = 0.75; // Target opacity for background (0.0 = transparent, 1.0 = opaque)
    public static readonly int IdleContainerCornerRadius = 36; // Corner radius for the white container in pixels

    // Scheduled Messages
    public static readonly int ScheduledMessageCooldownSeconds = 30; // Time window for detecting scheduled messages (seconds)
    public static readonly int ScheduledMessageSuppressionMinutes = 1; // Don't show same message again within this time

    // UI Configuration
    public static readonly string DefaultIdleMessage = "You have been idle for a while";
    public static readonly string DefaultFontName = "Segoe UI";
    public static readonly int IdleMessageFontSize = 24;
    public static readonly int ScheduledMessageFontSize = 12;

    // Scheduled Message Window
    public static readonly int ScheduledMessageWidth = 350;
    public static readonly int ScheduledMessageHeight = 150;
    public static readonly int ScheduledMessageMargin = 20; // Distance from screen edge

    // Reward Configuration
    public static readonly int RewardCheckIntervalSeconds = 20; // How often to check for activity and award rewards (seconds)
    public static readonly int RewardActivityWindowSeconds = 20; // Time window to check for user activity (seconds)

    // Schedule Loader Configuration
    public static string DataFolderPath { get; set; } = string.Empty;
    public static string ScheduleFilePath => Path.Combine(DataFolderPath, "schedule.json");
    public static string ScheduleLogFilePath => Path.Combine(DataFolderPath, "schedule_loader.log");
    public static readonly int ScheduleReloadIntervalMinutes = 5; // How often to reload schedule from file
}
