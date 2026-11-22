# Badri TradeFlow R2.1 — Daily-work pilot (22 November 2026)

> **Demo/Pilot ONLY**. Azure Free F1 + single XML file. Not a GST filing, bank clearing, multi-tenant or production ledger product.

## Live target and deployment
- App: `badritraders`, South India, `badritradersPOC`.
- Login URL: https://badritraders-hga6f6amdubqbhfb.southindia-01.azurewebsites.net/
- Git branch: `feature/azure-identity-and-deployment-foundation`, merged [PR #14](https://github.com/Muthukumar123/Project-Badri/pull/14).
- Deployment workflow verifies health, anonymous HTTP 401 and three seeded accounts. CI also checks full synthetic business flows, individual accounts, beat authorizations, password resets, cash/date reconciliation, duplicate bank references and identical-PDF blocking.
- XML data file `/home/badri-data/tradeflow.xml` remains on the existing App Service storage. Deployment must not delete that path.

## Exactly three role types — multiple private logins

| User type | Existing demonstration login | What they see |
|---|---|---|
| Owner | `owner` | Today → Orders → Stock → Accounts → More |
| Salesperson | `salesperson` | My day → Take order → Deliver → Collect → Cash handover |
| Retailer | `retailer` | My shop → Place order → Deliveries |

**The original passwords were neither read nor changed.** They remain configured privately in Azure App Service under `BADRI_PASS_OWNER`, `BADRI_PASS_SALESPERSON` / `BADRI_PASS_SALESMAN`, and `BADRI_PASS_RETAILER`.

The three role types do **not** mean three shared credentials for all businesses. New shop/employee accounts are created as follows:
1. Sign in as `owner`.
2. Open **More → Team & shop logins**.
3. Create a **Retailer** login, selecting the exact retailer shop from the list; or create a **Salesperson** login and select one assigned beat (GK Road/Bommidi etc).
4. Choose a unique username and separate password of **at least 14 characters**, then provide it privately to that person. The app does not print, email or reveal passwords. Do not put credentials into chat or GitHub.
5. Use the same page to disable a custom account immediately or reset its password. The existing session is rejected after disable/reset.
6. Built-in `owner`, `salesperson`, `retailer` passwords are managed **only in Azure Portal**. The legacy `salesperson` demo identity can see all beats for pilot convenience; create named field accounts for correct restrictions.

## The daily flow in six steps

1. Retailer logs into **their own shop**, selects item and count, sends an order. No posting to invoices, stock or money.
2. Owner reviews/accepts the order, verifies pricing and stock manually, and creates an invoice record. **Current pilot invoice is lump-sum only, not GST compliant.**
3. Owner dispatches goods. The product and quantity must match a linked accepted order (when present) and saleable stock must exist.
4. The responsible salesperson or retailer confirms receiving quantity and receiver. This does not clear any money.
5. Salesperson records cash/UPI receipt against a bill. Cash stays pending until owner cash handover; UPI remains pending until separate bank evidence is recorded. The same bank reference cannot clear two receipts.
6. Salesperson counts the day's rupee denominations in **India's business date** and submits once; owner approves. Reused cash receipts cannot enter another pending handover.

Owner Accounts → Retailer outstanding now shows unpaid invoice buckets: Current, 1–7, 8–30, 31–60, 60+ days. This uses **only pilot-entered invoices and cleared receipts**. Historic sheets cannot become overdue until explicitly migrated and approved.

## Uploading source PDFs

Owner can upload Supplier Invoice, Delivery Sheet or Collection Sheet; salesperson can upload field Delivery/Collection sheets. File-signature checks and SHA-256 exact-byte duplicate detection are implemented. **A second identical PDF/photo returns HTTP 409 and does not create a second review item.** There is no OCR, no GST extraction, no scan virus scanning or immutable Blob store in this pilot. Uploads never create bills, payable, stock or cleared receipts by themselves.

## Tested before deployment

- Three-role original order→GRN→bill→dispatch→POD→cash/UPI→owner close→return tests.
- Two different assigned-beat accounts cannot collect/see records outside their beat.
- Two distinct retailer accounts only see their mapped shop (including on direct API calls).
- Disable and password reset revoke previous cookies, and accounts survive XML migration/restart.
- Linked dispatch rejects incorrect SKU and excess quantity.
- Cash-close cannot submit the same pending receipts twice; India business date is used.
- Reused bank verification reference rejected.
- Identical PDF bytes uploaded twice return HTTP 409; no ledger posting.
- Mobile-oriented HTML/JS rendering smoke tests in CI, plus Azure post-deploy browser assets and identity checks.

**Test evidence:** [CI run 45033357038](https://github.com/Muthukumar123/Project-Badri/actions/runs/45033357038). Azure [deployment run 45033468643](https://github.com/Muthukumar123/Project-Badri/actions/runs/45033468643) is the corresponding deploy.

## Still incomplete — do not claim all FR-01–FR-25 delivered

The [25-requirement audit](REQUIREMENTS_COVERAGE_AUDIT_2026-11-22.md) tracks remaining work; see [#7–#13](https://github.com/Muthukumar123/Project-Badri/issues). In particular:
- GSTIN/HSN and multi-line GST invoices, PDF invoice and numbered receipt/credit note
- Proper opening balances, seller/supplier AR/AP aging, multi-bill allocation and HC older receipts
- Full supplier invoice/collection/delivery OCR and structured human-verified row processing, source-page provenance
- FEFO batch picking, paid/free goods, stock valuation, supplier PO, physical variance/short receipt claims
- Offline field sync and notification integrations (SMS/WhatsApp) with recipient consent
- Real independent maker/checker for supplier payments, statement-backed bank verification
- Production Azure SQL transactional DB, Blob storage, durable backups, tenant onboarding, subscription plans, privacy/legal and security review.

The correct next stage is another tested incremental release, not a claim that an Azure pilot build satisfies full UAT.
