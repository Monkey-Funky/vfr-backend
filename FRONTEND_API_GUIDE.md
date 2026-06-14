# 🎯 VFR Backend — Frontend Integration Guide

> **Audience:** Frontend team  
> **Base URL:** `https://your-domain.com` (Render.com)  
> **Auth:** All endpoints require `Authorization: Bearer {accessToken}` header  
> **Role:** All endpoints below require **Customer** role JWT token

---

## 📌 Table of Contents

1. [Authentication & User Setup](#1-authentication--user-setup)
2. [Avatar (Body Measurements)](#2-avatar--body-measurements)
3. [Virtual Try-On](#3-virtual-try-on)
4. [Outfits](#4-outfits)
5. [Outfit Suggestions (AI)](#5-outfit-suggestions-ai)
6. [Wardrobe Collections](#6-wardrobe-collections)
7. [Complete User Journey Example](#7-complete-user-journey-example)
8. [Error Codes Reference](#8-error-codes-reference)
9. [Important Notes](#9-important-notes)

---

## 1. Authentication & User Setup

### How Roles Work

The system has **two separate roles** — they are completely independent:

| Role | Purpose | Can Access |
|------|---------|------------|
| **Customer** | End-user shopper | Avatar, TryOn, Outfits, Wardrobe, Catalog, Favorites |
| **Retailer** | Store owner/dashboard | Products, Categories, Inventory, Orders, Analytics |

> ⚠️ **A single JWT token has exactly ONE role.** A Customer token CANNOT access Retailer endpoints and vice versa. There is no "super admin" role.

### Register a Customer

```
POST /api/customer/auth/register
```

**Request Body:**
```json
{
  "fullName": "Ahmed Test User",
  "email": "ahmed@test.com",
  "password": "Test@12345"
}
```

**Response (200):** Returns a `tempStepToken` for profile completion.

### Complete Profile

```
POST /api/customer/auth/complete-profile
```

**Request Body:**
```json
{
  "gender": "Male",
  "dateOfBirth": "1998-05-15",
  "phoneNumber": "+201234567890",
  "tempStepToken": "eyJ..."
}
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "abc...",
    "expiresAt": "2026-06-14T01:00:00Z"
  }
}
```

### Login

```
POST /api/customer/auth/login
```

**Request Body:**
```json
{
  "email": "ahmed@test.com",
  "password": "Test@12345",
  "rememberMe": false
}
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "abc...",
    "expiresAt": "2026-06-14T01:00:00Z"
  }
}
```

> 💡 Use the `accessToken` in ALL subsequent requests as `Authorization: Bearer {accessToken}`

### About Retailer Data Visibility

- **Customer endpoints** (catalog, products browse) show **ALL retailers'** products that are `Active` and not deleted
- A customer does NOT need to belong to a specific retailer — they see **all products from all retailers**
- The seeded data includes 100 fashion products from a seed retailer — your customer can browse, favorite, and try-on any of them

---

## 2. Avatar (Body Measurements)

> **Base Path:** `api/customers/{customerId}/avatar`

The Avatar represents the customer's **body measurements** used for size recommendations and virtual try-on.

### 2.1 Create Avatar (Manual Measurements)

```
POST /api/customers/{customerId}/avatar
```

**Request Body:**
```json
{
  "heightCm": 175.0,
  "weightKg": 70.0,
  "chestCm": 95.0,
  "waistCm": 80.0,
  "hipsCm": 97.0,
  "shoulderWidthCm": 45.0,
  "inseamCm": 82.0,
  "neckCm": 38.0,
  "armLengthCm": 65.0,
  "shoeSizeEu": 43.0,
  "bodyShape": "Athletic",
  "source": "Manual"
}
```

> Only `heightCm`, `weightKg`, and `source` are required. All others are optional (nullable).

**Response (201 Created):**
```json
{
  "success": true,
  "message": "Resource created successfully.",
  "data": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2026-06-13T20:00:00Z"
}
```

**Errors:**
- `422` — Avatar already exists (one per customer)
- `400` — Validation error (e.g., heightCm < 50)

---

### 2.2 Create/Update Avatar from Photo (AI Extraction)

```
POST /api/customers/{customerId}/avatar/extract-from-image
Content-Type: multipart/form-data
```

**Form Data:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ImageFile` | File (JPEG/PNG) | ✅ | Full-body photo, max 5 MB |
| `HeightCm` | decimal | ✅ | Real height for AI scaling |

**Response (200):**
```json
{
  "success": true,
  "message": "Measurements extracted and saved successfully.",
  "data": {
    "id": "550e8400-...",
    "heightCm": 175.0,
    "weightKg": 72.5,
    "chestCm": 96.0,
    "waistCm": 81.0,
    "hipsCm": 98.0,
    "shoulderWidthCm": 46.0,
    "inseamCm": null,
    "neckCm": null,
    "armLengthCm": null,
    "shoeSizeEu": null,
    "bodyShape": null,
    "avatar3dModelUrl": "https://fal.ai/..../model.glb",
    "lastMeasuredAt": "2026-06-13T20:05:00Z"
  }
}
```

> 💡 This endpoint **creates** a new avatar if none exists, or **updates** the existing one. It also generates a 3D model URL via fal.ai.

---

### 2.3 Get Active Avatar

```
GET /api/customers/{customerId}/avatar
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "id": "550e8400-...",
    "heightCm": 175.0,
    "weightKg": 70.0,
    "chestCm": 95.0,
    "waistCm": 80.0,
    "hipsCm": 97.0,
    "shoulderWidthCm": 45.0,
    "inseamCm": 82.0,
    "neckCm": 38.0,
    "armLengthCm": 65.0,
    "shoeSizeEu": 43.0,
    "bodyShape": "Athletic",
    "avatar3dModelUrl": "https://fal.ai/.../model.glb",
    "lastMeasuredAt": "2026-06-13T20:00:00Z"
  }
}
```

**Errors:**
- `404` — No avatar exists yet

---

### 2.4 Update Measurements

```
PATCH /api/customers/{customerId}/avatar/measurements
```

**Request Body:**
```json
{
  "avatarId": "550e8400-...",
  "heightCm": 175.0,
  "weightKg": 72.0,
  "chestCm": 96.0,
  "waistCm": 82.0,
  "hipsCm": 98.0,
  "shoulderWidthCm": 45.0,
  "inseamCm": 82.0,
  "neckCm": 38.0,
  "armLengthCm": 65.0,
  "shoeSizeEu": 43.0,
  "bodyShape": "Athletic",
  "source": "Manual"
}
```

**Response:** `204 No Content`

> Each update automatically creates a **history snapshot** for tracking changes over time.

---

### 2.5 Get Measurement History

```
GET /api/customers/{customerId}/avatar/history?pageNumber=1&pageSize=20
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "...",
        "measurementDataJson": "{\"heightCm\":175,\"weightKg\":72,...}",
        "source": "Manual",
        "recordedAt": "2026-06-13T20:10:00Z"
      }
    ],
    "totalCount": 3,
    "pageNumber": 1,
    "pageSize": 20
  }
}
```

---

### 2.6 Get Size Recommendation

```
GET /api/customers/{customerId}/avatar/size-recommendation/{productId}
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "productId": "...",
    "recommendedSize": "L",
    "confidenceScore": 0.87,
    "justification": "Based on chest 96cm and waist 82cm..."
  }
}
```

---

### 2.7 Delete Avatar

```
DELETE /api/customers/{customerId}/avatar
```

**Request Body:**
```json
{
  "avatarId": "550e8400-..."
}
```

**Response:** `204 No Content`

> Soft-delete only. History is preserved.

---

## 3. Virtual Try-On

> **Base Path:** `api/customers/{customerId}`

### 3.1 Initiate Try-On Session

```
POST /api/customers/{customerId}/try-on
```

> ⚠️ Rate-limited — max concurrent requests per customer.

**Request Body:**
```json
{
  "customerId": "your-customer-id",
  "productId": "product-guid-to-try",
  "retailerId": "retailer-guid-who-owns-product",
  "sessionType": "VirtualTryOn"
}
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "status": "Completed",
    "resultImageUrl": "https://cloudinary.com/.../tryon-result.jpg",
    "recommendedSize": "M",
    "confidenceScore": 0.92,
    "durationSeconds": 12
  }
}
```

**Status Values:** `Pending`, `Processing`, `Completed`, `Failed`

---

### 3.2 Get All Try-On Sessions

```
GET /api/customers/{customerId}/try-on/sessions?pageNumber=1&pageSize=20
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "session-guid",
        "customerId": "...",
        "productId": "...",
        "retailerId": "...",
        "avatarId": "...",
        "sessionType": "VirtualTryOn",
        "status": "Completed",
        "recommendedSize": "M",
        "confidenceScore": 0.92,
        "resultImageUrl": "https://cloudinary.com/.../result.jpg",
        "durationSeconds": 12,
        "createdAt": "2026-06-13T20:15:00Z"
      }
    ],
    "totalCount": 5,
    "pageNumber": 1,
    "pageSize": 20
  }
}
```

---

### 3.3 Get Single Session by ID

```
GET /api/customers/{customerId}/try-on/sessions/{sessionId}
```

Same response shape as a single item from 3.2.

---

### 3.4 Get Sessions for a Specific Product

```
GET /api/customers/{customerId}/products/{productId}/sessions?pageNumber=1&pageSize=20
```

Same paginated response shape as 3.2, filtered to one product.

---

## 4. Outfits

> **Base Path:** `api/customers/{customerId}/outfits`

Outfits are curated collections of products organized by clothing slots.

### 4.1 Create Outfit

```
POST /api/customers/{customerId}/outfits
```

**Request Body:**
```json
{
  "name": "Summer Office Look",
  "styleCategory": "Business Casual",
  "items": [
    { "productId": "product-1-guid", "slotType": "Top", "displayOrder": 0 },
    { "productId": "product-2-guid", "slotType": "Bottom", "displayOrder": 1 },
    { "productId": "product-3-guid", "slotType": "Footwear", "displayOrder": 2 }
  ]
}
```

**SlotType Values:** `Top`, `Bottom`, `Footwear`, `Accessory`, `Outerwear`, `Dress`

**Response (201):**
```json
{
  "success": true,
  "message": "Outfit created successfully.",
  "data": "new-outfit-guid"
}
```

---

### 4.2 Get All Outfits

```
GET /api/customers/{customerId}/outfits
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "outfit-guid",
        "name": "Summer Office Look",
        "style": "Business Casual",
        "itemCount": 3,
        "slotPreviews": {
          "Top": "https://cloudinary.com/.../top-image.jpg",
          "Bottom": "https://cloudinary.com/.../bottom-image.jpg",
          "Footwear": null
        }
      }
    ],
    "totalCount": 2,
    "pageNumber": 1,
    "pageSize": 2
  }
}
```

---

### 4.3 Get Outfit Detail

```
GET /api/customers/{customerId}/outfits/{outfitId}
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "id": "outfit-guid",
    "name": "Summer Office Look",
    "style": "Business Casual",
    "createdAt": "2026-06-13T20:20:00Z",
    "items": [
      {
        "id": "item-guid",
        "productId": "product-1-guid",
        "slot": "Top",
        "displayOrder": 0,
        "productName": "Linen Button-Down Shirt",
        "brandName": "H&M",
        "price": 29.99,
        "primaryImageUrl": "https://cloudinary.com/.../shirt.jpg",
        "availableColors": ["White", "Blue", "Beige"],
        "stockStatus": "In Stock"
      }
    ]
  }
}
```

---

### 4.4 Update Outfit

```
PUT /api/customers/{customerId}/outfits/{outfitId}
```

**Request Body:**
```json
{
  "name": "Updated Summer Look",
  "styleCategory": "Casual",
  "items": [
    { "productId": "product-1-guid", "slotType": "Top", "displayOrder": 0 },
    { "productId": "product-4-guid", "slotType": "Bottom", "displayOrder": 1 }
  ]
}
```

**Response (200):** `{ "success": true, "data": true }`

---

### 4.5 Delete Outfit

```
DELETE /api/customers/{customerId}/outfits/{outfitId}
```

**Response:** `204 No Content`

---

### 4.6 Get Complementary Products (AI)

```
GET /api/customers/{customerId}/outfits/complementary?productId={guid}
```

Returns AI-recommended products that match the given product visually.

---

## 5. Outfit Suggestions (AI)

> **Base Path:** `api/customer/wardrobe/suggestions`

### 5.1 Generate AI Suggestions

```
POST /api/customer/wardrobe/suggestions
```

**Request Body:**
```json
{
  "weatherCondition": "Sunny",
  "temperatureF": 75.0,
  "occasion": "Office",
  "mood": "Professional"
}
```

**Response (200):**
```json
{
  "success": true,
  "data": [
    {
      "title": "Smart Summer Office",
      "description": "A light, breathable outfit perfect for warm office days",
      "matchPercentage": 92,
      "styleTags": ["Business Casual", "Summer", "Light"],
      "items": [
        {
          "id": "ephemeral-guid",
          "productId": "product-guid",
          "slot": "Top",
          "displayOrder": 0,
          "productName": "Cotton Oxford Shirt",
          "brandName": "Zara",
          "price": 35.99,
          "primaryImageUrl": "https://cloudinary.com/.../shirt.jpg",
          "availableColors": ["White", "Blue"],
          "stockStatus": "In Stock"
        }
      ]
    }
  ]
}
```

> The AI picks from the customer's **favorited products** (wardrobe) and filters by weather.

---

### 5.2 Save a Suggestion as Outfit

```
POST /api/customer/wardrobe/suggestions/save
```

**Request Body:** Same as [Create Outfit](#41-create-outfit) — pass the suggested items with name/style.

**Response (200):** `{ "success": true, "data": "saved-outfit-guid" }`

---

## 6. Wardrobe Collections

> **Base Path:** `api/customers/{customerId}/wardrobe/collections`

Collections are folders of favorited products (like Pinterest boards).

### 6.1 Get All Collections

```
GET /api/customers/{customerId}/wardrobe/collections
```

**Response (200):**
```json
{
  "success": true,
  "data": [
    {
      "id": "collection-guid",
      "name": "Summer Essentials",
      "itemCount": 8,
      "coverImageUrl": "https://cloudinary.com/.../first-product.jpg"
    }
  ]
}
```

### 6.2 Create Collection

```
POST /api/customers/{customerId}/wardrobe/collections
```

**Request Body:**
```json
{ "name": "Winter Favorites" }
```

**Response (201):** Returns the new collection GUID.

### 6.3 Rename Collection

```
PATCH /api/customers/{customerId}/wardrobe/collections/{collectionId}
```

**Request Body:**
```json
{ "newName": "Winter Must-Haves" }
```

**Response:** `204 No Content`

### 6.4 Delete Collection

```
DELETE /api/customers/{customerId}/wardrobe/collections/{collectionId}
```

**Response:** `204 No Content`

### 6.5 Get Collection Items

```
GET /api/customers/{customerId}/wardrobe/collections/{collectionId}/items?pageNumber=1&pageSize=20
```

Returns paginated `ProductCardDto` items (same shape as catalog browse).

### 6.6 Add Item to Collection

```
POST /api/customers/{customerId}/wardrobe/collections/{collectionId}/items
```

**Request Body:**
```json
{ "productId": "product-guid" }
```

**Response:** `204 No Content`

### 6.7 Remove Item from Collection

```
DELETE /api/customers/{customerId}/wardrobe/collections/{collectionId}/items/products/{productId}
```

**Response:** `204 No Content`

---

## 7. Complete User Journey Example

Here is the **exact step-by-step flow** a customer follows from registration to virtual try-on:

### Step 1: Register & Login
```
POST /api/customer/auth/register → get tempStepToken
POST /api/customer/auth/complete-profile → get accessToken
```
Save the `accessToken` — use it for all subsequent requests.
Save the customer ID from the JWT `sub` claim.

### Step 2: Create Avatar (choose ONE method)

**Option A — Manual entry:**
```
POST /api/customers/{customerId}/avatar
Body: { heightCm: 175, weightKg: 70, source: "Manual", ... }
```

**Option B — Photo upload (recommended):**
```
POST /api/customers/{customerId}/avatar/extract-from-image
Form: ImageFile=photo.jpg, HeightCm=175
```

### Step 3: Browse Products & Favorite Some
```
GET /api/catalog/products?pageNumber=1&pageSize=20
POST /api/customers/{customerId}/favorites   (body: { productId: "..." })
```

### Step 4: Try On a Product
```
POST /api/customers/{customerId}/try-on
Body: { customerId, productId, retailerId, sessionType: "VirtualTryOn" }
```
→ Returns `resultImageUrl` showing the product on the customer's avatar.

### Step 5: Get Size Recommendation
```
GET /api/customers/{customerId}/avatar/size-recommendation/{productId}
```
→ Returns recommended size (S/M/L/XL) with confidence score.

### Step 6: Build an Outfit
```
POST /api/customers/{customerId}/outfits
Body: { name: "My Look", items: [...] }
```

### Step 7: Get AI Outfit Suggestions
```
POST /api/customer/wardrobe/suggestions
Body: { weatherCondition: "Sunny", temperatureF: 75, occasion: "Office" }
```

### Step 8: Save a Suggestion
```
POST /api/customer/wardrobe/suggestions/save
Body: { name: "AI Summer Look", items: [...from suggestion...] }
```

---

## 8. Error Codes Reference

All error responses follow this shape:
```json
{
  "code": "ERROR_CODE",
  "message": "Human-readable description",
  "traceId": "request-trace-id"
}
```

| HTTP Status | Code | When |
|------------|------|------|
| `400` | `VALIDATION_ERROR` | Invalid request body |
| `401` | `AUTHENTICATION_FAILED` | Missing/invalid/expired token |
| `403` | `UNAUTHORIZED` | Accessing another customer's resource |
| `404` | `NOT_FOUND` | Avatar/session/outfit doesn't exist |
| `409` | `CONFLICT` | Duplicate collection name |
| `422` | `BUSINESS_RULE_VIOLATION` | Avatar already exists, invalid body shape |
| `429` | `TOO_MANY_REQUESTS` | Try-on rate limit exceeded |
| `500` | `INTERNAL_ERROR` | Server error (report to backend team) |

---

## 9. Important Notes

### The `{customerId}` in URLs
- This MUST match the authenticated user's ID from the JWT token
- If it doesn't match → `403 Forbidden: "You do not have permission to access this resource"`
- Get your customerId from the JWT `sub` claim after login

### Standard Response Wrapper
Every successful response is wrapped in:
```json
{
  "success": true,
  "message": "Request successful",
  "data": { ... },
  "errors": [],
  "timestamp": "2026-06-13T20:00:00Z"
}
```

### Pagination
Paginated endpoints accept `?pageNumber=1&pageSize=20` and return:
```json
{
  "items": [...],
  "totalCount": 50,
  "pageNumber": 1,
  "pageSize": 20
}
```

### Caching
- Avatar: cached 10 min (auto-invalidated on update)
- TryOn sessions: cached 5-10 min
- Outfits: cached 5-10 min
- Categories: cached 15 min

### One Avatar Per Customer
- A customer can only have ONE active avatar at a time
- Creating a second one returns `422`
- Use `PATCH .../measurements` to update, or `DELETE` then re-create
- The `extract-from-image` endpoint auto-creates OR auto-updates (recommended approach)
