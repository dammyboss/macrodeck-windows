using System.Text.Json.Serialization;

namespace MacroDeck.Models;

public enum RecordedEventType
{
    MouseMove,
    LeftDown, LeftUp,
    RightDown, RightUp,
    MiddleDown, MiddleUp,
    KeyDown, KeyUp,
    Scroll
}

public class RecordedEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double Timestamp { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecordedEventType Type { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public int? KeyCode { get; set; }
    public uint Flags { get; set; }
    public int? ScrollDeltaY { get; set; }

    public string Label => Type switch
    {
        RecordedEventType.MouseMove => "Move",
        RecordedEventType.LeftDown => "Click L",
        RecordedEventType.LeftUp => "Up L",
        RecordedEventType.RightDown => "Click R",
        RecordedEventType.RightUp => "Up R",
        RecordedEventType.MiddleDown => "Click M",
        RecordedEventType.MiddleUp => "Up M",
        RecordedEventType.KeyDown => "Key \u2193",
        RecordedEventType.KeyUp => "Key \u2191",
        RecordedEventType.Scroll => "Scroll",
        _ => "?"
    };

    public string ChipType => Type switch
    {
        RecordedEventType.MouseMove => "move",
        RecordedEventType.LeftDown or RecordedEventType.LeftUp or
        RecordedEventType.RightDown or RecordedEventType.RightUp or
        RecordedEventType.MiddleDown or RecordedEventType.MiddleUp => "click",
        RecordedEventType.KeyDown or RecordedEventType.KeyUp => "key",
        RecordedEventType.Scroll => "scroll",
        _ => "move"
    };

    public string TargetDescription => Type switch
    {
        RecordedEventType.MouseMove or
        RecordedEventType.LeftDown or RecordedEventType.LeftUp or
        RecordedEventType.RightDown or RecordedEventType.RightUp or
        RecordedEventType.MiddleDown or RecordedEventType.MiddleUp
            => $"@ {X:F0}, {Y:F0}",
        RecordedEventType.KeyDown or RecordedEventType.KeyUp
            => KeyCode.HasValue ? $"vk 0x{KeyCode.Value:X2}" : "key",
        RecordedEventType.Scroll
            => $"\u0394y {ScrollDeltaY ?? 0}",
        _ => ""
    };
}
