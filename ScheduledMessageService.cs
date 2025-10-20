namespace MyFancyHud;

public class ScheduledMessageService
{
    public ScheduledMessageService()
    {
    }

    public Schedule.Item? GetScheduledMessageForNow()
    {
        if (ScheduleLoader.Schedule == null)
            return null;

        var now = TimeOnly.FromDateTime(DateTime.Now);

        foreach (var item in ScheduleLoader.Schedule.ScheduleItems)
        {
            // Check if we're within the cooldown window of the scheduled time
            var diff = Math.Abs((now - item.At).TotalSeconds);
            if (diff < Constants.ScheduledMessageCooldownSeconds)
            {
                return item;
            }
        }

        return null;
    }

    public IReadOnlyList<Schedule.Item> GetAllScheduledMessages() =>
        ScheduleLoader.Schedule?.ScheduleItems.AsReadOnly() ?? new List<Schedule.Item>().AsReadOnly();
}
