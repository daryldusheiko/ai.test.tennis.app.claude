namespace SocialTennis.Players;

/// <summary>
/// Reads the text of the Import dialog (see "Import" in spec.md): one player per line, as first name, last name and an optional
/// rank, separated by commas. Any value may be in double quotes, and two quotes inside quotes are one literal quote.
/// </summary>
public static class PlayerImport
{
    public static ImportResult Parse(string? text)
    {
        var players = new List<ImportedPlayer>();
        var errors = new List<ImportError>();
        var lines = (text ?? string.Empty).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var lineErrors = ParseLine(line, index + 1, out var player);
            if (lineErrors.Count > 0)
            {
                errors.AddRange(lineErrors);
            }
            else if (player is not null)
            {
                players.Add(player);
            }
        }

        return new ImportResult(players, errors);
    }

    private static List<ImportError> ParseLine(string line, int number, out ImportedPlayer? player)
    {
        player = null;
        ImportError Error(int start, int length, string problem)
        {
            return new(number, start, Math.Max(1, length), problem);
        }

        var values = new List<Value>();
        var position = 0;
        while (true)
        {
            while (position < line.Length && line[position] is ' ' or '\t')
            {
                position++;
            }

            if (position < line.Length && line[position] == '"')
            {
                var open = position;
                var (text, end) = ReadQuoted(line, position);
                if (end < 0)
                {
                    return [Error(open, line.Length - open, "a quote is not closed.")];
                }

                position = end;
                while (position < line.Length && line[position] is ' ' or '\t')
                {
                    position++;
                }

                if (position < line.Length && line[position] != ',')
                {
                    var next = line.IndexOf(',', position);
                    return [Error(position, (next < 0 ? line.Length : next) - position, "there is text after a closing quote.")];
                }

                values.Add(new Value(text, open, end - open));
            }
            else
            {
                var next = line.IndexOf(',', position);
                var stop = next < 0 ? line.Length : next;
                var raw = line[position..stop];
                var trimmed = raw.Trim();
                if (trimmed.Contains('"'))
                {
                    return [Error(position + (raw.Length - raw.TrimStart().Length), trimmed.Length, "quotes must go around the whole value.")];
                }

                values.Add(new Value(trimmed, position, raw.Length));
                position = stop;
            }

            if (position < line.Length && line[position] == ',')
            {
                position++;
                continue;
            }

            break;
        }

        return Check(values, line, number, out player);
    }

    private static List<ImportError> Check(List<Value> values, string line, int number, out ImportedPlayer? player)
    {
        player = null;
        ImportError Error(int start, int length, string problem)
        {
            return new(number, start, Math.Max(1, length), problem);
        }

        if (values.Count < 2)
        {
            return [Error(0, line.Length, "enter a first name and a last name, separated by a comma.")];
        }

        if (values.Count > 3)
        {
            var extra = values[3];
            return [Error(extra.Start, line.Length - extra.Start, "too many values: only first name, last name and rank.")];
        }

        var errors = new List<ImportError>();
        var (first, last) = (values[0], values[1]);
        if (first.Text.Length == 0)
        {
            errors.Add(Error(first.Start, first.Length, "the first name is missing."));
        }

        foreach (var name in new[] { first, last })
        {
            if (name.Text.Length > PlayerValidator.MaxNameLength)
            {
                errors.Add(Error(name.Start, name.Length, "names must be 50 characters or fewer."));
            }
        }

        int? rank = null;
        if (values.Count == 3 && values[2].Text.Length > 0)
        {
            var value = values[2];
            if (int.TryParse(value.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed is >= Player.MinRank and <= Player.MaxRank)
            {
                rank = parsed;
            }
            else
            {
                errors.Add(Error(value.Start, value.Length, "the rank must be 1 to 3 (or left out)."));
            }
        }

        if (errors.Count == 0)
        {
            player = new ImportedPlayer(first.Text, last.Text, rank);
        }

        return errors;
    }

    // Returns the unquoted text and the position after the closing quote, or -1 when the quote is never closed.
    private static (string Text, int End) ReadQuoted(string line, int open)
    {
        var text = new System.Text.StringBuilder();
        var position = open + 1;
        while (position < line.Length)
        {
            if (line[position] == '"')
            {
                if (position + 1 < line.Length && line[position + 1] == '"')
                {
                    text.Append('"');
                    position += 2;
                    continue;
                }

                return (text.ToString().Trim(), position + 1);
            }

            text.Append(line[position]);
            position++;
        }

        return (string.Empty, -1);
    }

    private sealed record Value(string Text, int Start, int Length);
}
