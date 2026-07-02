# Commerce API

Backend API for a single-seller e-commerce platform built with **ASP.NET Core 10**, **Entity Framework Core**, **PostgreSQL**, **Stripe**, **AWS (S3 & SES)**, **Hangfire**, and a robust integration testing suite.

> [!NOTE]
> For a comprehensive architectural analysis, database design, domain relationships, and detailed API documentation, please refer to the **[Detailed Specification](Docs/spec_commerce.md)**.

---

## 🚀 Key Features

* **Secure Authentication**: JWT tokens, rotated refresh tokens, token family tracking, multi-device logout.
* **Product Catalog & Images**: Category filtering, specification JSON, soft-delete, S3-backed storage with duplicate prevention.
* **Cart & Checkout**: Authenticated shopping cart, stock reservation, Stripe Checkout integration.
* **Order & Payments**: Order status machine, address snapshotting, Stripe webhook processing.
* **Background Jobs & Email**: Hangfire-driven transactional emails (SES/SMTP), payment timeouts, and cleanups.
* **Hardened API**: Rate-limiting, global exception middleware, health checks.

---

## 🛠️ Tech Stack

* **Core**: .NET 10, ASP.NET Core Controllers, EF Core, FluentValidation
* **Database**: PostgreSQL (with migrations, check constraints, JSONB)
* **Background Processing**: Hangfire (PostgreSQL backend)
* **Payments & Storage**: Stripe.net, AWS S3
* **Development Email**: SMTP / Mailpit
* **Testing**: xUnit, Testcontainers, Respawn, NSubstitute, Shouldly

---

## 🛡️ Reliability, Scalability & Hardening

This project demonstrates several production-grade system design patterns:
* **Reliability & Consistency**:
  * **Transactional Checkout**: Cart check, inventory reservation, and order creation occur within a single ACID database transaction.
  * **Optimistic Concurrency**: Protects product inventory from race conditions using PostgreSQL `xmin` concurrency tokens.
  * **Idempotent Webhooks**: Prevents duplicate payment processing using unique event verification and storage.
  * **Outbox-style Emailing**: Persists transactional emails first, then delivers them via Hangfire to survive network/SMTP failures.
* **Scalability**:
  * **Stateless API Layer**: Uses stateless JWT authentication, making horizontal scaling behind a load balancer trivial.
  * **Decoupled Storage**: Product assets are offloaded to AWS S3 rather than local storage.
  * **Worker Separation**: Heavy background operations (cleanups, timeouts, email processing) run in Hangfire, keeping the HTTP thread pool responsive.
* **Load Hardening**:
  * **Rate Limiting**: Custom rate-limiting policies partitioned by IP/User ID to mitigate brute-force and DDoS attacks.
  * **Health Probes**: Built-in `/health` endpoints for database connection health.

---

## 🚦 Quick Start

### Prerequisites
* Docker & Docker Compose
* .NET 10 SDK (for running locally or running tests)

### Spin Up Local Environment (Docker Compose)
The fastest way to run the API, PostgreSQL, and Mailpit together:

```bash
# Copy env configuration
cp .env.example .env

# Start all services
docker compose -f compose.development.yaml --profile api up --build
```

* **API & Swagger**: [http://localhost:5082/swagger](http://localhost:5082/swagger)
* **Health Check**: [http://localhost:5082/health](http://localhost:5082/health)
* **Hangfire Dashboard**: [http://localhost:5082/hangfire](http://localhost:5082/hangfire)
* **Mailpit (Local SMTP Web UI)**: [http://localhost:8025](http://localhost:8025)

**Admin Credentials (Dev Mode Only):**
* **Email**: `admin@commerce.local`
* **Password**: `Admin123!`

---

## 🧪 Testing

The test suite runs integration tests against real PostgreSQL containers using Testcontainers and Respawn.

```bash
# Run all tests (Unit & Integration)
dotnet test

# Run only unit tests
dotnet test Commerce.Tests/Commerce.Tests.csproj --filter FullyQualifiedName~UnitTests
```

---

## 📁 Project Structure

* `Commerce.Api` - HTTP Controllers, middleware, rate-limiting, Swagger
* `Commerce.Application` - Domain models, business services, database context/migrations, background jobs
* `Commerce.Contracts` - Reusable Request/Response DTOs
* `Commerce.Tests` - Unit & Integration tests
* `Docs` - Detailed planning notes and [spec_commerce.md](Docs/spec_commerce.md)
