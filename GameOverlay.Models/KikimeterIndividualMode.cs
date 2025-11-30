using Newtonsoft.Json;

namespace GameOverlay.Models;

public class KikimeterIndividualMode
{
    [JsonProperty("IndividualMode")]
    public bool IndividualMode { get; set; } = false;
}






