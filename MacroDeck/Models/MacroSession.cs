using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroDeck.Models;

public class MacroSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Session";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RecordedEvent> Events { get; set; } = new();
    public int RepeatCount { get; set; } = 1;
    public double StartDelaySeconds { get; set; }
    public double PlaybackSpeed { get; set; } = 1.0;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HumanizeLevel Humanize { get; set; } = HumanizeLevel.Off;

    public static MacroSession LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var session = JsonSerializer.Deserialize<MacroSession>(json, JsonOpts) ?? new();
        if (string.IsNullOrWhiteSpace(session.Name) || session.Name == "Untitled Session")
        {
            session.Name = Path.GetFileNameWithoutExtension(path);
        }
        return session;
    }

    public void SaveToFile(string path)
    {
        var baseName = Path.GetFileNameWithoutExtension(path);
        if (!string.IsNullOrWhiteSpace(baseName))
            Name = baseName;

        var json = JsonSerializer.Serialize(this, JsonOpts);
        File.WriteAllText(path, json);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

public enum HumanizeLevel { Off, Subtle, Strong }
