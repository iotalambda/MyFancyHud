using System.Runtime.InteropServices;

namespace MyFancyHud;

public class IdleDetectionService
{
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public TimeSpan IdleTimeThreshold { get; set; } = TimeSpan.FromMinutes(Constants.DefaultIdleTimeoutMinutes);
    public string IdleMessage { get; set; } = Constants.DefaultIdleMessage;

    public TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

        if (!GetLastInputInfo(ref lastInputInfo))
        {
            return TimeSpan.Zero;
        }

        var idleTime = Environment.TickCount - lastInputInfo.dwTime;
        return TimeSpan.FromMilliseconds(idleTime);
    }

    public bool IsIdle()
    {
        return GetIdleTime() >= IdleTimeThreshold;
    }
}
