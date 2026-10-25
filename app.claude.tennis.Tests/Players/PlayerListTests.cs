using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerListTests
{
    private static readonly Player[] People =
    [
        Make("Bob", "Adams"),
        Make("ann", "Smith"),
        Make("Cat", "Zed"),
        Make("Ann", "Brown"),
    ];

    private static Player Make(string first, string last) => new(Guid.NewGuid(), first, last, null, string.Empty);

    private static string[] Names(IEnumerable<Player> players) => [.. players.Select(p => p.FullName)];

    [Test]
    public void Sort_FirstNameAscending_OrdersByFirstThenLast() =>
        Assert.That(Names(PlayerList.Sort(People, PlayerSortOrder.FirstNameAscending)), Is.EqualTo(["Ann Brown", "ann Smith", "Bob Adams", "Cat Zed"]));

    [Test]
    public void Sort_FirstNameDescending_ReversesIncludingTies() =>
        Assert.That(Names(PlayerList.Sort(People, PlayerSortOrder.FirstNameDescending)), Is.EqualTo(["Cat Zed", "Bob Adams", "ann Smith", "Ann Brown"]));

    [Test]
    public void Sort_LastNameAscending_OrdersByLast() =>
        Assert.That(Names(PlayerList.Sort(People, PlayerSortOrder.LastNameAscending)), Is.EqualTo(["Bob Adams", "Ann Brown", "ann Smith", "Cat Zed"]));

    [Test]
    public void Sort_LastNameDescending_OrdersByLastReversed() =>
        Assert.That(Names(PlayerList.Sort(People, PlayerSortOrder.LastNameDescending)), Is.EqualTo(["Cat Zed", "ann Smith", "Ann Brown", "Bob Adams"]));

    [Test]
    public void Sort_SameLastName_BreaksTieOnFirstName()
    {
        Player[] players = [Make("Zoe", "Lee"), Make("Amy", "Lee")];

        Assert.That(Names(PlayerList.Sort(players, PlayerSortOrder.LastNameAscending)), Is.EqualTo(["Amy Lee", "Zoe Lee"]));
    }

    [Test]
    public void Sort_UnknownOrder_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerList.Sort(People, (PlayerSortOrder)99));

    [Test]
    public void SortOptions_Listed_UseSpecWording() =>
        Assert.That(PlayerList.SortOptions.Select(o => o.Label), Is.EqualTo(["First name (A–Z)", "First name (Z–A)", "Last name (A–Z)", "Last name (Z–A)"]));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Search_Empty_KeepsEveryone(string? search) =>
        Assert.That(PlayerList.Search(People, search), Has.Exactly(4).Items);

    [Test]
    public void Search_IgnoresCase() =>
        Assert.That(Names(PlayerList.Search(People, "ANN")), Is.EquivalentTo(["Ann Brown", "ann Smith"]));

    [Test]
    public void Search_EveryWordMustMatchFirstOrLastName() =>
        Assert.That(Names(PlayerList.Search(People, "ann sm")), Is.EqualTo(["ann Smith"]));

    [Test]
    public void Search_MatchesInsideLastName() =>
        Assert.That(Names(PlayerList.Search(People, "zed")), Is.EqualTo(["Cat Zed"]));

    [Test]
    public void Search_NoMatch_IsEmpty() =>
        Assert.That(PlayerList.Search(People, "xyz"), Is.Empty);

    [Test]
    public void Suggest_OneLetter_MatchesFirstOrLastNameAndSortsByFirstName() =>
        Assert.That(Names(PlayerList.Suggest(People, "z", new HashSet<Guid>(), 8)), Is.EqualTo(["Cat Zed"]));

    [Test]
    public void Suggest_NothingTyped_SuggestsNobody() =>
        Assert.That(PlayerList.Suggest(People, "  ", new HashSet<Guid>(), 8), Is.Empty);

    [Test]
    public void Suggest_PlayerAlreadyAdded_IsLeftOut() =>
        Assert.That(Names(PlayerList.Suggest(People, "a", new HashSet<Guid> { People[1].Id }, 8)), Is.EqualTo(["Ann Brown", "Bob Adams", "Cat Zed"]));

    [Test]
    public void Suggest_MoreMatchesThanTheLimit_ReturnsOnlyTheLimit()
    {
        var many = Enumerable.Range(0, 20).Select(i => Make($"Pat{i:00}", "Lee")).ToList();

        Assert.That(PlayerList.Suggest(many, "pat", new HashSet<Guid>(), 8), Has.Count.EqualTo(8));
    }
}
