using SocialTennis.Navigation;

namespace SocialTennis.Tests.Navigation;

public class AppStartTests
{
    [TestCase("")]
    [TestCase("/")]
    public void TakePlayCheck_AppOpenedOnPlay_IsTrueOnce(string path)
    {
        var start = new AppStart();
        start.Begin(path);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(start.TakePlayCheck(), Is.True);
            Assert.That(start.TakePlayCheck(), Is.False);
        }
    }

    [TestCase("players")]
    [TestCase("schedule")]
    [TestCase("about")]
    public void TakePlayCheck_AppOpenedOnAnotherPage_IsFalse(string path)
    {
        var start = new AppStart();
        start.Begin(path);

        Assert.That(start.TakePlayCheck(), Is.False);
    }

    [Test]
    public void Begin_CalledAgainLater_IsIgnored()
    {
        var start = new AppStart();
        start.Begin("players");

        start.Begin(string.Empty);

        Assert.That(start.TakePlayCheck(), Is.False);
    }
}
