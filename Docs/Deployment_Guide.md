# VFR Backend — Complete Production Deployment Guide

This document provides a professional, step-by-step guide to deploying the Virtual Fitting Room (VFR) backend using Neon (PostgreSQL), Upstash (Redis), Cloudflare R2 (Image Storage), and Render.com (Hosting).

---

## Phase 1: External Services Setup

Before deploying the code, you must create the necessary databases and storage buckets.

### Step 1: Neon PostgreSQL (Database)

1. Go to [console.neon.tech](https://console.neon.tech) and **Sign up** (using GitHub is recommended).
2. Click the green **"Create Project"** button.
3. Configure the settings:
   - **Project name**: `vfr-backend`
   - **Region**: Choose the region closest to where your application will be hosted (e.g., `aws-eu-central-1` or `aws-us-east-1`).
   - **PostgreSQL version**: `17` (latest).
4. Click **"Create Project"**.
5. In the left sidebar, navigate to **Settings** → **Connection Pooling** and toggle it **ON**.
   - *Why?* Neon's free tier allows 5 direct connections. Connection pooling acts as a proxy (PgBouncer) to multiplex many connections efficiently, preventing your app from crashing under load.
6. Copy the **Pooled Connection String**. It will look similar to this:
   `postgresql://neondb_owner:password@ep-xxx-yyy.region.aws.neon.tech/neondb?sslmode=require`

### Step 2: Upstash Redis (Cache)

1. Go to [console.upstash.com](https://console.upstash.com) and **Sign up**.
2. Under the **Redis** section, click **"Create Database"**.
3. Configure the settings:
   - **Name**: `vfr-cache`
   - **Region**: Select the *same region* you chose for your Neon database.
   - **Type**: `Regional` (free tier).
   - **Eviction**: Enable this option (it prevents the cache from crashing if it runs out of memory by deleting old items).
   - **TLS**: Leave enabled.
4. Click **"Create"**.
5. Once created, go to the database's **Details** tab.
6. Scroll down to find the **"StackExchange.Redis"** connection string format and copy it. It looks like:
   `vfr-cache-xxxxx.upstash.io:6379,password=AxxxxxxxxxxxxxxYYY=,ssl=True,abortConnect=False`

### Step 3: Cloudflare R2 (Image Storage)

1. Go to [dash.cloudflare.com](https://dash.cloudflare.com) and **Sign up** or log in.
2. In the left sidebar, click on **R2 Object Storage**.
3. Click **"Create bucket"**.
   - **Bucket name**: `vfr-assets`
   - **Location**: Automatic.
   - Click **"Create bucket"**.
4. **Enable Public Access**:
   - Inside your new bucket, click the **Settings** tab.
   - Scroll to **"Public Access"** and click **"Allow Access"**. Confirm the prompt.
   - Copy the public URL provided (e.g., `https://pub-xxxxx.r2.dev`).
5. **Create API Tokens**:
   - Go back to the **R2 Overview** page (left sidebar).
   - On the right side of the screen, click **"Manage R2 API Tokens"**.
   - Click **"Create API Token"**.
   - **Token name**: `vfr-backend`
   - **Permissions**: Object Read & Write.
   - **Bucket scope**: Limit to `vfr-assets` only.
   - Click **"Create API Token"**.
6. **Save these credentials immediately** (they are only shown once):
   - `Access Key ID`
   - `Secret Access Key`
   - `S3 API endpoint` (looks like `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`)

---

## Phase 2: Render.com Backend Hosting

Now that your external services are ready, it's time to deploy the actual .NET API using Render.com.

### Step 1: Create the Web Service

1. Go to [render.com](https://render.com) and **Sign up** (using GitHub is highly recommended as we need repo access).
2. Click **"New +"** in the top right corner and select **"Web Service"**.
3. Connect your GitHub account and select the `vfr-backend` repository.
4. Configure the service:
   - **Name**: `vfr-backend`
   - **Region**: Choose the *same region* as your database and Redis.
   - **Branch**: `main`
   - **Runtime**: `Docker` (Render will automatically detect the `Dockerfile` we created).
   - **Instance type**: Select the Free tier (or Starter $7/month if you want to avoid cold starts).

### Step 2: Configure Environment Variables

Scroll down to the **Environment Variables** section. You must add every single variable listed below. 

*Note: In .NET, nested JSON configurations use double underscores (`__`) in environment variables.*

| Key | Value | Where to get it |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Standard value |
| `ConnectionStrings__DefaultConnection` | `Host=ep-xxx.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=xxx;SslMode=Require;Trust Server Certificate=true` | Neon (convert the URL you copied into this format) |
| `Redis__ConnectionString` | `vfr-cache-xxx.upstash.io:6379,password=xxx,ssl=True,abortConnect=False` | Upstash |
| `S3__BucketName` | `vfr-assets` | Cloudflare R2 |
| `S3__Region` | `auto` | Standard value for R2 |
| `S3__BaseUrl` | `https://pub-xxx.r2.dev` | Cloudflare R2 Public URL |
| `S3__AccessKey` | `...` | Cloudflare R2 API Token |
| `S3__SecretKey` | `...` | Cloudflare R2 API Token |
| `S3__ServiceUrl` | `https://<ACCOUNT_ID>.r2.cloudflarestorage.com` | Cloudflare R2 Endpoint |
| `Email__Username` | `...` | Mailtrap Username |
| `Email__Password` | `...` | Mailtrap Password |
| `JwtSettings__StepTokenSecret` | `...` | Generate a random string (e.g., use `openssl rand -base64 48` locally) |
| `EncryptionSettings__Key` | `...` | Generate a random string (e.g., use `openssl rand -base64 32` locally) |
| `JwtSettings__PrivateKeyPem` | `-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----` | Your RSA Private Key (from `private.pem`) |
| `JwtSettings__PublicKeyPem` | `-----BEGIN PUBLIC KEY----- ... -----END PUBLIC KEY-----` | Your RSA Public Key (from `public.pem`) |

*Optional but recommended later: Add Google OAuth and Stripe keys when ready.*

### Step 3: Deploy

1. Click **"Create Web Service"**.
2. Render will now start building your Docker image. This will take a few minutes.
3. Once the build finishes, Render will deploy the container. You can watch the logs to ensure there are no startup crashes.
4. When you see the message "Your service is live", click on the URL provided by Render (e.g., `https://vfr-backend.onrender.com`).

---

## Phase 3: Setup Automated Deployments (CI/CD)

The project includes a GitHub Actions pipeline (`.github/workflows/ci-cd.yml`) that runs tests and tells Render to update automatically whenever you push code to `main`.

1. Go to your new Render Web Service dashboard.
2. Click on **Settings** in the left menu.
3. Scroll down to **Deploy Hook** and copy the URL.
4. Go to your repository on **GitHub.com**.
5. Click **Settings** (the repository settings, not your account).
6. In the left sidebar, go to **Secrets and variables** → **Actions**.
7. Click **"New repository secret"**.
   - **Name**: `RENDER_DEPLOY_HOOK_URL`
   - **Secret**: Paste the URL you copied from Render.
8. Click **"Add secret"**.

Now, every time you push code to `main`, GitHub Actions will build it, run your tests, and if everything passes, it will trigger Render to deploy the new version!

---

## Verification

To verify your deployment is successful:
1. **Health Check:** Visit `https://vfr-backend.onrender.com/health`. You should see a JSON response showing `"status": "Healthy"` for both the database and Redis.
2. **Swagger Docs:** Visit `https://vfr-backend.onrender.com/swagger`. The Swagger interface should load properly.
