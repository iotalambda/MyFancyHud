namespace MyFancyHud;
public record Schedule(TimeOnly StartsAt, TimeOnly EndsAt, List<Schedule.Item> ScheduleItems)
{
    public record Item(TimeOnly At, string Label, Item.Kind ItemKind)
    {
        public enum Kind { Alert, Success };
    }
}