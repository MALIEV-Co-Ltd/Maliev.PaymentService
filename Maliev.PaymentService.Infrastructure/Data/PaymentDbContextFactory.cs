using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.PaymentService.Infrastructure.Data;

/// <summary>
/// Factory for creating PaymentDbContext instances at design-time (for EF Core migrations).
/// This enables running commands like `dotnet ef migrations add` without needing to run the application.
/// </summary>
public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();

        // Use hardcoded connection string for design-time operations
        // This is only used during migration generation, not at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=payment_service_design;Username=postgres;Password=postgres");

        return new PaymentDbContext(optionsBuilder.Options);
    }
}
