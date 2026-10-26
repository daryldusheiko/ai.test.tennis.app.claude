using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerMessagesTests
{
    [TestCase(1, "1 player ready to import.")]
    [TestCase(2, "2 players ready to import.")]
    [TestCase(12, "12 players ready to import.")]
    public void ReadyToImport_Count_UsesTheSpecWording(int count, string expected) =>
        Assert.That(PlayerMessages.ReadyToImport(count), Is.EqualTo(expected));

    [Test]
    public void Imported_NothingSkipped_HasNoBrackets() =>
        Assert.That(PlayerMessages.Imported(5, 0), Is.EqualTo("Imported 5 players."));

    [Test]
    public void Imported_SomeSkipped_SaysHowManyWereAlreadyHere() =>
        Assert.That(PlayerMessages.Imported(3, 2), Is.EqualTo("Imported 3 players (2 already here)."));

    [Test]
    public void Restored_NothingSkipped_HasNoBrackets() =>
        Assert.That(PlayerMessages.Restored(5, 0), Is.EqualTo("Restored 5 players."));

    [Test]
    public void Restored_SomeSkipped_SaysHowManyWereAlreadyHere() =>
        Assert.That(PlayerMessages.Restored(3, 2), Is.EqualTo("Restored 3 players (2 were already here)."));

    [Test]
    public void BackupSaved_InTheFolder_NamesTheFolder() =>
        Assert.That(PlayerMessages.BackupSaved(4, inFolder: true), Is.EqualTo("Backup of 4 players saved in social.tennis.app."));

    [Test]
    public void BackupSaved_ElsewhereOnThePhone_DoesNotNameAFolder() =>
        Assert.That(PlayerMessages.BackupSaved(4, inFolder: false), Is.EqualTo("Backup of 4 players saved."));
}
