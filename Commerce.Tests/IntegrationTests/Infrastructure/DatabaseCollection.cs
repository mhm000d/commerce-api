using Xunit;

namespace Commerce.Tests.IntegrationTests.Infrastructure;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>, ICollectionFixture<ApiFactory>
{
    // The purpose of this class is to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<DatabaseFixture> and ICollectionFixture<ApiFactory> interfaces.
}
