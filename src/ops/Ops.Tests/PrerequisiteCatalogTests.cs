using Ops.Agent.Services;

namespace Ops.Tests;

public class PrerequisiteCatalogTests
{
    [Fact]
    public void Definitions_HaveUniqueIds()
    {
        var ids = PrerequisiteCatalog.Definitions.Select(x => x.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Definitions_HaveRequiredFields()
    {
        foreach (var def in PrerequisiteCatalog.Definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Id));
            Assert.False(string.IsNullOrWhiteSpace(def.Name));
            Assert.False(string.IsNullOrWhiteSpace(def.DownloadUrl));
        }
    }
}
