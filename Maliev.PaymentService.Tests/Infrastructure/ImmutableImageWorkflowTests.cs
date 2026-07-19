using System.Text.RegularExpressions;

namespace Maliev.PaymentService.Tests.Infrastructure;

public sealed class ImmutableImageWorkflowTests
{
    private static readonly string[] DeploymentWorkflowNames =
    [
        "ci-develop.yml",
        "ci-staging.yml",
        "ci-main.yml"
    ];

    [Fact]
    public void DeploymentWorkflows_UseOidcAndPinnedActionsWithoutLongLivedCloudKeys()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var workflowName in DeploymentWorkflowNames)
        {
            var workflow = ReadWorkflow(repositoryRoot, workflowName);

            Assert.Contains("permissions:", workflow, StringComparison.Ordinal);
            Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
            Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
            Assert.Contains("workload_identity_provider: ${{ vars.GCP_WORKLOAD_IDENTITY_PROVIDER }}", workflow, StringComparison.Ordinal);
            Assert.Contains("service_account: ${{ vars.GCP_SERVICE_ACCOUNT }}", workflow, StringComparison.Ordinal);
            Assert.Contains("project_id: ${{ vars.GCP_PROJECT_ID }}", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("GCP_SA_KEY", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentials_json", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GITOPS_PAT", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secrets.", workflow, StringComparison.OrdinalIgnoreCase);

            var unpinnedActions = Regex.Matches(
                workflow,
                @"uses:\s+[^\s@]+@(?![0-9a-f]{40}(?:\s|$))[^\s]+",
                RegexOptions.CultureInvariant);
            Assert.Empty(unpinnedActions.Select(match => match.Value));
        }

        var qualityGate = ReadWorkflow(repositoryRoot, "_build-and-test.yml");
        AssertExactDependencyCheckouts(qualityGate);
        Assert.Contains("workflow_call:", qualityGate, StringComparison.Ordinal);
        Assert.Contains("contents: read", qualityGate, StringComparison.Ordinal);
        Assert.DoesNotContain("id-token: write", qualityGate, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", qualityGate, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GITOPS_PAT", qualityGate, StringComparison.OrdinalIgnoreCase);

        var unpinnedQualityActions = Regex.Matches(
            qualityGate,
            @"uses:\s+[^\s@]+@(?![0-9a-f]{40}(?:\s|$))[^\s]+",
            RegexOptions.CultureInvariant);
        Assert.Empty(unpinnedQualityActions.Select(match => match.Value));

        AssertExactDependencyCheckouts(ReadWorkflow(repositoryRoot, "ci-develop.yml"));
    }

    [Fact]
    public void DeploymentWorkflows_BuildOnceThenPromoteTheExactDigestWithoutDeploying()
    {
        var repositoryRoot = FindRepositoryRoot();
        var development = ReadWorkflow(repositoryRoot, "ci-develop.yml");
        var staging = ReadWorkflow(repositoryRoot, "ci-staging.yml");
        var production = ReadWorkflow(repositoryRoot, "ci-main.yml");
        var combined = string.Join(Environment.NewLine, development, staging, production);

        Assert.Contains("IMAGE_REPOSITORY: maliev-payment-artifact-dev", development, StringComparison.Ordinal);
        Assert.Contains("IMAGE_NAME: maliev-payment-service", development, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/_build-and-test.yml", development, StringComparison.Ordinal);
        Assert.Contains("docker/build-push-action@", development, StringComparison.Ordinal);
        Assert.Contains("push: true", development, StringComparison.Ordinal);
        Assert.Contains("dev-${{ github.sha }}", development, StringComparison.Ordinal);
        Assert.Contains("digest: ${{ steps.build.outputs.digest }}", development, StringComparison.Ordinal);

        Assert.Contains("SOURCE_REPOSITORY: maliev-payment-artifact-dev", staging, StringComparison.Ordinal);
        Assert.Contains("TARGET_REPOSITORY: maliev-payment-artifact-staging", staging, StringComparison.Ordinal);
        Assert.Contains("IMAGE_NAME: maliev-payment-service", staging, StringComparison.Ordinal);
        Assert.Contains("bash scripts/promote-artifact-image.sh", staging, StringComparison.Ordinal);
        Assert.DoesNotContain("2>/dev/null || true", staging, StringComparison.Ordinal);
        Assert.DoesNotContain("docker build \\", staging, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker/build-push-action", staging, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("workflow_dispatch:", production, StringComparison.Ordinal);
        Assert.Contains("release_version:", production, StringComparison.Ordinal);
        Assert.Contains("expected_digest:", production, StringComparison.Ordinal);
        Assert.Contains("environment: production", production, StringComparison.Ordinal);
        Assert.Contains("test \"$GITHUB_REF\" = \"refs/heads/main\"", production, StringComparison.Ordinal);
        Assert.Contains("SOURCE_REPOSITORY: maliev-payment-artifact-staging", production, StringComparison.Ordinal);
        Assert.Contains("TARGET_REPOSITORY: maliev-payment-artifact-prod", production, StringComparison.Ordinal);
        Assert.Contains("IMAGE_NAME: maliev-payment-service", production, StringComparison.Ordinal);
        Assert.Contains("bash scripts/promote-artifact-image.sh", production, StringComparison.Ordinal);
        Assert.DoesNotContain("2>/dev/null || true", production, StringComparison.Ordinal);
        Assert.DoesNotContain("docker build \\", production, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("docker/build-push-action", production, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("maliev-gitops", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kustomize", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh pr", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argocd", combined, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "bash scripts/tests/promote-artifact-image.test.sh",
            ReadWorkflow(repositoryRoot, "_build-and-test.yml"),
            StringComparison.Ordinal);
        Assert.Contains(
            "bash scripts/tests/promote-artifact-image.test.sh",
            ReadWorkflow(repositoryRoot, "pr-validation.yml"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PullRequestWorkflow_ExposesProtectedBranchAggregateGate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pullRequestWorkflow = ReadWorkflow(repositoryRoot, "pr-validation.yml");
        var aggregateGate = ReadWorkflow(repositoryRoot, "_protected-branch-gate.yml");

        Assert.Contains("protected-branch-gate:", pullRequestWorkflow, StringComparison.Ordinal);
        Assert.Contains("needs: [dependency-packages, build-and-test, container]", pullRequestWorkflow, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/_protected-branch-gate.yml", pullRequestWorkflow, StringComparison.Ordinal);
        Assert.Contains("workflow_call:", aggregateGate, StringComparison.Ordinal);
        Assert.Contains("validate:", aggregateGate, StringComparison.Ordinal);
        Assert.Contains("name: validate", aggregateGate, StringComparison.Ordinal);
    }

    private static string ReadWorkflow(string repositoryRoot, string workflowName) =>
        File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", workflowName));

    private static void AssertExactDependencyCheckouts(string workflow)
    {
        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.MessagingContracts", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: 0bcd4c704d842211c5ff9bd6b9c4b3aacfcbd8e7", workflow, StringComparison.Ordinal);
        Assert.Contains("path: .ci-sources/Maliev.MessagingContracts", workflow, StringComparison.Ordinal);
        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.Aspire", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: 7121d57705fc1eff6c7ebb6a69e33e9c26ebfccc", workflow, StringComparison.Ordinal);
        Assert.Contains("path: .ci-sources/Maliev.Aspire", workflow, StringComparison.Ordinal);
        Assert.True(
            Regex.Matches(workflow, "persist-credentials: false", RegexOptions.CultureInvariant).Count >= 3,
            "The service and both dependency checkouts must disable credential persistence.");
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
