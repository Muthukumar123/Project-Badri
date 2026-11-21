# Badri TradeFlow SaaS — Independent Repository

> **Status:** Initial **.NET 10/C# collections-first implementation scaffold**, not a production product. This is a **standalone** project; it has no dependency on `document-upload-processing` or BCS ONE.

## Why this code exists

The evidence-based v2.0 review changes the roadmap to **capture-first**: employees capture money at a shop, immediately issue a receipt, and the owner closes the day's cash. Uploading handwritten collection/delivery PDFs is a temporary bridge, not the primary daily workflow. The detailed requirements and original-source transcriptions are in `docs/BADRI_TRADEFLOW_REQUIREMENTS_v2.0.md`.

## Stack

- Backend: ASP.NET Core 10/C# 14 Web API, Entity Framework Core 10; SQLite for *local development only*, Azure SQL for production after migrations and security work.
- Frontend: React + TypeScript + Vite; orange-and-cream Tamil Nadu-friendly UI, with initial English/Tamil labels.
- Production target: Azure SQL, private Azure Blob Storage, queue + .NET worker, Azure AI Document Intelligence (human verification), OIDC authentication, managed identity and audit.
- Architecture: modular monolith planned, no premature microservices.

## Included in this starter

- Tenant-scoped **development-only** API endpoints for retailers, opening bills, receipt capture, cash handover, UPI/bank verification *simulation*, earlier-collection (`HC`) review, outstanding amounts and a 3-type document **metadata inbox**.
- Decimal INR arithmetic, idempotency keys on receipts, audit events, distinct pending/cleared payment states, cash denomination comparison and owner approval requirements.
- One front-end workspace showing main R1 navigation and direct-entry forms, with `Upload Collection / Invoice / Delivery` document tiles explicitly marked as **future OCR**.
- Unit tests for money rules, clearing rules and sample cash-calculation cases (not executed here; .NET SDK is not installed in this runtime).
- CI examples and phased development notes.

## Limitations / security warning

**Do not deploy to public Azure or enter real customer data.** Authentication currently uses spoofable `X-Dev-*` headers and **the server intentionally refuses to run outside `Development`**. This is only a local engineering starter, not a secure multi-tenant system. The API does not implement production OIDC, granular beat authorization, offline sync, Blob upload, actual OCR, financial double-entry accounting, supplier AP, GRN, dispatch, credit notes, subscriptions or verified bank integrations. `POST /collections/{id}/verify-demo` simulates bank clearing, and must be replaced with an authenticated bank reconciliation workflow. `EnsureCreated()` is development-only; switch to reviewed migrations before production.

## Local run

Requires **.NET 10 SDK** and **Node.js 22+** (install dependencies from trusted official sources).

```bash
cd src/TradeFlow.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5090

# in another terminal:
cd web
npm install
npm run dev

# run backend domain tests:
cd tests/TradeFlow.UnitTests
dotnet test
```

By default the UI points to `http://localhost:5090`. The frontend has a demonstration tenant identifier. This is **not authentication**. API requests carry `X-Dev-Tenant`, `X-Dev-User` and `X-Dev-Role` for local development only.

## Repo to create

Repository: `Muthukumar123/Project-Badri`. Source maintained on `feature/r1-collections-foundation` for review, then merge after the CI checks pass.

## Reviewed business rules to preserve

1. Collections must specify a payment mode; cash is **not cleared** before owner-approved handover, and UPI is **not cleared** by a screenshot.
2. Money owed by a retailer must be computed from verified invoices and cleared payments; old paper bills without a matching invoice remain **unallocated**.
3. Paper labels `HC`, `G/P`, circles and ticks are reviewed, not automatically interpreted as posted transactions.
4. Uploaded supplier invoice approval never posts stock; only a physical GRN may do so.
5. Source-document totals are claimed evidence and must be reconciled with computed lines.
6. No source paper data is treated as a confirmed live balance without opening-balances migration and owner approval.
