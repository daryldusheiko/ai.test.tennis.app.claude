using SocialTennis.Players;

namespace SocialTennis.Tests.Players;

public class PlayerImportTests
{
    private static readonly ImportedPlayer[] NoPlayers = [];

    [Test]
    public void Parse_FirstAndLastName_ReadsThePlayer()
    {
        var result = PlayerImport.Parse("Daryl, Smith");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Daryl", "Smith", null)]));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.CanSave, Is.True);
        }
    }

    [Test]
    public void Parse_QuotedNames_ReadsTheNamesWithoutTheQuotes()
    {
        var result = PlayerImport.Parse("\"Mary Ann\", \"Jones\"");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Mary Ann", "Jones", null)]));
    }

    [Test]
    public void Parse_WithRank_ReadsTheRank()
    {
        var result = PlayerImport.Parse("Sam, Lee, 2");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Sam", "Lee", 2)]));
    }

    [Test]
    public void Parse_QuotedRank_ReadsTheRank()
    {
        var result = PlayerImport.Parse("\"Jo\", \"De Silva\", \"3\"");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Jo", "De Silva", 3)]));
    }

    [Test]
    public void Parse_EmptyRank_MeansNone()
    {
        var result = PlayerImport.Parse("Sam, Lee,");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Sam", "Lee", null)]));
    }

    [Test]
    public void Parse_EmptyLastName_IsAllowed()
    {
        var result = PlayerImport.Parse("Sam,");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Sam", string.Empty, null)]));
    }

    [Test]
    public void Parse_DoubledQuoteInsideQuotes_IsOneLiteralQuote()
    {
        var result = PlayerImport.Parse("\"Dan \"\"Dino\"\"\", Roe");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Dan \"Dino\"", "Roe", null)]));
    }

    [Test]
    public void Parse_CommaInsideQuotes_StaysInTheName()
    {
        var result = PlayerImport.Parse("\"Smith, Ann\", Jones");

        Assert.That(result.Players, Is.EqualTo([new ImportedPlayer("Smith, Ann", "Jones", null)]));
    }

    [Test]
    public void Parse_SeveralLinesWithBlankOnes_ReadsEveryPlayer()
    {
        var result = PlayerImport.Parse("Ann, Smith\r\n\r\n   \nBob, Jones, 1\n");

        Assert.That(result.Players.Select(player => player.FirstName), Is.EqualTo(["Ann", "Bob"]));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n ")]
    public void Parse_NothingEntered_HasNoPlayersAndCannotSave(string? text)
    {
        var result = PlayerImport.Parse(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Players, Is.EqualTo(NoPlayers));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.CanSave, Is.False);
        }
    }

    [Test]
    public void Parse_QuoteNotClosed_ReportsFromTheQuoteToTheEnd()
    {
        var result = PlayerImport.Parse("Ann, Smith\nBob, \"Jo");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(2, 5, 3, "a quote is not closed.")]));
    }

    [Test]
    public void Parse_TextAfterClosingQuote_ReportsTheExtraText()
    {
        var result = PlayerImport.Parse("\"Ann\"x, Smith");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 5, 1, "there is text after a closing quote.")]));
    }

    [Test]
    public void Parse_QuoteInTheMiddleOfAValue_ReportsTheWholeValue()
    {
        var result = PlayerImport.Parse("An\"n, Smith");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 0, 4, "quotes must go around the whole value.")]));
    }

    [Test]
    public void Parse_TextBeforeAnOpeningQuote_ReportsTheWholeValue()
    {
        var result = PlayerImport.Parse("Ann, x\"Smith\"");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 5, 8, "quotes must go around the whole value.")]));
    }

    [Test]
    public void Parse_OnlyOneValue_AsksForFirstAndLastName()
    {
        var result = PlayerImport.Parse("Ann");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 0, 3, "enter a first name and a last name, separated by a comma.")]));
    }

    [Test]
    public void Parse_TooManyValues_ReportsFromTheFourthValue()
    {
        var result = PlayerImport.Parse("Ann, Smith, 1, extra");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 15, 5, "too many values: only first name, last name and rank.")]));
    }

    [Test]
    public void Parse_MissingFirstName_IsReported()
    {
        var result = PlayerImport.Parse(", Smith");

        Assert.That(result.Errors.Select(error => error.Problem), Is.EqualTo(["the first name is missing."]));
    }

    [Test]
    public void Parse_NameOverFiftyCharacters_ReportsThatName()
    {
        var longName = new string('a', 51);

        var result = PlayerImport.Parse($"Ann, {longName}");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 5, 51, "names must be 50 characters or fewer.")]));
    }

    [Test]
    public void Parse_NameOfFiftyCharacters_IsAccepted()
    {
        var result = PlayerImport.Parse($"{new string('a', 50)}, Smith");

        Assert.That(result.CanSave, Is.True);
    }

    [TestCase("0")]
    [TestCase("4")]
    [TestCase("x")]
    [TestCase("-1")]
    public void Parse_RankOutsideOneToThree_ReportsTheRank(string rank)
    {
        var result = PlayerImport.Parse($"Ann, Smith, {rank}");

        Assert.That(result.Errors, Is.EqualTo([new ImportError(1, 12, rank.Length, "the rank must be 1 to 3 (or left out).")]));
    }

    [Test]
    public void Parse_ErrorsOnSeveralLines_ListsThemInOrderAndKeepsTheGoodPlayers()
    {
        var result = PlayerImport.Parse("Ann, Smith\nBob\nCat, Jones, 9");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors.Select(error => error.Message), Is.EqualTo(
            [
                "Line 2: enter a first name and a last name, separated by a comma.",
                "Line 3: the rank must be 1 to 3 (or left out).",
            ]));
            Assert.That(result.Players, Has.Count.EqualTo(1));
            Assert.That(result.CanSave, Is.False);
        }
    }

    [Test]
    public void Parse_LineWithWindowsLineEnding_IgnoresTheCarriageReturn()
    {
        var result = PlayerImport.Parse("Ann, Smith\r\nBob, Jones\r\n");

        Assert.That(result.Players, Has.Count.EqualTo(2));
    }
}
