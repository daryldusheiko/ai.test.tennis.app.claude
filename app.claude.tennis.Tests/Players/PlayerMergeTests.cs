using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerMergeTests
{
    private static Player Make(string first, string last = "Smith", Guid? id = null) => new(id ?? Guid.NewGuid(), first, last, null, string.Empty);

    [Test]
    public void Merge_NewPlayers_AreAdded()
    {
        var result = PlayerMerge.Merge([Make("Ann")], [Make("Bob"), Make("Cat")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Added, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(result.Players.Select(player => player.FirstName), Is.EqualTo(["Ann", "Bob", "Cat"]));
        }
    }

    [Test]
    public void Merge_SameNameIgnoringCase_IsSkipped()
    {
        var result = PlayerMerge.Merge([Make("Ann", "Smith")], [Make("ANN", "smith")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Added, Is.Zero);
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(result.Players, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Merge_SameIdWithADifferentName_IsSkipped()
    {
        var id = Guid.NewGuid();

        var result = PlayerMerge.Merge([Make("Ann", id: id)], [Make("Anna", id: id)]);

        Assert.That((result.Added, result.Skipped), Is.EqualTo((0, 1)));
    }

    [Test]
    public void Merge_SameNameTwiceInTheIncomingPlayers_AddsOnlyTheFirst()
    {
        var result = PlayerMerge.Merge([], [Make("Ann"), Make("Ann")]);

        Assert.That((result.Added, result.Skipped), Is.EqualTo((1, 1)));
    }

    [Test]
    public void Merge_SameFirstNameDifferentLastName_IsANewPlayer()
    {
        var result = PlayerMerge.Merge([Make("Ann", "Smith")], [Make("Ann", "Jones")]);

        Assert.That(result.Added, Is.EqualTo(1));
    }
}
