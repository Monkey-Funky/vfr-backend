import json
import re

with open(r'g:\Graduate_Project_Backend\Infrastructure\Data\data.json', 'r', encoding='utf-8') as f:
    items = json.load(f)

# Category mapping: Broad Category -> fixed GUID
cat_map = {
    "Bottoms":   "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001",
    "Dresses":   "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002",
    "Knitwear":  "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003",
    "Outerwear": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004",
    "Tops":      "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005",
}

# Price ranges by category
price_map = {
    "Bottoms": [89.99, 119.99, 149.99, 79.99, 99.99, 129.99, 109.99, 139.99, 69.99, 159.99],
    "Dresses": [149.99, 199.99, 179.99, 169.99, 229.99, 189.99, 159.99, 209.99, 139.99, 219.99],
    "Knitwear": [69.99, 89.99, 79.99, 99.99, 109.99, 59.99, 119.99, 84.99, 74.99, 94.99],
    "Outerwear": [189.99, 249.99, 219.99, 179.99, 269.99, 199.99, 229.99, 299.99, 159.99, 239.99],
    "Tops": [49.99, 69.99, 59.99, 79.99, 89.99, 44.99, 54.99, 64.99, 74.99, 99.99],
}
price_counters = {k: 0 for k in price_map}

def get_price(broad_cat):
    prices = price_map[broad_cat]
    idx = price_counters[broad_cat] % len(prices)
    price_counters[broad_cat] += 1
    return prices[idx]

def escape_cs(s):
    """Escape a string for C# verbatim string (double the quotes)"""
    return s.replace('"', '""').replace('\n', ' ').replace('\r', '')

def make_product_name(idx, desc, broad_cat):
    """Create a short product name from description, max ~120 chars"""
    # Take first meaningful words from description
    desc = desc.strip()
    # Remove "This is a" / "These are" / "A " / "An " prefix
    desc = re.sub(r'^(This is an? |These are |An? )', '', desc, flags=re.IGNORECASE)
    # Truncate to ~100 chars
    if len(desc) > 100:
        desc = desc[:97] + "..."
    return f"{idx:03d} - {desc}"

# Stock quantities (cycle through)
stock_values = [25, 50, 100, 75, 30, 60, 45, 80, 35, 55, 40, 90, 20, 70, 15]

lines = []

for i, item in enumerate(items):
    idx = i + 1
    img_id = item["ID for Image"]
    broad_cat = item["Broad Category"]
    sub_cat = item["Sub-Category"]
    season = item["Suitable Season"]
    temps = item["Temperatures"]
    slot = item["Slot"]
    desc = item["Short Description"]
    img_url = item["Image URL"]
    
    cat_guid = cat_map[broad_cat]
    price = get_price(broad_cat)
    stock = stock_values[i % len(stock_values)]
    
    # Build product name
    name = make_product_name(idx, desc, broad_cat)
    
    # Build full description with metadata
    full_desc = f"{desc}\\n\\nSub-Category: {sub_cat} | Season: {season} | Temperature: {temps} | Slot: {slot}"
    
    # Generate deterministic GUIDs
    prod_guid = f"cccccccc-cccc-cccc-cccc-cccc{idx:08d}"
    img_guid = f"dddddddd-dddd-dddd-dddd-dddd{idx:08d}"
    inv_guid = f"eeeeeeee-eeee-eeee-eeee-eeee{idx:08d}"
    
    lines.append({
        "idx": idx,
        "prod_guid": prod_guid,
        "img_guid": img_guid,
        "inv_guid": inv_guid,
        "cat_guid": cat_guid,
        "name": escape_cs(name),
        "desc": escape_cs(full_desc),
        "price": price,
        "img_url": img_url,
        "stock": stock,
        "broad_cat": broad_cat,
    })

# Generate C# code
cs = []
cs.append('// AUTO-GENERATED from Data.xlsx — do not edit manually.')
cs.append('// src/Infrastructure/Persistence/Seeders/ExcelDataSeeder.cs')
cs.append('')
cs.append('namespace Infrastructure.Persistence.Seeders;')
cs.append('')
cs.append('/// <summary>')
cs.append('/// Seeds 100 fashion items from the graduate project Excel dataset.')
cs.append('///')
cs.append('/// DESIGN RULES (same as SubscriptionPlanSeeder):')
cs.append('///   • Idempotent — uses INSERT ... ON CONFLICT (id) DO NOTHING.')
cs.append('///   • Uses ExecuteSqlAsync(FormattableString) — EF Core 9 parameterized SQL.')
cs.append('///   • Fixed GUIDs for all entities — deterministic, reproducible.')
cs.append('///   • Creates a dedicated seed retailer to own all products.')
cs.append('///   • Creates 5 broad categories + 100 products with images and inventory.')
cs.append('/// </summary>')
cs.append('internal sealed class ExcelDataSeeder : ISeeder')
cs.append('{')
cs.append('    // ── Fixed GUIDs ─────────────────────────────────────────────────────────')
cs.append('')
cs.append('    internal static readonly Guid SeedRetailerId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa001");')
cs.append('')
cs.append('    // Category GUIDs')
cs.append('    private static readonly Guid BottomsCategoryId  = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001");')
cs.append('    private static readonly Guid DressesCategoryId  = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002");')
cs.append('    private static readonly Guid KnitwearCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003");')
cs.append('    private static readonly Guid OuterwearCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004");')
cs.append('    private static readonly Guid TopsCategoryId     = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005");')
cs.append('')
cs.append('    private static readonly IReadOnlyList<Guid> AllCategoryIds =')
cs.append('    [')
cs.append('        BottomsCategoryId, DressesCategoryId, KnitwearCategoryId,')
cs.append('        OuterwearCategoryId, TopsCategoryId,')
cs.append('    ];')
cs.append('')
cs.append('    // ── Dependencies ────────────────────────────────────────────────────────')
cs.append('')
cs.append('    private readonly ApplicationDbContext _context;')
cs.append('    private readonly ILogger<ExcelDataSeeder> _logger;')
cs.append('')
cs.append('    public ExcelDataSeeder(ApplicationDbContext context, ILogger<ExcelDataSeeder> logger)')
cs.append('    {')
cs.append('        _context = context;')
cs.append('        _logger = logger;')
cs.append('    }')
cs.append('')
cs.append('    // ── Public Entry Point ──────────────────────────────────────────────────')
cs.append('')
cs.append('    public async Task SeedAsync(CancellationToken cancellationToken = default)')
cs.append('    {')
cs.append('        // Fast-path: if seed retailer and all categories exist, assume fully seeded.')
cs.append('        var retailerExists = await _context.RetailerAccounts')
cs.append('            .IgnoreQueryFilters()')
cs.append('            .AnyAsync(r => r.Id == SeedRetailerId, cancellationToken);')
cs.append('')
cs.append('        var existingCatCount = await _context.Categories')
cs.append('            .IgnoreQueryFilters()')
cs.append('            .Where(c => AllCategoryIds.Contains(c.Id))')
cs.append('            .CountAsync(cancellationToken);')
cs.append('')
cs.append('        var existingProductCount = await _context.Products')
cs.append('            .IgnoreQueryFilters()')
cs.append('            .Where(p => p.RetailerId == SeedRetailerId)')
cs.append('            .CountAsync(cancellationToken);')
cs.append('')
cs.append('        if (retailerExists && existingCatCount >= 5 && existingProductCount >= 100)')
cs.append('        {')
cs.append('            _logger.LogDebug("ExcelDataSeeder: all seed data already present, skipping.");')
cs.append('            return;')
cs.append('        }')
cs.append('')
cs.append('        _logger.LogInformation("ExcelDataSeeder: seeding 100 fashion items...");')
cs.append('')
cs.append('        await using var transaction = await _context.Database')
cs.append('            .BeginTransactionAsync(cancellationToken);')
cs.append('')
cs.append('        try')
cs.append('        {')
cs.append('            await SeedRetailerAsync(cancellationToken);')
cs.append('            await SeedCategoriesAsync(cancellationToken);')
cs.append('            await SeedProductsAsync(cancellationToken);')
cs.append('            await transaction.CommitAsync(cancellationToken);')
cs.append('')
cs.append('            _logger.LogInformation("ExcelDataSeeder: all seed data committed successfully.");')
cs.append('        }')
cs.append('        catch (Exception ex)')
cs.append('        {')
cs.append('            await transaction.RollbackAsync(cancellationToken);')
cs.append('            _logger.LogError(ex, "ExcelDataSeeder: seeding failed — transaction rolled back.");')
cs.append('            throw;')
cs.append('        }')
cs.append('    }')
cs.append('')
cs.append('    // ── Seed Retailer ───────────────────────────────────────────────────────')
cs.append('')
cs.append('    private async Task SeedRetailerAsync(CancellationToken ct)')
cs.append('    {')
cs.append('        // Password: SeedRetailer@2026  (BCrypt work factor 12)')
cs.append('        const string passwordHash = "$2b$12$BBah5s2G9Ht/6yPGR7stFuBavPOvA8m3wN1qdiF9QsR2s16MNXc3u";')
cs.append('')
cs.append('        await _context.Database.ExecuteSqlAsync(')
cs.append('            $"""')
cs.append('             INSERT INTO retailer_accounts')
cs.append('             (')
cs.append('                 id, full_name, email, password_hash, brand_name,')
cs.append('                 business_type, has3d_models, is_email_verified,')
cs.append('                 account_status, access_failed_count, available_balance,')
cs.append('                 is_remember_me_session, is_deleted, created_at, updated_at')
cs.append('             )')
cs.append('             VALUES')
cs.append('             (')
cs.append('                 {SeedRetailerId}, {"VFR Demo Store"}, {"seed@vfr-demo.com"}, {passwordHash},')
cs.append('                 {"VFR Fashion House"}, {"Fashion"}, {false}, {true},')
cs.append('                 {"Active"}, {0}, {0m},')
cs.append('                 {false}, {false}, now(), now()')
cs.append('             )')
cs.append('             ON CONFLICT (id) DO NOTHING')
cs.append('             """, ct);')
cs.append('    }')
cs.append('')
cs.append('    // ── Seed Categories ─────────────────────────────────────────────────────')
cs.append('')
cs.append('    private async Task SeedCategoriesAsync(CancellationToken ct)')
cs.append('    {')
cs.append('        var categories = new (Guid Id, string Name, string Description, string CoverImageUrl)[]')
cs.append('        {')
cs.append('            (BottomsCategoryId,  "Bottoms",   "Trousers, Skirts, Jeans, and all bottom-wear",')
cs.append('                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/26_ymk9bz.jpg"),')
cs.append('            (DressesCategoryId,  "Dresses",   "Maxi dresses, Wrap dresses, and all dress styles",')
cs.append('                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/56_wtsghs.jpg"),')
cs.append('            (KnitwearCategoryId, "Knitwear",  "Sweaters, Cardigans, Pullovers, and knit tops",')
cs.append('                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/4_xysvov.jpg"),')
cs.append('            (OuterwearCategoryId,"Outerwear", "Blazers, Jackets, Coats, and outer layers",')
cs.append('                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/1_g84tdy.jpg"),')
cs.append('            (TopsCategoryId,     "Tops",      "Blouses, Shirts, Statement Tops, and all upper-wear",')
cs.append('                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/2_ic6pdj.jpg"),')
cs.append('        };')
cs.append('')
cs.append('        foreach (var cat in categories)')
cs.append('        {')
cs.append('            await _context.Database.ExecuteSqlAsync(')
cs.append('                $"""')
cs.append('                 INSERT INTO categories')
cs.append('                 (id, retailer_id, name, description, cover_image_url, status, is_deleted, created_at)')
cs.append('                 VALUES')
cs.append('                 ({cat.Id}, {SeedRetailerId}, {cat.Name}, {cat.Description}, {cat.CoverImageUrl},')
cs.append('                  {"Active"}, {false}, now())')
cs.append('                 ON CONFLICT (id) DO NOTHING')
cs.append('                 """, ct);')
cs.append('        }')
cs.append('    }')
cs.append('')
cs.append('    // ── Seed Products + Images + Inventory ──────────────────────────────────')
cs.append('')
cs.append('    private async Task SeedProductsAsync(CancellationToken ct)')
cs.append('    {')
cs.append('        var products = BuildProducts();')
cs.append('')
cs.append('        foreach (var p in products)')
cs.append('        {')
cs.append('            // Insert product')
cs.append('            await _context.Database.ExecuteSqlAsync(')
cs.append('                $"""')
cs.append('                 INSERT INTO products')
cs.append('                 (id, retailer_id, category_id, name, description, price, currency, status,')
cs.append('                  is_deleted, created_at, updated_at)')
cs.append('                 VALUES')
cs.append('                 ({p.ProductId}, {SeedRetailerId}, {p.CategoryId},')
cs.append('                  {p.Name}, {p.Description}, {p.Price}, {"EGP"}, {"Active"},')
cs.append('                  {false}, now(), now())')
cs.append('                 ON CONFLICT (id) DO NOTHING')
cs.append('                 """, ct);')
cs.append('')
cs.append('            // Insert product image')
cs.append('            await _context.Database.ExecuteSqlAsync(')
cs.append('                $"""')
cs.append('                 INSERT INTO product_images')
cs.append('                 (id, product_id, image_url, display_order, is_deleted)')
cs.append('                 VALUES')
cs.append('                 ({p.ImageId}, {p.ProductId}, {p.ImageUrl}, {0}, {false})')
cs.append('                 ON CONFLICT (id) DO NOTHING')
cs.append('                 """, ct);')
cs.append('')
cs.append('            // Insert inventory record')
cs.append('            await _context.Database.ExecuteSqlAsync(')
cs.append('                $"""')
cs.append('                 INSERT INTO inventory_records')
cs.append('                 (id, retailer_id, product_id, product_name, current_stock,')
cs.append('                  sold_quantity, low_stock_threshold, row_version, status,')
cs.append('                  is_deleted, created_at)')
cs.append('                 VALUES')
cs.append('                 ({p.InventoryId}, {SeedRetailerId}, {p.ProductId}, {p.Name}, {p.Stock},')
cs.append('                  {0}, {10}, {0}, {"InStock"},')
cs.append('                  {false}, now())')
cs.append('                 ON CONFLICT (id) DO NOTHING')
cs.append('                 """, ct);')
cs.append('        }')
cs.append('    }')
cs.append('')
cs.append('    // ── Product Data Records ────────────────────────────────────────────────')
cs.append('')
cs.append('    private static IReadOnlyList<ProductSeedRow> BuildProducts() => new ProductSeedRow[]')
cs.append('    {')

# Generate all 100 product records
for item in lines:
    cs.append(f'        new(')
    cs.append(f'            ProductId:   new("{item["prod_guid"]}"),')
    cs.append(f'            ImageId:     new("{item["img_guid"]}"),')
    cs.append(f'            InventoryId: new("{item["inv_guid"]}"),')
    cs.append(f'            CategoryId:  new("{item["cat_guid"]}"),')
    cs.append(f'            Name:        @"{item["name"]}",')
    cs.append(f'            Description: @"{item["desc"]}",')
    cs.append(f'            Price:       {item["price"]}m,')
    cs.append(f'            ImageUrl:    "{item["img_url"]}",')
    cs.append(f'            Stock:       {item["stock"]}')
    cs.append(f'        ),')

cs.append('    };')
cs.append('')
cs.append('    // ── Private Data Record ─────────────────────────────────────────────────')
cs.append('')
cs.append('    private sealed record ProductSeedRow(')
cs.append('        Guid ProductId,')
cs.append('        Guid ImageId,')
cs.append('        Guid InventoryId,')
cs.append('        Guid CategoryId,')
cs.append('        string Name,')
cs.append('        string Description,')
cs.append('        decimal Price,')
cs.append('        string ImageUrl,')
cs.append('        int Stock')
cs.append('    );')
cs.append('}')

output = '\r\n'.join(cs) + '\r\n'

with open(r'g:\Graduate_Project_Backend\Infrastructure\Persistence\Seeders\ExcelDataSeeder.cs', 'w', encoding='utf-8') as f:
    f.write(output)

print(f"Generated ExcelDataSeeder.cs with {len(lines)} product records")
print(f"Total lines: {len(cs)}")
