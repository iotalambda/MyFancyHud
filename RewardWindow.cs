namespace MyFancyHud;

/// <summary>
/// Small reward window that shows a character at the screen edge
/// </summary>
public class RewardWindow : Form
{
    private System.Windows.Forms.Timer? animationTimer;
    private System.Windows.Forms.Timer? lifetimeTimer;
    private int colorPhase = 0;
    private const int AnimationDurationMs = 1000; // Show for 1 second
    private readonly string rewardCharacter; // Random Wingdings character

    public RewardWindow()
    {
        // Pick a random Wingdings character
        var random = new Random();
        // Wingdings has many symbols in the range 33-255
        // Popular symbols are roughly in these ranges:
        // 33-126: various symbols, arrows, shapes
        // 161-255: more symbols
        int charCode = random.Next(33, 256);
        rewardCharacter = ((char)charCode).ToString();

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form configuration - no border, transparent, topmost
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.BackColor = Color.Magenta; // Will be transparent
        this.TransparencyKey = Color.Magenta;
        const int STANDARD_CONTAINER_EDGE = 128;
        this.Width = STANDARD_CONTAINER_EDGE;
        this.Height = STANDARD_CONTAINER_EDGE;
        this.Opacity = 0.4;

        // Position randomly at one of the four edges
        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            var random = new Random();
            var edge = random.Next(4); // 0=top, 1=right, 2=bottom, 3=left

            // Distance from edge (variance 20-60 pixels)
            var distanceFromEdge = random.Next(20, 61);

            int x, y;

            switch (edge)
            {
                case 0: // Top edge
                    x = random.Next(screen.Bounds.Left, screen.Bounds.Right) - this.Width / 2;
                    y = screen.Bounds.Top + distanceFromEdge - this.Height / 2;
                    break;

                case 1: // Right edge
                    x = screen.Bounds.Right - this.Width - distanceFromEdge + this.Width / 2;
                    y = random.Next(screen.Bounds.Top, screen.Bounds.Bottom) - this.Height / 2;
                    break;

                case 2: // Bottom edge
                    x = random.Next(screen.Bounds.Left, screen.Bounds.Right) - this.Width / 2;
                    y = screen.Bounds.Bottom - this.Height - distanceFromEdge + this.Height / 2;
                    break;

                default: // Left edge (case 3)
                    x = screen.Bounds.Left + distanceFromEdge - this.Width / 2;
                    y = random.Next(screen.Bounds.Top, screen.Bounds.Bottom) - this.Height / 2;
                    break;
            }

            this.Location = new Point(x, y);
        }

        // Enable double buffering for smooth rendering
        this.DoubleBuffered = true;
        this.Paint += OnPaint;

        this.ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Start animation timer for blinking
        animationTimer = new System.Windows.Forms.Timer { Interval = 30 }; // 30ms for smooth animation
        animationTimer.Tick += (s, e) =>
        {
            colorPhase = (colorPhase + 20) % 360; // Increment by 20 for faster color cycling
            this.Invalidate();
        };
        animationTimer.Start();

        // Auto-close after animation duration
        lifetimeTimer = new System.Windows.Forms.Timer { Interval = AnimationDurationMs };
        lifetimeTimer.Tick += (s, e) =>
        {
            lifetimeTimer?.Stop();
            this.Close();
        };
        lifetimeTimer.Start();
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Calculate color based on current phase
        var rainbowColor = GetRainbowColor(colorPhase);

        // Draw the character with color
        var font = new Font("Wingdings", 48, FontStyle.Regular);
        var size = g.MeasureString(rewardCharacter, font);

        // Center the character in the window
        var x = (this.ClientSize.Width - size.Width) / 2;
        var y = (this.ClientSize.Height - size.Height) / 2;

        // Draw with full color (transparency is handled by window Opacity)
        using (var brush = new SolidBrush(rainbowColor))
        {
            g.DrawString(rewardCharacter, font, brush, x, y);
        }

        font.Dispose();
    }

    private Color GetRainbowColor(int hue)
    {
        // Convert HSV to RGB for rainbow effect
        // Hue: 0-360, Saturation: 100%, Value: 100%
        var h = hue / 60.0;
        var c = 1.0; // Chroma (saturation * value)
        var x = c * (1 - Math.Abs((h % 2) - 1));
        var m = 0.0;

        double r = 0, g = 0, b = 0;
        if (h >= 0 && h < 1) { r = c; g = x; b = 0; }
        else if (h >= 1 && h < 2) { r = x; g = c; b = 0; }
        else if (h >= 2 && h < 3) { r = 0; g = c; b = x; }
        else if (h >= 3 && h < 4) { r = 0; g = x; b = c; }
        else if (h >= 4 && h < 5) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromArgb(
            255,
            (int)((r + m) * 255),
            (int)((g + m) * 255),
            (int)((b + m) * 255)
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer?.Stop();
            animationTimer?.Dispose();
            lifetimeTimer?.Stop();
            lifetimeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    // Make the window click-through and non-activating (doesn't steal focus)
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT - click-through
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE - doesn't activate/steal focus
            return cp;
        }
    }

    // Prevent the window from taking focus
    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }
}
