using D3B.Engine.Criteria;

namespace D3B.Engine.Tests.Criteria;

public sealed class CriterionTests
{
    private static readonly CriterionFacts _facts = new()
    {
        Level = 50,
        Items = new Dictionary<int, int> { [930] = 3 },
        JobLevels = new Dictionary<int, int> { [28] = 12 },
        Subscribed = true,
        MapId = 191104002,
        SubAreaId = 95,
        AreaId = 0,
        Cell = 302,
    };

    private static CriterionAnswer Answer(string text, CriterionFacts? facts = null)
    {
        return Criterion.Parse(text).Evaluate(facts ?? _facts);
    }

    [Theory]
    [InlineData("PL>49", CriterionAnswer.True)]
    [InlineData("PL<50", CriterionAnswer.False)]
    [InlineData("PL=50", CriterionAnswer.True)]
    [InlineData("PL!50", CriterionAnswer.False)]
    [InlineData("PO=930", CriterionAnswer.True)]
    [InlineData("PO!930", CriterionAnswer.False)]
    [InlineData("PO>930,2", CriterionAnswer.True)]
    [InlineData("PO>930,3", CriterionAnswer.False)]
    [InlineData("PO<931,1", CriterionAnswer.True)]
    [InlineData("PJ=28", CriterionAnswer.True)]
    [InlineData("Pj>28,19", CriterionAnswer.False)]
    [InlineData("PJ!24", CriterionAnswer.True)]
    [InlineData("PZ=1", CriterionAnswer.True)]
    [InlineData("Pm=191104002", CriterionAnswer.True)]
    [InlineData("PB!95", CriterionAnswer.False)]
    [InlineData("Po=0", CriterionAnswer.True)]
    [InlineData("PC=302", CriterionAnswer.True)]
    [InlineData("PC=303", CriterionAnswer.False)]
    public void AConditionTheBotKnowsIsAnswered(string text, CriterionAnswer expected)
    {
        Assert.Equal(expected, Answer(text));
    }

    [Theory]
    [InlineData("Qf=2450")]
    [InlineData("Sc=3500")]
    [InlineData("PL~50")]
    [InlineData("PL>a")]
    [InlineData("PO>930,2,1")]
    [InlineData("[413]MC=0")]
    [InlineData("Pz=0")]
    public void AConditionTheBotDoesNotKnowIsUnknown(string text)
    {
        Assert.Equal(CriterionAnswer.Unknown, Answer(text));
    }

    [Fact]
    public void AFactNotKnownIsUnknown()
    {
        var nothing = new CriterionFacts();

        Assert.Equal(CriterionAnswer.Unknown, Answer("PL>49", nothing));
        Assert.Equal(CriterionAnswer.Unknown, Answer("PO=930", nothing));
        Assert.Equal(CriterionAnswer.Unknown, Answer("PZ=1", nothing));
        Assert.Equal(CriterionAnswer.Unknown, Answer("Pm=1", nothing));
    }

    [Fact]
    public void ACellNotChosenYetMeetsAConditionSomeCellMeets()
    {
        var planning = _facts with { Cell = null };

        Assert.Equal(CriterionAnswer.True, Answer("#(PC=4|PC=5|PC=18|PC=19)", planning));
        Assert.Equal(CriterionAnswer.True, Answer("PC!4", planning));
        Assert.Equal(CriterionAnswer.False, Answer("PC=560", planning));
    }

    [Theory]
    [InlineData("PL>49&PZ=1", CriterionAnswer.True)]
    [InlineData("PL>49&PL<20", CriterionAnswer.False)]
    [InlineData("PL<20&Qf=1", CriterionAnswer.False)]
    [InlineData("PL>49&Qf=1", CriterionAnswer.Unknown)]
    [InlineData("PL<20|PZ=1", CriterionAnswer.True)]
    [InlineData("Qf=1|PZ=1", CriterionAnswer.True)]
    [InlineData("Qf=1|PL<20", CriterionAnswer.Unknown)]
    [InlineData("PL<20|PL>60", CriterionAnswer.False)]
    public void GroupsAnswerOnThreeValues(string text, CriterionAnswer expected)
    {
        Assert.Equal(expected, Answer(text));
    }

    [Fact]
    public void AndBindsTighterThanOrAndParenthesesGroup()
    {
        Assert.Equal(CriterionAnswer.True, Answer("PL>60&PL<20|PZ=1"));
        Assert.Equal(CriterionAnswer.False, Answer("PL>60&(PL<20|PZ=1)"));
        Assert.Equal(CriterionAnswer.True, Answer("((Qa=1612&Qo>9480)|Qf=1614|PL=50)&PO>930,0"));
    }

    [Theory]
    [InlineData("(PL>49")]
    [InlineData("PL>49)")]
    [InlineData("PL>49&")]
    [InlineData("P")]
    [InlineData("")]
    public void ATextThatCannotBeReadIsUnknown(string text)
    {
        var criterion = Criterion.Parse(text);

        Assert.False(criterion.IsRead);
        Assert.Equal(CriterionAnswer.Unknown, criterion.Evaluate(_facts));
    }

    [Fact]
    public void ACriterionKeepsItsText()
    {
        var criterion = Criterion.Parse("(Qa=610|Qf=610)");

        Assert.True(criterion.IsRead);
        Assert.Equal("(Qa=610|Qf=610)", criterion.Text);
    }

    [Fact]
    public void AStatesCriterionAsksTheFightersStates()
    {
        var states = new CriterionFacts { States = [7, 135] };

        Assert.Equal(CriterionAnswer.True, Answer("HS=135", states));
        Assert.Equal(CriterionAnswer.False, Answer("HS!7&HS!97&HS!6", states));
        Assert.Equal(CriterionAnswer.True, Answer("HS=3|(HS=7&HS!6)", states));
        Assert.Equal(CriterionAnswer.True, Answer("HS!7", new CriterionFacts { States = [] }));
        Assert.Equal(CriterionAnswer.Unknown, Answer("HS!7", new CriterionFacts()));
    }
}
