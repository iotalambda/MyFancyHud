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
    private ActivityVignetteWindow? vignetteWindow;
    private bool wasIdle = false;
    private DateTime? lastScheduledMessageTime;
    private DateTime lastRewardCheckTime = DateTime.MinValue;
    private DateTime? activityStartTime = null;
    private int starCount = 0;

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

        // Check if we are currently in a tracking period
        var schedule = ScheduleLoader.Schedule;
        bool isTrackingPeriod = schedule?.IsCurrentlyTracking(TimeOnly.FromDateTime(DateTime.Now)) ?? false;

        // Reward logic: Check at interval if there was any activity in the activity window
        if (isTrackingPeriod && (DateTime.Now - lastRewardCheckTime).TotalSeconds >= Constants.RewardCheckIntervalSeconds)
        {
            var idleTime = idleDetectionService.GetIdleTime();

            // If there was activity in the activity window (idle time < activity window)
            if (idleTime.TotalSeconds < Constants.RewardActivityWindowSeconds)
            {
                starCount++;
                ShowReward(starCount);
            }
            else
            {
                // No activity in the activity window, reset counter
                starCount = 0;
            }

            lastRewardCheckTime = DateTime.Now;
        }

        // Reset star count if we're not in tracking period
        if (!isTrackingPeriod)
        {
            starCount = 0;
        }

        // Activity vignette logic: Show after continuous activity, hide when idle
        if (isTrackingPeriod)
        {
            if (!isIdle)
            {
                // User is active
                if (activityStartTime == null)
                {
                    // Just became active, start tracking
                    activityStartTime = DateTime.Now;
                    logger?.LogInformation($"Activity tracking started at {activityStartTime}");
                }

                var activeSeconds = (DateTime.Now - activityStartTime.Value).TotalSeconds;

                if (activeSeconds >= Constants.ActivityVignetteDelaySeconds)
                {
                    // Been active long enough, show vignette and update growth
                    ShowVignette();

                    // Start growth and update continuously
                    if (vignetteWindow != null && !vignetteWindow.IsDisposed)
                    {
                        vignetteWindow.StartGrowth();
                        vignetteWindow.UpdateGrowth();
                    }
                }
            }
            else
            {
                // User is idle, hide vignette and reset activity timer
                if (activityStartTime != null)
                {
                    logger?.LogInformation("User became idle, hiding vignette");
                }
                HideVignette();
                activityStartTime = null;
            }
        }
        else
        {
            // Not in tracking period, hide vignette
            if (activityStartTime != null)
            {
                logger?.LogInformation("Not in tracking period, hiding vignette");
            }
            HideVignette();
            activityStartTime = null;
        }

        // Always respond to user becoming active - hide window immediately
        if (!isIdle && wasIdle)
        {
            HideIdleMessage();
            wasIdle = false;
            return;
        }

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
    /// Show the activity vignette overlay
    /// </summary>
    private void ShowVignette()
    {
        InvokeOnUIThread(() =>
        {
            if (vignetteWindow == null || vignetteWindow.IsDisposed)
            {
                vignetteWindow = new ActivityVignetteWindow();
                vignetteWindow.Show();
                logger?.LogInformation("Activity vignette window created and shown");
                System.Diagnostics.Debug.WriteLine("MessageController: Vignette window created and shown");
            }
        });
    }

    /// <summary>
    /// Hide the activity vignette overlay
    /// </summary>
    private void HideVignette()
    {
        InvokeOnUIThread(() =>
        {
            if (vignetteWindow != null && !vignetteWindow.IsDisposed)
            {
                vignetteWindow.ResetGrowth();
                vignetteWindow.Close();
                vignetteWindow.Dispose();
                vignetteWindow = null;
                logger?.LogInformation("Activity vignette hidden");
            }
        });
    }

    /// <summary>
    /// Show reward windows for staying active
    /// </summary>
    private void ShowReward(int starCount)
    {
        InvokeOnUIThread(() =>
        {
            var random = new Random();

            // Show multiple stars with random delays (0.. seconds) so they trickle in
            for (int i = 0; i < starCount; i++)
            {
                var delay = random.Next(1, Constants.RewardCheckIntervalSeconds * 1000 + 1);

                var delayTimer = new System.Windows.Forms.Timer { Interval = delay + 1 };
                delayTimer.Tick += (s, e) =>
                {
                    delayTimer.Stop();
                    delayTimer.Dispose();

                    var rewardWindow = new RewardWindow();
                    rewardWindow.Show();
                };
                delayTimer.Start();
            }
            logger?.LogInformation($"Reward shown: {starCount} star(s) for staying active");
        });
    }

    /// <summary>
    /// Clean up all open windows
    /// </summary>
    public void Cleanup()
    {
        HideIdleMessage();
        HideVignette();

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
