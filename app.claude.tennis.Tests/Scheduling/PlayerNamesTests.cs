using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

public class PlayerNamesTests
{
    private static SchedulePlayer Person(string first, string last) => new(Guid.NewGuid(), first, last, null);

    [Test]
    public void NameOf_FirstNameAndLastName_ShowsFirstNameAndInitial()
    {
        var daryl = Person("Daryl", "dusheiko");

        Assert.That(new PlayerNames([daryl]).NameOf(daryl.Id), Is.EqualTo("Daryl D."));
    }

    [Test]
    public void NameOf_NoLastName_ShowsFirstNameOnly()
    {
        var ann = Person("Ann", string.Empty);

        Assert.That(new PlayerNames([ann]).NameOf(ann.Id), Is.EqualTo("Ann"));
    }

    [Test]
    public void NameOf_TwoWouldLookTheSame_ShowsFullNamesForBothOnly()
    {
        var smith = Person("Ann", "Smith");
        var simms = Person("Ann", "Simms");
        var bob = Person("Bob", "Smith");
        var names = new PlayerNames([smith, simms, bob]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names.NameOf(smith.Id), Is.EqualTo("Ann Smith"));
            Assert.That(names.NameOf(simms.Id), Is.EqualTo("Ann Simms"));
            Assert.That(names.NameOf(bob.Id), Is.EqualTo("Bob S."));
        }
    }

    [Test]
    public void NameOf_UnknownPlayer_ShowsAQuestionMark()
    {
        Assert.That(new PlayerNames([]).NameOf(Guid.NewGuid()), Is.EqualTo("?"));
    }
}
