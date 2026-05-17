# SPEC-1-Amazon-like E-Commerce App

## Background

This design addresses the need for a startup to launch a **minimum viable e-commerce platform** inspired by Amazon’s core shopping experience. The goal is to enable real users to browse products, place orders, and complete payments with a reliable, scalable, and maintainable system while keeping infrastructure complexity and costs low.

The system is designed as a **single-seller retail platform**, meaning all inventory is owned and managed by the business itself. This significantly simplifies fulfillment, pricing, and seller management while still allowing future evolution into a multi-vendor marketplace if needed.

The architecture prioritizes:
- Fast time-to-market
- Clean separation of concerns
- Cloud-native scalability
- API-first design for future mobile apps

---

## Requirements

### Must Have (M)
- User registration and authentication (email/password)
- Password reset via email (forgot/reset flow)
- JWT-based authentication with refresh tokens
- Role-based access control (`Customer`, `Admin`)
- **Product catalog browsing with pagination (default 20 items/page)**
- **Product search by keyword (name + description)**
- Product detail pages (images, price, description, stock)
- Shopping cart (authenticated users only)
- **Stock validation when adding/updating cart items**
- Checkout flow
- Online payment processing via third-party provider
- Payment tracking and retries
- Order creation and persistence
- Order status tracking (PLACED, PAID, SHIPPED, DELIVERED, CANCELLED)
- Order confirmation email
- Admin ability to manage products and inventory
- HTTPS-only communication

### Should Have (S)
 (S)
- User order history
- Order cancellation (rules-based)
- Basic product ratings (1–5 stars)
- Stock validation during cart operations
- Email delivery failure retry

### Could Have (C)
- Wishlist / save-for-later
- Discount codes
- Product analytics dashboard

### Won’t Have (W) – MVP Exclusions
- Anonymous carts or guest checkout
- Multi-seller marketplace
- Advanced search (full-text / Elasticsearch)
- Recommendation engine
- Native mobile apps

## Method

### Operational Rules & State Management

#### Order Status Transitions

Valid transitions:
- `PLACED → PAID → SHIPPED → DELIVERED`
- `PLACED → CANCELLED`
- `PAID → CANCELLED` *(with refund)*

Invalid transitions must throw a domain exception.

---

#### Order Cancellation Rules

**User-initiated:**
- Allowed when status is `PLACED` or `PAID`
- Not allowed when `SHIPPED` or `DELIVERED`

**Admin-initiated:**
- Allowed for any status except `DELIVERED`

**On cancellation:**
1. Update Order.Status → `CANCELLED`
2. Restore Product.StockQuantity for all OrderItems
3. If Payment is `COMPLETED`, initiate refund via payment provider
4. Update Payment.Status → `REFUNDED` when confirmed

---

### Error Handling Standards

All API errors return a standard format:
```json
{
  "error": "Human readable message",
  "code": "ERROR_CODE",
  "details": {}
}
```

HTTP status usage:
- `400` – Validation errors
- `401` – Authentication required/failed
- `403` – Forbidden (role/permission)
- `404` – Resource not found
- `409` – Business rule or concurrency conflict
- `500` – Unexpected server error

---

### Logging & Observability

- Structured logging using **Serilog**
- Correlation ID per request
- Log:
  - All API requests (duration, userId)
  - Order state transitions
  - Payment events and webhooks
  - Failed emails and retries

---

### Testing

- Unit tests for domain logic
- Integration tests: using **Testcontainers** and **Respawn**
- FOR MORE INFO: Lookup Claude chat-name -> "rating implementation with tests" & "*Two-layer testing strategy for MVP with unit and integration tests*"

---

### Rate Limiting

- 100 requests/minute per authenticated user
- 20 requests/minute per IP for unauthenticated endpoints
- Implemented via ASP.NET Core middleware (in-memory store for MVP)

---

### Input Validation

- Centralized validation using **FluentValidation**
- Key rules:
  - Email: valid format, max 255 chars
  - Password: min 8 chars, must include letter + number
  - Product name: required, max 200 chars
  - Product price: > 0, max 2 decimals
  - Quantity: > 0, max 999

---

### JWT Configuration

- Access token expiration: **15 minutes**
- Refresh token expiration: **7 days**
- Signing algorithm: HS256
- Claims: `userId`, `email`, `role` (`Customer` or `Admin`)
- Refresh tokens:
  - Stored hashed
  - Rotated on each use
  - Revoked on password reset

---

### API Endpoints Summary

#### Authentication
- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout` **NEW**
- `POST /auth/logout-all` **NEW**
- `POST /auth/forgot-password`
- `POST /auth/reset-password`

#### Products (Public)
- `GET /products?page=&pageSize=&category=&search=&sortBy=`
- `GET /products/{id}`

#### Cart (Authenticated)
- `GET /cart`
- `POST /cart/items`
- `PUT /cart/items/{id}`
- `DELETE /cart/items/{id}`
- `DELETE /cart`

#### Checkout & Orders (Authenticated)
- `POST /checkout`
- `POST /checkout/session-status` **NEW**
- `GET /orders`
- `GET /orders/{id}`
- `POST /orders/{id}/cancel`

#### Addresses (Authenticated)
- `GET /addresses`
- `POST /addresses`
- `PUT /addresses/{id}`
- `DELETE /addresses/{id}`

#### Ratings
- `POST /products/{id}/ratings`
- `PUT /ratings/{id}`
- `DELETE /ratings/{id}`
- `GET /products/{{productId:guid}}/ratings` **NEW** *(public)*

Rating create/update/delete endpoints require authentication.

#### Admin (`Admin` role)
- `POST /admin/products`
- `PUT /admin/products/{id}`
- `DELETE /admin/products/{id}` *(soft delete)*
- `POST /admin/products/{id}/images`
- `DELETE /admin/products/{productId}/images/{imageId}`
- `PUT /admin/products/{productId}/images/{imageId}/set-primary`
- `GET /admin/orders`
- `PUT /admin/orders/{id}/status`

Note:
- `[HttpPut("{imageId}/set-primary")]` for more details check *MVP review v2* chat.

#### Webhooks
- `POST /webhooks/stripe`

---

### Non-Functional Requirements

- HTTPS enforced (HSTS enabled)
- Stateless backend (except DB)
- Horizontal scalability supported
- All background jobs must be idempotent

---


---

## Pass 4: Infrastructure & Supporting Domains (Finalized)

### 3.2 Core Domain Models (Additions)

#### RefreshToken
```
RefreshToken:
- Id (GUID, PK)
- UserId (FK → User)
- TokenHash (string)
- ExpiresAt (datetime)
- CreatedAt (datetime)
- RevokedAt (nullable datetime)

Indexes:
- IX_RefreshToken_UserId
- IX_RefreshToken_TokenHash
```

#### PasswordResetToken
```
PasswordResetToken:
- Id (GUID, PK)
- UserId (FK → User)
- Token (string, unique)
- ExpiresAt (datetime)
- UsedAt (nullable datetime)
- CreatedAt (datetime)

Indexes:
- IX_PasswordResetToken_Token (unique)
- IX_PasswordResetToken_UserId
```

#### EmailNotification
```
EmailNotification:
- Id (GUID, PK)
- RecipientEmail
- Template (ORDER_CONFIRMATION | PASSWORD_RESET)
- TemplateData (JSON)
- Status (PENDING | SENT | FAILED | PERMANENTLY_FAILED)
- Attempts (int)
- MaxAttempts (int = 3)
- LastAttemptAt (nullable)
- SentAt (nullable)
- ErrorMessage (nullable)
- OrderId (FK, nullable)
- CreatedAt

Indexes:
- IX_EmailNotification_Status
- IX_EmailNotification_OrderId
```

#### WebhookEvent
```
WebhookEvent:
- Id (GUID, PK)
- EventId (string, unique) // Stripe evt_xxx
- EventType
- Payload (JSON)
- Status (PENDING | PROCESSED | FAILED)
- ProcessedAt (nullable)
- ErrorMessage (nullable)
- CreatedAt

Indexes:
- IX_WebhookEvent_EventId (unique)
- IX_WebhookEvent_Status
```

---

### 3.8 Background Jobs

Framework: **Hangfire** (using same PostgreSQL database)

Jobs:

1. **EmailSenderJob** (every 1 minute)
   - Process EmailNotification where Status IN (PENDING, FAILED)
   - Retry until MaxAttempts
   - On success → mark SENT, update Order.ConfirmationEmailSent

2. **PaymentTimeoutJob** (every 5 minutes)
   - Find Payments with Status = PENDING and CreatedAt < now - 30 minutes
   - Cancel Order, restore stock, mark Payment FAILED

3. **CleanupJob** (daily at 02:00)
   - Delete expired PasswordResetTokens
   - Delete revoked RefreshTokens older than 30 days

All jobs must be idempotent.

---

### 3.7 Security & Authentication (Expanded)

- JWT access token: 15 minutes
- Refresh token: 7 days
- Refresh tokens stored hashed
- Rotation on every refresh
- Revoke all refresh tokens on password reset or logout

Password Reset:
- Token valid for 1 hour
- Single-use only

---

### 3.9 Email Service

Provider: **SendGrid** or **AWS SES**

Emails sent:
- Order confirmation
- Password reset

All emails are sent asynchronously via EmailNotification queue.

---

### 4.1 File Upload Configuration

Product Images:
- Max size: 5MB
- Formats: JPG, PNG, WEBP
- Max 5 images per product
- Storage:
  - Dev: Local /uploads
  - Prod: AWS S3

Admin endpoint:
POST /admin/products/{id}/images

---

### 4.2 Search Implementation

- Case-insensitive
- Partial match
- Fields: Name, Description
- Multi-word AND semantics
- Filter IsDeleted = false

---

### 4.3 Pagination Format

Default pageSize: 20
Max pageSize: 100

Response:
```
{
  "data": [...],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 156,
    "totalPages": 8,
    "hasNext": true,
    "hasPrevious": false
  }
}
```

---

### 4.4 CORS Configuration

Allowed origins:
- Dev: http://localhost:3000
- Prod: https://app.example.com

Allowed headers: Content-Type, Authorization
Allowed methods: GET, POST, PUT, DELETE

---

### 4.5 Configuration & Secrets

Secrets stored outside source control:
- JWT signing key
- Stripe secret + webhook secret
- SendGrid API key

Dev: User Secrets
Prod: Cloud secret manager

---

## Status: SPEC-1 COMPLETE

This specification is now implementation-ready for an Amazon-like E-Commerce MVP using ASP.NET Core, EF Core, PostgreSQL, Hangfire, and SendGrid.
