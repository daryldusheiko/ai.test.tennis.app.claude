using SocialTennis.Settings;

namespace SocialTennis.Tests.Settings;

public class VersionInfoTests
{
    [Test]
    public void VersionText_WithCommit_ShowsTheCommitInBrackets() =>
        Assert.That(new VersionInfo("1.0.42", "abc1234", "2026-03-07").VersionText, Is.EqualTo("Version 1.0.42 (abc1234)"));

    [Test]
    public void VersionText_WithoutCommit_HasNoBrackets() =>
        Assert.That(new VersionInfo("1.0.42", string.Empty, "2026-03-07").VersionText, Is.EqualTo("Version 1.0.42"));

    [Test]
    public void ReleasedText_Date_UsesDayMonthNameYear() =>
        Assert.That(new VersionInfo("1.0.42", "abc1234", "2026-03-07").ReleasedText, Is.EqualTo("Released 7 March 2026"));

    [TestCase("")]
    [TestCase("soon")]
    [TestCase("07/03/2026")]
    public void ReleasedText_NoUsableDate_SaysLocalBuild(string released) =>
        Assert.That(new VersionInfo("1.0.0-dev", "local", released).ReleasedText, Is.EqualTo("Local build"));

    [Test]
    public void Development_IsAFixedLocalBuild()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(VersionInfo.Development.Version, Is.EqualTo(VersionInfo.DevelopmentVersion));
            Assert.That(VersionInfo.Development.ReleasedText, Is.EqualTo("Local build"));
        }
    }
}
