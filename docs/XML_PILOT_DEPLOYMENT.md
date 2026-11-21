# Badri TradeFlow — XML Pilot Deployment Guide

**Environment:** A single-trader, single-instance authenticated *review prototype*. It uses an XML file for temporary persistence and reads transcribed examples from the seven historical paper samples. It is **not** a live accounting system, bank integration, or production database. Financial balances begin at ₹0 until someone posts verified bills and payments.

## 1. Workflows to demonstrate to the trader

| Sign in | Page to open | What to test |
|---|---|---|
| Owner | Today, Documents, Stock, Receivables, Cash, Payables, Reports, Audit | See paper exceptions, approve supplier bill, opening stock, approve cash and confirm business controls |
| Accounts | Upload, Purchases, Sales Bills, Collections, Supplier Payments | Three upload types, manual review, record bills, payment method, bank verification, prepare payments |
| Warehouse | Purchases, Inventory, Delivery, Returns | Count goods into GRN only after invoice approval, close GRN, dispatch stock, quarantine return |
| Salesman | My Beat/Delivery, Collections, Cash Count | Confirm delivered quantity, issue receipt, count physical notes, submit cash to owner |
| Retailer | My Order and Receipt | Only demo Kumar Maligai; acknowledge its delivery; view payment status |
| SaaS Operator | Platform | Plan/tenant usage only, no retailer financial data |

### Source-data rule

Sample PA Traders ₹57,977 and Maha ME0398 ₹26,059 are invoice *face values*, **not** current payables. Santhosh GK Road ₹1,66,040 handwritten versus ₹1,66,041 transcribed, Vijay ₹34,178 handwritten versus ₹34,186 transcribed and ₹34,172 counted cash, Santhosh ₹1,12,774 handwritten versus ₹1,12,754 transcribed and ₹88,586 notes are **potential review discrepancies only**. No source sheet automatically posts cash, dues or delivery.

Original scanned PDFs and third-party phone/bank/GST identifiers are deliberately **not embedded** in the GitHub repository or deployed seed. To review actual originals, users upload their copies after signing in; files remain privately on the configured storage path, with controlled access.

## 2. Local testing (Docker)

From the **feature branch**, on a machine with Docker Compose and Python 3:

```bash
python tools/generate-demo-accounts.py
# Store the displayed passwords securely. The .env.pilot is untracked and private.
docker compose -f compose.pilot.yml up --build
# Open http://127.0.0.1:5091
```

The generator creates six independent passwords. The names are `owner.badri`, `accounts.badri`, `warehouse.badri`, `salesman.santhosh`, `retailer.demo`, `operator.demo`.

## 3. Existing Azure App Service — exact destination (confirmed by owner)

| Setting | Existing resource |
|---|---|
| Subscription | `Azure subscription 1` |
| Subscription ID | `36ed0aa4-e3dd-404b-b210-4a6e4dc38c74` |
| Directory (Microsoft Entra) ID | `e40f632c-d3b5-415e-bc51-7a06c131e0dd` |
| Resource group | `badritradersPOC` |
| Linux App Service | `badritraders` |
| Azure region | **South India** |
| Runtime | **DOTNETCORE / .NET 10** |
| Plan | **Free F1** — temporary demo only, idle/cold-start and CPU limits |
| Instances | **1** (required for XML) |
| HTTPS only | **Enabled** |
| Expected host | `https://badritraders-hga6f6amdubqbhfb.southindia-01.azurewebsites.net` |
| Deploy source | `feature/azure-identity-and-deployment-foundation` |

**Having a running Azure App Service does NOT mean this pilot code has been deployed.**
Only the GitHub Actions **Deploy Badri XML pilot to existing Azure App Service** workflow
can claim deployment success after Azure login, ZIP deployment and HTTPS/API smoke checks succeed.

**Do not upgrade F1 or provision any extra Azure resource without owner approval.**
This existing code-hosted App Service is the intended target; it is **not** a Docker container deployment.

### 3.1 Create GitHub-to-Azure deployment identity (once, owner/administrator)

From Azure Cloud Shell (Bash), using an account permitted to create app registrations and
grant role assignments:

```bash
set -euo pipefail
SUB=36ed0aa4-e3dd-404b-b210-4a6e4dc38c74
RG=badritradersPOC
APP=badritraders
DIRECTORY=e40f632c-d3b5-415e-bc51-7a06c131e0dd
az login --tenant "$DIRECTORY"
az account set --subscription "$SUB"
AZURE_RESOURCE_ID=$(az webapp show -g "$RG" -n "$APP" --query id -o tsv)

# Create a dedicated identity for this pilot, NOT personal credentials:
CLIENT_ID=$(az ad app create --display-name 'Project-Badri GitHub Pilot Deployer' --query appId -o tsv)
az ad sp create --id "$CLIENT_ID" >/dev/null
OBJECT_ID=$(az ad sp show --id "$CLIENT_ID" --query id -o tsv)

# Scope deployment rights ONLY to the existing target web app:
az role assignment create --assignee-object-id "$OBJECT_ID" \
  --assignee-principal-type ServicePrincipal --role 'Website Contributor' \
  --scope "$AZURE_RESOURCE_ID"

# Federated trust restricts the identity to GitHub's environment-scoped workflow:
cat >/tmp/badri-gh-federation.json <<'JSON'
{
  "name": "badri-gh-azure-pilot",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:Muthukumar123/Project-Badri:environment:azure-pilot",
  "audiences": ["api://AzureADTokenExchange"]
}
JSON
az ad app federated-credential create --id "$CLIENT_ID" \
  --parameters /tmp/badri-gh-federation.json
echo "GitHub environment secret AZURE_CLIENT_ID = $CLIENT_ID"
```

The client ID is not a password. Do not create or share a client secret.

In GitHub repository **Settings → Environments**, create `azure-pilot` and set its
secret `AZURE_CLIENT_ID` to the printed **application client ID**. Restrict the
environment to the feature branch; add required reviewer approval where available.

### 3.2 Configure App Service application settings (once, owner)

In the Azure Portal: **badritraders → Settings → Environment variables → App settings**:

| App Setting | Value / setup |
|---|---|
| `BADRI_XML_PATH` | `/home/badri-data/tradeflow.xml` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | `true` (persist `/home` for demo; verify restart) |
| `BADRI_PASS_OWNER` | One random unique password with at least 14 characters |
| `BADRI_PASS_ACCOUNTS` | A different random unique password |
| `BADRI_PASS_WAREHOUSE` | A different random unique password |
| `BADRI_PASS_SALESMAN` | A different random unique password |
| `BADRI_PASS_RETAILER` | A different random unique password |
| `BADRI_PASS_OPERATOR` | A different random unique password |

**Do not commit, paste into chat or expose these six passwords.** Store them
in a password manager and share individual credentials privately with testers.
The pilot hashes passwords with PBKDF2 inside XML on first launch.
**Changing an App Setting after initial launch does not reset an existing XML password hash;**
password rotation needs a deliberate reset mechanism and should not be improvised.

The six account names are:

| User | Role |
|---|---|
| `owner.badri` | Business owner |
| `accounts.badri` | Purchase and accounts |
| `warehouse.badri` | Warehouse |
| `salesman.santhosh` | Salesman/delivery |
| `retailer.demo` | Retailer (restricted demo shop) |
| `operator.demo` | SaaS operator (metadata only) |

**Free F1 is not production hosting**: no Always On, shared CPU/memory quota,
possible cold starts, no deployment slots, and no managed disaster recovery.
Use only synthetic/testing transactions and redacted historical excerpts.
The XML file is a disposable single-instance POC database, not a supported
multi-vendor financial ledger.

### 3.3 Deploy the feature branch

The **single canonical Azure deployment workflow** is now `.github/workflows/feature-azure-identity-and-deployment-foundation_badritraders.yml`.
It runs on pushes to the feature branch when `pilot/**` or that workflow file changes. It builds
`pilot/Badri.Pilot.csproj`, packages the compiled pilot, uses the GitHub OIDC app registration
created by Azure Deployment Center, validates the exact existing App Service, then deploys and
checks `/health`, the browser UI, and anonymous API HTTP 401. The previous overlapping
`pilot-xml-azure-deploy.yml` workflow was removed.

**Required prerequisites:** The Portal-created OIDC app must be authorized for this branch
and have sufficient permissions on `badritraders`.
The workflow now configures the non-secret XML path / environment, then generates and securely
stores any **missing** six `BADRI_PASS_*` passwords as App Service application settings.
It does **not** rotate passwords that already exist. For existing seeded XML users, do not
change the App Settings to reset the password: XML contains an independent PBKDF2 hash.
The six pilot passwords must be retrieved privately in the Azure Portal and shared only
with appropriate test users. They are **not** included in workflow logs, source code
or artifacts. A previously downloaded password file is not automatically synchronized
with Azure-generated passwords. No Azure passwords
or publishing profiles are committed to source control.

On the next feature-branch push, the workflow runs automatically. Azure Deployment Center may
show a read-only record; always review the related GitHub Actions run for the actual status.
A green *build* does not mean a successful Azure deployment.

### 3.4 If the deployment fails

| GitHub Actions failure | Fix |
|---|---|
| Missing `AZURE_CLIENT_ID` | Add the GitHub environment secret in `azure-pilot` |
| OIDC/AADSTS error | Confirm client ID, directory, federated subject, and app's role assignment |
| Access denied / 403 from Azure API | Grant resource-scoped Website Contributor role to the service principal |
| Missing `BADRI_PASS_*` | Set six different passwords directly in Azure App Settings |
| Wrong runtime/instance count | Do not change without review; target requires `DOTNETCORE|10.0` and one instance |
| App Service ZIP deployment problem | Check SCM/Deployment Center logs and App Service F1 resource limits |
| Startup failure | Check Azure App Service Log Stream without exposing secrets |
| Health/UI check fails | Application did not start correctly; inspect `/health` and startup logs |

### Important deployment limitations

- This XML pilot is not appropriate for multiple concurrent trader tenants. SQL migration is required before commercial rollout.
- If HTTPS-only and forwarded-proxy handling differ, verify login cookie persistence behind App Service TLS termination; never change cookies to HTTP-only publicly.
- Native App Service runtime/zip deployment support and regional availability must be confirmed against Azure subscription capabilities.
- No Azure resources are created by the repository and no app is deployed until the owner connects their subscription and registers a federated GitHub identity.
- Prefer non-public network access / IP restriction and allow only invited testers. This is a limited pilot, **not** the enterprise Entra solution in the separate draft PR.
- GDPR/DPDP notices, customer consent, backup/restore, real GST/IRP, verified bank payments, offline sync and proper accounting controls remain production gates.

## 4. Helpful references

- Azure App Service .NET 10 hosting: https://learn.microsoft.com/azure/app-service/quickstart-dotnetcore
- App Service custom container persistent storage: https://learn.microsoft.com/azure/app-service/configure-custom-container
- GitHub OpenID Connect to Azure: https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect
