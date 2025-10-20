namespace MyFancyHud;

/// <summary>
/// Full-screen overlay window that shows a layered vignette effect (black edges fading to transparent center)
/// Uses 4 stacked layers with different sized transparent holes to create a gradient effect
/// Used to indicate active user activity - appears when user is active during tracking periods
/// </summary>
public class ActivityVignetteWindow : Form
{
    private readonly List<VignetteLayer> layers = [];
    private DateTime? growthStartTime = null;

    public ActivityVignetteWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Start the vignette growth animation (called when first star is awarded)
    /// </summary>
    public void StartGrowth()
    {
        if (growthStartTime == null)
        {
            growthStartTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("ActivityVignetteWindow: Growth started");
        }
    }

    /// <summary>
    /// Update the vignette appearance based on growth progress
    /// </summary>
    public void UpdateGrowth()
    {
        if (growthStartTime == null)
        {
            // Not growing yet, keep at minimum
            UpdateLayers(0.0, 1, 0.0);
            return;
        }

        var elapsed = (DateTime.Now - growthStartTime.Value).TotalSeconds;
        var stageDuration = Constants.ActivityVignetteStageDurationSeconds;

        if (elapsed < stageDuration)
        {
            // Stage 1: Grow opacity and size
            var progress = elapsed / stageDuration;
            var currentOpacity = progress * Constants.ActivityVignetteGradeMaxOpacity;
            var currentGradeSize = (int)(1 + (progress * (Constants.ActivityVignetteMaxGradeSizePixels - 1)));

            System.Diagnostics.Debug.WriteLine($"ActivityVignetteWindow: Stage 1 - progress={progress:F2}, opacity={currentOpacity:F3}, gradeSize={currentGradeSize}");
            UpdateLayers(currentOpacity, currentGradeSize, 0.0);
        }
        else
        {
            // Stage 2: Rainbow wave animation
            var stage2Elapsed = elapsed - stageDuration;
            var stage2Progress = Math.Min(1.0, stage2Elapsed / stageDuration);

            // Keep stage 1 values at max
            var currentOpacity = Constants.ActivityVignetteGradeMaxOpacity;
            var currentGradeSize = Constants.ActivityVignetteMaxGradeSizePixels;

            System.Diagnostics.Debug.WriteLine($"ActivityVignetteWindow: Stage 2 - progress={stage2Progress:F2}, rainbow intensity={stage2Progress:F3}");
            UpdateLayers(currentOpacity, currentGradeSize, stage2Progress);
        }
    }

    /// <summary>
    /// Reset vignette to initial state (opacity=0, gradeSize=1)
    /// </summary>
    public void ResetGrowth()
    {
        growthStartTime = null;
        UpdateLayers(0.0, 1, 0.0);
        System.Diagnostics.Debug.WriteLine("ActivityVignetteWindow: Growth reset");
    }

    private void UpdateLayers(double opacity, int gradeSize, double rainbowIntensity)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            // Exponential opacity falloff: outer layers are more opaque
            double exponent = i * Constants.ActivityVignetteOpacityExponentMultiplier;
            double layerOpacity = (exponent == 0.0 && opacity > 0.0) ? opacity : Math.Pow(opacity, exponent);

            layers[i].UpdateAppearance(layerOpacity, gradeSize, rainbowIntensity, i);
        }
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // This main form is completely transparent and just acts as a container
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        this.StartPosition = FormStartPosition.Manual;
        this.TopMost = false; // Not topmost - reward windows will be above this
        this.ShowInTaskbar = false;
        this.BackColor = Color.Magenta;
        this.TransparencyKey = Color.Magenta;
        this.Opacity = 1.0; // Full opacity for the container

        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            this.Bounds = screen.Bounds;
        }

        this.ResumeLayout(false);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var screen = Screen.PrimaryScreen;
        if (screen == null)
        {
            System.Diagnostics.Debug.WriteLine("ActivityVignetteWindow: No primary screen found");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"ActivityVignetteWindow: Creating 4 layers on screen {screen.Bounds}");

        // Create 4 layers, each identified by its index
        // The actual inset is calculated dynamically based on currentGradeSize
        for (int i = 0; i < 4; i++)
        {
            var layer = new VignetteLayer(screen.Bounds, i);
            layers.Add(layer);
            System.Diagnostics.Debug.WriteLine($"ActivityVignetteWindow: Layer {i} created");
        }

        // Initialize all layers to minimum state (opacity=0, gradeSize=1, no rainbow) BEFORE showing
        UpdateLayers(0.01, 1, 0.0);

        // Now show the layers after they're properly initialized
        foreach (var layer in layers)
        {
            layer.Show();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var layer in layers)
            {
                layer.Close();
                layer.Dispose();
            }
            layers.Clear();
        }
        base.Dispose(disposing);
    }

    // Make the container window click-through and non-activating
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

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }
}

/// <summary>
/// Individual vignette layer with a transparent hole in the center
/// </summary>
internal class VignetteLayer : Form
{
    private readonly Rectangle screenBounds;
    private readonly int layerIndex; // Which layer this is (0=outermost, 3=innermost)
    private int currentGradeSize = 1;
    private double currentOpacity = 0.0;
    private double currentRainbowIntensity = 0.0;
    private System.Windows.Forms.Timer? rainbowTimer;
    private int colorPhase = 0;

    public VignetteLayer(Rectangle bounds, int index)
    {
        this.screenBounds = bounds;
        this.layerIndex = index;
        InitializeComponent();

        // Create timer for rainbow animation
        rainbowTimer = new System.Windows.Forms.Timer { Interval = 30 }; // 30ms for smooth animation
        rainbowTimer.Tick += (s, e) =>
        {
            if (currentRainbowIntensity > 0.0)
            {
                colorPhase = (colorPhase + 5) % 360; // Increment phase
                this.Invalidate(); // Trigger repaint
            }
        };
    }

    /// <summary>
    /// Update the layer's opacity, grade size, and rainbow intensity
    /// </summary>
    public void UpdateAppearance(double opacity, int gradeSize, double rainbowIntensity, int phaseOffset)
    {
        bool needsRepaint = false;

        if (Math.Abs(this.currentOpacity - opacity) > 0.001)
        {
            this.currentOpacity = opacity;
            this.Opacity = opacity;
        }

        if (this.currentGradeSize != gradeSize)
        {
            this.currentGradeSize = gradeSize;
            needsRepaint = true;
        }

        if (Math.Abs(this.currentRainbowIntensity - rainbowIntensity) > 0.001)
        {
            this.currentRainbowIntensity = rainbowIntensity;
            needsRepaint = true;

            // Start/stop timer based on intensity
            if (rainbowIntensity > 0.0 && rainbowTimer != null && !rainbowTimer.Enabled)
            {
                // Apply phase offset for wave effect (outer layers start first)
                colorPhase = phaseOffset * 90; // 90 degrees offset per layer
                rainbowTimer.Start();
            }
            else if (rainbowIntensity == 0.0 && rainbowTimer != null && rainbowTimer.Enabled)
            {
                rainbowTimer.Stop();
            }
        }

        if (needsRepaint)
        {
            this.Invalidate();
        }
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Bounds = screenBounds;
        this.TopMost = true; // Topmost to appear above user content
        this.ShowInTaskbar = false;
        this.BackColor = Color.Black;
        this.TransparencyKey = Color.Magenta; // Magenta will be transparent
        this.Opacity = 0.0; // Start invisible, will be updated dynamically

        this.DoubleBuffered = true;
        this.Paint += OnPaint;

        this.ResumeLayout(false);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None; // No antialiasing to avoid magenta leakage

        // Calculate background color: blend from black to rainbow based on intensity
        Color bgColor;
        if (currentRainbowIntensity > 0.0)
        {
            var rainbowColor = GetRainbowColor(colorPhase);
            // Linear interpolation from black to rainbow color
            bgColor = Color.FromArgb(
                255,
                (int)(rainbowColor.R * currentRainbowIntensity),
                (int)(rainbowColor.G * currentRainbowIntensity),
                (int)(rainbowColor.B * currentRainbowIntensity)
            );
        }
        else
        {
            bgColor = Color.Black;
        }

        // Fill entire window with background color
        g.Clear(bgColor);

        // Calculate inset based on layer index and current grade size
        int insetPixels = (layerIndex + 1) * currentGradeSize;

        // Calculate the transparent rounded rectangle hole
        var holeRect = new Rectangle(
            insetPixels,
            insetPixels,
            this.ClientRectangle.Width - (insetPixels * 2),
            this.ClientRectangle.Height - (insetPixels * 2)
        );

        // Draw magenta (transparent) rounded rectangle in the center
        using (var brush = new SolidBrush(Color.Magenta))
        using (var path = CreateRoundedRectanglePath(holeRect, Constants.ActivityVignetteCornerRadius))
        {
            g.FillPath(brush, path);
        }
    }

    private Color GetRainbowColor(int hue)
    {
        // Convert HSV to RGB for rainbow effect
        // Hue: 0-360, Saturation: 100%, Value: 100%
        var h = hue / 60.0;
        var c = 1.0; // Chroma (saturation * value)
        var x = c * (1 - Math.Abs((h % 2) - 1));

        double r = 0, g = 0, b = 0;
        if (h >= 0 && h < 1) { r = c; g = x; b = 0; }
        else if (h >= 1 && h < 2) { r = x; g = c; b = 0; }
        else if (h >= 2 && h < 3) { r = 0; g = c; b = x; }
        else if (h >= 3 && h < 4) { r = 0; g = x; b = c; }
        else if (h >= 4 && h < 5) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int diameter = radius * 2;

        // Top-left corner
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        // Top-right corner
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        // Bottom-right corner
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        // Bottom-left corner
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);

        path.CloseFigure();
        return path;
    }

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

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            rainbowTimer?.Stop();
            rainbowTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
