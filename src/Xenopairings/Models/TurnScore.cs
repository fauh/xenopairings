using System.Text.Json.Serialization;

namespace Xenopairings.Models;

/// <summary>Per-game-turn score breakdown stored as a JSON array on Match.</summary>
public record TurnScore(
    [property: JsonPropertyName("t")] int Turn,
    [property: JsonPropertyName("p")] int? Primary,
    [property: JsonPropertyName("s")] int? Secondary
);
