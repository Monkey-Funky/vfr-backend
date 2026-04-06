# VFR Retailer Module — Backend Documentation

**Project:** Virtual Fitting Room (VFR) — weAR Platform  
**Module:** Retailer Backend  
**Stack:** .NET 9 · Clean Architecture · CQRS · MediatR · EF Core 9 · PostgreSQL · Redis · JWT RS256 · Serilog · Polly v8 · xUnit · NetArchTest  
**Author:** Tarek Abozeid  
**Backend Team:** Tarek Abozeid · Sherif Mesbah · Aya Jamal · Mariam Ehab  
**Current Status:** ✅ Phases 1–6 Complete — ⏳ Phase 7 (Orders) is next

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture Decision — Modular Monolith](#2-architecture-decision--modular-monolith)
3. [Clean Architecture Layers](#3-clean-architecture-layers)
4. [Technology Stack](#4-technology-stack)
5. [Solution Folder Structure](#5-solution-folder-structure)
6. [Database Schema Overview](#6-database-schema-overview)
7. [What Has Been Built (Phases 1–6)](#7-what-has-been-built-phases-16)
   - [Phase 1 — Foundation & Standards](#phase-1--foundation--standards)
   - [Phase 2 — Authentication](#phase-2--authentication)
   - [Phase 3 — Subscriptions & Payments](#phase-3--subscriptions--payments)
   - [Phase 4 — Categories](#phase-4--categories)
   - [Phase 5 — Products](#phase-5--products)
   - [Phase 6 — Offers](#phase-6--offers)
8. [API Endpoint Reference](#8-api-endpoint-reference)
9. [Core Coding Standards](#9-core-coding-standards)
10. [CQRS & MediatR Conventions](#10-cqrs--mediatr-conventions)
11. [Validation Conventions](#11-validation-conventions)
12. [Security Model](#12-security-model)
13. [Logging Strategy](#13-logging-strategy)
14. [Background Jobs](#14-background-jobs)
15. [Error Handling & HTTP Response Map](#15-error-handling--http-response-map)
16. [Non-Negotiable Rules (Read This First)](#16-non-negotiable-rules-read-this-first)
17. [Development Setup](#17-development-setup)
18. [What Comes Next — Phase 7 (Orders)](#18-what-comes-next--phase-7-orders)

---

## 1. Project Overview

The **Virtual Fitting Room (VFR)** platform is a SaaS product that allows retailers to onboard their product catalog, manage subscriptions, and let customers virtually try on clothing using augmented reality. This repository contains the **Retailer backend module** only — the backend API that a retailer uses to manage their store.

The Retailer module covers:
- Retailer account registration and authentication (email/password + Google OAuth)
- Subscription plan management and Stripe payment processing
- Product catalog (categories, sub-categories, products, product images)
- Promotional offers
- Order management *(next phase)*
- Inventory management *(future)*
- Dashboard analytics *(future)*
- Notifications, Global Search, and Help Center *(future)*

The system is designed as a **multi-tenant** platform. Every retailer's data is completely isolated from every other retailer's data via a `RetailerId` claim stored in their JWT token.

---

## 2. Architecture Decision — Modular Monolith

**We chose a Modular Monolith over Microservices.** This decision was recorded formally as ADR-001.

**Why:** The team is four people, we share one PostgreSQL database, and we do not have the DevOps infrastructure for service meshes, message brokers, or distributed tracing. Microservices would add enormous complexity with zero business benefit at this project scale.

**What we get instead:** Clean module boundaries enforced by folder structure, namespace conventions, and automated NetArchTest architecture tests. Each bounded context (Auth, Subscriptions, Categories, Products, Offers, Orders…) has its own CQRS folder hierarchy, its own EF Core entity configurations, and its own API controller group. Modules communicate only through the Application layer — never by directly touching another module's database tables or repositories.

**Future path:** Because boundaries are correctly drawn now, any single module can be extracted into a true microservice later when a scaling requirement actually exists.

---

## 3. Clean Architecture Layers

The solution is divided into **four layers**. The dependency rule is strict and enforced by automated tests: **dependencies only flow inward.**

```
  ┌─────────────────────────────────────────────┐
  │                   API Layer                  │  ← HTTP controllers, middleware, DI wiring
  │  depends on Application only                 │
  ├─────────────────────────────────────────────┤
  │             Infrastructure Layer             │  ← EF Core, Redis, Stripe, AWS S3, SMTP
  │  depends on Domain + Application             │
  ├─────────────────────────────────────────────┤
  │             Application Layer                │  ← CQRS handlers, validators, interfaces
  │  depends on Domain only                      │
  ├─────────────────────────────────────────────┤
  │               Domain Layer                   │  ← Entities, enums, exceptions, domain rules
  │  zero external dependencies                  │
  └─────────────────────────────────────────────┘
```

### Domain Layer
Contains all business entities (`BaseEntity`, `RetailerAccount`, `Product`, `Category`, `Offer`, etc.), domain enumerations (`SubscriptionStatus`, `OfferType`, `ProductStatus`), and domain exceptions (`NotFoundException`, `BusinessRuleException`, `ConflictException`). It has **zero NuGet dependencies** beyond the .NET runtime. This layer is testable in complete isolation.

### Application Layer
Orchestrates business workflows using CQRS commands and queries (dispatched via MediatR), FluentValidation validators, and pipeline behaviors (`LoggingBehavior`, `ValidationBehavior`, `PerformanceBehavior`). It defines interfaces (`IApplicationDbContext`, `ICurrentUserService`, `IRepository<T>`, `ICacheService`) that Infrastructure implements. **AutoMapper is forbidden here** — all mapping is done with explicit manual methods.

### Infrastructure Layer
Implements all Application interfaces. Contains `ApplicationDbContext` (EF Core), EF Core entity type configurations, `Repository<T>`, `UnitOfWork`, `CacheService` (Redis), `TokenService`, `EmailService`, `FileStorageService` (AWS S3), `StripePaymentGatewayService`, and background jobs. The API layer may reference Infrastructure **only in `Program.cs` for DI registration** — never in controllers.

### API Layer
Thin HTTP layer. Controllers call `ISender` (MediatR) to dispatch commands/queries and map results to HTTP responses. Contains `ExceptionHandlingMiddleware`, `CurrentUserService` (reads JWT claims), Swagger config, rate limiting, and CORS. **No business logic lives here.**

---

## 4. Technology Stack

| Technology | Version | Why We Use It |
|---|---|---|
| **.NET 9** | 9.x | Latest LTS runtime, top performance for ASP.NET Core web APIs |
| **PostgreSQL** | 15+ | ACID-compliant, supports `jsonb`, `tsvector` (full-text search), partial indexes |
| **EF Core** | 9.x | ORM with `UseNpgsql` + `UseSnakeCaseNamingConvention`; global query filters for soft-delete |
| **MediatR** | 13.x | In-process mediator; decouples controllers from handlers; enables pipeline behaviors |
| **FluentValidation** | 12.x | Expressive validation rules with automatic 422 responses via `ValidationBehavior` |
| **Redis** | StackExchange.Redis | Distributed caching for hot read paths; distributed locks for background jobs |
| **JWT RS256** | System.IdentityModel.Tokens.Jwt | Asymmetric keypair; private key signs, public key verifies — safe to distribute public key |
| **BCrypt.Net-Next** | 4.x | Password hashing with work factor 12; never plain SHA |
| **Serilog** | 3.x | Structured logging with `{RetailerId}`, `{CorrelationId}`, `{Feature}` enrichment |
| **Polly v8** | 8.x | Resilience pipelines for external calls (Stripe, S3, SMTP) — Retry + CircuitBreaker |
| **Stripe.net** | Latest | Payment processing; card data never stored raw (last4 + Stripe token only) |
| **AWS SDK S3** | Latest | Product image and brand logo storage |
| **MailKit** | Latest | Transactional email (verification, password reset) |
| **Google.Apis.Auth** | Latest | Google OAuth2 token verification for social login |
| **xUnit** | 2.x | Unit and integration tests |
| **NetArchTest** | Latest | Architecture enforcement — CI fails if a layer boundary is violated |

> ⚠️ **IMPORTANT:** The original scaffold used `UseSqlServer`. This has been replaced with `UseNpgsql` throughout. The NuGet package `Microsoft.EntityFrameworkCore.SqlServer` has been removed and replaced with `Npgsql.EntityFrameworkCore.PostgreSQL 9.x`. Never add SQL Server packages back.

---

## 5. Solution Folder Structure

```
VirtualFittingRoom/
│
├── src/
│   ├── Domain/
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs                  ← Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted
│   │   │   └── IAuditableEntity.cs
│   │   ├── Entities/
│   │   │   ├── Retailer/
│   │   │   │   ├── RetailerAccount.cs
│   │   │   │   ├── Category.cs
│   │   │   │   ├── SubCategory.cs
│   │   │   │   ├── Product.cs
│   │   │   │   ├── ProductImage.cs
│   │   │   │   ├── InventoryRecord.cs
│   │   │   │   ├── Offer.cs
│   │   │   │   └── PaymentMethod.cs
│   │   │   └── Subscriptions/
│   │   │       ├── SubscriptionPlan.cs
│   │   │       ├── Subscription.cs
│   │   │       ├── SubscriptionPayment.cs
│   │   │       └── SaasEnquiry.cs
│   │   ├── Enums/
│   │   │   ├── UserRole.cs
│   │   │   ├── SubscriptionStatus.cs
│   │   │   ├── SubscriptionPaymentStatus.cs
│   │   │   ├── SaasEnquiryStatus.cs
│   │   │   ├── ProductStatus.cs
│   │   │   ├── InventoryStatus.cs
│   │   │   ├── OfferType.cs
│   │   │   ├── DiscountType.cs
│   │   │   └── OfferStatus.cs
│   │   ├── Events/
│   │   │   ├── IDomainEvent.cs
│   │   │   └── SaasEnquirySubmittedDomainEvent.cs
│   │   └── Exceptions/
│   │       ├── DomainException.cs
│   │       ├── NotFoundException.cs
│   │       ├── ValidationException.cs
│   │       ├── BusinessRuleException.cs
│   │       ├── ConflictException.cs
│   │       ├── UnauthorizedException.cs
│   │       └── ExternalServiceException.cs
│   │
│   ├── Shared/
│   │   ├── Constants/
│   │   │   └── CacheKeys.cs
│   │   └── DTOs/
│   │       ├── Result.cs
│   │       ├── PagedResult.cs
│   │       ├── ApiResponse.cs
│   │       └── ApiErrorResponse.cs
│   │
│   ├── Application/
│   │   ├── Behaviors/
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── PerformanceBehavior.cs
│   │   │   └── ValidationBehavior.cs
│   │   ├── Interfaces/
│   │   │   ├── IApplicationDbContext.cs
│   │   │   ├── ICacheService.cs
│   │   │   ├── ICurrentUserService.cs
│   │   │   ├── IDateTime.cs
│   │   │   ├── IRepository.cs
│   │   │   ├── IUnitOfWork.cs
│   │   │   ├── ITokenService.cs
│   │   │   ├── IEmailService.cs
│   │   │   ├── IFileStorageService.cs
│   │   │   ├── IGoogleAuthService.cs
│   │   │   ├── IPaymentGatewayService.cs
│   │   │   ├── IEncryptionService.cs
│   │   │   ├── IProductRepository.cs
│   │   │   └── ISubscriptionService.cs
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   │   ├── Commands/  (Login, LoginWithGoogle, RegisterStep1, RegisterStep2,
│   │   │   │   │              RefreshToken, Logout, ForgotPassword, ResetPassword)
│   │   │   │   └── ...
│   │   │   ├── Subscriptions/
│   │   │   │   ├── Queries/   (GetSubscriptionPlans, GetCurrentSubscription, GetSubscriptionHistory)
│   │   │   │   └── Commands/  (SubscribeToPlan, UpgradePlan, CancelSubscription, SubmitSaasEnquiry)
│   │   │   ├── PaymentMethods/
│   │   │   │   ├── Queries/   (GetPaymentMethods)
│   │   │   │   └── Commands/  (AddPaymentMethod, SetDefaultPaymentMethod, DeletePaymentMethod)
│   │   │   ├── Categories/
│   │   │   │   ├── Queries/   (GetCategories, GetCategoryById, GetSubCategories)
│   │   │   │   └── Commands/  (CreateCategory, UpdateCategory, DeleteCategory, ToggleCategoryStatus,
│   │   │   │                   CreateSubCategory, UpdateSubCategory, DeleteSubCategory)
│   │   │   ├── Products/
│   │   │   │   ├── Queries/   (GetProducts, GetProductById)
│   │   │   │   └── Commands/  (CreateProduct, UpdateProduct, DeleteProduct, ToggleProductStatus,
│   │   │   │                   AddProductImage, RemoveProductImage)
│   │   │   └── Offers/
│   │   │       ├── Queries/   (GetOffers, GetOfferById)
│   │   │       └── Commands/  (CreateOffer, UpdateOffer, DeleteOffer, ToggleOfferStatus)
│   │   ├── Mappings/
│   │   │   ├── CategoryDto.cs / CategoryMappings.cs
│   │   │   ├── ProductDto.cs / ProductMappings.cs
│   │   │   └── OfferDto.cs / OfferMappings.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   │   ├── RetailerAccountConfiguration.cs
│   │   │   │   ├── SubscriptionPlanConfiguration.cs
│   │   │   │   ├── SubscriptionConfiguration.cs
│   │   │   │   ├── SubscriptionPaymentConfiguration.cs
│   │   │   │   ├── SaasEnquiryConfiguration.cs
│   │   │   │   ├── PaymentMethodConfiguration.cs
│   │   │   │   ├── CategoryConfiguration.cs
│   │   │   │   ├── SubCategoryConfiguration.cs
│   │   │   │   ├── ProductConfiguration.cs
│   │   │   │   ├── ProductImageConfiguration.cs
│   │   │   │   ├── InventoryRecordConfiguration.cs
│   │   │   │   └── OfferConfiguration.cs
│   │   │   ├── Migrations/          ← Generated by dotnet-ef
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Repository.cs
│   │   │   └── UnitOfWork.cs
│   │   ├── Services/
│   │   │   ├── CacheService.cs
│   │   │   ├── DateTimeService.cs
│   │   │   ├── TokenService.cs
│   │   │   ├── EmailService.cs
│   │   │   ├── FileStorageService.cs
│   │   │   ├── GoogleAuthService.cs
│   │   │   ├── StripePaymentGatewayService.cs
│   │   │   └── AesEncryptionService.cs
│   │   ├── BackgroundJobs/
│   │   │   ├── OfferExpiryJob.cs
│   │   │   ├── RecurringPaymentJob.cs
│   │   │   ├── SubscriptionExpiryJob.cs
│   │   │   ├── AccountDeletionJob.cs
│   │   │   └── DashboardSnapshotJob.cs
│   │   └── DependencyInjection.cs
│   │
│   └── API/
│       ├── Controllers/
│       │   ├── Auth/AuthController.cs
│       │   ├── Subscriptions/SubscriptionPlansController.cs
│       │   ├── Subscriptions/SubscriptionsController.cs
│       │   ├── Subscriptions/PaymentMethodsController.cs
│       │   ├── Categories/CategoriesController.cs
│       │   ├── Products/ProductsController.cs
│       │   └── Offers/OffersController.cs
│       ├── Contracts/               ← Request body models (not domain objects)
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Services/
│       │   └── CurrentUserService.cs
│       ├── Program.cs
│       ├── appsettings.json
│       └── appsettings.Development.json
│
└── tests/
    ├── Tests.Unit/
    │   └── Tests.Unit.csproj
    └── Tests.Architecture/
        ├── ArchitectureTests.cs        ← NetArchTest — CI fails on layer violations
        └── Tests.Architecture.csproj
```

---

## 6. Database Schema Overview

All tables use PostgreSQL-native types. The naming convention is **snake_case** enforced globally by `UseSnakeCaseNamingConvention()` in EF Core.

**Key design rules:**
- Every entity extends `BaseEntity` which provides: `id uuid PK`, `created_at timestamptz`, `updated_at timestamptz`, `created_by uuid`, `updated_by uuid`, `is_deleted boolean DEFAULT false`
- Every retailer-owned entity has a non-nullable `retailer_id uuid FK` column
- Soft deletes are used throughout — records are never physically deleted from the database. EF Core global query filters ensure `WHERE is_deleted = false` is applied automatically to every query
- Optimistic concurrency on `InventoryRecord` via `row_version integer`
- Full-text search on `products` via a generated `tsvector` column indexed with GIN

**Core tables built so far:**

| Table | Purpose |
|---|---|
| `retailer_accounts` | Retailer identity, credentials, OAuth, email verification, subscription FK |
| `subscription_plans` | Available plans: Basic / Standard / Enterprise / SaaS |
| `subscriptions` | A retailer's active subscription to a plan |
| `subscription_payments` | Payment transaction records |
| `saas_enquiries` | Enterprise/SaaS custom enquiry submissions |
| `payment_methods` | Saved Stripe payment methods (card_last4, encrypted cardholder name) |
| `categories` | Product categories owned by a retailer |
| `sub_categories` | One level of sub-categories under a category (max depth = 1) |
| `products` | Core product catalog item with full-text search vector |
| `product_images` | Images attached to a product with display ordering |
| `inventory_records` | One record per product tracking current stock |
| `offers` | Promotional offers — can target a Product or a Category |

**Subscription tiers and product limits:**

| Tier | Max Active Products | Commission Rate |
|---|---|---|
| Basic | 250 | 5% |
| Standard | 1,000 | 3% |
| Enterprise | Unlimited | 1% |

---

## 7. What Has Been Built (Phases 1–6)

### Phase 1 — Foundation & Standards

**Prompts:** P-001 through P-010  
**Files:** `01-SystemArchitecture.md`, `02-DatabaseSchema.md`, `03-CodingStandards.md`, `04-CQRSConventions.md`, `05-ValidationConventions.md`, `06-RepositoryConventions.md`, `07-SecurityArchitecture.md`, `08-LoggingStrategy.md`, `09-BackgroundJobs.md`, `10-BaseApiController.md`, `InitialCode.md`

This phase established every foundational piece before writing a single feature line:

**What was built:**
- Complete solution scaffold with all four projects (`Domain`, `Application`, `Infrastructure`, `API`) and both test projects (`Tests.Unit`, `Tests.Architecture`)
- `BaseEntity` with audit fields and soft-delete flag
- Full domain exception hierarchy: `NotFoundException`, `ValidationException`, `BusinessRuleException`, `ConflictException`, `UnauthorizedException`, `ExternalServiceException`
- `ApiResponse<T>` and `ApiErrorResponse` standardized response envelopes used by every endpoint
- `BaseApiController` (authenticated) and `PublicController` (unauthenticated) — every controller in the system inherits from one of these
- **IDOR protection** in `BaseApiController.EnsureRetailerOwnership()` — any attempt to access another retailer's resource throws `UnauthorizedException` before the handler executes
- `ICurrentUserService` that extracts `RetailerId` (typed `Guid`) from JWT claims — this is the **only** place `RetailerId` ever comes from
- `ExceptionHandlingMiddleware` that converts all domain exceptions to the correct HTTP status codes (see Section 15)
- Three MediatR pipeline behaviors: `LoggingBehavior`, `ValidationBehavior` (auto-triggers FluentValidation), `PerformanceBehavior` (logs warnings for slow requests)
- `IRepository<T>` and `IUnitOfWork` contracts
- `ICacheService` contract (Redis-backed)
- Architecture tests (`ArchitectureTests.cs`) that run on every build and fail CI if any layer dependency rule is violated
- PostgreSQL connection string format and `UseNpgsql` wiring
- All coding standards, naming conventions, and project-wide rules documented

---

### Phase 2 — Authentication

**Prompts:** P-011 (Domain/App), P-012 (Infra/API)  
**Files:** `11-Auth-DomainApp.md`, `12-Auth-InfraAPI.md`

**Domain entities added:**
- `RetailerAccount` — full identity entity with email, `password_hash` (BCrypt work factor 12), `google_id`, `is_email_verified`, `refresh_token_hash`, `refresh_token_expires_at`, `failed_login_attempts`, `lockout_until`, `subscription_id` FK, `status`

**Application commands & handlers:**

| Command | What It Does |
|---|---|
| `LoginCommand` | Validates email/password, checks lockout, increments failed attempts, returns RS256 JWT + refresh token |
| `LoginWithGoogleCommand` | Verifies Google ID token via Google SDK, upserts retailer account, returns tokens |
| `RegisterStep1Command` | Creates account with unverified email, sends verification OTP via email |
| `RegisterStep2Command` | Validates OTP, marks email as verified, activates account |
| `RefreshTokenCommand` | Rotates refresh token (old hash invalidated, new pair issued) |
| `LogoutCommand` | Clears `refresh_token_hash` in DB — token cannot be reused |
| `ForgotPasswordCommand` | Generates reset token, sends password reset email |
| `ResetPasswordCommand` | Validates reset token, hashes new password with BCrypt, saves |

**Infrastructure services added:**
- `TokenService` — Issues RS256 JWT (15-min expiry) + secure random refresh token (7-day expiry). Access token carries: `sub`, `retailer_id`, `email`, `role=Retailer`
- `GoogleAuthService` — Verifies Google ID token using `Google.Apis.Auth`
- `EmailService` — Sends HTML emails (OTP verification, password reset) via MailKit/SMTP
- `FileStorageService` — AWS S3 upload/delete for brand logos and product images

**API endpoints (all under `/api/v1/auth`):**

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/login` | Public | Email + password login |
| POST | `/login/google` | Public | Google OAuth login |
| POST | `/register/step1` | Public | Create account + send OTP |
| POST | `/register/step2` | Public | Verify OTP |
| POST | `/refresh-token` | Public | Rotate access + refresh tokens |
| POST | `/logout` | JWT | Invalidate refresh token |
| POST | `/forgot-password` | Public | Request password reset email |
| POST | `/reset-password` | Public | Submit new password with reset token |

**Security highlights:**
- Passwords hashed with BCrypt, work factor 12 — never stored plain or as SHA
- Account lockout after 5 failed login attempts (15-minute lockout window)
- Refresh tokens are hashed before storage — raw token only lives in the HTTP response
- Email verification is mandatory before login is permitted

---

### Phase 3 — Subscriptions & Payments

**Prompts:** P-015 (Domain/App), P-016 (Infra/API)  
**Files:** `15-Subscriptions-DomainApp.md`, `16-Subscriptions-InfraAPI.md`

**Domain entities added:**
- `SubscriptionPlan` — defines tier (`Basic`/`Standard`/`Enterprise`/`SaaS`), billing cycle (`Monthly`/`Yearly`/`SaaS`), `price_amount`, `commission_rate`, `max_active_products`, `max_monthly_try_ons`, feature flags (`is_white_label`, `includes_source_code`, `includes_mobile_apps`, `has_sla`)
- `Subscription` — a retailer's current plan binding; statuses: `None`, `Trial`, `Active`, `PendingDowngrade`, `Expired`, `Cancelled`
- `SubscriptionPayment` — payment transaction records per billing cycle
- `SaasEnquiry` — enterprise custom plan request form
- `PaymentMethod` — saved card (Stripe `payment_method_id` + `card_last4` + AES-256 encrypted `cardholder_name`)

**Key business rules enforced in handlers:**
- Prorated upgrade calculation: `(days_remaining / total_days) × (new_price - old_price)`
- Downgrade is queued as `PendingDowngrade` and takes effect at current period end — never immediately
- Plan limit enforcement for product creation lives in `CreateProductCommandHandler`, not in the controller
- `SaasEnquirySubmittedDomainEvent` is raised on new enterprise enquiries (domain event pattern)

**Infrastructure services added:**
- `StripePaymentGatewayService` — wraps Stripe.net with Polly retry (3 attempts, exponential backoff) + CircuitBreaker (5 failures in 30s → open circuit 60s)
- `AesEncryptionService` — AES-256-CBC for cardholder name encryption at rest

**Background jobs (registered in Infrastructure):**
- `RecurringPaymentJob` — runs daily, processes auto-renewal for active subscriptions
- `SubscriptionExpiryJob` — runs daily, expires subscriptions past their `end_date`

**API endpoints:**

| Method | Route | Description |
|---|---|---|
| GET | `/api/v1/subscription-plans` | List all available public plans |
| GET | `/api/v1/subscriptions/current` | Get retailer's current subscription |
| GET | `/api/v1/subscriptions/history` | Paginated payment history |
| POST | `/api/v1/subscriptions/subscribe` | Subscribe to a plan |
| POST | `/api/v1/subscriptions/upgrade` | Upgrade to higher plan (prorated charge) |
| POST | `/api/v1/subscriptions/cancel` | Cancel at period end |
| POST | `/api/v1/subscriptions/enquiry` | Submit enterprise/SaaS enquiry |
| GET | `/api/v1/payment-methods` | List saved payment methods |
| POST | `/api/v1/payment-methods` | Add new payment method |
| PUT | `/api/v1/payment-methods/{id}/default` | Set as default |
| DELETE | `/api/v1/payment-methods/{id}` | Remove payment method |

---

### Phase 4 — Categories

**Prompt:** P-019  
**File:** `19-Categories-Full.md`

**Domain entities added:**
- `Category` — aggregate root with private setters; state changes only via factory method (`Create`) or domain methods (`Update`, `Activate`, `Deactivate`, `SoftDelete`)
- `SubCategory` — max depth = 1 enforced in `CreateSubCategoryCommandHandler` (a SubCategory cannot have its own SubCategory)

**Business rules:**
- Category name must be unique per retailer (partial unique index: `(retailer_id, name) WHERE is_deleted = false`)
- SubCategory name must be unique within a parent category (partial unique index: `(category_id, name) WHERE is_deleted = false`)
- Deleting a category with active products throws `BusinessRuleException` — category must be empty first
- `ToggleCategoryStatus` flips between `Active` and `Inactive`

**Queries and commands built:**

| Feature | Type | Description |
|---|---|---|
| `GetCategories` | Query | Paginated list with optional status filter and sub-category count |
| `GetCategoryById` | Query | Single category with its sub-categories |
| `GetSubCategories` | Query | Sub-categories for a given category |
| `CreateCategory` | Command | Validates name uniqueness, creates category |
| `UpdateCategory` | Command | Updates name/description/cover image |
| `DeleteCategory` | Command | Soft delete — only if no active products |
| `ToggleCategoryStatus` | Command | Active ↔ Inactive |
| `CreateSubCategory` | Command | Validates depth constraint and name uniqueness |
| `UpdateSubCategory` | Command | Updates name, validates new name uniqueness |
| `DeleteSubCategory` | Command | Soft delete |

**API endpoints (all under `/api/v1/categories`):**

| Method | Route | Description |
|---|---|---|
| GET | `/` | List all categories (paginated) |
| GET | `/{id}` | Get single category with sub-categories |
| GET | `/{id}/sub-categories` | List sub-categories for a category |
| POST | `/` | Create category |
| PUT | `/{id}` | Update category |
| DELETE | `/{id}` | Soft-delete category |
| PATCH | `/{id}/toggle-status` | Toggle Active/Inactive |
| POST | `/{categoryId}/sub-categories` | Create sub-category |
| PUT | `/{categoryId}/sub-categories/{id}` | Update sub-category |
| DELETE | `/{categoryId}/sub-categories/{id}` | Soft-delete sub-category |

---

### Phase 5 — Products

**Prompts:** P-022 (Domain/App), P-023 (Infra/API)  
**Files:** `22-Products-DomainApp.md`, `23-Products-InfraAPI.md`

**Domain entities added:**
- `Product` — full catalog entity with `name`, `description`, `price`, `currency`, `barcode`, `category_id` (nullable), `sub_category_id` (nullable), `status`
- `ProductImage` — linked image URLs with `display_order`
- `InventoryRecord` — stub created alongside product; tracks `current_stock`, `sold_quantity`, `low_stock_threshold`, `row_version` (optimistic concurrency)

**Enums added:** `ProductStatus` (`Active`, `Inactive`, `Draft`), `InventoryStatus`

**Full-text search:** The `products` table has a generated `tsvector` column:
```sql
to_tsvector('english', coalesce(name,'') || ' ' || coalesce(description,'') || ' ' || coalesce(barcode,''))
```
Indexed with GIN. Queries use `plainto_tsquery` (safe for user input — never `to_tsquery`).

**Plan limit enforcement:** `CreateProductCommandHandler` calls `ISubscriptionService.GetCurrentPlanAsync()` and throws `BusinessRuleException` if `active_product_count >= max_active_products`.

**New interfaces introduced:**
- `IProductRepository` — specialized repository for full-text search queries
- `ISubscriptionService` — checks current plan limits from the Application layer

**Queries and commands built:**

| Feature | Type | Description |
|---|---|---|
| `GetProducts` | Query | Paginated list; supports full-text search, category filter, status filter |
| `GetProductById` | Query | Single product with images and inventory snapshot |
| `CreateProduct` | Command | Enforces plan product limit; creates product + inventory record atomically |
| `UpdateProduct` | Command | Updates all product fields |
| `DeleteProduct` | Command | Soft-delete; marks inventory as deleted |
| `ToggleProductStatus` | Command | Active ↔ Inactive ↔ Draft |
| `AddProductImage` | Command | Uploads image to S3, saves URL with display order |
| `RemoveProductImage` | Command | Deletes from S3, soft-deletes image record |

**API endpoints (all under `/api/v1/products`):**

| Method | Route | Description |
|---|---|---|
| GET | `/` | List products (paginated, searchable) |
| GET | `/{id}` | Get product details with images |
| POST | `/` | Create product |
| PUT | `/{id}` | Update product |
| DELETE | `/{id}` | Soft-delete product |
| PATCH | `/{id}/toggle-status` | Toggle product status |
| POST | `/{id}/images` | Upload product image |
| DELETE | `/{id}/images/{imageId}` | Remove product image |

---

### Phase 6 — Offers

**Prompt:** P-026  
**File:** `26-Offers-Full.md`

**Domain entities added:**
- `Offer` — promotional offer targeting either a single Product or an entire Category

**Enums added:** `OfferType` (`Product`, `Category`), `DiscountType` (`Percentage`, `Fixed`), `OfferStatus` (`Active`, `Inactive`, `Expired`, `Scheduled`)

**Database constraint:** A DB CHECK constraint enforces mutual exclusivity:
```sql
CHECK (
  (offer_type = 'Product' AND product_id IS NOT NULL AND category_id IS NULL)
  OR
  (offer_type = 'Category' AND category_id IS NOT NULL AND product_id IS NULL)
)
```

**Business rules:**
- `discount_value` for `Percentage` type must be between 0.01 and 100
- `start_date` must not be in the past on creation
- `end_date`, if provided, must be after `start_date`
- Offer cannot target a product or category belonging to a different retailer (IDOR check)

**Background job:** `OfferExpiryJob` runs every hour and calls `offer.Deactivate()` on all offers whose `end_date < now()`. Uses a Redis distributed lock to prevent duplicate execution.

**Queries and commands built:**

| Feature | Type | Description |
|---|---|---|
| `GetOffers` | Query | Paginated; filter by type, status, date range |
| `GetOfferById` | Query | Single offer with linked product/category details |
| `CreateOffer` | Command | Creates offer, validates target ownership |
| `UpdateOffer` | Command | Updates all offer fields |
| `DeleteOffer` | Command | Soft-delete |
| `ToggleOfferStatus` | Command | Active ↔ Inactive |

**API endpoints (all under `/api/v1/offers`):**

| Method | Route | Description |
|---|---|---|
| GET | `/` | List offers (paginated, filterable) |
| GET | `/{id}` | Get offer details |
| POST | `/` | Create offer |
| PUT | `/{id}` | Update offer |
| DELETE | `/{id}` | Soft-delete offer |
| PATCH | `/{id}/toggle-status` | Toggle offer status |

---

## 8. API Endpoint Reference

All authenticated endpoints require the header:
```
Authorization: Bearer <access_token>
```

The base route is: `/api/v1/{resource}`

**Authentication** (Public — no JWT required)

| Method | Route | Description |
|---|---|---|
| POST | `/api/v1/auth/login` | Email/password login |
| POST | `/api/v1/auth/login/google` | Google OAuth login |
| POST | `/api/v1/auth/register/step1` | Register + send OTP |
| POST | `/api/v1/auth/register/step2` | Verify OTP |
| POST | `/api/v1/auth/refresh-token` | Rotate tokens |
| POST | `/api/v1/auth/logout` | Invalidate session |
| POST | `/api/v1/auth/forgot-password` | Send reset email |
| POST | `/api/v1/auth/reset-password` | Reset password |

**Subscriptions** (JWT required)

| Method | Route | Description |
|---|---|---|
| GET | `/api/v1/subscription-plans` | All available plans |
| GET | `/api/v1/subscriptions/current` | Current subscription |
| GET | `/api/v1/subscriptions/history` | Payment history |
| POST | `/api/v1/subscriptions/subscribe` | New subscription |
| POST | `/api/v1/subscriptions/upgrade` | Plan upgrade |
| POST | `/api/v1/subscriptions/cancel` | Cancel subscription |
| POST | `/api/v1/subscriptions/enquiry` | SaaS enquiry |
| GET | `/api/v1/payment-methods` | Saved cards |
| POST | `/api/v1/payment-methods` | Add card |
| PUT | `/api/v1/payment-methods/{id}/default` | Set default |
| DELETE | `/api/v1/payment-methods/{id}` | Remove card |

**Categories** (JWT required)

| Method | Route | Description |
|---|---|---|
| GET | `/api/v1/categories` | List categories |
| GET | `/api/v1/categories/{id}` | Category + sub-categories |
| GET | `/api/v1/categories/{id}/sub-categories` | Sub-categories only |
| POST | `/api/v1/categories` | Create category |
| PUT | `/api/v1/categories/{id}` | Update category |
| DELETE | `/api/v1/categories/{id}` | Delete category |
| PATCH | `/api/v1/categories/{id}/toggle-status` | Toggle status |
| POST | `/api/v1/categories/{categoryId}/sub-categories` | Create sub-category |
| PUT | `/api/v1/categories/{categoryId}/sub-categories/{id}` | Update sub-category |
| DELETE | `/api/v1/categories/{categoryId}/sub-categories/{id}` | Delete sub-category |

**Products** (JWT required)

| Method | Route | Description |
|---|---|---|
| GET | `/api/v1/products` | List products (search + filter) |
| GET | `/api/v1/products/{id}` | Product details |
| POST | `/api/v1/products` | Create product |
| PUT | `/api/v1/products/{id}` | Update product |
| DELETE | `/api/v1/products/{id}` | Delete product |
| PATCH | `/api/v1/products/{id}/toggle-status` | Toggle status |
| POST | `/api/v1/products/{id}/images` | Add image |
| DELETE | `/api/v1/products/{id}/images/{imageId}` | Remove image |

**Offers** (JWT required)

| Method | Route | Description |
|---|---|---|
| GET | `/api/v1/offers` | List offers |
| GET | `/api/v1/offers/{id}` | Offer details |
| POST | `/api/v1/offers` | Create offer |
| PUT | `/api/v1/offers/{id}` | Update offer |
| DELETE | `/api/v1/offers/{id}` | Delete offer |
| PATCH | `/api/v1/offers/{id}/toggle-status` | Toggle status |

---

## 9. Core Coding Standards

These are binding rules. Code that violates them must be corrected before code review.

### Naming Rules

| Construct | Convention | Example |
|---|---|---|
| Class / Record / Enum | PascalCase | `CreateProductCommand`, `SubscriptionTier` |
| Interface | `I` prefix + PascalCase | `IRepository`, `ICacheService` |
| Public method | PascalCase + `Async` suffix if async | `GetByIdAsync`, `CreateAsync` |
| Private field | `_camelCase` | `_unitOfWork`, `_cacheService` |
| Local variable | camelCase | `activeProductCount` |
| Constant | PascalCase | `public const string DefaultCurrency = "EGP"` |
| Boolean | Affirmative predicate | `isActive`, `hasActiveSubscription` ✅ — NOT `active`, `notDeleted` ❌ |
| Test class | `{ClassName}Tests` | `CreateProductCommandHandlerTests` |

> `ALL_CAPS_SNAKE_CASE` is **explicitly forbidden** for any identifier in this project.

### File & Namespace Rules
- One class per file. File name equals class name.
- Namespace mirrors folder path: `Application.Features.Products.Commands.CreateProduct`
- Use `global using` directives in `GlobalUsings.cs` per project for common namespaces
- No `using` directives inside class bodies

### Method Length & Complexity
- Maximum method length: 30 lines. If longer, extract a private helper method
- Maximum cyclomatic complexity: 10. Deeply nested `if` trees must be refactored
- Early return pattern preferred over `else` after a return/throw

### Dependency Injection Rules
- Constructor injection only — no property injection, no service locator (`IServiceProvider` direct calls)
- Maximum 4 constructor parameters. If more are needed, introduce a dependency facade or reconsider responsibility
- Scoped services must never be injected into singleton services (produces a captive dependency bug)

---

## 10. CQRS & MediatR Conventions

Every feature in the Application layer follows the same file layout:

```
Application/Features/{Feature}/
├── Commands/
│   └── {CommandName}/
│       ├── {CommandName}Command.cs         ← record with properties
│       ├── {CommandName}CommandHandler.cs  ← IRequestHandler<TCommand, TResponse>
│       └── {CommandName}CommandValidator.cs ← AbstractValidator<TCommand>
└── Queries/
    └── {QueryName}/
        ├── {QueryName}Query.cs
        └── {QueryName}QueryHandler.cs
```

**Commands** return one of:
- `Guid` (created resource Id)
- `Unit` (no return value needed)
- A DTO if the caller needs data after the mutation

**Queries** always return a DTO or `PagedResult<TDto>`. They never return domain entities.

**Pipeline behaviors execute in this order for every request:**
1. `LoggingBehavior` — logs request start with feature name and `RetailerId`
2. `ValidationBehavior` — runs all FluentValidation validators; throws `ValidationException` on failure (→ HTTP 422)
3. `PerformanceBehavior` — records execution time; logs a warning if a request exceeds 500ms
4. Handler executes

---

## 11. Validation Conventions

FluentValidation is used for all input validation. Validators live in the same folder as the command they validate.

**Key rules:**
- Use `.WithMessage("…")` on every rule — generic messages are not permitted
- Use `.WithErrorCode("…")` for business-rule failures so the client can identify the specific error
- Validators must not call the database. If a uniqueness check requires a DB query, it belongs in the command handler, which throws `ConflictException`
- Use `.NotEmpty()` for required GUIDs (not `.NotNull()` alone)
- Use `.MaximumLength(N)` for all string fields to match the database column definition
- Use `.InclusiveBetween(0.01, 100)` for percentage discount values

---

## 12. Security Model

### JWT RS256
- Access tokens are signed with a **2048-bit RSA private key** (RS256 algorithm). HS256 (symmetric) is forbidden
- Access token lifetime: **15 minutes**
- Refresh token lifetime: **7 days** — stored as a BCrypt hash in `retailer_accounts.refresh_token_hash`
- Refresh token rotation: every `RefreshToken` call issues a new pair and invalidates the old hash
- Token payload claims: `sub` (retailer account id), `retailer_id`, `email`, `role=Retailer`

### Tenant Isolation
- `RetailerId` is **always** read from `ICurrentUserService.RetailerId` (extracted from the validated JWT claim)
- It is **never** read from a URL path parameter or request body
- `BaseApiController.EnsureRetailerOwnership(resourceRetailerId)` must be called on every write endpoint that loads a resource by ID. It throws `UnauthorizedException` if the resource belongs to a different retailer

### Password Security
- Passwords are hashed with BCrypt, work factor 12
- Account lockout: 5 consecutive failures → `lockout_until = now() + 15 minutes`

### Sensitive Data
- Card numbers are **never stored** — only `card_last4` and the Stripe `payment_method_id`
- Cardholder name is stored AES-256-CBC encrypted
- Refresh tokens are BCrypt-hashed before storage
- PII fields are never written to structured logs (email is allowed; password hash, card data, tokens are forbidden)

### Rate Limiting
- Built-in ASP.NET Core rate limiting (not `AspNetCoreRateLimit` — that package is forbidden)
- Auth endpoints (login, register, forgot-password): 10 requests per IP per minute
- General API: 100 requests per JWT per minute

---

## 13. Logging Strategy

Serilog is used throughout with structured properties. The following enrichment properties are present on every log event:

| Property | Source | Example |
|---|---|---|
| `{RetailerId}` | `ICurrentUserService` | `"3f2ca1b0-..."` |
| `{CorrelationId}` | `X-Correlation-Id` header or generated | `"abc-123"` |
| `{Feature}` | `LoggingBehavior` | `"CreateProduct"` |
| `{RequestDuration}` | `PerformanceBehavior` | `"342ms"` |

**Log level rules:**
- `Information` — successful command/query completions
- `Warning` — slow requests (>500ms), business rule violations, retry attempts
- `Error` — unhandled exceptions caught by `ExceptionHandlingMiddleware`
- `Critical` — external service (Stripe, S3, SMTP) circuit breaker opening

**What is NEVER logged:**
- Passwords, password hashes, or any credential
- Full JWT tokens
- Raw card numbers or CVV
- Full personal identification data beyond what is necessary

---

## 14. Background Jobs

All jobs extend `Microsoft.Extensions.Hosting.BackgroundService`. No third-party scheduler (Hangfire, Quartz) is used.

| Job | Schedule | What It Does |
|---|---|---|
| `OfferExpiryJob` | Every hour | Sets `status = Expired` on offers whose `end_date < now()` |
| `RecurringPaymentJob` | Daily at 02:00 | Charges retailers for auto-renewal subscriptions |
| `SubscriptionExpiryJob` | Daily at 01:00 | Sets `status = Expired` on subscriptions past `end_date` |
| `AccountDeletionJob` | Daily at 03:00 | Hard-deletes accounts that were soft-deleted >30 days ago |
| `DashboardSnapshotJob` | Daily at 00:30 | Pre-aggregates dashboard analytics into snapshot tables |

**Idempotency:** Every job acquires a **Redis distributed lock** before executing. If the lock is held, the job skips that cycle. This prevents duplicate execution when multiple instances are running.

**Failure isolation:** Each job iteration is wrapped in a `try/catch`. A single failure does not crash the host. Errors are logged at `Error` level with full context. The next scheduled cycle will retry.

---

## 15. Error Handling & HTTP Response Map

`ExceptionHandlingMiddleware` converts all domain exceptions to RFC 7807 Problem Details responses.

| Exception Type | HTTP Status | When Used |
|---|---|---|
| `NotFoundException` | 404 Not Found | Resource does not exist or is soft-deleted |
| `ValidationException` (FluentValidation) | 422 Unprocessable Entity | Input field validation failures |
| `BusinessRuleException` | 422 Unprocessable Entity | Domain rule violated (e.g., product limit reached) |
| `ConflictException` | 409 Conflict | Duplicate record (e.g., category name already exists) |
| `UnauthorizedException` | 403 Forbidden | IDOR check failed — wrong retailer |
| `ExternalServiceException` | 502 Bad Gateway | Stripe / S3 / SMTP call failed after retries |
| `Exception` (unhandled) | 500 Internal Server Error | Unexpected error — full details logged, safe message returned |

**Success response envelope (`ApiResponse<T>`):**
```json
{
  "success": true,
  "data": { ... },
  "traceId": "abc-123"
}
```

**Error response envelope (`ApiErrorResponse`):**
```json
{
  "success": false,
  "message": "Validation failed.",
  "code": "PRODUCT_LIMIT_EXCEEDED",
  "errors": ["Name is required.", "Price must be greater than 0."],
  "traceId": "abc-123"
}
```

---

## 16. Non-Negotiable Rules (Read This First)

Every team member must understand and follow these rules without exception. They are enforced by architecture tests, code review, and CI.

1. **No AutoMapper** — All mapping is done manually with private static extension methods in the Application layer. AutoMapper is forbidden because it hides mapping bugs at compile time.

2. **PostgreSQL only** — `UseNpgsql` and `UseSnakeCaseNamingConvention` everywhere. Never add `Microsoft.EntityFrameworkCore.SqlServer`.

3. **JWT RS256** — Never HS256. Access tokens use an asymmetric RSA keypair.

4. **RetailerId from JWT only** — `ICurrentUserService.RetailerId` is the only source. Never from URL parameters or the request body.

5. **ValidationException → 422** — Not 400. This is the standard throughout the project.

6. **BusinessRuleException → 422** with a `code` field in the response so clients can identify the rule.

7. **ConflictException → 409** — Used for any duplicate resource attempt.

8. **`plainto_tsquery` for user input** — Never `to_tsquery` directly on user-supplied strings (SQL injection risk).

9. **`IAsyncEnumerable` for any export** — CSV and large data exports must stream — never load all rows into memory.

10. **ASP.NET Core built-in rate limiting** — Not `AspNetCoreRateLimit`.

11. **Polly v8 pipeline syntax** — Not legacy v7 `Policy.Handle<>()`.

12. **Atomic operations** — Any command that touches multiple DB tables uses ONE transaction via `IUnitOfWork`. Never save partial state.

13. **`EnsureRetailerOwnership` on every write** — IDOR check is mandatory on every controller action that modifies a resource loaded by ID.

14. **No logic in controllers** — Controllers dispatch to MediatR and return the result. Zero business logic, zero mapping, zero validation.

---

## 17. Development Setup

### Prerequisites
- .NET 9 SDK
- PostgreSQL 15+
- Redis (local or remote)
- AWS account (for S3 — product images)
- Stripe test account
- Google OAuth client credentials

### Connection String Format
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=vfr_db;Username=postgres;Password=yourpassword;MaxPoolSize=20;MinPoolSize=2;ConnectionIdleLifetime=300"
}
```

### JWT RS256 Keys (Development)
In development the application auto-generates an in-memory RSA key on startup. For production, generate a persistent keypair:
```bash
openssl genrsa -out private.pem 2048
openssl rsa -in private.pem -pubout -out public.pem
```
Store both files in your secrets manager and point `appsettings.json` to their paths:
```json
"Jwt": {
  "PrivateKeyPath": "/run/secrets/jwt_private.pem",
  "PublicKeyPath": "/run/secrets/jwt_public.pem",
  "Issuer": "VFR-API",
  "Audience": "VFR-Retailer",
  "AccessTokenExpiryMinutes": 15,
  "RefreshTokenExpiryDays": 7
}
```

### Running Migrations
```bash
cd src/API
dotnet ef database update --project ../Infrastructure
```

### Running Architecture Tests
```bash
cd tests/Tests.Architecture
dotnet test
```
If any layer boundary is violated, these tests will fail with a descriptive message naming the offending dependency.

### Running All Tests
```bash
dotnet test
```

---

## 18. What Comes Next — Phase 7 (Orders)

**Status:** 🔴 Not yet started  
**Prompt:** P-029 — `29-Orders-Full.md`

The Orders phase will add the following to the system:

**Domain entities to be added:**
- `Order` — order header with status lifecycle: `NotProcessed` → `Processing` → `Shipped` → `Delivered` / `Cancelled`
- `OrderItem` — line items with snapshotted `product_name`, `unit_price`, and `quantity` at time of order

**Important design decision for the team:** Order items snapshot the product name and price at the time the order is placed. This is intentional — if a product's price changes later, existing orders must still reflect the original price. Do not create a FK from `order_items` to `products` for pricing purposes.

**Features to be implemented:**
- `GetOrders` query — paginated list with filters by status, date range
- `GetOrderById` query — order detail with all line items
- `UpdateOrderStatus` command — retailer moves order through the status lifecycle
- `CancelOrder` command — with business rule: orders in `Shipped` or `Delivered` state cannot be cancelled

**Integration with Inventory:** When an order moves to `Processing`, the `InventoryRecord.current_stock` must be decremented atomically using optimistic concurrency (`row_version`). If the row version has changed since the record was read (concurrent order), the handler must retry the transaction. This is the most complex part of Phase 7.

**Files the team needs to review before implementing:**
- `02-DatabaseSchema.md` — orders + order_items table schema
- `06-RepositoryConventions.md` — `IUnitOfWork` transaction pattern
- `22-Products-DomainApp.md` — `InventoryRecord` entity and optimistic concurrency design
- `04-CQRSConventions.md` — handler structure template

---

*Documentation generated for the VFR weAR graduation project — Retailer Backend Module*  
*Phases 1–6 complete | Phase 7 (Orders) is the next milestone*