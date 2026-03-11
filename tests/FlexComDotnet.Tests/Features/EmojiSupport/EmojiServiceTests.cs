using FlexComDotnet.Core.Features.EmojiSupport.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.EmojiSupport;

public class EmojiServiceTests
{
    private readonly EmojiService _service;

    public EmojiServiceTests()
    {
        _service = new EmojiService();
    }

    [Fact]
    public void GetAll_ShouldReturnNonEmptyList()
    {
        var all = _service.GetAll();
        all.Should().NotBeEmpty();
        all.Count.Should().BeGreaterThan(100);
    }

    [Fact]
    public void GetAll_ShouldHaveUniqueShortcodes()
    {
        var all = _service.GetAll();
        var shortcodes = all.Select(e => e.Shortcode.ToLowerInvariant()).ToList();
        shortcodes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAll_AllEntriesShouldHaveValidData()
    {
        var all = _service.GetAll();
        foreach (var entry in all)
        {
            entry.Emoji.Should().NotBeNullOrEmpty();
            entry.Shortcode.Should().NotBeNullOrEmpty();
            entry.Category.Should().NotBeNullOrEmpty();
            entry.FullShortcode.Should().StartWith(":").And.EndWith(":");
        }
    }

    [Theory]
    [InlineData("smile")]
    [InlineData("heart")]
    [InlineData("fire")]
    [InlineData("rocket")]
    [InlineData("thumbsup")]
    public void GetByShortcode_WithValidShortcode_ShouldReturnEntry(string shortcode)
    {
        var entry = _service.GetByShortcode(shortcode);
        entry.Should().NotBeNull();
        entry!.Shortcode.Should().Be(shortcode);
        entry.Emoji.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetByShortcode_CaseInsensitive()
    {
        var lower = _service.GetByShortcode("smile");
        var upper = _service.GetByShortcode("SMILE");
        var mixed = _service.GetByShortcode("Smile");

        lower.Should().NotBeNull();
        upper.Should().NotBeNull();
        mixed.Should().NotBeNull();
        lower!.Emoji.Should().Be(upper!.Emoji).And.Be(mixed!.Emoji);
    }

    [Fact]
    public void GetByShortcode_WithInvalidShortcode_ShouldReturnNull()
    {
        var entry = _service.GetByShortcode("nonexistent_emoji_xyz");
        entry.Should().BeNull();
    }

    [Fact]
    public void Search_WithPrefix_ShouldReturnMatchingEntries()
    {
        var results = _service.Search("sm");
        results.Should().NotBeEmpty();
        results.Should().Contain(e => e.Shortcode.StartsWith("sm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_ShouldPrioritizePrefixMatches()
    {
        var results = _service.Search("star");
        results.Should().NotBeEmpty();
        // "star" and "star2" should come before "star_struck"
        results[0].Shortcode.Should().StartWith("star");
    }

    [Fact]
    public void Search_ShouldIncludeContainsMatches()
    {
        // "heart" prefix matches: "heart", "heart_eyes", etc.
        // Contains matches: "broken_heart", "two_hearts", etc.
        var results = _service.Search("heart", maxResults: 20);
        results.Should().HaveCountGreaterThan(5);
        results.Should().Contain(e => e.Shortcode == "heart"); // prefix match
        results.Should().Contain(e => e.Shortcode == "broken_heart"); // contains match
    }

    [Fact]
    public void Search_ShouldRespectMaxResults()
    {
        var results = _service.Search("s", maxResults: 5);
        results.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public void Search_WithEmptyPrefix_ShouldReturnEmpty()
    {
        var results = _service.Search("");
        results.Should().BeEmpty();
    }

    [Fact]
    public void Search_WithNullPrefix_ShouldReturnEmpty()
    {
        var results = _service.Search(null!);
        results.Should().BeEmpty();
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var lower = _service.Search("SMILE");
        var upper = _service.Search("smile");

        lower.Should().BeEquivalentTo(upper);
    }

    [Fact]
    public void FullShortcode_ShouldHaveCorrectFormat()
    {
        var entry = _service.GetByShortcode("smile");
        entry.Should().NotBeNull();
        entry!.FullShortcode.Should().Be(":smile:");
    }

    [Theory]
    [InlineData("表情")]
    [InlineData("手势")]
    [InlineData("心形")]
    [InlineData("符号")]
    [InlineData("状态")]
    [InlineData("物品")]
    [InlineData("动物")]
    public void GetAll_ShouldContainCategory(string category)
    {
        var all = _service.GetAll();
        all.Should().Contain(e => e.Category == category);
    }
}
