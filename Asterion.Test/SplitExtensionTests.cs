using Asterion.Extensions;

namespace Asterion.Test;

[TestFixture]
public class SplitExtensionTests
{
    [Test]
    public void TestSliceOnEmptyArray()
    {
        var array = Array.Empty<int>();
        
        var result = array.Split(10).ToArray();
        
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void TestSplitEvenArray()
    {
        int[] numbers = {1, 2, 3, 4, 5, 6};
        var segments = numbers.Split(2).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(segments, Has.Length.EqualTo(3));
            Assert.That(segments[0], Has.Count.EqualTo(2));
            Assert.That(segments[0].Array, Is.Not.Null);
            Assert.That(segments[0].Array![segments[0].Offset], Is.EqualTo(1));
            Assert.That(segments[0].Array![segments[0].Offset + 1], Is.EqualTo(2));
        });
    }

    [Test]
    public void TestSplitOddArray()
    {
        int[] numbers = {1, 2, 3, 4, 5, 6, 7};
        var segments = numbers.Split(3).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(segments, Has.Length.EqualTo(3));
            Assert.That(segments[0], Has.Count.EqualTo(3));
            Assert.That(segments[0].Array, Is.Not.Null);
            Assert.That(segments[0].Array![segments[0].Offset], Is.EqualTo(1));
            Assert.That(segments[0].Array![segments[0].Offset + 1], Is.EqualTo(2));
            Assert.That(segments[0].Array![segments[0].Offset + 2], Is.EqualTo(3));
            
            // Last segment should have 1 element
            Assert.That(segments[2], Has.Count.EqualTo(1));
        });
    }
}