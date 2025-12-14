using Xunit;

namespace TaskManagement.Tests;

/// <summary>
/// Collection definition for database integration tests.
/// Ensures DatabaseFixture is shared across tests in the same collection.
/// </summary>
[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, it just defines the collection
}
