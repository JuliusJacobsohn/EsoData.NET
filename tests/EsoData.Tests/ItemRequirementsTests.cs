using EsoData.Items;

namespace EsoData.Tests;

public class ItemRequirementsTests
{
    [Theory]
    [InlineData(358, 50, 50, 160)] // Green CP160 quest reward, e.g. Bonesplinter.
    [InlineData(369, 50, 50, 160)] // Crafted purple CP160.
    [InlineData(125, 50, 50, 10)]
    [InlineData(174, 50, 50, 100)]
    [InlineData(231, 50, 50, 110)]
    [InlineData(307, 50, 50, 150)]
    [InlineData(23, 32, 32, 0)]
    [InlineData(23, 50, 50, 0)]
    [InlineData(999, 50, null, null)]
    public void RequirementsComeFromVariantRatherThanTheRawLevelField(int subtype, int level, int? expectedLevel, int? expectedCp)
    {
        var fields = new long[21]; fields[0] = 900001; fields[1] = subtype; fields[2] = level;
        var item = new ItemLink(fields);
        Assert.Equal(expectedLevel, item.RequiredLevel);
        Assert.Equal(expectedCp, item.RequiredChampionPoints);
    }
}
