using EsoData.Addons;
using EsoData.Catalogs;
using EsoData.Lua;

namespace EsoData.Tests;

public class SetCollectionProgressTests
{
    [Fact]
    public void CapturedMasksResolveNamesWithoutAssumingOrdinalBits()
    {
        var catalog = SetCollectionCatalog.Parse(SavedVariables.Parse("""
            LibMultiAccountSetsSavedVariables={EsoDataPieces={[7]={
                {101,8192,'Example Ring'},{102,2,'Example Sword'},{103,1073741824,'Example Staff'}
            }}}
            """));
        var result = SetCollectionProgress.Describe(7, 8194, catalog);
        Assert.Equal(2, result.CollectedCount);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(50, result.ReconstructionCrystals);
        Assert.False(result.Complete);
        Assert.False(result.Pieces.Single(p => p.PieceId == 103).Collected);
        Assert.True(result.Pieces.Single(p => p.Name == "Example Ring").Collected);
        Assert.Equal(25, SetCollectionProgress.Describe(7, 1073750018, catalog).ReconstructionCrystals);
        Assert.Equal(3, GameCatalog.FromJson(catalog.ToJson()).CollectionPieces.Count);
    }

    [Fact]
    public void MissingCoverageDoesNotFabricateCompletionOrCost()
    {
        var absent = SetCollectionProgress.Describe(7, null, null);
        Assert.Null(absent.CollectedCount);
        var noDefinitions = SetCollectionProgress.Describe(7, 9346, null);
        Assert.Equal(4, noDefinitions.CollectedCount);
        Assert.Null(noDefinitions.TotalCount);
        Assert.Null(noDefinitions.ReconstructionCrystals);
        var catalog = new GameCatalog { CollectionPieces = [new(7, 101, 8192)] };
        var unknownBits = SetCollectionProgress.Describe(7, 8194, catalog);
        Assert.Equal(2, unknownBits.UnknownSlotMask);
        Assert.Null(unknownBits.Complete);
        Assert.Null(unknownBits.ReconstructionCrystals);
        Assert.Null(SetCollectionProgress.Describe(7, 0, catalog).ReconstructionCrystals);
        Assert.Equal(25, SetCollectionProgress.Describe(7, 8192, catalog).ReconstructionCrystals);
        Assert.Empty(SetCollectionCatalog.Parse(SavedVariables.Parse("Other={}")).CollectionPieces);
    }

    [Fact]
    public void MalformedCapturedSlotMasksAreRejected()
    {
        Assert.Throws<FormatException>(() => SetCollectionCatalog.Parse(SavedVariables.Parse(
            "LibMultiAccountSetsSavedVariables={EsoDataPieces={[7]={{101,3,'bad'}}}}")));
    }
}
