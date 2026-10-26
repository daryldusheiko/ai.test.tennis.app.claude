using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerServiceTests
{
    private InMemoryStorage _storage = new();

    [SetUp]
    public void SetUp() => _storage = new InMemoryStorage();

    private PlayerService Create() => new(_storage);

    private static Player Make(string first = "Ann", string last = "Smith", int? rank = null, string comment = "") =>
        new(Guid.NewGuid(), first, last, rank, comment);

    [Test]
    public async Task GetAllAsync_NothingStored_IsEmpty() =>
        Assert.That(await Create().GetAllAsync(), Is.Empty);

    [Test]
    public async Task SaveAsync_NewPlayer_IsReadBackByANewService()
    {
        var player = Make(rank: 2, comment: "Left handed");
        await Create().SaveAsync(player);

        var loaded = await Create().GetAllAsync();

        Assert.That(loaded, Is.EqualTo([player]));
    }

    [Test]
    public async Task SaveAsync_SameId_ReplacesRatherThanAdds()
    {
        var service = Create();
        var player = Make();
        await service.SaveAsync(player);

        await service.SaveAsync(player with { FirstName = "Anna" });

        var loaded = await service.GetAllAsync();
        Assert.That(loaded.Select(p => p.FirstName), Is.EqualTo(["Anna"]));
    }

    [Test]
    public async Task SaveAsync_PaddedNames_AreTrimmed()
    {
        var service = Create();
        var player = Make(first: "  Ann ", last: " Smith  ");
        await service.SaveAsync(player);

        var saved = await service.GetAsync(player.Id);

        Assert.That(saved?.FullName, Is.EqualTo("Ann Smith"));
    }

    [Test]
    public async Task SaveAsync_QuotesAndUnicode_SurviveTheRoundTrip()
    {
        var player = Make(first: "Zoë \"Z\"", last: "O'Neil – 李", comment: "line1\nline2");
        await Create().SaveAsync(player);

        Assert.That(await Create().GetAsync(player.Id), Is.EqualTo(player));
    }

    [Test]
    public async Task GetAsync_UnknownId_IsNull() =>
        Assert.That(await Create().GetAsync(Guid.NewGuid()), Is.Null);

    [Test]
    public async Task DeleteAsync_ExistingPlayer_RemovesOnlyThatPlayer()
    {
        var service = Create();
        var keep = Make("Bob");
        var remove = Make("Ann");
        await service.SaveAsync(keep);
        await service.SaveAsync(remove);

        await service.DeleteAsync(remove.Id);

        Assert.That(await Create().GetAllAsync(), Is.EqualTo([keep]));
    }

    [Test]
    public async Task DeleteAsync_UnknownId_ChangesNothing()
    {
        var service = Create();
        var player = Make();
        await service.SaveAsync(player);

        await service.DeleteAsync(Guid.NewGuid());

        Assert.That(await service.GetAllAsync(), Is.EqualTo([player]));
    }

    [Test]
    public async Task GetAllAsync_RankAboveThree_IsTreatedAsThree()
    {
        _storage.Values[PlayerService.PlayersKey] = $$"""[{"Id":"{{Guid.NewGuid()}}","FirstName":"Ann","LastName":"","Rank":5,"Comment":""}]""";

        var loaded = await Create().GetAllAsync();

        Assert.That(loaded[0].Rank, Is.EqualTo(3));
    }

    [Test]
    public async Task GetAllAsync_OlderDataWithMissingAndExtraFields_StillLoads()
    {
        _storage.Values[PlayerService.PlayersKey] = $$"""[{"Id":"{{Guid.NewGuid()}}","FirstName":"Ann","Nickname":"A"}]""";

        var loaded = await Create().GetAllAsync();

        Assert.That(loaded.Select(p => p.FullName), Is.EqualTo(["Ann"]));
    }

    [TestCase("not json")]
    [TestCase(/*lang=json,strict*/ "{\"Id\":1}")]
    public async Task GetAllAsync_UnreadableData_IsEmptyRatherThanThrowing(string json)
    {
        _storage.Values[PlayerService.PlayersKey] = json;

        Assert.That(await Create().GetAllAsync(), Is.Empty);
    }

    [Test]
    public async Task GetSortOrderAsync_NothingStored_IsFirstNameAscending() =>
        Assert.That(await Create().GetSortOrderAsync(), Is.EqualTo(PlayerSortOrder.FirstNameAscending));

    [TestCase(PlayerSortOrder.FirstNameDescending)]
    [TestCase(PlayerSortOrder.LastNameAscending)]
    [TestCase(PlayerSortOrder.LastNameDescending)]
    public async Task SetSortOrderAsync_Chosen_IsRememberedByANewService(PlayerSortOrder order)
    {
        await Create().SetSortOrderAsync(order);

        Assert.That(await Create().GetSortOrderAsync(), Is.EqualTo(order));
    }

    [TestCase("garbage")]
    [TestCase("99")]
    public async Task GetSortOrderAsync_UnknownStoredValue_FallsBackToDefault(string stored)
    {
        _storage.Values[PlayerService.SortKey] = stored;

        Assert.That(await Create().GetSortOrderAsync(), Is.EqualTo(PlayerSortOrder.FirstNameAscending));
    }

    [Test]
    public void FullName_NoLastName_HasNoTrailingSpace() =>
        Assert.That(Make(last: string.Empty).FullName, Is.EqualTo("Ann"));
}
