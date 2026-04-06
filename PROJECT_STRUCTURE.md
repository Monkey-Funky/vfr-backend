# VFR Backend Project Structure

## Overview
This document outlines the complete structure of the VFR Backend project organized by architectural layers: API, Domain, Infrastructure, and Shared, following Clean Architecture principles.

---

## 🌐 API Layer
**Purpose**: Entry point for HTTP requests, controllers, and API-specific configuration

### API/
```
API/
├── API.csproj                           # Project file
├── API.csproj.user                      # User-specific project settings
├── API.http                             # HTTP testing file
├── Program.cs                           # Application entry point and configuration
├── WeatherForecast.cs                   # Sample weather forecast model
├── appsettings.json                     # Application configuration
├── appsettings.Development.json         # Development environment configuration
├── GlobalUsings.cs                      # Global using statements
├── Controllers/                         # API Controllers
│   ├── BaseApiController.cs            # Base controller with common functionality
│   ├── PublicController.cs              # Public endpoints controller
│   ├── WeatherForecastController.cs    # Weather forecast controller
│   ├── Auth/                           # Authentication controllers
│   │   └── AuthController.cs            # Authentication endpoints
│   ├── Categories/                     # Category management controllers
│   │   ├── CategoriesController.cs     # Main category controller
│   │   ├── CreateCategoryRequest.cs    # DTO for creating categories
│   │   ├── CreateSubCategoryRequest.cs # DTO for creating subcategories
│   │   ├── UpdateCategoryRequest.cs    # DTO for updating categories
│   │   └── UpdateSubCategoryRequest.cs # DTO for updating subcategories
│   ├── Offers/                         # Offer management controllers
│   │   ├── OffersController.cs         # Main offer controller
│   │   └── Requests/                   # Offer request DTOs
│   │       ├── CreateOfferRequest.cs   # DTO for creating offers
│   │       └── UpdateOfferRequest.cs   # DTO for updating offers
│   ├── PaymentMethods/                 # Payment method controllers
│   │   └── PaymentMethodsController.cs # Payment method endpoints
│   ├── Products/                       # Product management controllers
│   │   ├── ProductsController.cs       # Main product controller
│   │   └── Requests/                   # Product request DTOs
│   │       ├── AddProductImageRequest.cs # DTO for adding product images
│   │       ├── CreateProductRequest.cs  # DTO for creating products
│   │       └── UpdateProductRequest.cs  # DTO for updating products
│   └── Subscriptions/                  # Subscription controllers
│       ├── SubscriptionPlansController.cs # Subscription plan endpoints
│       └── SubscriptionsController.cs  # Subscription management endpoints
├── Middleware/                         # Custom middleware
│   └── ExceptionHandlingMiddleware.cs  # Global exception handling
├── Services/                           # API-specific services
│   └── CurrentUserService.cs           # Current user context service
├── Logs/                              # Log files directory
└── Properties/                         # Project properties
    └── launchSettings.json            # Launch configuration
```

---

## 🏛️ Domain Layer
**Purpose**: Core business logic, entities, enums, and domain rules

### Domain/
```
Domain/
├── Domain.csproj                       # Project file
├── GlobalUsings.cs                     # Global using statements
├── Common/                            # Common domain components
│   ├── BaseEntity.cs                  # Base entity with common properties
│   └── IAuditableEntity.cs            # Interface for auditable entities
├── Entities/                          # Domain entities
│   ├── Retailer/                      # Retailer-related entities
│   │   ├── Category.cs                # Product category entity
│   │   ├── InventoryRecord.cs         # Inventory tracking entity
│   │   ├── NotificationPreference.cs # User notification preferences
│   │   ├── Offer.cs                   # Product offer entity
│   │   ├── PaymentMethod.cs           # Payment method entity
│   │   ├── Product.cs                 # Product entity
│   │   ├── ProductImage.cs            # Product image entity
│   │   ├── RetailerAccount.cs         # Retailer account entity
│   │   └── SubCategory.cs             # Product subcategory entity
│   └── Subscriptions/                 # Subscription-related entities
│       ├── SaasEnquiry.cs            # SaaS enquiry entity
│       ├── Subscription.cs            # Subscription entity
│       ├── SubscriptionPayment.cs     # Subscription payment entity
│       └── SubscriptionPlan.cs        # Subscription plan entity
├── Enums/                             # Domain enumerations
│   ├── DiscountType.cs               # Discount type enumeration
│   ├── InventoryStatus.cs            # Inventory status enumeration
│   ├── OfferStatus.cs                # Offer status enumeration
│   ├── OfferType.cs                  # Offer type enumeration
│   ├── ProductStatus.cs              # Product status enumeration
│   ├── SaasEnquiryStatus.cs          # SaaS enquiry status enumeration
│   ├── SubscriptionPaymentStatus.cs  # Subscription payment status enumeration
│   ├── SubscriptionStatus.cs         # Subscription status enumeration
│   └── UserRole.cs                   # User role enumeration
├── Events/                            # Domain events
│   ├── IDomainEvent.cs               # Domain event interface
│   └── SaasEnquirySubmittedDomainEvent.cs # SaaS enquiry submitted event
└── Exceptions/                        # Domain-specific exceptions
    ├── BusinessRuleException.cs      # Business rule violation exception
    ├── ConflictException.cs          # Conflict exception
    ├── DomainException.cs            # Base domain exception
    ├── ExternalServiceException.cs   # External service failure exception
    ├── NotFoundException.cs          # Resource not found exception
    ├── UnauthorizedException.cs      # Unauthorized access exception
    └── ValidationException.cs        # Validation failure exception
```

---

## 🔧 Infrastructure Layer
**Purpose**: External concerns, data persistence, and infrastructure services

### Infrastructure/
```
Infrastructure/
├── Infrastructure.csproj              # Project file
├── GlobalUsings.cs                    # Global using statements
├── DependencyInjection.cs             # Infrastructure DI registration
├── BackgroundJobs/                    # Background job processing
│   ├── CronScheduler.cs              # Cron job scheduler
│   └── OfferExpiryJob.cs             # Offer expiry background job
├── Persistence/                       # Data persistence
│   ├── ApplicationDbContext.cs      # Entity Framework DbContext
│   ├── Configurations/               # Entity configurations
│   │   ├── CategoryConfiguration.cs
│   │   ├── InventoryRecordConfiguration.cs
│   │   ├── NotificationPreferenceConfiguration.cs
│   │   ├── OfferConfiguration.cs
│   │   ├── PaymentMethodConfiguration.cs
│   │   ├── ProductConfiguration.cs
│   │   ├── ProductImageConfiguration.cs
│   │   ├── RetailerAccountConfiguration.cs
│   │   ├── SaasEnquiryConfiguration.cs
│   │   ├── SubCategoryConfiguration.cs
│   │   ├── SubscriptionConfiguration.cs
│   │   ├── SubscriptionPaymentConfiguration.cs
│   │   └── SubscriptionPlanConfiguration.cs
│   ├── Repositories/                  # Repository implementations
│   │   └── ProductRepository.cs       # Product-specific repository
│   ├── Repository.cs                  # Generic repository implementation
│   └── UnitOfWork.cs                  # Unit of work pattern implementation
├── Services/                          # Infrastructure services
│   ├── AesEncryptionService.cs       # AES encryption service
│   ├── CacheService.cs               # Caching service
│   ├── DateTimeService.cs            # DateTime service
│   ├── EmailService.cs               # Email sending service
│   ├── EmailSettings.cs              # Email configuration settings
│   ├── FileStorageService.cs         # File storage service
│   ├── GoogleAuthService.cs          # Google authentication service
│   ├── GoogleSettings.cs             # Google API settings
│   ├── JwtSettings.cs                # JWT configuration settings
│   ├── S3Settings.cs                 # AWS S3 settings
│   ├── StripePaymentGatewayService.cs # Stripe payment gateway service
│   ├── SubscriptionService.cs        # Subscription management service
│   └── TokenService.cs               # Token generation/validation service
├── Settings/                          # Configuration settings
│   └── StripeSettings.cs             # Stripe payment settings
└── Migrations/                        # Database migrations
    ├── 20260401215827_Initial_RetailerAuth.Designer.cs
    ├── 20260401215827_Initial_RetailerAuth.cs
    ├── 20260402150337_AddSubscriptionEntities.Designer.cs
    ├── 20260402150337_AddSubscriptionEntities.cs
    └── ApplicationDbContextModelSnapshot.cs
```

---

## 🔄 Shared Layer
**Purpose**: Common utilities, DTOs, and shared components across layers

### Shared/
```
Shared/
├── Shared.csproj                      # Project file
├── GlobalUsings.cs                    # Global using statements
├── Constants/                         # Application constants
│   └── CacheKeys.cs                   # Cache key constants
└── DTOs/                             # Data Transfer Objects
    ├── ApiErrorResponse.cs           # API error response DTO
    ├── ApiResponse.cs                # API response wrapper DTO
    ├── PagedResult.cs                # Paginated result DTO
    └── Result.cs                     # Operation result DTO
```

---

## 📦 Application Layer
**Purpose**: Application services, CQRS handlers, and business logic orchestration

### Application/
```
Application/
├── Application.csproj                 # Project file
├── GlobalUsings.cs                    # Global using statements
├── DependencyInjection.cs             # Application DI registration
├── Behaviors/                         # MediatR pipeline behaviors
│   ├── LoggingBehavior.cs            # Request/response logging behavior
│   ├── PerformanceBehavior.cs        # Performance monitoring behavior
│   └── ValidationBehavior.cs         # Request validation behavior
├── Common/                            # Common application components
│   └── FileUploadDto.cs              # File upload data transfer object
├── Features/                          # CQRS features organized by domain
│   ├── Auth/                         # Authentication features
│   │   ├── Commands/                 # Auth command handlers
│   │   │   ├── ForgotPassword/       # Forgot password functionality
│   │   │   │   ├── ForgotPasswordCommand.cs
│   │   │   │   ├── ForgotPasswordCommandHandler.cs
│   │   │   │   └── ForgotPasswordCommandValidator.cs
│   │   │   ├── Login/                # User login functionality
│   │   │   │   ├── LoginCommand.cs
│   │   │   │   ├── LoginCommandHandler.cs
│   │   │   │   └── LoginCommandValidator.cs
│   │   │   ├── LoginWithGoogle/      # Google OAuth login
│   │   │   │   ├── LoginWithGoogleCommand.cs
│   │   │   │   └── LoginWithGoogleCommandHandler.cs
│   │   │   ├── Logout/               # User logout functionality
│   │   │   │   ├── LogoutCommand.cs
│   │   │   │   └── LogoutCommandHandler.cs
│   │   │   ├── RefreshToken/         # Token refresh functionality
│   │   │   │   ├── RefreshTokenCommand.cs
│   │   │   │   ├── RefreshTokenCommandHandler.cs
│   │   │   │   └── RefreshTokenCommandValidator.cs
│   │   │   ├── RegisterStep1/        # Registration step 1 (basic info)
│   │   │   │   ├── RegisterStep1Command.cs
│   │   │   │   ├── RegisterStep1CommandHandler.cs
│   │   │   │   └── RegisterStep1CommandValidator.cs
│   │   │   ├── RegisterStep2/        # Registration step 2 (detailed info)
│   │   │   │   ├── RegisterStep2Command.cs
│   │   │   │   ├── RegisterStep2CommandHandler.cs
│   │   │   │   └── RegisterStep2CommandValidator.cs
│   │   │   └── ResetPassword/        # Password reset functionality
│   │   │       ├── ResetPasswordCommand.cs
│   │   │       ├── ResetPasswordCommandHandler.cs
│   │   │       └── ResetPasswordCommandValidator.cs
│   │   ├── DTOs/                     # Authentication data transfer objects
│   │   │   ├── AuthTokenResponse.cs  # Authentication token response
│   │   │   └── RetailerProfileDto.cs # Retailer profile data
│   │   └── Mappings/                 # Authentication mappings
│   │       └── RetailerMappings.cs  # Retailer entity mappings
│   ├── Categories/                   # Category management features
│   │   ├── Commands/                # Category command handlers
│   │   │   ├── CreateCategory/      # Create category functionality
│   │   │   │   ├── CreateCategoryCommand.cs
│   │   │   │   ├── CreateCategoryCommandHandler.cs
│   │   │   │   └── CreateCategoryCommandValidator.cs
│   │   │   ├── CreateSubCategory/   # Create subcategory functionality
│   │   │   │   ├── CreateSubCategoryCommand.cs
│   │   │   │   ├── CreateSubCategoryCommandHandler.cs
│   │   │   │   └── CreateSubCategoryCommandValidator.cs
│   │   │   ├── DeleteCategory/      # Delete category functionality
│   │   │   │   ├── DeleteCategoryCommand.cs
│   │   │   │   └── DeleteCategoryCommandHandler.cs
│   │   │   ├── DeleteSubCategory/   # Delete subcategory functionality
│   │   │   │   ├── DeleteSubCategoryCommand.cs
│   │   │   │   └── DeleteSubCategoryCommandHandler.cs
│   │   │   ├── ToggleCategoryStatus/ # Toggle category active/inactive status
│   │   │   │   ├── ToggleCategoryStatusCommand.cs
│   │   │   │   └── ToggleCategoryStatusCommandHandler.cs
│   │   │   ├── UpdateCategory/      # Update category functionality
│   │   │   │   ├── UpdateCategoryCommand.cs
│   │   │   │   ├── UpdateCategoryCommandHandler.cs
│   │   │   │   └── UpdateCategoryCommandValidator.cs
│   │   │   └── UpdateSubCategory/   # Update subcategory functionality
│   │   │       ├── UpdateSubCategoryCommand.cs
│   │   │       ├── UpdateSubCategoryCommandHandler.cs
│   │   │       └── UpdateSubCategoryCommandValidator.cs
│   │   └── Queries/                 # Category query handlers
│   │       ├── GetCategories/       # Get all categories query
│   │       │   ├── GetCategoriesQuery.cs
│   │       │   ├── GetCategoriesQueryHandler.cs
│   │       │   └── GetCategoriesQueryValidator.cs
│   │       ├── GetCategoryById/     # Get category by ID query
│   │       │   ├── GetCategoryByIdQuery.cs
│   │       │   └── GetCategoryByIdQueryHandler.cs
│   │       └── GetSubCategories/    # Get subcategories query
│   │           ├── GetSubCategoriesQuery.cs
│   │           └── GetSubCategoriesQueryHandler.cs
│   ├── Offers/                      # Offer management features
│   │   ├── Commands/                # Offer command handlers
│   │   │   ├── CreateOffer/          # Create offer functionality
│   │   │   │   ├── CreateOfferCommand.cs
│   │   │   │   ├── CreateOfferCommandHandler.cs
│   │   │   │   └── CreateOfferCommandValidator.cs
│   │   │   ├── DeleteOffer/          # Delete offer functionality
│   │   │   │   ├── DeleteOfferCommand.cs
│   │   │   │   └── DeleteOfferCommandHandler.cs
│   │   │   ├── ToggleOfferStatus/    # Toggle offer active/inactive status
│   │   │   │   ├── ToggleOfferStatusCommand.cs
│   │   │   │   └── ToggleOfferStatusCommandHandler.cs
│   │   │   └── UpdateOffer/          # Update offer functionality
│   │   │       ├── UpdateOfferCommand.cs
│   │   │       ├── UpdateOfferCommandHandler.cs
│   │   │       └── UpdateOfferCommandValidator.cs
│   │   └── Queries/                 # Offer query handlers
│   │       ├── GetOfferById/         # Get offer by ID query
│   │       │   ├── GetOfferByIdQuery.cs
│   │       │   └── GetOfferByIdQueryHandler.cs
│   │       └── GetOffers/            # Get all offers query
│   │           ├── GetOffersQuery.cs
│   │           ├── GetOffersQueryHandler.cs
│   │           └── GetOffersQueryValidator.cs
│   ├── PaymentMethods/              # Payment method management features
│   │   ├── Commands/                # Payment method command handlers
│   │   │   ├── AddPaymentMethod/     # Add payment method functionality
│   │   │   │   ├── AddPaymentMethodCommand.cs
│   │   │   │   ├── AddPaymentMethodCommandHandler.cs
│   │   │   │   └── AddPaymentMethodCommandValidator.cs
│   │   │   ├── RemovePaymentMethod/  # Remove payment method functionality
│   │   │   │   ├── RemovePaymentMethodCommand.cs
│   │   │   │   └── RemovePaymentMethodCommandHandler.cs
│   │   │   └── SetDefaultPaymentMethod/ # Set default payment method
│   │   │       ├── SetDefaultPaymentMethodCommand.cs
│   │   │       └── SetDefaultPaymentMethodCommandHandler.cs
│   │   └── Queries/                 # Payment method query handlers
│   │       └── GetPaymentMethods/   # Get payment methods query
│   │           ├── GetPaymentMethodsQuery.cs
│   │           └── GetPaymentMethodsQueryHandler.cs
│   ├── Products/                    # Product management features
│   │   ├── Commands/                # Product command handlers
│   │   │   ├── AddProductImage/      # Add product image functionality
│   │   │   │   ├── AddProductImageCommand.cs
│   │   │   │   ├── AddProductImageCommandHandler.cs
│   │   │   │   └── AddProductImageCommandValidator.cs
│   │   │   ├── CreateProduct/        # Create product functionality
│   │   │   │   ├── CreateProductCommand.cs
│   │   │   │   ├── CreateProductCommandHandler.cs
│   │   │   │   └── CreateProductCommandValidator.cs
│   │   │   ├── DeleteProduct/        # Delete product functionality
│   │   │   │   ├── DeleteProductCommand.cs
│   │   │   │   └── DeleteProductCommandHandler.cs
│   │   │   ├── RemoveProductImage/  # Remove product image functionality
│   │   │   │   ├── RemoveProductImageCommand.cs
│   │   │   │   └── RemoveProductImageCommandHandler.cs
│   │   │   ├── ToggleProductStatus/  # Toggle product active/inactive status
│   │   │   │   ├── ToggleProductStatusCommand.cs
│   │   │   │   └── ToggleProductStatusCommandHandler.cs
│   │   │   └── UpdateProduct/        # Update product functionality
│   │   │       ├── UpdateProductCommand.cs
│   │   │       ├── UpdateProductCommandHandler.cs
│   │   │       └── UpdateProductCommandValidator.cs
│   │   └── Queries/                 # Product query handlers
│   │       ├── GetProductById/       # Get product by ID query
│   │       │   ├── GetProductByIdQuery.cs
│   │       │   └── GetProductByIdQueryHandler.cs
│   │       └── GetProducts/          # Get all products query
│   │           ├── GetProductsQuery.cs
│   │           ├── GetProductsQueryHandler.cs
│   │           └── GetProductsQueryValidator.cs
│   └── Subscriptions/               # Subscription management features
│       ├── Commands/                # Subscription command handlers
│       │   ├── DowngradePlan/        # Downgrade subscription plan
│       │   │   ├── DowngradePlanCommand.cs
│       │   │   ├── DowngradePlanCommandHandler.cs
│       │   │   └── DowngradePlanCommandValidator.cs
│       │   ├── SelectPlan/           # Select subscription plan
│       │   │   ├── SelectPlanCommand.cs
│       │   │   ├── SelectPlanCommandHandler.cs
│       │   │   └── SelectPlanCommandValidator.cs
│       │   ├── StartTrial/           # Start subscription trial
│       │   │   ├── StartTrialCommand.cs
│       │   │   ├── StartTrialCommandHandler.cs
│       │   │   └── StartTrialCommandValidator.cs
│       │   ├── SubmitSaasEnquiry/     # Submit SaaS enquiry
│       │   │   ├── SubmitSaasEnquiryCommand.cs
│       │   │   └── SubmitSaasEnquiryCommandHandler.cs
│       │   ├── ToggleRecurringPayment/ # Toggle recurring payment
│       │   │   ├── ToggleRecurringPaymentCommand.cs
│       │   │   └── ToggleRecurringPaymentCommandHandler.cs
│       │   └── UpgradePlan/          # Upgrade subscription plan
│       │       ├── UpgradePlanCommand.cs
│       │       ├── UpgradePlanCommandHandler.cs
│       │       └── UpgradePlanCommandValidator.cs
│       ├── DomainEventHandlers/     # Domain event handlers
│       │   └── SaasEnquirySubmittedDomainEventHandler.cs # SaaS enquiry event handler
│       └── Queries/                 # Subscription query handlers
│           ├── GetAllSubscriptionPlans/ # Get all subscription plans
│           │   ├── GetAllSubscriptionPlansQuery.cs
│           │   └── GetAllSubscriptionPlansQueryHandler.cs
│           ├── GetCurrentSubscription/ # Get current subscription
│           │   ├── GetCurrentSubscriptionQuery.cs
│           │   └── GetCurrentSubscriptionQueryHandler.cs
│           ├── GetCurrentSubscriptionDetails/ # Get current subscription details
│           │   ├── GetCurrentSubscriptionDetailsQuery.cs
│           │   └── GetCurrentSubscriptionDetailsQueryHandler.cs
│           └── GetSubscriptionPlanById/ # Get subscription plan by ID
│               ├── GetSubscriptionPlanByIdQuery.cs
│               └── GetSubscriptionPlanByIdQueryHandler.cs
├── Interfaces/                        # Application interfaces (14 items)
│   ├── IApplicationDbContext.cs    # Application database context interface
│   ├── ICacheService.cs             # Cache service interface
│   ├── ICurrentUserService.cs       # Current user service interface
│   ├── IDateTime.cs                 # DateTime service interface
│   ├── IEmailService.cs             # Email service interface
│   ├── IEncryptionService.cs        # Encryption service interface
│   ├── IFileStorageService.cs       # File storage service interface
│   ├── IGoogleAuthService.cs        # Google authentication service interface
│   ├── IPaymentGatewayService.cs    # Payment gateway service interface
│   ├── IProductRepository.cs        # Product repository interface
│   ├── IRepository.cs               # Generic repository interface
│   ├── ISubscriptionService.cs      # Subscription service interface
│   ├── ITokenService.cs             # Token service interface
│   └── IUnitOfWork.cs               # Unit of work interface
└── Mappings/                         # AutoMapper profiles (12 items)
    ├── CategoryDto.cs               # Category data transfer object
    ├── CategoryMappings.cs          # Category entity mappings
    ├── CurrentSubscriptionDetailsDto.cs # Current subscription details DTO
    ├── CurrentSubscriptionDto.cs    # Current subscription DTO
    ├── OfferDto.cs                  # Offer data transfer object
    ├── OfferMappings.cs             # Offer entity mappings
    ├── PaymentMethodDto.cs          # Payment method data transfer object
    ├── ProductDto.cs                # Product data transfer object
    ├── ProductMappings.cs           # Product entity mappings
    ├── SubscriptionPlanDto.cs       # Subscription plan data transfer object
    ├── SubscriptionPlanGroupDto.cs  # Subscription plan group DTO
    └── SubscriptionSummaryDto.cs    # Subscription summary DTO
```

---

## 🧪 Test Projects

### Tests.Unit/
```
Tests.Unit/
├── Tests.Unit.csproj                  # Unit test project file
└── Auth/                              # Authentication unit tests (7 items)
```

### Tests.Integration/
```
Tests.Integration/
├── Tests.Integration.csproj           # Integration test project file
└── Auth/                              # Authentication integration tests (4 items)
```

### Tests.Architecture/
```
Tests.Architecture/
├── Tests.Architecture.csproj          # Architecture test project file
└── ArchitectureTests.cs               # Architecture compliance tests
```

---

## 📁 Root Level Files

### Configuration & Documentation
```
├── vfr-backend.sln                    # Solution file
├── README.md                          # Project documentation
├── LICENSE                            # License file
├── .gitignore                         # Git ignore rules
├── .git/                              # Git repository
├── .github/                           # GitHub workflows
└── .vs/                               # Visual Studio settings
```

### Security Files
```
├── private.pem                        # Private key file
└── public.pem                         # Public key file
```

---

## 🏗️ Architecture Summary

This project follows **Clean Architecture** principles with clear separation of concerns:

- **API Layer**: Handles HTTP requests and responses
- **Domain Layer**: Contains core business logic and entities
- **Infrastructure Layer**: Manages data persistence and external services
- **Shared Layer**: Provides common utilities and DTOs
- **Application Layer**: Orchestrates business logic using CQRS pattern

Each layer has its own project file and dependencies flow inward, with the Domain layer at the center having no dependencies on other layers.
