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

## 3. What is needed for Azure PaaS deployment

Your directory (`Entra tenant`) ID: `e40f632c-d3b5-415e-bc51-7a06c131e0dd`. **This does not grant deployment rights or identify the Azure subscription.**

The prepared target is **Azure App Service (Linux, .NET 10, B1 or higher)** in an agreed Indian region. The XML file is written to `/home/badri-data/tradeflow.xml` with persistent App Service storage, single app instance, no staging slots, no horizontal scaling, and explicit HTTPS-only. This is short-lived pilot storage, not a recommended financial system of record.

The Azure account owner must identify:
1. Azure **subscription ID**, resource group, App Service name and region.
2. GitHub Actions workload identity/Entra **application registration** with federated credential for environment `azure-pilot` and least-privilege role assignment.
3. GitHub environment secrets `AZURE_CLIENT_ID`, `AZURE_SUBSCRIPTION_ID` (directory ID is in the workflow and is not a password).
4. **Six unique 14+ character passwords**, set **in Azure App Service app settings**, not committed to Git and not pasted into the chat.

### Initial manual provisioning (Azure CLI, run by Azure subscription owner)

```bash
az login --tenant e40f632c-d3b5-415e-bc51-7a06c131e0dd
az account list -o table
az account set --subscription '<YOUR_SUBSCRIPTION_ID>'

# Choose globally unique app name and an existing resource group or create one.
az group create --name rg-badri-pilot --location centralindia
az appservice plan create --name plan-badri-pilot --resource-group rg-badri-pilot --location centralindia --is-linux --sku B1
az webapp create --resource-group rg-badri-pilot --plan plan-badri-pilot --name '<GLOBALLY_UNIQUE_WEBAPP_NAME>' --runtime 'DOTNETCORE:10.0'
az webapp update --resource-group rg-badri-pilot --name '<GLOBALLY_UNIQUE_WEBAPP_NAME>' --https-only true
az webapp config appsettings set --resource-group rg-badri-pilot --name '<GLOBALLY_UNIQUE_WEBAPP_NAME>' --settings BADRI_XML_PATH=/home/badri-data/tradeflow.xml WEBSITES_ENABLE_APP_SERVICE_STORAGE=true ASPNETCORE_ENVIRONMENT=Production
# Set BADRI_PASS_* settings in the Azure Portal or Key Vault references, without printing passwords in a shell command.
# Keep the App Service plan at ONE instance. Do not configure scale-out or slot swaps with XML state.
```

Then configure GitHub environment `azure-pilot` and workload federation. In Actions, manually run **Deploy Badri pilot feature branch to Azure App Service** choosing the feature branch, resource group and app name. The workflow checks app-setting names before uploading a compiled ZIP. Do not enable auto-deploy to production from `main`.

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
