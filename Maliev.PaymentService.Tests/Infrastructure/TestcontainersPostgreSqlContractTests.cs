using System.Xml.Linq;

namespace Maliev.PaymentService.Tests.Infrastructure;

public sealed class TestcontainersModuleContractTests
{
    [Fact]
    public void TestcontainersModules_UseVersion413ImageBoundBuilders()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testProject = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Maliev.PaymentService.Tests",
            "Maliev.PaymentService.Tests.csproj"));
        foreach (var packageName in new[]
                 {
                     "Testcontainers.PostgreSql",
                     "Testcontainers.RabbitMq",
                     "Testcontainers.Redis"
                 })
        {
            var package = Assert.Single(
                testProject.Descendants("PackageReference"),
                reference => reference.Attribute("Include")?.Value == packageName);
            Assert.Equal("4.13.0", package.Attribute("Version")?.Value);
        }

        foreach (var relativePath in new[]
                 {
                     Path.Combine("Maliev.PaymentService.Tests", "Integration", "TestContainersFixture.cs"),
                     Path.Combine("Maliev.PaymentService.Tests", "Testing", "BaseIntegrationTestFactory.cs")
                 })
        {
            var source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
            Assert.Contains("new PostgreSqlBuilder(\"postgres:18-alpine\")", source, StringComparison.Ordinal);
            Assert.Contains("new RedisBuilder(\"redis:8.4-alpine\")", source, StringComparison.Ordinal);
            Assert.Contains("new RabbitMqBuilder(\"rabbitmq:4.2-alpine\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new PostgreSqlBuilder()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new RedisBuilder()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new RabbitMqBuilder()", source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Maliev.PaymentService.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the PaymentService repository root.");
    }
}
