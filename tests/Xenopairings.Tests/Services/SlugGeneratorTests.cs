using Xenopairings.Services;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class SlugGeneratorTests
{
    private readonly SlugGenerator _sut = new(new TokenGenerator());

    [Fact]
    public void Generate_NormalTitle_ProducesKebabCaseWithSuffix()
    {
        var slug = _sut.Generate("Winter Invitational");
        slug.ShouldMatch(@"^winter-invitational-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public void Generate_NullTitle_ProducesTournamentFallback()
    {
        var slug = _sut.Generate(null);
        slug.ShouldMatch(@"^tournament-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public void Generate_WhitespaceTitle_ProducesTournamentFallback()
    {
        var slug = _sut.Generate("   ");
        slug.ShouldMatch(@"^tournament-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public void Generate_LongTitle_TruncatesKebabPart()
    {
        var title = new string('a', 100);
        var slug = _sut.Generate(title);
        // kebab part should be at most 40 chars, plus dash, plus 6 char suffix
        slug.Length.ShouldBeLessThanOrEqualTo(47);
    }

    [Fact]
    public void Generate_TitleWithSpecialChars_StripsNonAlphanumeric()
    {
        var slug = _sut.Generate("GT Season 4!");
        slug.ShouldStartWith("gt-season-4-");
    }

    [Fact]
    public void Generate_ProducesUniqueSlugsSameTitle()
    {
        var slugs = Enumerable.Range(0, 50)
            .Select(_ => _sut.Generate("Same Title"))
            .ToList();

        slugs.Distinct().Count().ShouldBe(50);
    }
}
