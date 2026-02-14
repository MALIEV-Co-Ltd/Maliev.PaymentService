using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.PaymentService.Tests.Infrastructure;

/// <summary>Integrity tests.</summary>
public class ModelIntegrityTests
{
    /// <summary>Check for pending migrations.</summary>
    [Fact]
    public void Model_ShouldNotHavePendingChanges()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelCheck")
            .Options;

        using var context = new PaymentDbContext(options);
        var hasChanges = context.Database.HasPendingModelChanges();

        Assert.False(hasChanges, "Run 'dotnet ef migrations add <Name> --project Maliev.PaymentService.Infrastructure --startup-project Maliev.PaymentService.Api'");
    }
}
