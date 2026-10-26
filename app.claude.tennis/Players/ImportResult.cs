namespace SocialTennis.Players;

/// <summary>What was read from the import text: the players, and the problems that stop it being saved.</summary>
public sealed record ImportResult(IReadOnlyList<ImportedPlayer> Players, IReadOnlyList<ImportError> Errors)
{
    public bool CanSave => Errors.Count == 0 && Players.Count > 0;
}
