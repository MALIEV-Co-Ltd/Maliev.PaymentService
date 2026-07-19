# PaymentService immutable image promotion

PaymentService publishes container images without changing GitOps state. The development workflow builds the image once; staging and production copy the same OCI manifest digest into dedicated Artifact Registry repositories. A successful workflow run does not enable an Argo CD application or deploy a pod.

The development workflow first calls the repository's secretless `_build-and-test.yml` quality gate. Staging and production deliberately do not call that gate or rebuild the source: they only verify and promote the already-built registry digest.

## Promotion chain

1. A protected `develop` push builds `maliev-payment-service:dev-<commit-sha>` in `maliev-payment-artifact-dev` and records the registry digest.
2. A protected `release/vX.Y.Z` tag resolves the development image for the tagged commit and copies that exact digest to `maliev-payment-artifact-staging:X.Y.Z`.
3. An operator starts the production workflow with the staged version and its complete `sha256:<64 hex characters>` digest. The protected `production` environment must approve the run before the digest is copied to `maliev-payment-artifact-prod:X.Y.Z`.
4. A separate, reviewed GitOps change may later reference the digest in a disabled overlay. These workflows intentionally do not edit `maliev-gitops`.

The staging and production workflows fail if an existing release tag resolves to another digest. They never invoke a Docker build. Artifact lookup is fail-closed: only a `gcloud` response explicitly classified as `NOT_FOUND` permits first-time tag creation; authentication, authorization, API, malformed-output, and other lookup failures stop promotion.

The build workflows check out MessagingContracts commit `0bcd4c704d842211c5ff9bd6b9c4b3aacfcbd8e7` and Aspire commit `7121d57705fc1eff6c7ebb6a69e33e9c26ebfccc` with credential persistence disabled before reconstructing local packages. A missing or different checkout fails package preparation.

## Required GitHub configuration

Create the `development`, `staging`, and `production` GitHub Environments. Define these variables in each environment:

| Variable | Value |
| --- | --- |
| `GCP_PROJECT_ID` | Google Cloud project that owns the registries |
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | Full provider resource name: `projects/<number>/locations/global/workloadIdentityPools/<pool>/providers/<provider>` |
| `GCP_SERVICE_ACCOUNT` | Environment-specific service-account email |

Require a human reviewer for `production`. Restrict its deployment branches/tags to the protected release process. Protect `develop`, `main`, and `release/v*`; the production workflow must be run from a reviewed `main` revision.

Do not configure a service-account JSON key. After all workflows have migrated, remove the obsolete `GCP_SA_KEY` and PaymentService-specific `GITOPS_PAT` repository secrets.

## Required Google Cloud configuration

Create three Docker repositories in `asia-southeast1` with immutable tags enabled:

- `maliev-payment-artifact-dev`
- `maliev-payment-artifact-staging`
- `maliev-payment-artifact-prod`

Use one service account per GitHub Environment and grant only these repository-level roles:

| Environment identity | Source access | Target access |
| --- | --- | --- |
| development | none | Artifact Registry Writer on `maliev-payment-artifact-dev` |
| staging | Artifact Registry Reader on `maliev-payment-artifact-dev` | Artifact Registry Writer on `maliev-payment-artifact-staging` |
| production | Artifact Registry Reader on `maliev-payment-artifact-staging` | Artifact Registry Writer on `maliev-payment-artifact-prod` |

Grant each GitHub principal `roles/iam.workloadIdentityUser` on only its environment service account. The provider and service-account bindings must at least constrain the immutable GitHub claims for organization ID `166822242`, repository ID `1047465523`, and the relevant environment subject:

- `repo:MALIEV-Co-Ltd/Maliev.PaymentService:environment:development`
- `repo:MALIEV-Co-Ltd/Maliev.PaymentService:environment:staging`
- `repo:MALIEV-Co-Ltd/Maliev.PaymentService:environment:production`

Do not authorize by mutable repository name alone. No identity needs Kubernetes, Argo CD, or GitOps repository write access for image publication.

## Release verification

Before production approval, copy the digest printed by the staging workflow summary and independently verify it:

```bash
gcloud artifacts docker images describe \
  asia-southeast1-docker.pkg.dev/<project>/maliev-payment-artifact-staging/maliev-payment-service:X.Y.Z \
  --format='value(image_summary.digest)'
```

Pass the complete result as `expected_digest`. After promotion, verify that the staging and production tag queries return the same digest. A later GitOps manifest must reference `image@sha256:...`, not only the SemVer tag.
