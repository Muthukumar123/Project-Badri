# Project-Badri — deployment readiness (21 November 2026)

**Source:** `Muthukumar123/Project-Badri`, merged `main` commit `34a0906`.
**Status:** Main CI passes; full application is **NOT deployed**. This branch adds a single-container **localhost-only** preview and a future Azure deployment plan, not a public SaaS release.

## Why we have NOT deployed publicly
The source is the initial R1 collections-first starter, not a production service:
- The backend reads `X-Dev-Tenant`, `X-Dev-User`, `X-Dev-Role` from the client. These are forgeable and not authentication.
- The backend intentionally refuses to run outside `Development`. Never remove this gate without implementing a complete production identity and authorization flow.
- There are no Azure resources, subscriptions, credentials, GitHub OIDC federation or secure environment variables configured in this repository.
- State is stored in local SQLite using `Database.EnsureCreated()`; this is not a production data store/migration strategy.
- The UI has a synthetic tenant and no MSAL/OIDC. The document upload tiles save **metadata only**, not actual PDFs or OCR.
- The UPI/bank verification endpoint `verify-demo` is a simulation. Never use it with real financial transactions.
- Stock, supplier accounts payable, delivery/POD, subscription billing and more are still unfinished (see reviewed v2 specification).

## What works now: LOCAL preview
Install Docker Desktop or Docker Engine with Compose (the repo's GitHub Action can compile the image):
```bash
git clone https://github.com/Muthukumar123/Project-Badri.git
cd Project-Badri
git checkout deployment/local-preview-and-azure-readiness
docker compose -f compose.preview.yml up --build
# On the same machine open http://127.0.0.1:5090
# To shut down: docker compose -f compose.preview.yml down
```
The container is bound to `127.0.0.1`, not a public IP. Use **synthetic test data only**. The volume `badri-preview-data` is local SQLite state and is not backed up or encrypted as a production financial database.

## Target production architecture (not implemented yet)

| Layer | Proposed Azure resource | Release requirement |
|---|---|---|
| UI | Azure Static Web Apps or same-origin front-end behind API ingress | Secure frontend login via OIDC, Tamil UAT |
| API | Azure Container Apps or App Service | Validated JWT/OIDC identity, server-derived tenant/role permissions, no demo endpoints |
| Database | Azure SQL | EF Core migrations, transaction/audit/backup/restore, managed identity |
| PDF originals | Private Azure Blob Storage | Delegated access scoped to tenant, scan/size checks, retention, no arbitrary public URLs |
| PDF processing | Queue + .NET worker + Azure AI Document Intelligence | Human review before financial/stock posting |
| Monitoring | Azure Monitor + Application Insights | Traces without personal invoice detail, alerts |
| Delivery | GitHub Actions → federated Azure credentials (OIDC) | Environment approvals, secrets/config in GitHub/Azure, rollback |
| Security | Entra External ID / OIDC and independent pen-test | MFA for owners, RBAC, rate limits, end-to-end isolation tests |

**Region:** discuss South India/Central India and actual Azure resource availability with the owner. Choose the data region before uploading real invoices; do not assume that secondary region equals a configured data residency/DR policy.

## Minimum sequence to unlock a PUBLIC deployment
1. Obtain the trader owner's permission to process real records, and confirm the Azure subscription/region/domain and accountable operator.
2. Configure Entra/OIDC, account invitations, MFA and authoritative tenant membership; remove `X-Dev-*` trust. Put backend in Production and verify it refuses unsigned/mismatched tokens.
3. Replace SQLite/EnsureCreated with a provisioned Azure SQL database, EF migrations, safe financial ledger posting, restore testing, stored secret management and auditing.
4. Remove or disable the simulated UPI clearance endpoint; implement bank evidence/reconciliation and cash controls.
5. Add the actual three document file pipelines with tenant-scoped Blob permissions and human review.
6. Build Azure infrastructure as code, GitHub environment secrets, OIDC workload federation and gated deploy jobs; verify cost and disaster recovery.
7. Pass trader UAT using verified document examples, security testing and no cross-tenant leaks.
8. Only then publish a public Azure URL and enable real customer use.

## Current CI
Main-branch build-and-test succeeded: https://github.com/Muthukumar123/Project-Badri/actions/runs/44950595679 .
This establishes that the starter compiled and unit tests passed on that commit, **not** that it is ready to deploy or secure.

## References
- Azure Container Apps deployment: https://learn.microsoft.com/en-us/azure/container-apps/containerapp-up
- Azure Container Apps with GitHub Actions: https://learn.microsoft.com/en-us/azure/container-apps/github-actions
- GitHub OIDC for Azure login: https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect
