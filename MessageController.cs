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
        if (isTrackingPeriod && !isIdle && (DateTime.Now - lastRewardCheckTime).TotalSeconds >= Constants.RewardCheckIntervalSeconds)
        {
            var idleTime = idleDetectionService.GetIdleTime();

            // Only show rewards if idle time is very low (< 5 seconds)
            // This prevents reward windows from interfering with idle detection
            if (idleTime.TotalSeconds < Constants.RewardActivityWindowSeconds && idleTime.TotalSeconds < 5.0)
            {
                starCount++;
                ShowReward(starCount);
            }
            else if (idleTime.TotalSeconds >= Constants.RewardActivityWindowSeconds)
            {
                // No activity in the activity window, reset counter
                starCount = 0;
            }
            // If idle time is between 5s and activity window, keep star count but don't show new rewards

            lastRewardCheckTime = DateTime.Now;
        }

        // Reset star count if user is idle
        if (isIdle && starCount > 0)
        {
            starCount = 0;
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

                    // Start growth and update continuously (must be on UI thread)
                    InvokeOnUIThread(() =>
                    {
                        try
                        {
                            if (vignetteWindow != null && !vignetteWindow.IsDisposed)
                            {
                                vignetteWindow.StartGrowth();
                                vignetteWindow.UpdateGrowth();
                            }
                            else if (vignetteWindow == null)
                            {
                                logger?.LogWarning("Vignette window is null during update");
                            }
                            else if (vignetteWindow.IsDisposed)
                            {
                                logger?.LogWarning("Vignette window is disposed during update");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Error updating vignette growth");
                        }
                    });
                }
            }
            else
            {
                // User is idle, hide vignette and reset activity timer
                if (activityStartTime != null)
                {
                    logger?.LogInformation("User became idle during tracking period, hiding vignette");
                    HideVignette();
                    activityStartTime = null;
                }
                else
                {
                    // User was already idle, but still call HideVignette to be safe
                    if (vignetteWindow != null && !vignetteWindow.IsDisposed)
                    {
                        logger?.LogWarning("User is idle but vignette window still exists - forcing hide");
                        HideVignette();
                    }
                }
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
            }
        });
    }

    /// <summary>
    /// Hide the activity vignette overlay
    /// </summary>
    private void HideVignette()
    {
        // Use synchronous invoke for hiding to ensure it completes
        InvokeOnUIThreadSync(() =>
        {
            try
            {
                if (vignetteWindow != null && !vignetteWindow.IsDisposed)
                {
                    vignetteWindow.ForceHideAllLayers(); // Force hide layers first
                    vignetteWindow.ResetGrowth();
                    vignetteWindow.Close();
                    vignetteWindow.Dispose();
                    vignetteWindow = null;
                    logger?.LogInformation("Activity vignette hidden successfully");
                }
                else
                {
                    vignetteWindow = null; // Ensure it's null
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error hiding vignette");
                vignetteWindow = null; // Clear reference even on error
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
    /// Execute action on UI thread asynchronously (fire-and-forget)
    /// </summary>
    private void InvokeOnUIThread(Action action)
    {
        try
        {
            if (syncContext != null)
            {
                // Use SynchronizationContext if available (better for Worker)
                syncContext.Post(_ =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error executing UI action via SynchronizationContext");
                        System.Diagnostics.Debug.WriteLine($"UI action error: {ex.Message}");
                    }
                }, null);
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
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in InvokeOnUIThread");
            System.Diagnostics.Debug.WriteLine($"InvokeOnUIThread error: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute action on UI thread synchronously (waits for completion)
    /// </summary>
    private void InvokeOnUIThreadSync(Action action)
    {
        try
        {
            if (syncContext != null)
            {
                // Use Send instead of Post for synchronous execution
                syncContext.Send(_ =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error executing UI action via SynchronizationContext.Send");
                        System.Diagnostics.Debug.WriteLine($"UI action error (sync): {ex.Message}");
                        throw; // Re-throw to propagate to caller
                    }
                }, null);
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
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in InvokeOnUIThreadSync");
            System.Diagnostics.Debug.WriteLine($"InvokeOnUIThreadSync error: {ex.Message}");
        }
    }

    private bool IsOnUIThread()
    {
        return SynchronizationContext.Current != null;
    }
}
