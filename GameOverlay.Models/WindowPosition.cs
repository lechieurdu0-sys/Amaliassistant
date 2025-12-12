using Newtonsoft.Json;

namespace GameOverlay.Models;

public class WindowPosition
{
    [JsonProperty("Left")]
    public double Left { get; set; }

    [JsonProperty("Top")]
    public double Top { get; set; }

    [JsonProperty("Width")]
    public double Width { get; set; }

    [JsonProperty("Height")]
    public double Height { get; set; }
}

public class WindowPositions
{
    [JsonProperty("KikimeterWindow")]
    public WindowPosition? KikimeterWindow { get; set; }

    [JsonProperty("KikimeterWindowVertical")]
    public WindowPosition? KikimeterWindowVertical { get; set; }

    [JsonProperty("KikimeterWindowHorizontal")]
    public WindowPosition? KikimeterWindowHorizontal { get; set; }

    [JsonProperty("LootWindow")]
    public WindowPosition? LootWindow { get; set; }
    
    [JsonProperty("SaleNotificationWindow")]
    public WindowPosition? SaleNotificationWindow { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalWindows { get; set; }
}






