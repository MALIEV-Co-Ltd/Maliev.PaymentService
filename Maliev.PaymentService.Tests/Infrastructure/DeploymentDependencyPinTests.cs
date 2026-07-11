using System.Xml.Linq;

namespace Maliev.PaymentService.Tests.Infrastructure;

public sealed class DeploymentDependencyPinTests
{
    [Fact]
    public void DeploymentBoundaries_PinVerifiedSharedPackageVersions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var buildProperties = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var propertyGroup = Assert.Single(buildProperties.Root!.Elements("PropertyGroup"));

        Assert.Equal("1.0.81-alpha", propertyGroup.Element("ServiceDefaultsVersion")?.Value);
        Assert.Equal("1.0.91-alpha", propertyGroup.Element("MessagingContractsVersion")?.Value);
        Assert.Null(propertyGroup.Element("SharedLibraryVersion"));

        foreach (var projectFile in Directory.EnumerateFiles(
                     repositoryRoot,
                     "*.csproj",
                     SearchOption.AllDirectories))
        {
            var project = XDocument.Load(projectFile);
            foreach (var packageReference in project.Descendants("PackageReference"))
            {
                var packageName = packageReference.Attribute("Include")?.Value;
                var expectedProperty = packageName switch
                {
                    "Maliev.Aspire.ServiceDefaults" => "$(ServiceDefaultsVersion)",
                    "Maliev.MessagingContracts" => "$(MessagingContractsVersion)",
                    _ => null
                };

                if (expectedProperty is not null)
                {
                    Assert.Equal(expectedProperty, packageReference.Attribute("Version")?.Value);
                }
            }
        }

        var dockerfile = File.ReadAllText(
            Path.Combine(repositoryRoot, "Maliev.PaymentService.Api", "Dockerfile"));
        Assert.Contains("ARG SERVICE_DEFAULTS_VERSION=1.0.81-alpha", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ARG MESSAGING_CONTRACTS_VERSION=1.0.91-alpha", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENV GITHUB_ACTIONS=true", dockerfile, StringComparison.Ordinal);

        foreach (var workflowName in new[]
                 {
                     "_build-and-test.yml",
                     "ci-develop.yml",
                     "ci-main.yml",
                     "ci-staging.yml",
                     "pr-validation.yml"
                 })
        {
            var workflow = File.ReadAllText(
                Path.Combine(repositoryRoot, ".github", "workflows", workflowName));
            Assert.Contains("ServiceDefaultsVersion: 1.0.81-alpha", workflow, StringComparison.Ordinal);
            Assert.Contains("MessagingContractsVersion: 1.0.91-alpha", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("SharedLibraryVersion", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("1.0.*", workflow, StringComparison.Ordinal);
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
