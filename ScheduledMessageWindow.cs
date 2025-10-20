namespace MyFancyHud;

/// <summary>
/// Minimal scheduled message window with colored background.
/// </summary>
public class ScheduledMessageWindow : Form
{
    private Label? messageLabel;
    private Button? confirmButton;

    public event EventHandler? MessageConfirmed;

    public ScheduledMessageWindow(Schedule.Item scheduleItem)
    {
        InitializeComponent(scheduleItem);
        this.KeyPreview = true;
        this.KeyDown += OnKeyDown;
    }

    private void InitializeComponent(Schedule.Item scheduleItem)
    {
        this.SuspendLayout();

        // Colors based on message kind
        Color bgColor, fgColor, buttonBgColor, buttonFgColor;

        if (scheduleItem.ItemKind == Schedule.Item.Kind.Alert)
        {
            bgColor = Color.Yellow;
            fgColor = Color.Black;
            buttonBgColor = Color.Black;
            buttonFgColor = Color.Yellow;
        }
        else
        {
            bgColor = Color.FromArgb(0, 200, 0);
            fgColor = Color.White;
            buttonBgColor = Color.FromArgb(0, 120, 215);
            buttonFgColor = Color.White;
        }

        // Form
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.BackColor = bgColor;
        this.Size = new Size(Constants.ScheduledMessageWidth, Constants.ScheduledMessageHeight);

        // Position bottom-right
        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            this.Location = new Point(
                screen.WorkingArea.Right - this.Width - Constants.ScheduledMessageMargin,
                screen.WorkingArea.Bottom - this.Height - Constants.ScheduledMessageMargin
            );
        }

        // Message label
        messageLabel = new Label
        {
            Text = scheduleItem.Label,
            Font = new Font(Constants.DefaultFontName, Constants.ScheduledMessageFontSize, FontStyle.Bold),
            ForeColor = fgColor,
            AutoSize = false,
            Size = new Size(320, 70),
            Location = new Point(15, 15),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(messageLabel);

        // OK button
        confirmButton = new Button
        {
            Text = "OK",
            Font = new Font(Constants.DefaultFontName, 10, FontStyle.Bold),
            Size = new Size(100, 35),
            Location = new Point(235, 95),
            BackColor = buttonBgColor,
            ForeColor = buttonFgColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        confirmButton.FlatAppearance.BorderSize = 0;
        confirmButton.Click += (s, e) =>
        {
            MessageConfirmed?.Invoke(this, EventArgs.Empty);
            this.Close();
        };
        this.Controls.Add(confirmButton);

        this.ResumeLayout(false);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            MessageConfirmed?.Invoke(this, EventArgs.Empty);
            this.Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            confirmButton?.Dispose();
            messageLabel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
