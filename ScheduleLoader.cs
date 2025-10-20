using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFancyHud;

public class ScheduleLoader : IDisposable
{
    private static readonly Lazy<ScheduleLoader> _instance = new(() => new ScheduleLoader());
    public static Schedule? Schedule => _instance.Value.CurrentSchedule;

    private Schedule? _currentSchedule;
    private readonly System.Threading.Timer _reloadTimer;
    private readonly object _lock = new();

    private void Log(string message)
    {
        try
        {
            // Only log if the directory exists
            var logDir = Path.GetDirectoryName(Constants.ScheduleLogFilePath);
            if (!string.IsNullOrEmpty(logDir) && Directory.Exists(logDir))
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(Constants.ScheduleLogFilePath, logMessage);
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }

    public ScheduleLoader()
    {
        LoadSchedule();
        var reloadInterval = TimeSpan.FromMinutes(Constants.ScheduleReloadIntervalMinutes);
        _reloadTimer = new System.Threading.Timer(
            _ => LoadSchedule(),
            null,
            reloadInterval,
            reloadInterval
        );
    }

    public Schedule? CurrentSchedule
    {
        get
        {
            lock (_lock)
            {
                return _currentSchedule;
            }
        }
    }

    private void LoadSchedule()
    {
        try
        {
            Log("=== Starting LoadSchedule ===");

            if (!File.Exists(Constants.ScheduleFilePath))
            {
                Log($"Schedule file not found at {Constants.ScheduleFilePath}");
                lock (_lock)
                {
                    _currentSchedule = null;
                }
                return;
            }

            string jsonContent = File.ReadAllText(Constants.ScheduleFilePath);
            Log($"JSON Content: {jsonContent}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new TimeOnlyJsonConverter(this),
                    new JsonStringEnumConverter()
                },
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            Log("Starting JSON deserialization");
            var dto = JsonSerializer.Deserialize<ScheduleDto>(jsonContent, options);

            if (dto == null)
            {
                Log("Failed to deserialize schedule - result was null");
                lock (_lock)
                {
                    _currentSchedule = null;
                }
                return;
            }

            Log($"Deserialization successful. Creating Schedule object with {dto.Schedule.Count} items");

            lock (_lock)
            {
                _currentSchedule = new Schedule(
                    PadMinutes: dto.PadMinutes,
                    ScheduleItems: dto.Schedule.Select(i => new Schedule.Item(
                        At: i.At,
                        Label: i.Label,
                        ItemKind: i.Kind
                    )).ToList(),
                    AlarmSoundFile: dto.AlarmSoundFile
                );
            }

            Log($"Schedule loaded successfully from {Constants.ScheduleFilePath} with {dto.Schedule.Count} items");
        }
        catch (JsonException ex)
        {
            Log($"JSON parsing error: {ex.Message}");
            Log($"Path: {ex.Path}, Line: {ex.LineNumber}, Position: {ex.BytePositionInLine}");
            lock (_lock)
            {
                _currentSchedule = null;
            }
        }
        catch (Exception ex)
        {
            Log($"Error loading schedule: {ex.GetType().Name} - {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            lock (_lock)
            {
                _currentSchedule = null;
            }
        }
    }

    public void Dispose()
    {
        _reloadTimer?.Dispose();
    }

    private class ScheduleDto
    {
        public int PadMinutes { get; set; }
        public List<ItemDto> Schedule { get; set; } = new();
        public string? AlarmSoundFile { get; set; }
    }

    private class ItemDto
    {
        public TimeOnly At { get; set; }
        public string Label { get; set; } = string.Empty;
        public Schedule.Item.Kind Kind { get; set; }
    }

    private class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
    {
        private readonly ScheduleLoader _loader;

        public TimeOnlyJsonConverter(ScheduleLoader loader)
        {
            _loader = loader;
        }

        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
            {
                _loader.Log("TimeOnly converter: empty value, returning MinValue");
                return TimeOnly.MinValue;
            }

            _loader.Log($"TimeOnly converter: parsing '{value}'");

            try
            {
                // Parse formats like "8.00" or "08.00" or "8:00"
                var parts = value.Replace(':', '.').Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes))
                {
                    _loader.Log($"TimeOnly converter: successfully parsed to {hours:D2}:{minutes:D2}");
                    return new TimeOnly(hours, minutes);
                }

                // Fallback to standard parsing
                _loader.Log($"TimeOnly converter: using standard parse");
                var result = TimeOnly.Parse(value);
                _loader.Log($"TimeOnly converter: standard parse result {result}");
                return result;
            }
            catch (Exception ex)
            {
                _loader.Log($"TimeOnly converter: error parsing '{value}' - {ex.Message}");
                throw;
            }
        }

        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("HH.mm"));
        }
    }
}
