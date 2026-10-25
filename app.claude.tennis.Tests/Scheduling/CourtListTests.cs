using SocialTennis.Scheduling;

namespace SocialTennis.Tests.Scheduling;

public class CourtListTests
{
    [TestCase("4,5,6", "4,5,6")]
    [TestCase(" 6 , 4,5 ", "4,5,6")]
    [TestCase("7", "7")]
    [TestCase("4,5,", "4,5")]
    public void Parse_ValidList_GivesCourtsInAscendingOrder(string text, string expected)
    {
        var result = CourtList.Parse(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Error, Is.Null);
            Assert.That(CourtList.Format(result.Courts), Is.EqualTo(expected));
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(",,")]
    [TestCase(null)]
    public void Parse_NothingEntered_AsksForACourt(string? text) =>
        Assert.That(CourtList.Parse(text).Error, Is.EqualTo("Enter at least one court number, for example 4,5,6."));

    [TestCase("4,x,6", "“x” is not a court number.")]
    [TestCase("4.5", "“4.5” is not a court number.")]
    [TestCase("99999999999", "“99999999999” is not a court number.")]
    [TestCase("0", "Court numbers must be 1 or more.")]
    [TestCase("4,-2", "Court numbers must be 1 or more.")]
    [TestCase("4,5,4", "Court 4 is listed more than once.")]
    public void Parse_BadEntry_GivesTheSpecMessage(string text, string error)
    {
        var result = CourtList.Parse(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.Courts, Is.Empty);
        }
    }
}
