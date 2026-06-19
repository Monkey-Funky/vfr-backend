# Security Policy

## Reporting a Vulnerability

Please report security vulnerabilities by emailing the team directly. Do **not** open a public GitHub issue.

---

## Secrets Policy

### Critical Rule

> **Any secret committed to GitHub — even briefly — must be considered compromised and rotated immediately in the provider's dashboard. Moving the same key to `.env` is NOT sufficient.**

This applies to:
- fal.ai API keys
- Cloudinary API key and secret
- Stripe secret key and webhook secret
- SMTP credentials
- JWT private key and step-token secret
- Database connection strings / passwords
- Google OAuth client secrets
- AWS access/secret keys
- Any other token or credential

### What Was Exposed

Historical commits contained real credentials in `appsettings.Development.json`. These must be rotated:

| Secret | Provider Dashboard |
|---|---|
| `FalAi:ApiKey` | https://fal.ai/dashboard |
| `Cloudinary:ApiKey` + `ApiSecret` | https://console.cloudinary.com |
| `Stripe:SecretKey` + `PublishableKey` | https://dashboard.stripe.com/apikeys |
| `JwtSettings:PrivateKeyPem` + `StepTokenSecret` | Generate new RSA key pair |
| `Email:Password` (Mailtrap) | https://mailtrap.io |
| `WeatherApi:ApiKey` | https://www.weatherapi.com/my |
| `Google:ClientId` | https://console.cloud.google.com |

---

## Local Development

Use `dotnet user-secrets` to store secrets locally (never committed to git):

```bash
cd API
dotnet user-secrets set "FalAi:ApiKey" "your-new-key"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=vfr_dev;Username=postgres;Password=yourpassword"
dotnet user-secrets set "JwtSettings:PrivateKeyPem" "$(cat private.pem)"
dotnet user-secrets set "JwtSettings:PublicKeyPem" "$(cat public.pem)"
dotnet user-secrets set "JwtSettings:StepTokenSecret" "$(openssl rand -base64 64)"
dotnet user-secrets set "Cloudinary:ApiKey" "your-key"
dotnet user-secrets set "Cloudinary:ApiSecret" "your-secret"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Email:Password" "your-smtp-password"
```

---

## Production Deployment (Render)

Set secrets as **Environment Variables** in the Render dashboard (not in config files):

| Environment Variable | Maps To |
|---|---|
| `FalAi__ApiKey` | `FalAi:ApiKey` |
| `Cloudinary__ApiKey` | `Cloudinary:ApiKey` |
| `Cloudinary__ApiSecret` | `Cloudinary:ApiSecret` |
| `Stripe__SecretKey` | `Stripe:SecretKey` |
| `Stripe__PublishableKey` | `Stripe:PublishableKey` |
| `StripeSettings__WebhookSecret` | `StripeSettings:WebhookSecret` |
| `JwtSettings__PrivateKeyPem` | `JwtSettings:PrivateKeyPem` |
| `JwtSettings__StepTokenSecret` | `JwtSettings:StepTokenSecret` |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` |
| `Email__Password` | `Email:Password` |
| `Cloudinary__CloudName` | `Cloudinary:CloudName` |
| `Google__ClientId` | `Google:ClientId` |
| `Redis__ConnectionString` | `Redis:ConnectionString` |
| `EncryptionSettings__Key` | `EncryptionSettings:Key` |

Double underscores (`__`) are the .NET convention for nested config keys in environment variables.

---

## GitHub Actions

Store secrets in **GitHub → Settings → Secrets and variables → Actions** and reference them in workflows:

```yaml
env:
  FalAi__ApiKey: ${{ secrets.FAL_AI_API_KEY }}
  ConnectionStrings__DefaultConnection: ${{ secrets.DATABASE_URL }}
```

---

## Safe Files

The following config files are safe to commit because they contain only placeholders or non-sensitive defaults:

- `appsettings.json` — public defaults, no secrets
- `appsettings.Production.json` — log levels and rate limits only
- `.env.example` — placeholder values only
- `appsettings.Development.json` — placeholders only (real values via user-secrets)
