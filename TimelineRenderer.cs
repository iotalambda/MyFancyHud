using System.Text;

namespace MyFancyHud;

public class TimelineRenderer
{
    private const int MinutesPerCharacter = 10;

    public class TimelineChar
    {
        public char Character { get; set; } = '█';
        public Color BaseColor { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsPast { get; set; }
        public string? LabelAbove { get; set; }
        public bool IsLabelStrikethrough { get; set; }
    }

    public static List<TimelineChar> GenerateTimeline(Schedule schedule, TimeOnly currentTime)
    {
        var timeline = new List<TimelineChar>();

        // Calculate total characters needed
        var totalMinutes = (int)(schedule.EndsAt - schedule.StartsAt).TotalMinutes;
        var totalChars = totalMinutes / MinutesPerCharacter;

        // Sort schedule items by time
        var sortedItems = schedule.ScheduleItems.OrderBy(x => x.At).ToList();

        for (int i = 0; i < totalChars; i++)
        {
            var charTime = schedule.StartsAt.AddMinutes(i * MinutesPerCharacter);
            var charEndTime = charTime.AddMinutes(MinutesPerCharacter);

            var timelineChar = new TimelineChar();

            // Determine if this character represents current time
            timelineChar.IsCurrent = currentTime >= charTime && currentTime < charEndTime;
            timelineChar.IsPast = currentTime >= charEndTime;

            // Determine base color based on schedule items
            timelineChar.BaseColor = GetBaseColorForTime(charTime, sortedItems, schedule);

            // Check if there's a label at this position
            var itemAtThisTime = sortedItems.FirstOrDefault(item =>
                item.At >= charTime && item.At < charEndTime);

            if (itemAtThisTime != null)
            {
                timelineChar.LabelAbove = itemAtThisTime.Label;
                timelineChar.IsLabelStrikethrough = currentTime > itemAtThisTime.At;
            }

            timeline.Add(timelineChar);
        }

        return timeline;
    }

    private static Color GetBaseColorForTime(TimeOnly time, List<Schedule.Item> sortedItems, Schedule schedule)
    {
        // Default gray color for periods outside schedule items
        Color grayColor = Color.FromArgb(128, 128, 128);
        Color greenColor = Color.FromArgb(0, 200, 0);

        // Before first item or after last item = gray
        if (sortedItems.Count == 0)
            return grayColor;

        if (time < sortedItems.First().At || time >= sortedItems.Last().At)
            return grayColor;

        // Find which segment this time falls into
        for (int i = 0; i < sortedItems.Count - 1; i++)
        {
            var currentItem = sortedItems[i];
            var nextItem = sortedItems[i + 1];

            if (time >= currentItem.At && time < nextItem.At)
            {
                // Even index (0, 2, 4...) = green (active period)
                // Odd index (1, 3, 5...) = gray (break period)
                return i % 2 == 0 ? greenColor : grayColor;
            }
        }

        return grayColor;
    }

    public static Color DarkenColor(Color baseColor, double factor = 0.5)
    {
        return Color.FromArgb(
            baseColor.A,
            (int)(baseColor.R * factor),
            (int)(baseColor.G * factor),
            (int)(baseColor.B * factor)
        );
    }

    public static Color GetBlinkColor(Color baseColor, bool isYellowPhase)
    {
        return isYellowPhase ? Color.Yellow : baseColor;
    }
}
