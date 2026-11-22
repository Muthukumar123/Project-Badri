# Badri TradeFlow — Simple Three-Role Azure XML Pilot

## User experience (R2)

The application has exactly **three** login identities. All trading examples remain redacted/testing-only.
The app is publicly reachable for pilot testing but not a commercial financial ledger.

| User | Login username | Everyday tasks |
|---|---|---|
| Owner | `owner` | Approve retailer orders, create bills and dispatch goods, reconcile invoices, receive GRN, manage inventory, verify collections and payments, approve cash handover, see outstanding balances/reports. |
| Salesperson | `salesperson` | Take shop order, view deliveries, get receiver acknowledgment, record cash/UPI collection, submit cash denominations for owner approval, see task reminders. |
| Retailer | `retailer` | Place order for demo Kumar Maligai, track order status, confirm shop deliveries, view only own bills and receipts. |

The login names are **not** `owner.badri`, `salesman.santhosh`, or `retailer.demo` any longer. The other three old pilot identities (accounts, warehouse, operator) are retired.

### Changing login passwords in Azure Portal

App Service: `badritraders` → **Settings → Environment variables → App settings**.

| Password key | Username |
|---|---|
| `BADRI_PASS_OWNER` | `owner` |
| `BADRI_PASS_SALESPERSON` (preferred) OR existing `BADRI_PASS_SALESMAN` (fallback) | `salesperson` |
| `BADRI_PASS_RETAILER` | `retailer` |

Every password must be **different**, at least **14 characters**, and must never be put into source control or chat. Azure updates usually restart the App Service. On next app initialization, `PilotStore` synchronizes the **portal-configured passwords** to PBKDF2 hashes in XML; previous password hashes and login cookies become invalid. The previous XML is backed up once under `/home/badri-data/tradeflow.xml.before-three-roles.bak` before initial migration. Do not publish that backup.

The old `BADRI_PASS_ACCOUNTS`, `BADRI_PASS_WAREHOUSE`, and `BADRI_PASS_OPERATOR` App Settings are **no longer consumed**; they can be removed privately in Azure after rollout. The workflow does not rotate or print existing passwords.

### Everyday workflow

1. **Retailer:** Open website → login → **Place order** → choose product, quantity and note → view **New** status.
2. **Owner:** Open **Today** → review **Orders** → accept/decline → **Create bill** for accepted order → physically verify supplier stock/GRN → dispatch.
3. **Salesperson:** Open **My day** → deliveries → record shop's accepted quantity and receiver → record cash or UPI with matching bill → cash handover (count notes).
4. **Owner:** Review pending cash handover and bank evidence; posted balances subtract only cleared receipts. Track outstanding, supplier bills and reports.
5. **Retailer:** Check order status, confirm received quantity, view recorded receipts separately.

**Critical safety rules:** A request to order does not post sales, reduce stock, or collect payment. Supplier invoice approval does not create warehouse stock; only physical GRN does. An uploaded collection sheet does not clear accounts; cash needs owner handover and UPI needs bank evidence. Historical sample values are not current balances.

### Existing Azure deployment target

| Detail | Value |
|---|---|
| Repository | `Muthukumar123/Project-Badri` |
| Feature deployment branch | `feature/azure-identity-and-deployment-foundation` |
| Azure subscription | `36ed0aa4-e3dd-404b-b210-4a6e4dc38c74` |
| Resource group | `badritradersPOC` |
| App Service | `badritraders` |
| Region | South India |
| Runtime | Linux / .NET 10 |
| Plan | Free F1 — limited demo only |
| XML path | `/home/badri-data/tradeflow.xml` |
| Public HTTPS | `https://badritraders-hga6f6amdubqbhfb.southindia-01.azurewebsites.net` |

The workflow `.github/workflows/feature-azure-identity-and-deployment-foundation_badritraders.yml` packages `pilot/Badri.Pilot.csproj`, deploys to the **existing** App Service using GitHub OIDC, checks `/health` and HTTP 401 without a login, and runs read-only live sign-in checks for all three roles. It does not upgrade F1 or add new Azure resources.

### Pilot limitations

- `retailer` intentionally represents only the **Kumar Maligai** demo customer. Other retailers require individually scoped identity, not shared retailer credentials.
- Free F1 and XML are unsuitable for concurrent multi-vendor accounting; plan migration to Azure SQL before commercial rollout.
- Passwords in App Settings plus encrypted cookies are suitable for a **restricted test pilot**, not a mature customer identity system. Require managed authentication and MFA for a production financial product.
- Payment bank verification is manually recorded after external evidence; no actual payment processing is integrated.
- OCR is not integrated; PDF uploads are manual review.
- Do not upload identifiable customer financial records or actual originals without consent, privacy/security assessment and a production-grade database.

### Quality gates

- Automated build and XML pilot flow test for three roles.
- Frontend `node --check` syntax verification.
- Live test: `/health` = `pilot-r1`, anonymous `/api/state` = HTTP 401, all three logins authenticate and each role has only authorized business data.

**Do not merge code directly to `main` while the three-role pilot is under review.** Pilot is deployed from the feature branch only.
