using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerBackupTests
{
    private static Player Make(string first = "Ann", string last = "Smith", int? rank = null, string comment = "") =>
        new(Guid.NewGuid(), first, last, rank, comment);

    [Test]
    public void FileName_Date_UsesTheSpecFormat() =>
        Assert.That(PlayerBackup.FileName(new DateOnly(2026, 3, 7)), Is.EqualTo("social-tennis-players-2026-03-07.json"));

    [Test]
    public void Read_CreatedBackup_GivesBackTheSamePlayers()
    {
        var players = new List<Player> { Make("Ann", rank: 2, comment: "Left handed"), Make("Bob", "Jones") };

        var result = PlayerBackup.Read(PlayerBackup.Create(players));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(BackupReadStatus.Ok));
            Assert.That(result.Players, Is.EqualTo(players));
        }
    }

    [Test]
    public void Create_Players_MarksTheFileAsASocialTennisBackup() =>
        Assert.That(PlayerBackup.Create([Make()]), Does.Contain(PlayerBackup.AppMarker));

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json")]
    [TestCase("[1,2,3]")]
    [TestCase(/* lang=json,strict */ "{}")]
    [TestCase(/* lang=json,strict */ "{\"app\":\"something-else\",\"version\":1,\"players\":[]}")]
    [TestCase(/* lang=json,strict */ "{\"app\":\"social-tennis\",\"version\":1}")]
    public void Read_SomethingThatIsNotABackup_IsRejected(string text) =>
        Assert.That(PlayerBackup.Read(text).Status, Is.EqualTo(BackupReadStatus.NotABackup));

    [Test]
    public void Read_Null_IsRejected() =>
        Assert.That(PlayerBackup.Read(null).Status, Is.EqualTo(BackupReadStatus.NotABackup));

    [Test]
    public void Read_TextOverTheLimit_IsTooLarge() =>
        Assert.That(PlayerBackup.Read(new string(' ', PlayerBackup.MaxLength + 1)).Status, Is.EqualTo(BackupReadStatus.TooLarge));

    [Test]
    public void Read_RankAboveThree_BecomesThree()
    {
        var text = "{\"app\":\"social-tennis\",\"version\":1,\"players\":[{\"id\":\"" + Guid.NewGuid() + "\",\"firstName\":\"Ann\",\"lastName\":\"S\",\"rank\":9,\"comment\":\"\"}]}";

        var result = PlayerBackup.Read(text);

        Assert.That(result.Players.Single().Rank, Is.EqualTo(3));
    }

    [Test]
    public void Read_PlayerWithNoName_IsLeftOut()
    {
        var text = "{\"app\":\"social-tennis\",\"version\":1,\"players\":[{\"id\":\"" + Guid.NewGuid() + "\",\"firstName\":\" \"},{\"id\":\"" + Guid.NewGuid() + "\",\"firstName\":\"Bob\"}]}";

        var result = PlayerBackup.Read(text);

        Assert.That(result.Players.Select(player => player.FirstName), Is.EqualTo(["Bob"]));
    }

    [Test]
    public void Read_FieldsMissingFromAnOlderBackup_LoadAsEmpty()
    {
        var text = /* lang=json,strict */ "{\"app\":\"social-tennis\",\"version\":1,\"players\":[{\"firstName\":\"Bob\"}]}";

        var player = PlayerBackup.Read(text).Players.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(player.LastName, Is.Empty);
            Assert.That(player.Comment, Is.Empty);
            Assert.That(player.Rank, Is.Null);
            Assert.That(player.Id, Is.Not.EqualTo(Guid.Empty));
        }
    }
}
