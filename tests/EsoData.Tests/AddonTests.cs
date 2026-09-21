using EsoData.Addons;
using EsoData.Items;
using EsoData.Lua;

namespace EsoData.Tests;

public class AddonTests
{
    [Fact]
    public void IifaRetainsLocationsWithoutDoubleCountingSlots()
    {
        var link = CraftedItem.Create(900001, 32, ItemQuality.Epic).ToString();
        var lua = $$$$$$"""
            IIFA_DATABASE = { ["@Example"] = { servers = { EU = {
              CharIdToName = { ["123"] = "Example" }, DBv3 = {
                ["{{{{{{link}}}}}}"] = { itemName="Item", locations = {
                  ["123"]={ bagID=1, bagSlot={ [0]=2, [4]=3 } }, Bank={ bagID=2, bagSlot={ [9]=4 } }
                } }
              }
            } } } }
            """;
        var result = IifaReader.Parse(SavedVariables.Parse(lua));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(9, result.Inventories.SelectMany(x => x.Items).Sum(x => x.Count));
        Assert.Equal(5, Assert.Single(result.Inventories, x => x.CharacterId == "123").Items.Sum(x => x.Count));
        Assert.Single(result.Characters);
    }

    [Fact]
    public void KnowledgeUsesSavedMasterListAndDistinguishesMissingScans()
    {
        var lua = """
            LibCharacterKnowledgeData={formatVersion=999,masterList={api=999999,fieldSize=3,
              recipes="01b01c01d", grimoires="01b00001d", scripts="01b01c"},characters={EU={
              ["1"]={account="@Example", name="Scanned",timestamp=100,recipes="e00000",sc="e:G"},
              ["2"]={account="@Example",name="Unscanned"}
            }}}
            """;
        var data = CharacterKnowledgeReader.Parse(SavedVariables.Parse(lua));
        Assert.Equal(999999, data.Source.ApiVersion);
        var entries = data.Characters[0].Categories["recipes"];
        Assert.Equal([101L, 102L, 103L], entries.Select(x => x.ItemId));
        Assert.Equal(new bool?[] { true, false, true }, entries.Select(x => x.Known));
        Assert.True(data.Characters[0].Categories["scripts"][1].Known);
        Assert.Equal(0, data.Characters[0].Categories["grimoires"][1].ItemId);
        Assert.Empty(data.Characters[1].Categories);
    }

    [Fact]
    public void ResearchPreservesGlobalIndicesAndDoesNotInventCompletedKnowledge()
    {
        var rt = "W" + new string('0', 53) + "6" + "0100uG00S8";
        var lua = $$$$$$"""
            LibCharacterKnowledgeData={masterList={fieldSize=3},diagnostics={researchTraits=324},
              characters={EU={ ["1"]={timestamp=100,rt="{{{{{{rt}}}}}}"} }}}
            """;
        var research = CharacterKnowledgeReader.Parse(SavedVariables.Parse(lua)).Characters[0].Research!;
        Assert.True(research.Traits[0]);
        Assert.False(research.Traits[1]);
        Assert.Equal(3, research.MaxSlots["Blacksmithing"]);
        Assert.Equal(2, research.MaxSlots["Clothing"]);
        var timer = Assert.Single(research.Timers);
        Assert.Equal(TimeSpan.FromSeconds(3600), timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(-200), timer.Remaining(DateTimeOffset.FromUnixTimeSeconds(100), DateTimeOffset.FromUnixTimeSeconds(2100)));
    }

    [Fact]
    public void CollectionsDecode36BitMasksAndUnknownSetIds()
    {
        var data = SetCollectionReader.Parse(SavedVariables.Parse("LibMultiAccountSetsData2={EU={['@Example']='00001a000005W00000'}}"));
        var collection = Assert.Single(data.Accounts);
        Assert.Equal(100, collection.ObservedAt!.Value.ToUnixTimeSeconds());
        Assert.Equal(34359738368L, collection.SetMasks[2]);
        Assert.True(collection.IsCollected(1, 5));
        Assert.False(collection.IsCollected(1, 2));
        Assert.Null(collection.IsCollected(3, 1));
        Assert.Equal(2, collection.CollectedSlotCount(1));
    }

    [Fact]
    public void CollectionChunksJoinInNumericOrderAcrossBoundary()
    {
        var first = CodesEncoding.EncodeInteger(100, 6) + string.Concat(Enumerable.Repeat("000000", 254)) + "000005";
        var lua = $$$$$$"""LibMultiAccountSetsData2={EU={['@Example']={ [2]='W00000', [1]='{{{{{{first}}}}}}' }}}""";
        var collection = Assert.Single(SetCollectionReader.Parse(SavedVariables.Parse(lua)).Accounts);
        Assert.Equal(5, collection.SetMasks[255]);
        Assert.Equal(34359738368L, collection.SetMasks[256]);
        Assert.Throws<FormatException>(() => CodesEncoding.JoinChunks(SavedVariables.ParseTable("{[2]='abc'}")));
    }

    [Fact]
    public void LegacyCollectionIsReadable()
    {
        var data = SetCollectionReader.Parse(SavedVariables.Parse("LibMultiAccountSetsData={EU={['@Example']={timestamp=100,[7]=5}}}"));
        Assert.True(data.Accounts[0].IsCollected(7, 1));
    }

    [Fact]
    public void CspsSavedProfilesReassembleChunksAndKeepInstanceIds()
    {
        var lua = """
            CSPSSavedVariables={EU={['@Example']={['$AccountWide']={charData={['123']={
              ['$lastCharacterName']='Example',profiles={ [1]={name='Tank',lastSaved=100,
                werte={prog={part2='900102:2',part1='900100:0'},pass={part1='900200:2'},scribeStyleSubclass='-*-*-'},
                comp1='64;0;0#900100,-,-,-,-,-#100-20#100,-,-,-;-,-,-,-;-,-,-,-#-#2#0',
                comp2='-#8798292069151066#-'
              } }
            }}}}}}
            """;
        var profile = Assert.Single(CspsReader.Parse(SavedVariables.Parse(lua)).Profiles);
        Assert.Equal("Tank", profile.Name);
        Assert.Equal("8798292069151066", profile.EquipmentUniqueIds);
        Assert.Equal(900100, profile.Build.Skills!.Active[0].AbilityId);
        Assert.Equal(2, profile.Build.Skills.Passive[0].Rank);
        Assert.Equal(64, profile.Build.Attributes!.Health);
    }

    [Fact]
    public void UespCharacterInventoryAndCpUseDifferentIdentityFields()
    {
        var link = CraftedItem.Create(900001, 32, ItemQuality.Epic).ToString();
        var lua = $$$$$$"""
            uespLogSavedVars={Default={['@Example']={charData={data={CharName='Example',CharId='123',AccountName='@Example',
              UniqueAccountName='ServerPC@Example',TimeStamp=100,APIVersion=999999,Level=32,Skills={['1:1:1']={id=900100,rank=2,type='passive'}},
              ChampionPoints2={['Discipline:Star']={skillId=100,id=900200,points=20}},
              EquipSlots={ [0]={link='{{{{{{link}}}}}}'} },Inventory={ [1]='3 {{{{{{link}}}}}} Junk' }
            }}}}}
            """;
        var result = UespLogReader.Parse(SavedVariables.Parse(lua));
        var character = Assert.Single(result.CharacterStates);
        Assert.Equal(100, Assert.Single(character.ChampionAllocations).SkillId);
        Assert.True(Assert.Single(character.Skills).IsPassive);
        Assert.Equal(3, Assert.Single(Assert.Single(result.Inventories).Items).Count);
        Assert.Null(result.Inventories[0].Items[0].Slot);
        Assert.Equal(1, result.Inventories[0].Items[0].SourceIndex);
        Assert.Equal("ServerPC@Example", character.Character.SourceAccountId);
        Assert.Single(character.Equipment);
    }
}
