using Asterion.ComponentBuilders;
using Discord;

namespace Asterion.Test.Search;

[TestFixture]
public class ViewMoreResultsButtonTests
{
    [Test]
    public void LongQueryFitsIntoCustomId()
    {
        var button = ModrinthComponentBuilder.ViewMoreSearchResults(null, new string('a', 300));

        Assert.That(button.CustomId, Has.Length.LessThanOrEqualTo(ComponentBuilder.MaxCustomIdLength));
    }
}
