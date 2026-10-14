using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void Project_name_is_PennCraft()
    {
        Assert.Equal("PennCraft", typeof(App).Assembly.GetName().Name);
    }
}
