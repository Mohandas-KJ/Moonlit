namespace Moonlit.Models;

/// <summary>
/// Status state levels corresponding to the dashboard indicator.
/// </summary>
public enum StatusLevel
{
    NotReady,   // Red
    Attention,  // Amber
    Ready       // Green
}

/// <summary>
/// Encapsulates the evaluation of application readiness and health.
/// </summary>
public sealed record StatusInfo(
    StatusLevel Level,
    string Header,
    string Explanation,
    string? CorrectiveAction = null
)
{
    public static StatusInfo NotReady(string header, string explanation, string? correctiveAction = null) =>
        new(StatusLevel.NotReady, header, explanation, correctiveAction);

    public static StatusInfo Attention(string header, string explanation, string? correctiveAction = null) =>
        new(StatusLevel.Attention, header, explanation, correctiveAction);

    public static StatusInfo Ready(string header, string explanation) =>
        new(StatusLevel.Ready, header, explanation);
}
