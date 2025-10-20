namespace MyFancyHud;

/// <summary>
/// Minimal idle message window: black background that fades in with timeline in the center.
/// </summary>
public class IdleMessageWindow : Form
{
    private System.Windows.Forms.Timer? fadeTimer;
    private System.Windows.Forms.Timer? updateTimer;
    private DateTime fadeStartTime;
    private DateTime windowShownTime;
    private DateTime? fadeInActualStartTime; // When fade-in actually started (after delay)
    private bool blinkPhase = false;
    private List<TimelineRenderer.TimelineChar>? timelineData;
    private System.Media.SoundPlayer? soundPlayer;
    private bool alarmPlaying = false;
    private readonly TimeSpan idleTimeThreshold;

    public IdleMessageWindow(string message, TimeSpan idleThreshold)
    {
        InitializeComponent();
        windowShownTime = DateTime.Now;
        idleTimeThreshold = idleThreshold;
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form configuration
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.BackColor = Color.Black;
        this.WindowState = FormWindowState.Maximized;
        this.Opacity = 0.0; // Start invisible
        this.DoubleBuffered = true;

        this.Paint += OnPaint;

        this.ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Generate timeline data
        UpdateTimeline();

        // Start fade-in after delay (grace period)
        if (Constants.IdleFadeInDelaySeconds > 0)
        {
            var delayTimer = new System.Windows.Forms.Timer { Interval = Constants.IdleFadeInDelaySeconds * 1000 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                StartFadeIn();
            };
            delayTimer.Start();
        }
        else
        {
            // No delay, start immediately
            StartFadeIn();
        }

        // Start update timer for blinking and alarm checking
        updateTimer = new System.Windows.Forms.Timer { Interval = 500 };
        updateTimer.Tick += (s, e) =>
        {
            blinkPhase = !blinkPhase;
            UpdateTimeline();
            this.Invalidate();
            CheckAlarmTrigger();
        };
        updateTimer.Start();
    }

    private void CheckAlarmTrigger()
    {
        // Check if alarm should be triggered
        var schedule = ScheduleLoader.Schedule;
        if (schedule == null || string.IsNullOrWhiteSpace(schedule.AlarmSoundFile))
            return;

        if (alarmPlaying)
            return;

        // Only check alarm after fade-in has started
        if (fadeInActualStartTime == null)
            return;

        // Check if we've been idle for 2x the idle timeout AFTER the fade-in started
        var idleTimeoutMs = idleTimeThreshold.TotalMilliseconds;
        var elapsedSinceFadeInMs = (DateTime.Now - fadeInActualStartTime.Value).TotalMilliseconds;

        if (elapsedSinceFadeInMs >= idleTimeoutMs * 2)
        {
            StartAlarm(schedule.AlarmSoundFile);
        }
    }

    private void StartAlarm(string alarmSoundFile)
    {
        try
        {
            var soundFilePath = Path.Combine(Constants.DataFolderPath, alarmSoundFile);

            if (!File.Exists(soundFilePath))
            {
                // Log error but don't crash
                return;
            }

            // For MP3 files, we need to use a different approach
            if (soundFilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                // Use Windows Media Player COM object for MP3
                PlayMp3Loop(soundFilePath);
            }
            else
            {
                // Use SoundPlayer for WAV files
                soundPlayer = new System.Media.SoundPlayer(soundFilePath);
                soundPlayer.PlayLooping();
            }

            alarmPlaying = true;
        }
        catch
        {
            // Ignore errors
        }
    }

    private void PlayMp3Loop(string filePath)
    {
        // Use WMPLib to play MP3 in a loop
        // We'll create a simple background thread to handle this
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var playerType = Type.GetTypeFromProgID("WMPLayer.OCX.7");
                if (playerType == null)
                    return;

                dynamic? player = Activator.CreateInstance(playerType);
                if (player == null)
                    return;

                player.URL = filePath;
                player.settings.setMode("loop", true);
                player.controls.play();

                // Keep thread alive while window is open
                while (!IsDisposed && alarmPlaying)
                {
                    System.Threading.Thread.Sleep(100);
                }

                player.controls.stop();
            }
            catch
            {
                // Fallback: use System.Media.SoundPlayer (won't work for MP3 but won't crash)
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    private void UpdateTimeline()
    {
        if (ScheduleLoader.Schedule == null)
        {
            timelineData = new List<TimelineRenderer.TimelineChar>();
            return;
        }

        var currentTime = TimeOnly.FromDateTime(DateTime.Now);
        timelineData = TimelineRenderer.GenerateTimeline(ScheduleLoader.Schedule, currentTime);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (timelineData == null || timelineData.Count == 0)
            return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var font = new Font("Consolas", 14, FontStyle.Regular);
        var labelFont = new Font("Consolas", 10, FontStyle.Regular);
        var strikethroughFont = new Font("Consolas", 10, FontStyle.Strikeout);

        var charSize = TextRenderer.MeasureText("█", font, Size.Empty, TextFormatFlags.NoPadding);
        int charWidth = charSize.Width;

        int totalTimelineWidth = timelineData.Count * charWidth;
        int startX = (this.ClientSize.Width - totalTimelineWidth) / 2;
        int centerY = this.ClientSize.Height / 2;

        int labelY = centerY - 50;
        int timelineY = centerY;

        // Draw timeline blocks
        int segmentStart = 0;
        while (segmentStart < timelineData.Count)
        {
            var tc = timelineData[segmentStart];

            Color color = tc.BaseColor;
            if (tc.IsPast)
                color = TimelineRenderer.DarkenColor(color);
            if (tc.IsCurrent)
                color = TimelineRenderer.GetBlinkColor(color, blinkPhase);

            int segmentEnd = segmentStart + 1;
            while (segmentEnd < timelineData.Count)
            {
                var nextTc = timelineData[segmentEnd];
                Color nextColor = nextTc.BaseColor;
                if (nextTc.IsPast)
                    nextColor = TimelineRenderer.DarkenColor(nextColor);
                if (nextTc.IsCurrent)
                    nextColor = TimelineRenderer.GetBlinkColor(nextColor, blinkPhase);

                if (nextColor.ToArgb() != color.ToArgb())
                    break;

                segmentEnd++;
            }

            int segmentLength = segmentEnd - segmentStart;
            string segmentText = new string('█', segmentLength);
            int x = startX + (segmentStart * charWidth);

            TextRenderer.DrawText(g, segmentText, font, new Point(x, timelineY), color, TextFormatFlags.NoPadding);

            segmentStart = segmentEnd;
        }

        // Draw labels
        for (int i = 0; i < timelineData.Count; i++)
        {
            var tc = timelineData[i];

            if (!string.IsNullOrEmpty(tc.LabelAbove))
            {
                int x = startX + (i * charWidth);
                Color labelColor = tc.IsPast
                    ? TimelineRenderer.DarkenColor(Color.FromArgb(0, 200, 0))
                    : Color.FromArgb(0, 200, 0);

                var usedFont = tc.IsLabelStrikethrough ? strikethroughFont : labelFont;
                TextRenderer.DrawText(g, tc.LabelAbove, usedFont, new Point(x - 5, labelY), labelColor, TextFormatFlags.NoPadding);

                int arrowY = labelY + 15;
                TextRenderer.DrawText(g, "↓", labelFont, new Point(x - 5, arrowY), labelColor, TextFormatFlags.NoPadding);
            }
        }

        font.Dispose();
        labelFont.Dispose();
        strikethroughFont.Dispose();
    }

    private void StartFadeIn()
    {
        fadeStartTime = DateTime.Now;
        fadeInActualStartTime = DateTime.Now; // Track when fade-in actually started
        this.Opacity = 0.0;

        fadeTimer = new System.Windows.Forms.Timer { Interval = 50 };
        fadeTimer.Tick += FadeTimer_Tick;
        fadeTimer.Start();
    }

    private void FadeTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.Now - fadeStartTime).TotalMilliseconds;
        var progress = Math.Min(elapsed / Constants.IdleBackgroundFadeDurationMs, 1.0);

        this.Opacity = progress * Constants.IdleBackgroundTargetOpacity;

        if (progress >= 1.0)
        {
            fadeTimer?.Stop();
            fadeTimer?.Dispose();
            fadeTimer = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            alarmPlaying = false;
            soundPlayer?.Stop();
            soundPlayer?.Dispose();
            updateTimer?.Stop();
            updateTimer?.Dispose();
            fadeTimer?.Stop();
            fadeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
