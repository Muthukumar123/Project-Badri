# Badri TradeFlow — Requirements-to-Code Coverage Audit (22 November 2026)

**Status:** ACTION REQUIRED — Three-role R2 XML pilot is online, **but it is not requirements-complete and must not be sold or used as a verified financial ledger.**

**Sources of truth inspected:**
- [v2.0 approved requirements](BADRI_TRADEFLOW_REQUIREMENTS_v2.0.md) — 25 functional groups (FR-01–FR-25), 25 UAT scenarios and nine commercial launch gates.
- Actual deployed feature branch: `feature/azure-identity-and-deployment-foundation`, especially [pilot/Program.cs](../pilot/Program.cs), [pilot/PilotStore.cs](../pilot/PilotStore.cs), [pilot/Seed.cs](../pilot/Seed.cs), [pilot/wwwroot/app.js](../pilot/wwwroot/app.js) and [role integration tests](../tools/test_pilot_flows.py).
- Azure workflow [Recover and verify Badri website](../.github/workflows/ensure-badri-website-live.yml) passed deployment, root HTML/JS/CSS, `pilot-r2` health, anonymous HTTP 401 and all three logins in [run 45018376731](https://github.com/Muthukumar123/Project-Badri/actions/runs/45018376731). **This proves deployment and basic smoke tests, not the 25 full business UAT scenarios.**

## Scope decision: three role types, not three shared real-world accounts

- **Owner:** business masters, all purchase/inventory/accounts, reports, approvals and exception resolution.
- **Salesperson:** own assigned shops/beats, capture orders, delivery/POD, customer collection and cash handover, field reminders.
- **Retailer:** own orders, delivery acknowledgment and own bills/payments.
- For commercial use, support **many individually authenticated Salesperson and Retailer users** of these three types. Current pilot has one shared Salesperson identity and one Retailer account hardcoded to Kumar Maligai.
- SaaS platform support/operator is a separate internal **control plane**, not a fourth trader-facing menu or shared access to business finances. No such platform module is deployed.

## Interpretation of status

- **Pilot partial:** some corresponding API or screen exists and some synthetic-data tests pass, but the complete approved requirement does **not** pass UAT.
- **Not built:** only an example, text annotation, static display, or no working workflow. A static sample is **not** evidence that an upload/parser/reconciliation process works.
- **Complete:** meets the entire FR and its UAT; **none** has yet been established at production/approved-v2 scope.

### 25/25 functional requirement groups checked

| FR | Requirement | Verified R2 implementation | Coverage | Engineering issue |
|---|---|---|---|---|
| FR-01 | Multi-tenant SaaS / subscriptions / branding | One hardcoded demo tenant, three manually provisioned accounts, no billing/branding/control plane | **Not built** | [#13](https://github.com/Muthukumar123/Project-Badri/issues/13) |
| FR-02 | Supplier/retailer/product/beat masters | Sample masters; create retailer name/beat; no GSTIN, credit limits, SKU taxes, CSV import, staff/beat assignment | **Pilot partial** | [#7](https://github.com/Muthukumar123/Project-Badri/issues/7), [#11](https://github.com/Muthukumar123/Project-Badri/issues/11) |
| FR-03 | Universal three-PDF upload/review pipeline | PDF/JPG/PNG upload with header check and local file storage; manual note; no hash dedupe, Blob, row extraction, side-by-side review or malware scanning | **Pilot partial** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |
| FR-04 | Supplier invoice processing | Four seeded invoice examples; complete-page flag and manual approval; no actual field extraction/tax-line reconciliation/duplicate invoice hash detection | **Pilot partial** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |
| FR-05 | Opening physical count / GRN / stock journal | SKU opening and GRN create movements, damaged qty quarantined; no FEFO/batch dispatch/valuation, stocktake or partial receipt claims | **Pilot partial** | [#9](https://github.com/Muthukumar123/Project-Badri/issues/9) |
| FR-06 | Reorder/purchase orders | Simple stock-level and reorder text; no demand history, MOQ, pack rounding, confirmed inbound or PO lifecycle | **Pilot partial** | [#9](https://github.com/Muthukumar123/Project-Badri/issues/9) |
| FR-07 | Retailer ordering, credit and GST invoice | Single-line retailer order/accept/decline; owner manually enters **lump-sum** bill. No line-item GST/HSN, multiple SKU lines, credit checks or credit notes | **Pilot partial** | [#7](https://github.com/Muthukumar123/Project-Badri/issues/7) |
| FR-08 | Beat/dispatch/delivery/POD | Owner stock dispatch and receiver+accepted-qty POD; no assigned-beat filtering, route plans, PDF row matching, OTP/signature or short-delivery return process | **Pilot partial** | [#9](https://github.com/Muthukumar123/Project-Badri/issues/9), [#11](https://github.com/Muthukumar123/Project-Badri/issues/11) |
| FR-09 | Customer collection and collection sheet | Cash/UPI/bank/cheque, invoice reference, pending→cleared rules and same-key replay. One invoice per receipt; no printed numbered receipt, bank statement integration or uploaded sheet line review | **Pilot partial** | [#10](https://github.com/Muthukumar123/Project-Badri/issues/10) |
| FR-10 | Cash denomination/handover | Denominations, expected/count mismatch and owner approval exist; pending receipts are not yet scoped to an exact business-date/beat; no bank-deposit stage/settlement number | **Pilot partial** | [#10](https://github.com/Muthukumar123/Project-Badri/issues/10) |
| FR-11 | Retailer accounts receivable | Sum of live bills minus cleared receipts; no opening receivable posting, aging buckets, disputes, promises or credit hold | **Pilot partial** | [#7](https://github.com/Muthukumar123/Project-Badri/issues/7) |
| FR-12 | Supplier accounts payable | Approved complete GRN gives payable; partial external reference payment; no aging, bank integration, supplier credits/three-way match or mandatory independent maker/checker | **Pilot partial** | [#10](https://github.com/Muthukumar123/Project-Badri/issues/10) |
| FR-13 | Retailer-specific confirmation | One hardcoded Kumar Maligai retailer portal; can view bills/receipts and confirm delivery; no individual shop identities, expiring links, dispute or receipt acknowledgment | **Pilot partial** | [#11](https://github.com/Muthukumar123/Project-Badri/issues/11) |
| FR-14 | Returns / free schemes / damage | A return request can be recorded; no stock reintegration/quarantine stock movements, credit/debit note or promotional paid/free inventory separation | **Pilot partial** | [#9](https://github.com/Muthukumar123/Project-Badri/issues/9) |
| FR-15 | Reporting | Overview KPIs, simple owner report and bills CSV; no GST slab register, AR/AP aging, inventory valuation, cash/stock exceptions report or PDF statements | **Pilot partial** | [#12](https://github.com/Muthukumar123/Project-Badri/issues/12) |
| FR-16 | Alerts / WhatsApp / offline | On-screen reminders only; no actual notifications, opted-in messaging, PWA queue, offline storage or conflict resolution | **Not built** | [#12](https://github.com/Muthukumar123/Project-Badri/issues/12) |
| FR-17 | Governance / privacy / support / audit | XML audit entries for writes; cookie/CSRF and role checks; no production backup/restore, lifecycle privacy, independent operator support, encryption/retention and multi-tenant audit | **Pilot partial** | [#13](https://github.com/Muthukumar123/Project-Badri/issues/13), [#3](https://github.com/Muthukumar123/Project-Badri/issues/3) |
| FR-18 | Salesperson capture / assigned beat / offline | Direct order, POD, receipts and denomination submission; no real beat assignment, per-shop collector view, offline queue or receipt share | **Pilot partial** | [#11](https://github.com/Muthukumar123/Project-Badri/issues/11), [#12](https://github.com/Muthukumar123/Project-Badri/issues/12) |
| FR-19 | HC earlier collections / part payment proof | Basic part payment exists against **current** bills; no HC historical claim, owner accept/reject, or missing-bill reconciliation queue | **Not built** | [#10](https://github.com/Muthukumar123/Project-Badri/issues/10) |
| FR-20 | Paper legend/circles/ticks/G-Pay interpretation | Seeded annotation data only; no extraction, editable mark-to-meaning mapping or reviewer confirmation workflow | **Not built** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |
| FR-21 | Handwritten sums and overwritten digit review | Seeded claimed/computed totals displayed; no general handwritten row extraction, correction suggestions, row flags or cross-document arithmetic | **Not built** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |
| FR-22 | Real supplier invoice layout reader | Sample invoice layouts shown; no reader for rotated invoices, GST-inclusive/exclusive, cases+eaches, free units, embedded batches or multipage extraction | **Not built** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |
| FR-23 | IRN/e-invoice/CSV structured import | No QR decode or supplier CSV/Excel import workflow | **Not built** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |
| FR-24 | Opening balances and migration | Manual one-time SKU count; no supplier/customer unpaid bill migration, bulk import, unmatched old bill/payment queue | **Pilot partial** | [#7](https://github.com/Muthukumar123/Project-Badri/issues/7), [#10](https://github.com/Muthukumar123/Project-Badri/issues/10) |
| FR-25 | Source page-row provenance and revision | New uploads are attached to a document ID; no per-posted-figure page/row links or immutable original-vs-corrected values | **Not built** | [#8](https://github.com/Muthukumar123/Project-Badri/issues/8) |

**Summary:** 25 FR groups reviewed → **0 complete**, **17 pilot-partial**, **8 not built** under full v2.0 acceptance criteria. This is a **coverage assessment of the full requirements**, not a count of usable buttons or a claim the pilot lacks value.

### Acceptance testing: what is genuinely verified

| Scenarios | Current evidence | Assessment |
|---|---|---|
| UAT-06/07 cash+UPI and partial payment | Integration test asserts idempotency, pending bank payment, due reduction only after cash/UPI approval | **Core rules pass on synthetic records** |
| UAT-08 daily cash handover | Unit tests exercise denominations, salesperson submission and owner approval; date/beat/report rules not covered | **Partial** |
| UAT-09 supplier part payment | Test verifies ₹26,059→₹25,059 after a tested ₹1,000 approval; no true bank statement verification | **Partial** |
| UAT-10 low-stock sale | Dispatch rejects insufficient stock, but there is no reorder/partial-fill lifecycle | **Partial** |
| UAT-01/16/17 invoice completeness/GRN | Seeded ME invoice can be manually approved after a note; incomplete Himalaya blocked; synthetic GRN tests pass | **Partial, not PDF parser proof** |
| UAT-04 delivery/POD | Dispatch + accepted quantity tested; no route restriction/partial return or uploaded-sheet matching | **Partial** |
| UAT-11 return | Quarantine review record generated; no stock correction or credit note | **Partial** |
| UAT-12 multi-tenant isolation | Retailer example is isolated to Kumar Maligai, but there is no second trader tenant or real individually scoped shop identities | **Not passed** |
| UAT-14 receipt acknowledgment | Retailer POD tested, but receipt acknowledgment is separate and not implemented | **Not passed** |
| UAT-03/05/13/15/18/19/21/23/24/25 | Missing source-document dedupe, complete handwritten workflows, offline queue, GST summary, HC and old-bill reconciliation | **Not passed** |
| UAT-20 maker/checker | Current Owner can prepare **and** approve a supplier payment with a 10+ character evidence note. Requirement explicitly expects same-person refusal | **Known deviation — policy sign-off required** |
| UAT-02/22 | Batch/expiry and operator separation exist only as partial seed/governance concepts; no full end-to-end acceptance | **Not passed** |

The exact 25-scenario UAT contract remains in [v2.0 Section 13 and Section 17](BADRI_TRADEFLOW_REQUIREMENTS_v2.0.md#13-acceptanceuat-scenarios-directly-inspired-by-your-pdfs). Do not count the older static browser prototype's reported checks as the deployed R2 pilot's acceptance passes.

## Critical gaps ranked by owner/business harm

1. **Real individual retailer and sales identity**: a shared retailer password is not suitable for a rollout; own-shop/own-beat scoping must be enforced at the API layer. Issue [#11](https://github.com/Muthukumar123/Project-Badri/issues/11).
2. **Accounting integrity/GST**: R2 sales invoices are manually entered amounts without GST and SKU pricing; opening dues are not established. Issues [#7](https://github.com/Muthukumar123/Project-Badri/issues/7), [#10](https://github.com/Muthukumar123/Project-Badri/issues/10).
3. **Evidence processing**: uploaded PDF/photo is stored, but hand-written collections, delivery sheet and supplier bill details cannot be reconciled line-by-line. Issue [#8](https://github.com/Muthukumar123/Project-Badri/issues/8).
4. **Actual warehouse controls**: FEFO, short receipt claims, free goods, stock valuation, sales returns and credit notes not complete. Issue [#9](https://github.com/Muthukumar123/Project-Badri/issues/9).
5. **Reliable financial transaction control**: no real bank reconciliation; cash not date/beat scoped, no HC/missing-bill workflow, and owner self-approval differs from agreed UAT-20. Issue [#10](https://github.com/Muthukumar123/Project-Badri/issues/10).
6. **Operational usefulness**: user-friendly bilingual screen exists but low-signal offline field collection, receipt sharing, reminders and aging/GST owner reports are not complete. Issue [#12](https://github.com/Muthukumar123/Project-Badri/issues/12).
7. **SaaS and safety**: the app runs in Free F1 with XML on a single instance, not a transactional production DB; multi-tenancy, subscriptions, backups, support/privacy and external security validation are not complete. Issues [#3](https://github.com/Muthukumar123/Project-Badri/issues/3), [#13](https://github.com/Muthukumar123/Project-Badri/issues/13).

## Release plan preserving the simple 3-role experience

| Stage | What owner, salesperson and retailer can actually do when complete | Prerequisite / evidence |
|---|---|---|
| **R2.1 — Usable single trader pilot (P0)** | Retailers have individual accounts; sales staff see their own beats; owners issue line-item bills; stock/AR match; cash closes by exact date | Close #7, #10, #11; pass real supervised trader acceptance on test/sanitized data |
| **R2.2 — Paperless workday (P0)** | Supplier invoice, collection, delivery photo uploaded, rows verified, duplicate blocked; GRN short/damaged/free goods and returns tracked | Close #8, #9; replay seven real layouts with privacy-safe test fixtures and test corrections |
| **R2.3 — Field-ready (P1)** | Offline queue/visible sync state, Tamil UAT, follow-up reminders, receipts and owner reports | Close #12; offline and export UAT pass |
| **R3 — Paid district SaaS (P1/P0 release gate)** | Many tenant businesses, subscription plan limits, compliant operator support, transactional DB and proven backups | Close #13, #3; all Section 19 launch gates pass |

**Owner-facing feature rule:** keep only 3 role TYPES and a task-first home page. Do **not** expose extra SaaS/platform operations to the business user. The Azure XML pilot remains available for controlled demonstrations, but it must not be used as a trusted accounting source.

## Non-negotiable signoffs before asserting "all requirements covered"

- **Owner:** Confirm paper meanings HC, G/P, (F), tick/circle, missing-page policies and owner-only exception routes; verify every daily workflow in Tamil.
- **Accountant:** GST/HSN and rate calculations, paid/free schemes, invoice numbering, credit notes, outstanding balance imports, purchase payable timing and valuation; resolve UAT-20 maker/checker deviation explicitly.
- **Security/privacy reviewer:** Individual shop identities, beat isolation, real tenant boundaries, file storage and customer retention/consent.
- **Operations:** Restore drill, end-of-day reconciliation, monitoring, RPO/RTO and support.
- **UAT:** Mark each UAT-01 through UAT-25 as **pass** with evidence, owner sign-off and test date. No acceptance based solely on a green compile/build workflow.

## Evidence links

- R2 [application backend](../pilot/Program.cs), [XML model](../pilot/PilotStore.cs), [three-role UI](../pilot/wwwroot/app.js)
- R2 [end-to-end synthetic scenario tests](../tools/test_pilot_flows.py), [XML migration tests](../tools/test_three_role_migration.py), [UI smoke tests](../tools/check_pilot_ui.mjs)
- Verified [latest Azure R2 redeployment run](https://github.com/Muthukumar123/Project-Badri/actions/runs/45018376731)
- Main-branch `src/TradeFlow.Api` represents a separate **unfinished .NET + React starter**; it is **not** the XML pilot deployed by `pilot/Badri.Pilot.csproj` and must not be mistaken for a production-ready implementation.

**Audit classification is a point-in-time source review.** A new requirement is not complete until its issue contains acceptance evidence and the corresponding test is green.
