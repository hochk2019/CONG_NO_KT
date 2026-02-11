using Xunit;

namespace CongNoGolden.Tests.Integration;

[CollectionDefinition("Database")]
public sealed class TestDatabaseCollection : ICollectionFixture<TestDatabaseFixture>
{
}
