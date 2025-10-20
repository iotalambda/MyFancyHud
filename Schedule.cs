namespace MyFancyHud;
public record Schedule(int PadMinutes, List<Schedule.Item> ScheduleItems, string? AlarmSoundFile = null)
{
    public record Item(TimeOnly At, string Label, Item.Kind ItemKind)
    {
        public enum Kind { StartTracking, EndTracking };
    }

    // Calculate the timeline start and end based on schedule items and padding
    public TimeOnly StartsAt => ScheduleItems.Count > 0
        ? ScheduleItems.Min(i => i.At).AddMinutes(-PadMinutes)
        : TimeOnly.MinValue;

    public TimeOnly EndsAt => ScheduleItems.Count > 0
        ? ScheduleItems.Max(i => i.At).AddMinutes(PadMinutes)
        : TimeOnly.MaxValue;

    // Check if current time is within a tracking period (between StartTracking and EndTracking)
    public bool IsCurrentlyTracking(TimeOnly currentTime)
    {
        // Find the most recent StartTracking before or at current time
        var lastStart = ScheduleItems
            .Where(i => i.ItemKind == Item.Kind.StartTracking && i.At <= currentTime)
            .OrderByDescending(i => i.At)
            .FirstOrDefault();

        if (lastStart == null)
            return false; // No StartTracking found before current time

        // Find the first EndTracking after the lastStart
        var nextEnd = ScheduleItems
            .Where(i => i.ItemKind == Item.Kind.EndTracking && i.At > lastStart.At)
            .OrderBy(i => i.At)
            .FirstOrDefault();

        if (nextEnd == null)
            return false; // StartTracking without EndTracking means not tracking

        // Check if current time is before the EndTracking
        return currentTime < nextEnd.At;
    }
}