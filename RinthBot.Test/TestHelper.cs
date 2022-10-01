using RinthBot.Services.Modrinth;

namespace RinthBot.Test;

public class Tests
{
    private ModrinthHelper _helper;
    
    [SetUp]
    public void Setup()
    {
        _helper = new ModrinthHelper();
    }

    [Test]
    public void Test_ParsingModFromUrl()
    {
        var valid = _helper.TryParseProjectSlugOrId("https://modrinth.com/modpack/sop", out var id);
        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.EqualTo(true));
            Assert.That(id, Is.EqualTo("sop"));
        });
    }
}