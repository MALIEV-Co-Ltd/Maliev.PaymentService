using Xunit;

namespace Maliev.PaymentService.Tests.Integration;

/// <summary>
/// Collection definition to ensure integration tests run sequentially.
/// This prevents multiple test classes from trying to access the database simultaneously,
/// which can cause "database is being accessed by other users" errors.
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection), DisableParallelization = true)]
public class IntegrationTestCollection
{
}
