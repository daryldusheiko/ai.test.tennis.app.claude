namespace SocialTennis.Players;

/// <summary>Checks a player against the rules in the spec. Messages are the exact wording the app shows.</summary>
public static class PlayerValidator
{
    public const int MaxNameLength = 50;

    public const int MaxCommentLength = 500;

    public static IReadOnlyList<string> Validate(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(player.FirstName))
        {
            errors.Add("First name is required.");
        }
        else if (player.FirstName.Trim().Length > MaxNameLength)
        {
            errors.Add("First name must be 50 characters or fewer.");
        }

        if (player.LastName.Trim().Length > MaxNameLength)
        {
            errors.Add("Last name must be 50 characters or fewer.");
        }

        if (player.Rank is < Player.MinRank or > Player.MaxRank)
        {
            errors.Add("Rank must be between 1 and 3.");
        }

        if (player.Comment.Length > MaxCommentLength)
        {
            errors.Add("Comment must be 500 characters or fewer.");
        }

        return errors;
    }
}
