using Microsoft.Extensions.Logging;

namespace MyFancyHud;

/// <summary>
/// Unified message controller that handles both idle and scheduled message logic.
/// Eliminates duplication between Worker and DebugControllerForm.
/// </summary>
public class MessageController
{
    private readonly IdleDetectionService idleDetectionService;
    private readonly ScheduledMessageService scheduledMessageService;
    private readonly ILogger? logger;
    private readonly SynchronizationContext? syncContext;

    private IdleMessageWindow? idleWindow;
    private ScheduledMessageWindow? scheduledWindow;
    private bool wasIdle = false;
    private DateTime? lastScheduledMessageTime;

    public MessageController(
        IdleDetectionService idleDetectionService,
        ScheduledMessageService scheduledMessageService,
        SynchronizationContext? syncContext = null,
        ILogger? logger = null)
    {
        this.idleDetectionService = idleDetectionService;
        this.scheduledMessageService = scheduledMessageService;
        this.syncContext = syncContext;
        this.logger = logger;
    }

    /// <summary>
    /// Check idle state and show/hide idle message as needed
    /// </summary>
    public void CheckIdleState()
    {
        bool isIdle = idleDetectionService.IsIdle();

        // Always respond to user becoming active - hide window immediately
        if (!isIdle && wasIdle)
        {
            HideIdleMessage();
            wasIdle = false;
            return;
        }

        // Check if we are currently in a tracking period (only for showing window)
        var schedule = ScheduleLoader.Schedule;
        bool isTrackingPeriod = schedule?.IsCurrentlyTracking(TimeOnly.FromDateTime(DateTime.Now)) ?? false;

        // Only show idle window if we're in a tracking period
        if (isIdle && !wasIdle && isTrackingPeriod)
        {
            ShowIdleMessage();
            wasIdle = true;
            return;
        }

        // If we're showing idle window but not in tracking period anymore, hide it
        if (wasIdle && !isTrackingPeriod)
        {
            HideIdleMessage();
            wasIdle = false;
        }
    }

    /// <summary>
    /// Check for scheduled messages and show them if needed
    /// </summary>
    public void CheckScheduledMessages()
    {
        var scheduledMessage = scheduledMessageService.GetScheduledMessageForNow();

        if (scheduledMessage != null)
        {
            // Check if we already showed this message recently
            if (lastScheduledMessageTime.HasValue &&
                (DateTime.Now - lastScheduledMessageTime.Value).TotalMinutes < Constants.ScheduledMessageSuppressionMinutes)
            {
                return; // Don't show the same message again
            }

            ShowScheduledMessage(scheduledMessage);
            lastScheduledMessageTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Show idle message window
    /// </summary>
    public void ShowIdleMessage()
    {
        InvokeOnUIThread(() =>
        {
            if (idleWindow == null || idleWindow.IsDisposed)
            {
                idleWindow = new IdleMessageWindow(idleDetectionService.IdleMessage, idleDetectionService.IdleTimeThreshold);
                idleWindow.Show();
                wasIdle = true; // Track that we're showing the idle window
                logger?.LogInformation("Idle message shown");
            }
        });
    }

    /// <summary>
    /// Hide idle message window
    /// </summary>
    public void HideIdleMessage()
    {
        InvokeOnUIThread(() =>
        {
            if (idleWindow != null && !idleWindow.IsDisposed)
            {
                idleWindow.Close();
                idleWindow.Dispose();
                idleWindow = null;
                wasIdle = false; // Track that we're no longer showing the idle window
                logger?.LogInformation("Idle message hidden");
            }
        });
    }

    /// <summary>
    /// Show scheduled message window
    /// </summary>
    public void ShowScheduledMessage(Schedule.Item message)
    {
        InvokeOnUIThread(() =>
        {
            // Close existing scheduled message if any (new message overrides)
            if (scheduledWindow != null && !scheduledWindow.IsDisposed)
            {
                scheduledWindow.Close();
                scheduledWindow.Dispose();
            }

            scheduledWindow = new ScheduledMessageWindow(message);
            scheduledWindow.MessageConfirmed += (s, e) =>
            {
                logger?.LogInformation("Scheduled message confirmed");
            };
            scheduledWindow.Show();
            logger?.LogInformation($"Scheduled message shown: {message.Label}");
        });
    }

    /// <summary>
    /// Clean up all open windows
    /// </summary>
    public void Cleanup()
    {
        HideIdleMessage();

        InvokeOnUIThread(() =>
        {
            if (scheduledWindow != null && !scheduledWindow.IsDisposed)
            {
                scheduledWindow.Close();
                scheduledWindow.Dispose();
                scheduledWindow = null;
            }
        });
    }

    /// <summary>
    /// Execute action on UI thread (handles both SynchronizationContext and Control.Invoke approaches)
    /// </summary>
    private void InvokeOnUIThread(Action action)
    {
        if (syncContext != null)
        {
            // Use SynchronizationContext if available (better for Worker)
            syncContext.Post(_ => action(), null);
        }
        else
        {
            // Use Control.Invoke if we're already on UI thread (better for DebugControllerForm)
            if (Control.CheckForIllegalCrossThreadCalls && !IsOnUIThread())
            {
                // Create a temporary control to invoke on UI thread
                var invoker = new Control();
                var handle = invoker.Handle; // Force handle creation

                if (invoker.InvokeRequired)
                {
                    invoker.Invoke(action);
                }
                else
                {
                    action();
                }

                invoker.Dispose();
            }
            else
            {
                // We're already on UI thread or cross-thread calls are allowed
                action();
            }
        }
    }

    private bool IsOnUIThread()
    {
        return SynchronizationContext.Current != null;
    }
}
