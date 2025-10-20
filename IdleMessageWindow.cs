namespace MyFancyHud;

/// <summary>
/// Minimal idle message window: black background that fades in with timeline in the center.
/// </summary>
public class IdleMessageWindow : Form
{
    private System.Windows.Forms.Timer? fadeTimer;
    private System.Windows.Forms.Timer? updateTimer;
    private DateTime fadeStartTime;
    private bool blinkPhase = false;
    private List<TimelineRenderer.TimelineChar>? timelineData;

    public IdleMessageWindow(string message)
    {
        InitializeComponent();
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

        // Start fade-in
        StartFadeIn();

        // Start update timer for blinking
        updateTimer = new System.Windows.Forms.Timer { Interval = 500 };
        updateTimer.Tick += (s, e) =>
        {
            blinkPhase = !blinkPhase;
            UpdateTimeline();
            this.Invalidate();
        };
        updateTimer.Start();
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
            updateTimer?.Stop();
            updateTimer?.Dispose();
            fadeTimer?.Stop();
            fadeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
