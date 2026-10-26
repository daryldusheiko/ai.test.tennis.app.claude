namespace SocialTennis.Players;

/// <summary>One problem found in the import text: where it is (for the red squiggle) and what to tell the user.</summary>
/// <param name="Line">The line number, counting from 1.</param>
/// <param name="Start">The first offending character, counted from the start of the line.</param>
/// <param name="Length">How many characters are offending (at least 1).</param>
/// <param name="Problem">The problem in the spec's wording, for example "a quote is not closed.".</param>
public sealed record ImportError(int Line, int Start, int Length, string Problem)
{
    public string Message => $"Line {Line}: {Problem}";
}
