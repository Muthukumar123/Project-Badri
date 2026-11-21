# ADR 001 — Dedicated .NET 10 / Azure SQL architecture

**Decision:** Separate commercial repository `badri-tradeflow-saas`; modular monolith ASP.NET Core 10/C# with React/TypeScript PWA. Use Azure SQL for relational invoices, receivables and inventory movements; Blob Storage for original PDFs; queue + .NET worker + Document Intelligence for assisted extraction; Microsoft Entra External ID or another standards-based OIDC provider for identities.

**Why:** The user's requirements include money, physical stock, and partial settlement, which require strong transactional boundaries and explicit journal entries. A document upload engine is a supporting integration, not the business's system of record. The first customer is an Indian district distributor; ease of use and offline capture are critical.

**Rejected as default:** Cosmos DB as sole AR/AP/stock ledger: transactional batches are partition-scoped, and cross-aggregate correctness is challenging. Future Cosmos read models may be justified after measuring traffic.

**Tradeoff:** An initial relational system and single deployable backend may scale less independently than microservices, but reduces deployment/support cost and keeps a small team focused on financial correctness.

**Status:** Design decision; implementation and accountant review pending.
