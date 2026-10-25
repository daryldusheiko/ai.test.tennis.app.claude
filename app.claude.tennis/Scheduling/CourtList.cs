using System.Globalization;

namespace SocialTennis.Scheduling;

public static class CourtList
{
    public static CourtParseResult Parse(string? text)
    {
        var entries = (text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
        {
            return Fail("Enter at least one court number, for example 4,5,6.");
        }

        var courts = new List<int>();
        foreach (var entry in entries)
        {
            if (!int.TryParse(entry, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var court))
            {
                return Fail($"“{entry}” is not a court number.");
            }

            if (court < 1)
            {
                return Fail("Court numbers must be 1 or more.");
            }

            if (courts.Contains(court))
            {
                return Fail($"Court {court} is listed more than once.");
            }

            courts.Add(court);
        }

        courts.Sort();
        return new CourtParseResult(courts, null);
    }

    public static string Format(IEnumerable<int> courts) => string.Join(",", courts);

    private static CourtParseResult Fail(string error) => new([], error);
}
