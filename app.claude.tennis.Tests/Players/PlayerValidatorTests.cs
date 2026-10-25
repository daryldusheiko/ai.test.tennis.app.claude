using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerValidatorTests
{
    private static Player Make(string first = "Ann", string last = "Smith", int? rank = null, string comment = "") =>
        new(Guid.NewGuid(), first, last, rank, comment);

    [Test]
    public void Validate_ValidPlayer_HasNoErrors() =>
        Assert.That(PlayerValidator.Validate(Make(rank: 2, comment: "Left handed")), Is.Empty);

    [Test]
    public void Validate_NoLastNameOrRank_HasNoErrors() =>
        Assert.That(PlayerValidator.Validate(Make(last: string.Empty)), Is.Empty);

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_MissingFirstName_Required(string first) =>
        Assert.That(PlayerValidator.Validate(Make(first: first)), Is.EqualTo(["First name is required."]));

    [Test]
    public void Validate_FirstNameOf50_IsAllowed() =>
        Assert.That(PlayerValidator.Validate(Make(first: new string('a', 50))), Is.Empty);

    [Test]
    public void Validate_FirstNameOf51_IsTooLong() =>
        Assert.That(PlayerValidator.Validate(Make(first: new string('a', 51))), Is.EqualTo(["First name must be 50 characters or fewer."]));

    [Test]
    public void Validate_LastNameOf51_IsTooLong() =>
        Assert.That(PlayerValidator.Validate(Make(last: new string('a', 51))), Is.EqualTo(["Last name must be 50 characters or fewer."]));

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(-1)]
    public void Validate_RankOutsideOneToThree_IsRejected(int rank) =>
        Assert.That(PlayerValidator.Validate(Make(rank: rank)), Is.EqualTo(["Rank must be between 1 and 3."]));

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Validate_RankOneToThree_IsAllowed(int rank) =>
        Assert.That(PlayerValidator.Validate(Make(rank: rank)), Is.Empty);

    [Test]
    public void Validate_CommentOf500_IsAllowed() =>
        Assert.That(PlayerValidator.Validate(Make(comment: new string('a', 500))), Is.Empty);

    [Test]
    public void Validate_CommentOf501_IsTooLong() =>
        Assert.That(PlayerValidator.Validate(Make(comment: new string('a', 501))), Is.EqualTo(["Comment must be 500 characters or fewer."]));

    [Test]
    public void Validate_SeveralProblems_ReportsAll() =>
        Assert.That(PlayerValidator.Validate(Make(first: string.Empty, rank: 9)), Has.Count.EqualTo(2));
}
