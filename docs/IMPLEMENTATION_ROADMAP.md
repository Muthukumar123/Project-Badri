# Delivery plan — aligned to v2.0 review

This document distinguishes the code included in this repository from features not yet built. **The uploaded v2.0 specification is the source of business truth until owner/accountant sign-off.**

## Releases

| Stage | Deliverables | Critical gate |
| --- | --- | --- |
| R0 Discovery | Interview owner, Santhosh, Vijay, purchase/accounts; confirm `HC`, `G/P`, `(F)`, circles and ticks; verify handwritten sums; record opening dues and stock | Written confirmation of paper legend, eight policy decisions and example transactions |
| R1 Collections-first | Mobile-first direct collection, partial payments, receipt sharing, cash bag/denomination handover, verified UPI, retailer dues, opening bills, assisted collection-sheet upload | Two weeks of reconciled closing, measured paper reduction |
| R2 Delivery + suppliers | Beat routing, direct POD, scanned delivery sheets; supplier invoice extraction/line review, IRN QR, GRN and supplier payables | No stock posted without GRN; no duplicate supplier liability |
| R3 Inventory intelligence | Physical stocktake, batch/expiry/FEFO, stock movement journal, reorder suggestions | Physical count variance controlled |
| R4 Commercial SaaS | OIDC, tenant provisioning, subscriptions, limits, support/audit, hardened Azure infrastructure, backups and recovery | Security, privacy, Tamil UAT and unit economics gates met |

## Implemented in this starter (development only)

- Explicit tenant field on models and reads, server-derived **from developer headers only**.
- Create retailer, opening bill and collection (cash/UPI/bank/cheque), one bill per receipt.
- Idempotent collection requests (tenant-scoped uniqueness).
- Cash count/close decision with owner approval and immutable audit events.
- HC prior-collection review, simulated bank verification.
- Receivables aggregate and unallocated older-bill visibility.
- Metadata-only document inbox of three types.
- React starter dashboard with English/Tamil action labels.

## Engineering work required before anyone can use this commercially

1. Replace dev headers with OIDC and membership checks, including beat-level access; add rate limit and security testing.
2. Introduce database migrations, append-only AR/AP/stock journals, transactional outbox and concurrency controls; avoid direct balance overwrites.
3. Add data import + dry run for opening retailer bills, old-bill matching and supplier balances.
4. Define verified money semantics with accountant: cash handover approval, UPI clearing, disputed amounts, receipt reversals and cash-to-bank deposit.
5. Integrate payment verifier (bank/provider statement) rather than human-provided reference alone.
6. Build three document upload flows: Blob + checksum + malware/type checks + queue + extraction + side-by-side review + manual correction.
7. Implement OCR for rotated/multipage invoices, 'cases + eaches', batch, expiry, paid/free units, inclusive/exclusive GST, signed QR/IRN.
8. Implement delivery states and POD; warehouse GRN and stock journal, AR/AP entries, refunds, returns and credit notes.
9. Implement durable offline queue, idempotent replay and device-level recovery; do not store sensitive plaintext unsafely.
10. Build Tamil-first role views and screen-reader friendly icon+word buttons with real trader UAT.
11. Add complete integration/e2e tests with representative redacted originals; verify arithmetic against actual paper.
12. Azure infrastructure-as-code, managed identity, monitoring, backup/restore test, audit/retention, DPDP/GST review.

## Architectural decision

**C# / .NET 10 LTS** with React TypeScript and Azure SQL as default. Keep separate apps/modules under the new repository. Do **not** incorporate legacy document-processing project's existing `.NET 8` Azure Function until it is upgraded and evaluated for reusable patterns.
