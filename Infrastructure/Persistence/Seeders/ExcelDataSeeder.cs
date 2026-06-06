// AUTO-GENERATED from Data.xlsx — do not edit manually.

// src/Infrastructure/Persistence/Seeders/ExcelDataSeeder.cs



namespace Infrastructure.Persistence.Seeders;



/// <summary>

/// Seeds 100 fashion items from the graduate project Excel dataset.

///

/// DESIGN RULES (same as SubscriptionPlanSeeder):

///   • Idempotent — uses INSERT ... ON CONFLICT (id) DO NOTHING.

///   • Uses ExecuteSqlAsync(FormattableString) — EF Core 9 parameterized SQL.

///   • Fixed GUIDs for all entities — deterministic, reproducible.

///   • Creates a dedicated seed retailer to own all products.

///   • Creates 5 broad categories + 100 products with images and inventory.

/// </summary>

internal sealed class ExcelDataSeeder : ISeeder

{

    // ── Fixed GUIDs ─────────────────────────────────────────────────────────



    internal static readonly Guid SeedRetailerId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa001");



    // Category GUIDs

    private static readonly Guid BottomsCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001");

    private static readonly Guid DressesCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002");

    private static readonly Guid KnitwearCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003");

    private static readonly Guid OuterwearCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004");

    private static readonly Guid TopsCategoryId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005");



    private static readonly IReadOnlyList<Guid> AllCategoryIds =

    [

        BottomsCategoryId, DressesCategoryId, KnitwearCategoryId,

        OuterwearCategoryId, TopsCategoryId,

    ];



    // ── Dependencies ────────────────────────────────────────────────────────



    private readonly ApplicationDbContext _context;

    private readonly ILogger<ExcelDataSeeder> _logger;



    public ExcelDataSeeder(ApplicationDbContext context, ILogger<ExcelDataSeeder> logger)

    {

        _context = context;

        _logger = logger;

    }



    // ── Public Entry Point ──────────────────────────────────────────────────



    public async Task SeedAsync(CancellationToken cancellationToken = default)

    {

        // Fast-path: verify that ALL seed artefacts are present, including product_images.
        // Previously this check omitted product_images, so a partial seed (products inserted
        // but images missing) would silently skip — leaving ThumbnailUrl null for every product.

        var retailerExists = await _context.RetailerAccounts

            .IgnoreQueryFilters()

            .AnyAsync(r => r.Id == SeedRetailerId, cancellationToken);



        var existingCatCount = await _context.Categories

            .IgnoreQueryFilters()

            .Where(c => AllCategoryIds.Contains(c.Id))

            .CountAsync(cancellationToken);



        var existingProductCount = await _context.Products

            .IgnoreQueryFilters()

            .Where(p => p.RetailerId == SeedRetailerId)

            .CountAsync(cancellationToken);



        // ── FIX: also check product_images so a products-exist-but-no-images state is caught ──
        var existingImageCount = await _context.ProductImages

            .IgnoreQueryFilters()

            .Where(i => _context.Products

                .IgnoreQueryFilters()

                .Where(p => p.RetailerId == SeedRetailerId)

                .Select(p => p.Id)

                .Contains(i.ProductId))

            .CountAsync(cancellationToken);



        if (retailerExists && existingCatCount >= 5 && existingProductCount >= 100 && existingImageCount >= 100)

        {

            _logger.LogDebug("ExcelDataSeeder: all seed data already present, skipping.");

            return;

        }



        // Partial state: products exist but images are missing — re-seed images only.
        if (retailerExists && existingProductCount >= 100 && existingImageCount < 100)

        {

            _logger.LogWarning(

                "ExcelDataSeeder: products present ({ProductCount}) but product_images missing ({ImageCount}). " +

                "Re-seeding product images only.",

                existingProductCount, existingImageCount);



            await using var imgTx = await _context.Database.BeginTransactionAsync(cancellationToken);

            try

            {

                await SeedProductImagesOnlyAsync(cancellationToken);

                await imgTx.CommitAsync(cancellationToken);

                _logger.LogInformation("ExcelDataSeeder: product images re-seeded successfully.");

            }

            catch (Exception ex)

            {

                await imgTx.RollbackAsync(cancellationToken);

                _logger.LogError(ex, "ExcelDataSeeder: product image re-seed failed — transaction rolled back.");

                throw;

            }

            return;

        }



        _logger.LogInformation("ExcelDataSeeder: seeding 100 fashion items...");



        await using var transaction = await _context.Database

            .BeginTransactionAsync(cancellationToken);



        try

        {

            await SeedRetailerAsync(cancellationToken);

            await SeedCategoriesAsync(cancellationToken);

            await SeedProductsAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);



            _logger.LogInformation("ExcelDataSeeder: all seed data committed successfully.");

        }

        catch (Exception ex)

        {

            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(ex, "ExcelDataSeeder: seeding failed — transaction rolled back.");

            throw;

        }

    }



    // ── Seed Retailer ───────────────────────────────────────────────────────



    private async Task SeedRetailerAsync(CancellationToken ct)

    {

        // Password: SeedRetailer@2026  (BCrypt work factor 12)

        const string passwordHash = "$2b$12$BBah5s2G9Ht/6yPGR7stFuBavPOvA8m3wN1qdiF9QsR2s16MNXc3u";



        await _context.Database.ExecuteSqlAsync(

            $"""

             INSERT INTO retailer_accounts

             (

                 id, full_name, email, password_hash, brand_name,

                 business_type, has3d_models, is_email_verified,

                 account_status, access_failed_count, available_balance,

                 is_remember_me_session, is_deleted, created_at, updated_at

             )

             VALUES

             (

                 {SeedRetailerId}, {"VFR Demo Store"}, {"seed@vfr-demo.com"}, {passwordHash},

                 {"VFR Fashion House"}, {"Fashion"}, {false}, {true},

                 {"Active"}, {0}, {0m},

                 {false}, {false}, now(), now()

             )

             ON CONFLICT (id) DO NOTHING

             """, ct);

    }



    // ── Seed Categories ─────────────────────────────────────────────────────



    private async Task SeedCategoriesAsync(CancellationToken ct)

    {

        var categories = new (Guid Id, string Name, string Description, string CoverImageUrl)[]

        {

            (BottomsCategoryId,  "Bottoms",   "Trousers, Skirts, Jeans, and all bottom-wear",

                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/26_ymk9bz.jpg"),

            (DressesCategoryId,  "Dresses",   "Maxi dresses, Wrap dresses, and all dress styles",

                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/56_wtsghs.jpg"),

            (KnitwearCategoryId, "Knitwear",  "Sweaters, Cardigans, Pullovers, and knit tops",

                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/4_xysvov.jpg"),

            (OuterwearCategoryId,"Outerwear", "Blazers, Jackets, Coats, and outer layers",

                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/1_g84tdy.jpg"),

            (TopsCategoryId,     "Tops",      "Blouses, Shirts, Statement Tops, and all upper-wear",

                "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/2_ic6pdj.jpg"),

        };



        foreach (var cat in categories)

        {

            await _context.Database.ExecuteSqlAsync(

                $"""

                 INSERT INTO categories

                 (id, retailer_id, name, description, cover_image_url, status, is_deleted, created_at)

                 VALUES

                 ({cat.Id}, {SeedRetailerId}, {cat.Name}, {cat.Description}, {cat.CoverImageUrl},

                  {"Active"}, {false}, now())

                 ON CONFLICT (id) DO UPDATE SET

                     cover_image_url = EXCLUDED.cover_image_url,

                     name            = EXCLUDED.name,

                     description     = EXCLUDED.description

                 """, ct);

        }

    }



    // ── Seed Products + Images + Inventory ──────────────────────────────────



    private async Task SeedProductsAsync(CancellationToken ct)

    {

        var products = BuildProducts();



        foreach (var p in products)

        {

            // Insert product

            await _context.Database.ExecuteSqlAsync(

                $"""

                 INSERT INTO products

                 (id, retailer_id, category_id, name, description, price, currency, status,

                  model_id, is_deleted, created_at, updated_at)

                 VALUES

                 ({p.ProductId}, {SeedRetailerId}, {p.CategoryId},

                  {p.Name}, {p.Description}, {p.Price}, {"EGP"}, {"Active"},

                  {p.ModelId}, {false}, now(), now())

                 ON CONFLICT (id) DO UPDATE SET

                     model_id = EXCLUDED.model_id

                 WHERE products.model_id IS NULL

                 """, ct);



            // Insert product image

            await _context.Database.ExecuteSqlAsync(

                $"""

                 INSERT INTO product_images

                 (id, product_id, image_url, display_order, is_deleted)

                 VALUES

                 ({p.ImageId}, {p.ProductId}, {p.ImageUrl}, {0}, {false})

                 ON CONFLICT (id) DO NOTHING

                 """, ct);



            // Insert inventory record

            await _context.Database.ExecuteSqlAsync(

                $"""

                 INSERT INTO inventory_records

                 (id, retailer_id, product_id, product_name, current_stock,

                  sold_quantity, low_stock_threshold, row_version, status,

                  is_deleted, created_at)

                 VALUES

                 ({p.InventoryId}, {SeedRetailerId}, {p.ProductId}, {p.Name}, {p.Stock},

                  {0}, {10}, {0}, {"InStock"},

                  {false}, now())

                 ON CONFLICT (id) DO NOTHING

                 """, ct);

        }

    }



    // ── Image-only re-seed (used when products exist but product_images are missing) ──────────

    /// <summary>
    /// Re-inserts product_images rows for all seed products.
    /// Called when the fast-path detects that products exist but their images are absent
    /// (e.g. after a DB reset that cleared product_images but not products).
    /// Uses ON CONFLICT (id) DO NOTHING so it is safe to run multiple times.
    /// </summary>

    private async Task SeedProductImagesOnlyAsync(CancellationToken ct)

    {

        var products = BuildProducts();



        foreach (var p in products)

        {

            await _context.Database.ExecuteSqlAsync(

                $"""

                 INSERT INTO product_images

                 (id, product_id, image_url, display_order, is_deleted)

                 VALUES

                 ({p.ImageId}, {p.ProductId}, {p.ImageUrl}, {0}, {false})

                 ON CONFLICT (id) DO NOTHING

                 """, ct);

        }

        _logger.LogDebug("ExcelDataSeeder: {Count} product image rows upserted.", products.Count);

    }



    // ── Product Data Records ────────────────────────────────────────────────



    private static IReadOnlyList<ProductSeedRow> BuildProducts() => new ProductSeedRow[]

    {

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000001"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000001"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000001"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"001 - highly structured, long-sleeve black blazer featuring a distinctive asymmetrical wrap front. The ...",

            Description: @"This is a highly structured, long-sleeve black blazer featuring a distinctive asymmetrical wrap front. The design utilizes overlapping, origami-like fabric panels and a high crossover collar to create a striking, architectural silhouette. It is tightly tailored at the waist and finishes with a sharp, pointed hemline.\n\nSub-Category: Outerwear / Blazers & Jackets | Season: Fall and Winter | Temperature: 10°C to 20°C (50°F to 68°F) | Slot: Only",

            Price:       189.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/1_g84tdy.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000002"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000002"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000002"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"002 - women's short-sleeve, boat-neck blouse featuring a classic pinstripe pattern on a dark background...",

            Description: @"This is a women's short-sleeve, boat-neck blouse featuring a classic pinstripe pattern on a dark background. The standout design element is an asymmetrical knotted tie at the waist, which creates elegant ruching and a flattering draped effect across the front bodice.\n\nSub-Category: Tops & Blouses | Season: Summer, Spring, and Early Fall | Temperature: 22°C to 32°C (72°F to 90°F) | Slot: Inner",

            Price:       49.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/2_ic6pdj.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000003"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000003"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000003"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"003 - mixed-media statement top by the brand MICAS, designed to give the illusion of a layered outfit. ...",

            Description: @"This is a mixed-media statement top by the brand MICAS, designed to give the illusion of a layered outfit. It combines the wide collar, plunging V-neckline, and voluminous ruched sleeves of a crisp white button-down shirt with the structured, tailored fit of a black-and-white speckled corset bodice.\n\nSub-Category: Tops & Blouses / Statement Tops | Season: Spring, Fall | Temperature: 15°C and 24°C (59°F to 75°F) | Slot: Only",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/3_cbqvcu.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000004"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000004"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000004"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"004 - vintage-inspired, two-tone knit cardigan top. It features a cream-white body contrasted by a soli...",

            Description: @"This is a vintage-inspired, two-tone knit cardigan top. It features a cream-white body contrasted by a solid black polo-style collar, front button placket, and extended ribbed cuffs. The garment is detailed with gold-tone metal buttons down the center and on the two chest patch pockets. It finishes with a wide, fitted ribbed hem at the waist, creating a structured, tailored silhouette.\n\nSub-Category: Knitwear / Cardigans & Sweaters | Season: Spring, Fall, and Mild Winter | Temperature: 15°C and 22°C (59°F to 72°F) | Slot: Only",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/4_xysvov.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000005"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000005"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000005"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"005 - elegant, two-piece matching set featuring a tailored, collarless white cropped jacket and a coord...",

            Description: @"This is an elegant, two-piece matching set featuring a tailored, collarless white cropped jacket and a coordinating inner top. The jacket is structured with clean lines, prominent shoulders, and long sleeves. It is accented with striking, gold-tone embossed buttons that add a sophisticated, military-inspired or Parisian-chic aesthetic to the garment.\n\nSub-Category: Outerwear / Two-Piece Sets / Blazers & Jackets | Season: Spring and Fall | Temperature: 15°C and 23°C (59°F to 73°F) | Slot: Outer",

            Price:       249.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/5_j9ljjg.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000006"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000006"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000006"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"006 - preppy, vintage-inspired short-sleeve knit cardigan top in a solid cream or off-white color. It f...",

            Description: @"This is a preppy, vintage-inspired short-sleeve knit cardigan top in a solid cream or off-white color. It features a textured, woven-style knit pattern (reminiscent of tweed or basketweave), a classic round collarless neckline, and fine ribbed detailing at the hem and cuffs. The garment is elevated by polished gold-tone dome buttons down the center placket and on the two small chest pockets, giving it a refined, Parisian-chic aesthetic.\n\nSub-Category: Knitwear / Short-Sleeve Sweaters & Cardigans | Season: Spring, Summer, and Early Fall | Temperature: 20°C and 28°C (68°F to 82°F) | Slot: Inner",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/6_fxnvwj.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000007"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000007"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000007"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"007 - casual, relaxed-fit knit pullover sweater featuring a classic nautical or Breton-stripe design. I...",

            Description: @"This is a casual, relaxed-fit knit pullover sweater featuring a classic nautical or Breton-stripe design. It has a solid white upper chest and shoulders, with thin, horizontal black stripes running across the lower body and sleeves. The design incorporates an oversized silhouette with dropped shoulders, a silver-tone quarter-zip front that creates a spread collar when open, and thick ribbed detailing at the cuffs and hem to give it structure.\n\nSub-Category: Knitwear / Sweaters & Pullovers | Season: Winter, Late Fall, and Early Spring | Temperature: 10°C and 18°C (50°F to 64°F) | Slot: Only",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/7_ta4cbi.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000008"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000008"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000008"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"008 - preppy, ""two-in-one"" mixed media top by the brand CIDER, designed to create the illusion of a per...",

            Description: @"This is a preppy, ""two-in-one"" mixed media top by the brand CIDER, designed to create the illusion of a perfectly layered outfit without the added bulk. It features a cropped, navy blue crewneck sweatshirt seamlessly attached to a light blue and white striped woven shirt. The striped shirting fabric peeks out at the pointed collar, the extended cuffs, and the shirttail hem, offering a classic, smart-casual aesthetic.\n\nSub-Category: Tops & Sweatshirts / Mixed Media Tops | Season: Fall, Winter, and Early Spring | Temperature: 15°C and 22°C (59°F to 72°F) | Slot: Only",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/8_jzcw8t.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000009"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000009"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000009"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"009 - chunky, ribbed knit half-zip sweater in a deep navy blue. It features a slightly cropped, relaxed...",

            Description: @"This is a chunky, ribbed knit half-zip sweater in a deep navy blue. It features a slightly cropped, relaxed silhouette with dropped shoulders and voluminous sleeves that taper into fitted cuffs. The gold-tone front zipper, when opened, creates a wide, dramatic collar and reveals a matching knit underlayer, giving the garment a cozy, pre-layered aesthetic without the extra bulk of two separate heavy sweaters.\n\nSub-Category: Knitwear / Sweaters & Pullovers | Season: Winter, Late Fall, and Early Spring | Temperature: 10°C and 18°C (50°F to 64°F) | Slot: Only",

            Price:       109.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/9_lyxkn8.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000010"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000010"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000010"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"010 - navy blue, long-sleeve knit sweater featuring a classic, textured cable-knit pattern down the fro...",

            Description: @"This is a navy blue, long-sleeve knit sweater featuring a classic, textured cable-knit pattern down the front bodice. It has a preppy, polo-style collar with a subtle V-neckline. The design is structured to create a flattering, tailored silhouette, utilizing a wide, tightly ribbed band at the waist and extended ribbed cuffs on the sleeves to cinch the garment in.\n\nSub-Category: Knitwear / Sweaters & Pullovers | Season: Winter, Fall, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Inner",

            Price:       59.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/10_hsgsnd.jpg",

            Stock:       55

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000011"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000011"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000011"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"011 - highly tailored, dark wash denim shirt that can also function as a lightweight jacket (often call...",

            Description: @"This is a highly tailored, dark wash denim shirt that can also function as a lightweight jacket (often called a ""shacket""). It features a distinct corset-inspired silhouette with structural ""princess seams"" designed to tightly cinch the waist. The garment is detailed with classic contrasting gold or yellow topstitching, a pointed collar, silver-tone metal tack buttons, and dramatic, elongated cuffs on the sleeves.\n\nSub-Category: Tops & Blouses / Denim Shirts (or Lightweight Jackets) | Season: Spring, Fall, and Mild Winter | Temperature: 15°C and 24°C (59°F to 75°F) | Slot: Mid",

            Price:       59.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/11_a9ref8.jpg",

            Stock:       40

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000012"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000012"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000012"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb003"),

            Name:        @"012 - casual, ribbed knit sweater by Calvin Klein. It features a bold, wide black-and-white horizontal ...",

            Description: @"This is a casual, ribbed knit sweater by Calvin Klein. It features a bold, wide black-and-white horizontal block stripe pattern, a cozy mock neckline, and a relaxed, slightly boxy or cropped fit with dropped shoulders. The signature ""cK"" monogram logo patch is prominently displayed on the center of the chest, giving it a distinct, branded streetwear aesthetic.\n\nSub-Category: Knitwear / Sweaters & Pullovers | Season: Winter, Late Fall, and Early Spring | Temperature: 10°C and 18°C (50°F to 64°F) | Slot: Inner",

            Price:       119.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/12_mafd9q.jpg",

            Stock:       90

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000013"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000013"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000013"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"013 - sleek, tailored women's black leather jacket designed in a classic ""cafe racer"" or moto style. It...",

            Description: @"This is a sleek, tailored women's black leather jacket designed in a classic ""cafe racer"" or moto style. It features a short stand collar with a snap-tab closure, a full front zip, and symmetrical zippered pockets on both the chest and the waist. The smooth leather finish and structured princess seams create a sharp, form-fitting silhouette that is both edgy and polished. (The inner tag indicates it is made of real leather).\n\nSub-Category: Outerwear / Leather Jackets | Season: Winter, Fall, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Outer",

            Price:       219.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/13_uc9omt.jpg",

            Stock:       20

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000014"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000014"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000014"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"014 - cropped, brown faux leather jacket that blends classic moto jacket styling with the ribbed trims ...",

            Description: @"This is a cropped, brown faux leather jacket that blends classic moto jacket styling with the ribbed trims of a traditional bomber jacket. It features a wide, ribbed knit collar that can be worn standing up or folded down, along with matching ribbed knit cuffs and a wide hem to cinch the waist. The design includes a full front zipper, two horizontal zippered chest pockets, and angled welt pockets at the waist.\n\nSub-Category: Outerwear / Faux Leather & Bomber Jackets | Season: Fall, Winter, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Outer",

            Price:       179.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/14_qe1ang.jpg",

            Stock:       70

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000015"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000015"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000015"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"015 - classic women's denim jacket in a light blue, gently faded wash by the brand LE3NO. It features a...",

            Description: @"This is a classic women's denim jacket in a light blue, gently faded wash by the brand LE3NO. It features a tailored, slightly cropped silhouette with traditional ""trucker"" jacket styling. Key details include a pointed collar, two button-flap chest pockets, vertical structural seaming down the front to flatter the shape, and metallic tack buttons.\n\nSub-Category: Outerwear / Denim Jackets | Season: Spring, Fall, and Summer Evenings | Temperature: 16°C and 25°C (61°F to 77°F) | Slot: Outer",

            Price:       269.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/15_z6rtbk.jpg",

            Stock:       15

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000016"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000016"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000016"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"016 - classic women's long-sleeve denim jacket in a solid black wash. It features a tailored, slightly ...",

            Description: @"This is a classic women's long-sleeve denim jacket in a solid black wash. It features a tailored, slightly cropped ""trucker"" silhouette. Key design elements include a traditional pointed collar, a full button-up front, and two button-flap chest pockets, all accented with contrasting silver-tone metallic tack buttons for a sharp, versatile look.\n\nSub-Category: Outerwear / Denim Jackets | Season: Spring, Fall, and Summer Evenings | Temperature: 15°C and 24°C (59°F to 75°F) | Slot: Outer",

            Price:       199.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/16_telcvc.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000017"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000017"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000017"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"017 - women's tailored leather jacket (the inner tag indicates genuine leather) in a soft, dusty pink h...",

            Description: @"This is a women's tailored leather jacket (the inner tag indicates genuine leather) in a soft, dusty pink hue. It is designed in a sleek moto or ""cafe racer"" style. Key features include a stand collar with a snap-tab closure, a full front zipper, horizontal zippered chest pockets, and distinct ribbed or quilted paneling on the upper arms and shoulders, giving it a blend of edgy structure and feminine color.\n\nSub-Category: Outerwear / Leather Jackets | Season: Winter, Fall, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Outer",

            Price:       229.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/17_nlpkqc.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000018"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000018"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000018"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"018 - classic, tailored women's long-sleeve button-down shirt. It features a soft pink base with fine, ...",

            Description: @"This is a classic, tailored women's long-sleeve button-down shirt. It features a soft pink base with fine, vertical white pinstripes, which subtly elongate the torso. The design includes a traditional pointed collar, a standard button placket, and a fitted silhouette that gently tapers at the waist to provide a structured, professional look. It is an ideal staple for business or smart-casual attire.\n\nSub-Category: Tops & Blouses / Button-Down Shirts | Season: All Seasons | Temperature: 18°C and 26°C (64°F to 79°F) | Slot: Inner",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/18_of5dx1.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000019"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000019"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000019"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"019 - quintessential, highly tailored women's long-sleeve white button-down shirt. It is constructed wi...",

            Description: @"This is a quintessential, highly tailored women's long-sleeve white button-down shirt. It is constructed with prominent vertical princess seams that create a very fitted, hourglass silhouette, tightly cinching the waist. The design features a crisp pointed collar, a standard front button placket, and distinctive wide, folded cuffs that add a touch of formal elegance. It is a foundational wardrobe staple for professional, formal, or polished smart-casual wear.\n\nSub-Category: Tops & Blouses / Button-Down Shirts | Season: All Seasons | Temperature: 18°C and 26°C (64°F to 79°F) | Slot: Inner",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/19_ejltws.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000020"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000020"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000020"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"020 - sophisticated, long-sleeve mixed-media top designed to create the illusion of a perfectly layered...",

            Description: @"This is a sophisticated, long-sleeve mixed-media top designed to create the illusion of a perfectly layered outfit. The body of the garment is made from a plush, black velvet or velour material, which gives it a rich texture and slight sheen. It is sharply contrasted by a crisp, white woven pointed collar and matching white French cuffs with dark button details, creating a refined, preppy, and classic aesthetic.\n\nSub-Category: Tops & Blouses / Mixed Media Tops | Season: Winter, Fall, and Early Spring | Temperature: 12°C and 19°C (54°F to 66°F) | Slot: Inner",

            Price:       44.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/20_fri3zv.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000021"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000021"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000021"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"021 - chic, fine-ribbed knit top in a soft cream or off-white color. It features a modern, asymmetrical...",

            Description: @"This is a chic, fine-ribbed knit top in a soft cream or off-white color. It features a modern, asymmetrical draped neckline designed to sit slightly off one shoulder. The garment includes long sleeves with gentle ruching, a functional button-down front placket, and a unique, split asymmetrical hemline that adds a contemporary edge to a classic knit silhouette.\n\nSub-Category: Knitwear / Long-Sleeve Tops | Season: Spring, Fall, and Cool Summer Evenings | Temperature: 16°C and 24°C (61°F to 75°F) | Slot: Inner",

            Price:       54.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/21_lyauuw.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000022"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000022"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000022"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"022 - modern, cropped button-down shirt featuring a classic light blue and white vertical pinstripe pat...",

            Description: @"This is a modern, cropped button-down shirt featuring a classic light blue and white vertical pinstripe pattern. The design is heavily tailored with a wide, structured waistband that cinches the torso and voluminous, pleated balloon sleeves that taper into long, elegant cuffs. It combines traditional shirting elements, like a pointed collar and button placket, with a contemporary, high-fashion silhouette.\n\nSub-Category: Tops & Blouses / Fashion Shirts | Season: Spring, Summer, and Early Fall | Temperature: 20°C and 28°C (68°F to 82°F) | Slot: Inner",

            Price:       64.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/22_nucclp.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000023"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000023"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000023"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"023 - sophisticated, long-sleeve ribbed knit top in a deep burgundy or wine color. It features a wide b...",

            Description: @"This is a sophisticated, long-sleeve ribbed knit top in a deep burgundy or wine color. It features a wide boat neckline (sabrina neck) that elegantly frames the collarbones and dramatic flared bell sleeves. The bodice is detailed with side ruching (gathering) at the waist, which creates a flattering, form-fitting silhouette and added texture.\n\nSub-Category: Tops / Knit Tops | Season: Fall, Spring, and Mild Winter | Temperature: 16°C and 23°C (61°F to 73°F) | Slot: Only",

            Price:       74.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/23_zzzmdb.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000024"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000024"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000024"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"024 - contemporary, long-sleeve top in a deep burgundy hue. It features a soft cowl or boat neckline an...",

            Description: @"This is a contemporary, long-sleeve top in a deep burgundy hue. It features a soft cowl or boat neckline and dramatic, flared bell sleeves that provide a fluid silhouette. The garment is defined by its asymmetrical, slanted hemline and horizontal ruching (gathering) across the bodice, which creates a fitted, draped effect that accentuates the waist.\n\nSub-Category: Tops / Fashion Knit Tops | Season: Fall, Spring, and Mild Winter | Temperature: 16°C and 23°C (61°F to 73°F) | Slot: Only",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/24_ug1jdd.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000025"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000025"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000025"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"025 - avant-garde, architectural top in a rich chocolate brown hue. It features a unique ""two-piece"" la...",

            Description: @"This is an avant-garde, architectural top in a rich chocolate brown hue. It features a unique ""two-piece"" layered effect with a structured, cape-like overlay that creates dramatic, pointed wide sleeves. Underneath, there is a coordinated, form-fitting bodice with horizontal ruching (gathering) and an asymmetrical hemline. The design is bold and sculptural, making it a high-fashion statement piece.\n\nSub-Category: Fashion Tops / Statement Blouses | Season: Fall, Spring, and Mild Winter | Temperature: 17°C and 24°C (63°F to 75°F) | Slot: Only",

            Price:       49.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/25_pryzvr.jpg",

            Stock:       55

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000026"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000026"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000026"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"026 - elegant, high-waisted wide-leg trousers in a soft off-white or cream shade. They feature a struct...",

            Description: @"These are elegant, high-waisted wide-leg trousers in a soft off-white or cream shade. They feature a structured waistband with belt loops, a concealed front fastening, and classic front pleats that flow into sharp center creases down the length of the legs. The silhouette is floor-length and voluminous, offering a sophisticated, ""quiet luxury"" aesthetic suitable for both professional and formal settings.\n\nSub-Category: Bottoms / Trousers / Wide-Leg Pants | Season: All Seasons | Temperature: 18°C and 27°C (64°F to 81°F) | Slot: Bottoms",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/26_ymk9bz.jpg",

            Stock:       40

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000027"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000027"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000027"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"027 - high-waisted, wide-leg tailored trousers in a clean ivory or off-white shade. The design features...",

            Description: @"These are high-waisted, wide-leg tailored trousers in a clean ivory or off-white shade. The design features a structured waistband with belt loops and a visible tonal button closure. Front pleats add volume and a sophisticated drape, while a sharp pressed crease runs down the center of each leg to maintain a polished, elongated silhouette. These pants are a staple of ""minimalist"" or ""old money"" aesthetics, offering a blend of comfort and formal elegance.\n\nSub-Category: Bottoms / Trousers / Pleated Wide-Leg Pants | Season: All Seasons | Temperature: 18°C and 27°C (64°F to 81°F) | Slot: Bottoms",

            Price:       119.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/27_gcghlo.jpg",

            Stock:       90

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000028"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000028"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000028"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"028 - high-waisted, wide-leg tailored trousers in a rich burgundy or deep wine shade. They feature a st...",

            Description: @"These are high-waisted, wide-leg tailored trousers in a rich burgundy or deep wine shade. They feature a structured waistband with belt loops, a concealed fly front for a clean finish, and discrete side slit pockets. The front is detailed with subtle pleats that flow into a voluminous, straight-leg silhouette, offering a sophisticated and professional look with a modern edge.\n\nSub-Category: Bottoms / Trousers / Wide-Leg Dress Pants | Season: Fall, Winter, and Spring | Temperature: 14°C and 22°C (57°F to 72°F) | Slot: Bottoms",

            Price:       149.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/28_uton7h.jpg",

            Stock:       20

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000029"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000029"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000029"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"029 - high-waisted, wide-leg trousers in a soft dusty rose or pastel pink shade. They feature a structu...",

            Description: @"These are high-waisted, wide-leg trousers in a soft dusty rose or pastel pink shade. They feature a structured waistband with belt loops, a clean concealed fly front, and discrete side pockets. The front is detailed with sharp pleats that create a fluid, voluminous drape down the straight-leg silhouette, offering a blend of feminine color and professional tailoring.\n\nSub-Category: Bottoms / Trousers / Wide-Leg Tailored Pants | Season: Spring, Summer, and Early Fall | Temperature: 18°C and 27°C (64°F to 81°F) | Slot: Bottoms",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/29_yo4r5j.jpg",

            Stock:       70

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000030"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000030"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000030"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"030 - elegant, high-waisted trousers in a deep navy blue. They feature a clean, minimalist waistband wi...",

            Description: @"These are elegant, high-waisted trousers in a deep navy blue. They feature a clean, minimalist waistband without visible belt loops or buttons, creating a sleek silhouette. The design includes sharp center creases that run down the length of the wide, voluminous legs, providing a structured yet fluid drape. These trousers are a staple for professional or formal wardrobes, often associated with a sophisticated, timeless style.\n\nSub-Category: Bottoms / Trousers / High-Waisted Wide-Leg Pants | Season: All Seasons | Temperature: 15°C and 25°C (59°F to 77°F) | Slot: Bottoms",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/30_scqpav.jpg",

            Stock:       15

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000031"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000031"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000031"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"031 - sophisticated, high-waisted wide-leg trousers in a deep charcoal or ""espresso"" brown. They featur...",

            Description: @"These are sophisticated, high-waisted wide-leg trousers in a deep charcoal or ""espresso"" brown. They feature a clean, seamless waistband with a concealed front closure for a minimalist finish. The design is characterized by sharp vertical pressed creases that run down the center of each leg, enhancing the structured, voluminous drape. These trousers offer an elongated silhouette, making them a perfect staple for professional or formal ""quiet luxury"" wardrobes.\n\nSub-Category: Bottoms / Trousers / Wide-Leg Tailored Pants | Season: Fall, Winter, and Spring | Temperature: 14°C and 22°C (57°F to 72°F) | Slot: Bottoms",

            Price:       129.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/31_zle5hx.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000032"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000032"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000032"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"032 - classic, high-waisted wide-leg trousers in a deep black. They feature a structured, smooth waistb...",

            Description: @"These are classic, high-waisted wide-leg trousers in a deep black. They feature a structured, smooth waistband with a clean front closure and no visible belt loops, creating a sleek, minimalist look. Sharp center creases run the full length of the voluminous, straight-cut legs to provide structure and an elongated silhouette. These are an essential wardrobe staple for formal, evening, or professional ""power dressing"" outfits.\n\nSub-Category: Bottoms / Trousers / Wide-Leg Formal Pants | Season: All Seasons | Temperature: 15°C and 25°C (59°F to 77°F) | Slot: Bottoms",

            Price:       109.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/32_b2k5by.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000033"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000033"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000033"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"033 - avant-garde wide-leg trousers in a muted taupe or olive-brown hue. They are distinguished by a dr...",

            Description: @"These are avant-garde wide-leg trousers in a muted taupe or olive-brown hue. They are distinguished by a dramatic wrap-around fabric overlay that secures at the waist with a thin tie or buckle, creating the illusion of a maxi skirt from the front while maintaining the comfort of trousers. The high-waisted design and flowing, oversized silhouette offer a sophisticated, modern, and modest aesthetic.\n\nSub-Category: Bottoms / Trousers / Overlay Culottes | Season: Fall, Spring, and Summer Evenings | Temperature: 18°C and 26°C (64°F to 79°F) | Slot: Bottoms",

            Price:       139.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/33_grvw9m.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000034"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000034"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000034"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"034 - modern, high-waisted wide-leg trousers crafted from a smooth faux leather (polyurethane) material...",

            Description: @"These are modern, high-waisted wide-leg trousers crafted from a smooth faux leather (polyurethane) material in a soft beige or camel hue. The design follows a classic five-pocket jean-style construction, featuring a waistband with belt loops, a tonal button closure, and a visible fly. The clean, straight-cut legs provide a structured yet relaxed silhouette, blending the ""edgy"" texture of leather with a sophisticated, neutral color palette.\n\nSub-Category: Bottoms / Trousers / Faux Leather Pants | Season: Fall, Winter, and Early Spring | Temperature: 10°C and 20°C (50°F to 68°F) | Slot: Bottoms",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/34_qodp2g.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000035"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000035"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000035"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"035 - high-waisted, wide-leg trousers crafted from a smooth, chocolate brown faux leather. The design i...",

            Description: @"These are high-waisted, wide-leg trousers crafted from a smooth, chocolate brown faux leather. The design is unique for its ""V-shaped"" or yoke-style waist seam, which creates a flattering, structured look across the hips. They feature a single tonal button closure at the waistband and subtle front pleats that flow into a voluminous, floor-length silhouette. This piece balances the ""edgy"" feel of leather with a sophisticated, tailored shape.\n\nSub-Category: Bottoms / Trousers / Faux Leather Wide-Leg Pants | Season: Fall, Winter, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Bottoms",

            Price:       159.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/35_foq8gf.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000036"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000036"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000036"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"036 - high-waisted black trousers crafted from a smooth, semi-matte faux leather material. The standout...",

            Description: @"These are high-waisted black trousers crafted from a smooth, semi-matte faux leather material. The standout design feature is a sophisticated ""criss-cross"" or wrap-style waistband that creates a sharp V-line at the front, providing a tailored and modern aesthetic. The trousers feature a clean front with no visible pockets, flowing into a voluminous, wide-leg silhouette that offers both structure and a bold, high-fashion look.\n\nSub-Category: Bottoms / Trousers / Faux Leather Wide-Leg Pants | Season: Fall, Winter, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Bottoms",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/36_gc9nay.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000037"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000037"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000037"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"037 - classic, high-waisted wide-leg jeans in a rich, dark indigo wash. They feature a traditional five...",

            Description: @"These are classic, high-waisted wide-leg jeans in a rich, dark indigo wash. They feature a traditional five-pocket design with a zip fly and button closure. The silhouette is fitted through the waist and hips before opening into a clean, straight-to-wide leg that lacks heavy distressing or fading. This ""dark rinse"" style offers a more polished and versatile denim look, suitable for both casual and elevated outfits.\n\nSub-Category: Bottoms / Denim / Wide-Leg Jeans | Season: All Seasons | Temperature: 15°C and 25°C (59°F to 77°F) | Slot: Bottoms",

            Price:       119.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/37_mofjj4.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000038"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000038"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000038"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"038 - high-waisted wide-leg jeans in a light-to-medium blue wash with subtle fading through the thighs....",

            Description: @"These are high-waisted wide-leg jeans in a light-to-medium blue wash with subtle fading through the thighs. They feature a classic five-pocket construction, a high-rise fit that cinches the waist, and a prominent metallic button closure. The legs are cut wide and straight from the hip down to the hem, offering a relaxed, vintage-inspired silhouette that is both comfortable and on-trend.\n\nSub-Category: Bottoms / Denim / Wide-Leg Jeans | Season: Spring, Summer, and Early Fall | Temperature: 18°C and 27°C (64°F to 81°F) | Slot: Bottoms",

            Price:       149.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/38_ymngr2.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000039"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000039"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000039"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"039 - high-waisted, wide-leg jeans in a unique, textured ""marbled"" blue wash. The fabric has a visible ...",

            Description: @"These are high-waisted, wide-leg jeans in a unique, textured ""marbled"" blue wash. The fabric has a visible heathered or acid-wash-inspired grain, giving it a tactile, slightly vintage appearance. Key design details include a traditional zip fly with a silver-tone button closure, structured belt loops, and sharp center creases that add a tailored touch to the voluminous silhouette. The raw, frayed hemline provides a modern, casual finish.\n\nSub-Category: Bottoms / Denim / Wide-Leg Jeans | Season: Spring, Early Fall, and Summer Evenings | Temperature: 17°C and 26°C (63°F to 79°F) | Slot: Bottoms",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/39_kuchvf.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000040"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000040"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000040"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"040 - high-waisted, wide-leg jeans featuring a unique ""two-tone"" or color-blocked design. The top yoke ...",

            Description: @"These are high-waisted, wide-leg jeans featuring a unique ""two-tone"" or color-blocked design. The top yoke section around the waist and hips is a lighter-wash blue, separated by a distressed, raw-edge frayed seam from the rest of the dark-wash indigo legs. The silhouette is voluminous and straight-cut, combining traditional denim elements with a modern, deconstructed aesthetic.\n\nSub-Category: Bottoms / Denim / Two-Tone Wide-Leg Jeans | Season: Spring, Fall, and Early Winter | Temperature: 16°C and 25°C (61°F to 77°F) | Slot: Bottoms",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/40_watgc1.jpg",

            Stock:       55

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000041"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000041"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000041"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"041 - elegant A-line mini skirt crafted from smooth, high-quality leather (or high-grade faux leather) ...",

            Description: @"This is an elegant A-line mini skirt crafted from smooth, high-quality leather (or high-grade faux leather) in a rich dark chocolate or burgundy-brown hue. It features a structured high-waist design with a wide, matching belt that includes a prominent gold-tone hardware buckle. The skirt has a clean central seam and a subtle flared silhouette, offering a polished ""70s-inspired"" look that balances professional tailoring with an edgy material.\n\nSub-Category: Bottoms / Skirts / Leather Mini Skirts | Season: Fall, Winter, and Early Spring | Temperature: 12°C and 21°C (54°F to 70°F) | Slot: Bottoms",

            Price:       129.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/41_k6u3cg.jpg",

            Stock:       40

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000042"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000042"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000042"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"042 - sophisticated, knee-length (midi) A-line skirt featuring a soft, velvety suede or faux-suede text...",

            Description: @"This is a sophisticated, knee-length (midi) A-line skirt featuring a soft, velvety suede or faux-suede texture in a deep cocoa brown. The design includes sharp box pleats that provide volume and a structured drape. It is detailed with a high-waisted fit, belt loops, and a slim, matching dark brown leather-style belt with a gold-tone square buckle. This piece offers a refined, academic, or ""vintage-chic"" aesthetic.\n\nSub-Category: Bottoms / Skirts / Pleated Midi Skirts | Season: Fall, Winter, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Bottoms",

            Price:       109.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/42_zogrwm.jpg",

            Stock:       90

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000043"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000043"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000043"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"043 - sophisticated, high-waisted maxi skirt in a muted khaki or taupe shade. It features a column-like...",

            Description: @"This is a sophisticated, high-waisted maxi skirt in a muted khaki or taupe shade. It features a column-like, straight silhouette with a prominent center front slit for ease of movement. Key design details include two decorative flap pockets at the hips and a thin, contrasting black leather-style belt with a gold-tone buckle. This piece combines ""utility"" elements with professional tailoring, making it suitable for both office and casual-chic environments.\n\nSub-Category: Bottoms / Skirts / Maxi Skirts | Season: Spring, Fall, and Summer Evenings | Temperature: 18°C and 26°C (64°F to 79°F) | Slot: Bottoms",

            Price:       139.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/43_zcogdr.jpg",

            Stock:       20

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000044"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000044"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000044"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"044 - high-waisted, column-style maxi skirt in a crisp off-white or cream denim. It features a modern, ...",

            Description: @"This is a high-waisted, column-style maxi skirt in a crisp off-white or cream denim. It features a modern, clean silhouette with a high-rise fit and a traditional button-and-zip fly. The design is highlighted by a prominent center front slit that starts mid-thigh, adding a contemporary touch and allowing for ease of movement. This piece is a classic example of ""minimalist denim,"" offering a bright, sophisticated alternative to traditional blue jeans.\n\nSub-Category: Bottoms / Skirts / Denim Maxi Skirts | Season: Spring and Summer | Temperature: 18°C and 27°C (64°F to 81°F) | Slot: Bottoms",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/44_zol44o.jpg",

            Stock:       70

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000045"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000045"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000045"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"045 - high-waisted, column-style maxi skirt in a classic medium-blue denim wash with subtle horizontal ...",

            Description: @"This is a high-waisted, column-style maxi skirt in a classic medium-blue denim wash with subtle horizontal fading (whiskering) across the hips. It features a traditional five-pocket design, a zip fly with a silver-tone button, and structured belt loops. A prominent center front slit extends from the hem up to the mid-thigh, providing a modern edge and allowing for natural movement in the sturdy denim fabric.\n\nSub-Category: Bottoms / Denim / Denim Maxi Skirts | Season: Spring, Fall, and Early Winter | Temperature: 16°C and 24°C (61°F to 75°F) | Slot: Bottoms",

            Price:       159.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/45_yfomea.jpg",

            Stock:       15

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000046"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000046"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000046"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"046 - high-waisted denim midi skirt in a distinctive charcoal or acid-wash grey. It features a structur...",

            Description: @"This is a high-waisted denim midi skirt in a distinctive charcoal or acid-wash grey. It features a structured waistband with belt loops, a traditional button closure, and a zip fly. The design is characterized by its slim, column-like silhouette and a prominent center front slit that begins at the knee, offering a modern, edgy twist on classic denim. The faded, washed-out texture gives it a versatile, vintage-inspired aesthetic.\n\nSub-Category: Bottoms / Denim / Denim Midi Skirts | Season: Spring, Fall, and Early Winter | Temperature: 15°C and 23°C (59°F to 73°F) | Slot: Bottoms",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/46_ospkoq.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000047"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000047"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000047"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"047 - sleek, high-waisted midi skirt crafted from smooth, semi-matte black leather (or high-grade faux ...",

            Description: @"This is a sleek, high-waisted midi skirt crafted from smooth, semi-matte black leather (or high-grade faux leather). It features a tailored, pencil-style silhouette that contours the body. The design is elevated by a prominent offset front slit that provides a sharp, architectural detail and allows for ease of movement. This piece is a quintessential ""power dressing"" staple, blending professional structure with a bold, modern material.\n\nSub-Category: Bottoms / Skirts / Leather Midi Skirts | Season: Fall, Winter, and Early Spring | Temperature: 12°C and 20°C (54°F to 68°F) | Slot: Bottoms",

            Price:       119.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/47_kr4ry5.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000048"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000048"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000048"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"048 - sleek, form-fitting midi skirt in a deep matte black. It features a high-waisted design with soph...",

            Description: @"This is a sleek, form-fitting midi skirt in a deep matte black. It features a high-waisted design with sophisticated lateral ruching (gathering) on one side, which creates a flattering draped effect across the front. The skirt is defined by an asymmetrical wrap-style hemline with a sharp front slit, offering a modern and elegant silhouette.\n\nSub-Category: Bottoms / Skirts / Ruched Midi Skirts | Season: Fall, Winter, Spring | Temperature: 16°C and 23°C (61°F to 73°F) | Slot: Bottoms",

            Price:       149.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/48_frx2nj.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000049"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000049"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000049"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"049 - sophisticated, high-waisted pleated midi skirt in a rich burgundy or deep plum shade. It features...",

            Description: @"This is a sophisticated, high-waisted pleated midi skirt in a rich burgundy or deep plum shade. It features narrow, consistent accordion pleats that provide a structured yet fluid drape. The skirt is designed with a clean, flat waistband for a sleek fit around the midsection before flaring out into an A-line silhouette. The fabric has a slight sheen, suggesting a synthetic blend or treated material that holds its shape well.\n\nSub-Category: Bottoms / Skirts / Pleated Midi Skirts | Season: Fall, Winter, Spring | Temperature: 16°C and 23°C (61°F to 73°F) | Slot: Bottoms",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/49_i5fx2n.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000050"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000050"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000050"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"050 - timeless, high-waisted pleated midi skirt in a deep matte black. It features sharp accordion plea...",

            Description: @"This is a timeless, high-waisted pleated midi skirt in a deep matte black. It features sharp accordion pleats that provide elegant movement and a structured A-line drape. The design is finished with a clean, flat waistband that creates a smooth silhouette at the waist. Its minimalist aesthetic makes it a highly versatile ""capsule wardrobe"" piece that can be dressed up for formal events or down for professional office wear.\n\nSub-Category: Bottoms / Skirts / Pleated Midi Skirts | Season: All Seasons | Temperature: 18°C and 26°C (64°F to 79°F) | Slot: Bottoms",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/50_monkn9.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000051"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000051"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000051"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"051 - contemporary oversized denim jacket crafted from premium light-wash cotton denim for a vintage-in...",

            Description: @"A contemporary oversized denim jacket crafted from premium light-wash cotton denim for a vintage-inspired look. This statement piece features a relaxed drop-shoulder silhouette, classic metal button-down fastening, and dual chest flap pockets with subtle distressed detailing. The design is completed with a raw, frayed hemline and side welt pockets, offering a perfect blend of edgy street style and casual comfort for versatile layering.\n\nSub-Category: Outerwear / Denim Jackets | Season: Spring, Fall, and Early Winter | Temperature: 15°C to 25°C (59°F to 77°F) | Slot: Outer",

            Price:       299.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/53_thwvst.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000052"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000052"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000052"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"052 - wardrobe essential, this classic oversized white shirt is tailored from crisp, high-quality cotto...",

            Description: @"A wardrobe essential, this classic oversized white shirt is tailored from crisp, high-quality cotton poplin for a structured yet breathable feel. The design features a traditional pointed collar, a sleek button-down front, and elongated cuffed sleeves that add a modern edge to its timeless silhouette. With its slightly curved high-low hemline and relaxed fit, this versatile piece is perfect for effortless styling, whether tucked into tailored trousers for a professional look or worn loosely over denim for a casual aesthetic.\n\nSub-Category: Tops & Blouses / Button-Down Shirts | Season: Summer, Spring, and Early Fall | Temperature: 22°C to 32°C (72°F to 90°F) | Slot: Inner / Only",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/54_prnrjm.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000053"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000053"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000053"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"053 - Classic oversized blue pinstripe shirt featuring a crisp cotton feel, wide structured cuffs, and ...",

            Description: @"Classic oversized blue pinstripe shirt featuring a crisp cotton feel, wide structured cuffs, and a curved high-low hem.\n\nSub-Category: Tops & Blouses / Pinstripe Shirts | Season: Summer, Spring, and Early Fall | Temperature: 22°C to 32°C (72°F to 90°F) | Slot: Inner / Only",

            Price:       59.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/55_e9xlqu.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000054"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000054"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000054"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"054 - Sophisticated chocolate brown maxi dress featuring a structured wrap-style waist, high neckline, ...",

            Description: @"Sophisticated chocolate brown maxi dress featuring a structured wrap-style waist, high neckline, and elegant long sleeves.\n\nSub-Category: Dresses / Maxi Dresses | Season: Fall, Winter, and Early Spring | Temperature: 15°C to 25°C (59°F to 77°F) | Slot: Only",

            Price:       149.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/56_wtsghs.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000055"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000055"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000055"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"055 - Relaxed olive green maxi dress featuring a full-length button-down front, elasticated waist, and ...",

            Description: @"Relaxed olive green maxi dress featuring a full-length button-down front, elasticated waist, and wide flared sleeves.\n\nSub-Category: Dresses / Boho Maxi Dresses | Season: Spring and Summer | Temperature: 20°C to 30°C (68°F to 86°F) | Slot: Only",

            Price:       199.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/57_xhr1hc.jpg",

            Stock:       55

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000056"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000056"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000056"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"056 - Light blue floral wrap midi dress with long sleeves, a V-neckline, and a delicate side-tie waist.",

            Description: @"Light blue floral wrap midi dress with long sleeves, a V-neckline, and a delicate side-tie waist.\n\nSub-Category: Dresses / Wrap Dresses | Season: Spring and Summer | Temperature: 20°C to 32°C (68°F to 90°F) | Slot: Only",

            Price:       179.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/59_jhdp5i.jpg",

            Stock:       40

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000057"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000057"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000057"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"057 - Charming blue and white floral blouse featuring a ruffled high collar, button-down front, and bal...",

            Description: @"Charming blue and white floral blouse featuring a ruffled high collar, button-down front, and balloon sleeves with delicate tie-cuffs.\n\nSub-Category: Tops & Blouses / Floral Tops | Season: Spring and Summer | Temperature: 22°C to 32°C (72°F to 90°F) | Slot: Only",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/60_wm0poy.jpg",

            Stock:       90

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000058"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000058"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000058"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"058 - Flowy soft pink blouse featuring a pleated design, button-down front with a V-neckline, and delic...",

            Description: @"Flowy soft pink blouse featuring a pleated design, button-down front with a V-neckline, and delicate tie-strings.\n\nSub-Category: Tops & Blouses / Pleated Blouses | Season: Spring and Summer | Temperature: 22°C to 32°C (72°F to 90°F) | Slot: Inner / Only",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/61_idurxt.jpg",

            Stock:       20

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000059"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000059"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000059"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"059 - Burgundy long-sleeve cardigan top with a deep V-neckline, double front tie-closures, and a relaxe...",

            Description: @"Burgundy long-sleeve cardigan top with a deep V-neckline, double front tie-closures, and a relaxed cropped fit.\n\nSub-Category: Tops & Blouses / Tie-Front Tops | Season: Spring, Fall, and Summer Nights | Temperature: 18°C to 28°C (64°F to 82°F) | Slot: Outer",

            Price:       44.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/62_yy1bl4.jpg",

            Stock:       70

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000060"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000060"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000060"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"060 - Deep burgundy long-sleeve blouse with a scalloped V-neckline, vertical pleated detailing, and a b...",

            Description: @"Deep burgundy long-sleeve blouse with a scalloped V-neckline, vertical pleated detailing, and a button-down front.\n\nSub-Category: Tops & Blouses / Pleated Tops | Season: Fall, Winter, and Spring | Temperature: 15°C to 25°C (59°F to 77°F) | Slot: Inner / Only",

            Price:       54.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/63_ngnkca.jpg",

            Stock:       15

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000061"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000061"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000061"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"061 - Cream-colored boho blouse with a V-neckline, button-down front, puff sleeves, and delicate floral...",

            Description: @"Cream-colored boho blouse with a V-neckline, button-down front, puff sleeves, and delicate floral embroidery with lace inserts.\n\nSub-Category: Tops & Blouses / Boho Blouses | Season: Spring and Summer | Temperature: 22°C to 35°C (72°F to 95°F) | Slot: Inner / Only",

            Price:       64.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/64_wlcmak.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000062"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000062"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000062"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"062 - Elegant black maxi dress with a stand-up collar, gold button-down front, puff sleeves, and a defi...",

            Description: @"Elegant black maxi dress with a stand-up collar, gold button-down front, puff sleeves, and a defined waistband with a flared skirt.\n\nSub-Category: Dresses / Maxi Dresses | Season: Fall, Winter, and Spring | Temperature: 15°C to 22°C (59°F to 72°F) | Slot: Only",

            Price:       169.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/65_tocqmf.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000063"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000063"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000063"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"063 - High-waisted grey maxi skirt with a subtle textured fabric and a classic A-line silhouette.",

            Description: @"High-waisted grey maxi skirt with a subtle textured fabric and a classic A-line silhouette.\n\nSub-Category: Bottoms / Maxi Skirts | Season: Fall, Winter, and Spring | Temperature: 10°C to 22°C (50°F to 72°F) | Slot: Bottoms",

            Price:       129.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/66_d3xdez.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000064"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000064"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000064"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"064 - Sage green long-sleeve blouse with a pleated chest panel, delicate lace inserts, and puff sleeves.",

            Description: @"Sage green long-sleeve blouse with a pleated chest panel, delicate lace inserts, and puff sleeves.\n\nSub-Category: Tops & Blouses / Lace Detailing Tops | Season: Spring and Summer | Temperature: 20°C to 30°C (68°F to 86°F) | Slot: Inner / Only",

            Price:       74.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/67_z46ccx.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000065"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000065"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000065"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"065 - Sage green and brick-red paisley print blouse with a smocked chest, V-slit neckline, and volumino...",

            Description: @"Sage green and brick-red paisley print blouse with a smocked chest, V-slit neckline, and voluminous puff sleeves.\n\nSub-Category: Tops & Blouses | Season: Fall, Spring, and Late Summer | Temperature: 18°C to 28°C (64°F to 82°F) | Slot: Inner / Only",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/68_sohdgh.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000066"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000066"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000066"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"066 - Olive green blouse with a small yellow floral print, featuring a western-style yoke with piping, ...",

            Description: @"Olive green blouse with a small yellow floral print, featuring a western-style yoke with piping, gathered chest, and balloon sleeves.\n\nSub-Category: Tops & Blouses / Printed Blouses | Season: Fall and Spring | Temperature: 18°C to 26°C (64°F to 79°F) | Slot: Inner / Only",

            Price:       49.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/69_i4mn2w.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000067"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000067"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000067"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"067 - Checkered jacket with Peter Pan collar, puff sleeves, and front bow details",

            Description: @"Checkered jacket with Peter Pan collar, puff sleeves, and front bow details\n\nSub-Category: Jackets & Coats / Light Jackets | Season: Fall and Winter | Temperature: 10°C to 18°C (50°F to 64°F) | Slot: Outer",

            Price:       159.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/71_jo8tpk.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000068"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000068"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000068"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"068 - Sleeveless black ribbed top with button-down front and embroidered eyelet peplum hem.",

            Description: @"Sleeveless black ribbed top with button-down front and embroidered eyelet peplum hem.\n\nSub-Category: Sleeveless / Peplum Tops | Season: Summer | Temperature: 25°C to 40°C (77°F to 104°F) | Slot: Inner / Only",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/72_uallfr.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000069"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000069"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000069"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"069 - Sleeveless brown top with a V-slit neckline, side slits, and a pleated high-low hemline.",

            Description: @"Sleeveless brown top with a V-slit neckline, side slits, and a pleated high-low hemline.\n\nSub-Category: Sleeveless Tops / High-Low | Season: Summer and Spring | Temperature: 22°C to 35°C (72°F to 95°F) | Slot: Inner / Only",

            Price:       59.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/73_sqnfen.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000070"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000070"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000070"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"070 - Terracotta pleated maxi dress with a keyhole neckline, gathered collar, and long puff sleeves.",

            Description: @"Terracotta pleated maxi dress with a keyhole neckline, gathered collar, and long puff sleeves.\n\nSub-Category: Dresses / Maxi Dresses | Season: Fall, Spring, and Summer | Temperature: 20°C to 32°C (68°F to 90°F) | Slot: Only",

            Price:       229.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/74_ysjc65.jpg",

            Stock:       55

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000071"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000071"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000071"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"071 - Elegant burgundy maxi dress featuring a sophisticated high neckline, and a fluid pleated tiered s...",

            Description: @"Elegant burgundy maxi dress featuring a sophisticated high neckline, and a fluid pleated tiered skirt for a graceful silhouette.\n\nSub-Category: Dresses / Maxi Dresses | Season: Fall and Winter | Temperature: 12°C to 22°C (54°F to 72°F) | Slot: Only",

            Price:       189.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/75_cbbnkn.jpg",

            Stock:       40

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000072"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000072"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000072"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"072 - Charming pink button-down maxi dress with a pleated collar and airy lantern puff sleeves for a ro...",

            Description: @"Charming pink button-down maxi dress with a pleated collar and airy lantern puff sleeves for a romantic silhouette.\n\nSub-Category: Dresses / Maxi Dresses | Season: Spring and Summer | Temperature: 22°C to 35°C (72°F to 95°F) | Slot: Only",

            Price:       159.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/76_pvo2gb.jpg",

            Stock:       90

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000073"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000073"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000073"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"073 - Sophisticated off-white wide-leg trousers featuring a high-waisted fit, tailored pleats, and a cl...",

            Description: @"Sophisticated off-white wide-leg trousers featuring a high-waisted fit, tailored pleats, and a clean, minimalist silhouette.\n\nSub-Category: Bottoms / Wide-Leg Trousers | Season: All Seasons | Temperature: 15°C to 30°C (59°F to 86°F) | Slot: Only",

            Price:       109.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/77_kfcs46.jpg",

            Stock:       20

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000074"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000074"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000074"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"074 - Classic navy pinstripe wide-leg trousers featuring a sharp tailored fit, high-rise waist, and ele...",

            Description: @"Classic navy pinstripe wide-leg trousers featuring a sharp tailored fit, high-rise waist, and elegant vertical stripes for a polished look.\n\nSub-Category: Bottoms / Tailored Trousers | Season: All Seasons | Temperature: 15°C to 28°C (59°F to 82°F) | Slot: Only",

            Price:       139.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/78_y3ppkj.jpg",

            Stock:       70

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000075"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000075"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000075"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"075 - Elegant taupe wide-leg trousers featuring a self-tie waist belt, relaxed tailored fit, and a smoo...",

            Description: @"Elegant taupe wide-leg trousers featuring a self-tie waist belt, relaxed tailored fit, and a smooth minimalist finish.\n\nSub-Category: Bottoms / Wide-Leg Trousers | Season: All Seasons | Temperature: 18°C to 28°C (64°F to 82°F) | Slot: Only",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/79_iqxuyk.jpg",

            Stock:       15

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000076"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000076"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000076"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"076 - Chic brown midi skirt featuring a graceful twist-front detail, high-waisted fit, and a fluid A-li...",

            Description: @"Chic brown midi skirt featuring a graceful twist-front detail, high-waisted fit, and a fluid A-line silhouette for a timeless look.\n\nSub-Category: Bottoms / Midi Skirts | Season: Fall and Spring | Temperature: 18°C to 28°C (64°F to 82°F) | Slot: Only",

            Price:       159.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/80_dxguwk.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000077"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000077"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000077"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"077 - Sophisticated black midi skirt featuring a modern asymmetric wrap overlay and sharp accordion ple...",

            Description: @"Sophisticated black midi skirt featuring a modern asymmetric wrap overlay and sharp accordion pleats for a dynamic, edgy silhouette.\n\nSub-Category: Bottoms / Pleated Skirts | Season: Fall, Winter, and Spring | Temperature: 12°C to 25°C (54°F to 77°F) | Slot: Only",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/81_lsr1cu.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000078"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000078"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000078"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"078 - Elegant light blue midi skirt featuring a modern asymmetric wrap overlay with a buckle detail and...",

            Description: @"Elegant light blue midi skirt featuring a modern asymmetric wrap overlay with a buckle detail and sharp accordion pleats.\n\nSub-Category: Bottoms / Pleated Skirts | Season: Spring and Summer | Temperature: 18°C to 30°C (64°F to 86°F) | Slot: Only",

            Price:       119.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/82_pr1yg1.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000079"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000079"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000079"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"079 - Fresh blue and white pinstriped top featuring oversized shoulder tie-bows, a square neckline, and...",

            Description: @"Fresh blue and white pinstriped top featuring oversized shoulder tie-bows, a square neckline, and a playful flared peplum hem.\n\nSub-Category: Tops / Peplum Tops | Season: Summer | Temperature: 25°C to 40°C (77°F to 104°F) | Slot: Only",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/83_kkwuiq.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000080"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000080"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000080"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"080 - Minimalist beige linen wrap top featuring a serene V-neckline, adjustable side-tie closure, and r...",

            Description: @"Minimalist beige linen wrap top featuring a serene V-neckline, adjustable side-tie closure, and relaxed wide kimono-style sleeves.\n\nSub-Category: Tops / Wrap Tops | Season: Spring and Summer | Temperature: 22°C to 35°C (72°F to 95°F) | Slot: Only",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/84_z32zcq.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000081"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000081"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000081"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"081 - Cozy black knit cardigan featuring a distinctive front tie-bow closure, relaxed drop shoulders, a...",

            Description: @"Cozy black knit cardigan featuring a distinctive front tie-bow closure, relaxed drop shoulders, and wide flared sleeves for a chic, oversized silhouette.\n\nSub-Category: Outerwear / Cardigans | Season: Fall and Winter | Temperature: 10°C to 20°C (50°F to 68°F) | Slot: Outer",

            Price:       239.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/85_oplnxe.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000082"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000082"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000082"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"082 - Luxurious cream knit sweater with a cozy oversized fit, featuring a ribbed turtleneck and elegant...",

            Description: @"Luxurious cream knit sweater with a cozy oversized fit, featuring a ribbed turtleneck and elegant drop shoulders.\n\nSub-Category: Tops / Sweaters | Season: Winter | Temperature: 5°C to 15°C (41°F to 59°F) | Slot: Only",

            Price:       44.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/86_pedsf7.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000083"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000083"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000083"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"083 - Elegant off-white knit sweater featuring a stylish polo collar and distinctive black contrast sti...",

            Description: @"Elegant off-white knit sweater featuring a stylish polo collar and distinctive black contrast stitching along the edges.\n\nSub-Category: Sweaters & Knitwear | Season: Fall and Winter | Temperature: 10°C to 20°C (50°F to 68°F) | Slot: Only",

            Price:       54.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/87_nrteml.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000084"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000084"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000084"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"084 - Sophisticated black knit jacket with a double-breasted button closure, deep V-neckline, and struc...",

            Description: @"Sophisticated black knit jacket with a double-breasted button closure, deep V-neckline, and structured flared sleeves.\n\nSub-Category: Jackets & Blazers | Season: Fall and Winter | Temperature: 10°C to 20°C (50°F to 68°F) | Slot: Outer",

            Price:       189.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/88_oy1eyf.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000085"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000085"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000085"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"085 - Ethereal sage green chiffon blouse featuring intricate pleating, a high gathered neckline, and dr...",

            Description: @"Ethereal sage green chiffon blouse featuring intricate pleating, a high gathered neckline, and dramatic voluminous balloon sleeves.\n\nSub-Category: Blouses & Shirts | Season: Spring and Summer | Temperature: 20°C to 35°C (68°F to 95°F) | Slot: Only",

            Price:       64.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/89_ohydg6.jpg",

            Stock:       55

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000086"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000086"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000086"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"086 - Sophisticated burgundy chiffon blouse featuring intricate plissé pleating, a graceful scarf-tie n...",

            Description: @"Sophisticated burgundy chiffon blouse featuring intricate plissé pleating, a graceful scarf-tie neckline, and elegant bishop sleeves.\n\nSub-Category: Blouses & Shirts | Season: Spring and Summer | Temperature: 20°C to 35°C (68°F to 95°F) | Slot: Only",

            Price:       74.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/90_do3qaq.jpg",

            Stock:       40

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000087"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000087"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000087"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"087 - Elegant sage green satin blouse featuring a draped cowl neckline with layered chain embellishment...",

            Description: @"Elegant sage green satin blouse featuring a draped cowl neckline with layered chain embellishments and wide flared sleeves.\n\nSub-Category: Blouses & Shirts | Season: Spring and Winter | Temperature: 15°C to 25°C (59°F to 77°F) | Slot: Only",

            Price:       99.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/91_wuko6p.jpg",

            Stock:       90

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000088"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000088"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000088"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"088 - exquisitely designed dusty rose blouse featuring a fluid, silk-like drape that offers both comfor...",

            Description: @"An exquisitely designed dusty rose blouse featuring a fluid, silk-like drape that offers both comfort and elegance. The design is highlighted by a sophisticated self-tie pussy-bow neckline and dramatic, sunray pleated bell sleeves that end in delicate ruffled cuffs, creating a romantic and timeless silhouette perfect for professional or evening wear.\n\nSub-Category: Blouses & Shirts | Season: Spring and Summer | Temperature: 18°C to 28°C (64°F to 82°F) | Slot: Only",

            Price:       49.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/92_yx9jgj.jpg",

            Stock:       20

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000089"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000089"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000089"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"089 - Elegant burgundy blouse featuring intricate tonal floral embroidery, a gathered band collar, and ...",

            Description: @"Elegant burgundy blouse featuring intricate tonal floral embroidery, a gathered band collar, and voluminous puff sleeves with elasticated cuffs.\n\nSub-Category: Blouses & Shirts | Season: Fall and Winter | Temperature: 12°C to 22°C (54°F to 72°F) | Slot: Only",

            Price:       69.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/93_daaebs.jpg",

            Stock:       70

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000090"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000090"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000090"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"090 - relaxed navy blue boho-style blouse crafted from lightweight textured fabric. Features a graceful...",

            Description: @"A relaxed navy blue boho-style blouse crafted from lightweight textured fabric. Features a graceful V-neckline with delicate tassel tie-strings, a gathered empire waist for a flowing fit, and long sleeves with buttoned cuffs.\n\nSub-Category: Blouses & Shirts | Season: Fall and Spring | Temperature: 16°C to 24°C (61°F to 75°F) | Slot: Only",

            Price:       59.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/94_knl9bs.jpg",

            Stock:       15

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000091"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000091"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000091"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"091 - Sophisticated black blouse designed with a V-neckline featuring crossover lace-up tie details and...",

            Description: @"Sophisticated black blouse designed with a V-neckline featuring crossover lace-up tie details and dramatic wide-flare bell sleeves for a bold, feminine look.\n\nSub-Category: Blouses & Shirts | Season: Fall and Spring | Temperature: 14°C to 24°C (57°F to 75°F) | Slot: Only",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/95_wxxslm.jpg",

            Stock:       25

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000092"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000092"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000092"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb005"),

            Name:        @"092 - Sophisticated rust-colored blouse crafted from lightweight linen-blend fabric, featuring intricat...",

            Description: @"Sophisticated rust-colored blouse crafted from lightweight linen-blend fabric, featuring intricate vertical tuck-pleats on the front and sleeves, a sharp V-neckline with a delicate button-down closure, and elegant wide-flare sleeves.\n\nSub-Category: Blouses & Shirts | Season: Fall and Spring | Temperature: 15°C to 25°C (59°F to 77°F) | Slot: Only",

            Price:       89.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/96_tovwol.jpg",

            Stock:       50

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000093"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000093"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000093"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"093 - Vintage-inspired brown maxi dress featuring a classic white polka dot pattern, a flattering V-nec...",

            Description: @"Vintage-inspired brown maxi dress featuring a classic white polka dot pattern, a flattering V-neckline with a full button-down front, and elegant long sleeves with buttoned cuffs\n\nSub-Category: Dresses | Season: Fall and Spring | Temperature: 15°C to 25°C (59°F to 77°F) | Slot: Only",

            Price:       209.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/97_ad0bjp.jpg",

            Stock:       100

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000094"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000094"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000094"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"094 - elegant chocolate brown chiffon maxi dress featuring a high pleated bodice with a full-length but...",

            Description: @"An elegant chocolate brown chiffon maxi dress featuring a high pleated bodice with a full-length button closure, long bishop sleeves with buttoned cuffs, and a flowing flared skirt for a refined silhouette.\n\nSub-Category: Dresses | Season: Fall and Winter | Temperature: 12°C to 20°C (54°F to 68°F) | Slot: Only",

            Price:       139.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/98_antmfl.jpg",

            Stock:       75

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000095"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000095"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000095"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002"),

            Name:        @"095 - Elegant olive green maxi dress featuring a structured pointed collar, a full-length gold button-d...",

            Description: @"Elegant olive green maxi dress featuring a structured pointed collar, a full-length gold button-down front, a cinched waist with pleated detailing, and dramatic voluminous lantern sleeves with wide buttoned cuffs.\n\nSub-Category: Dresses | Season: Fall and Spring | Temperature: 16°C to 26°C (61°F to 79°F) | Slot: Only",

            Price:       219.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/99_dw9q49.jpg",

            Stock:       30

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000096"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000096"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000096"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"096 - Elegant olive green maxi skirt featuring a high-waisted structured fit with sophisticated yoke de...",

            Description: @"Elegant olive green maxi skirt featuring a high-waisted structured fit with sophisticated yoke detailing and a gracefully flowing flared hemline for a timeless feminine silhouette.\n\nSub-Category: Skirts | Season: All Seasons | Temperature: 16°C to 26°C (61°F to 79°F) | Slot: Only",

            Price:       149.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/100_yrtcgo.jpg",

            Stock:       60

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000097"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000097"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000097"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001"),

            Name:        @"097 - High-waisted midi skirt crafted from a heavy wool-blend fabric with a classic brown houndstooth-p...",

            Description: @"High-waisted midi skirt crafted from a heavy wool-blend fabric with a classic brown houndstooth-plaid pattern. Featuring a structured tailored waist accented by a slim leather-look belt with gold-tone hardware, and deep box pleats that provide a graceful flare and structured volume for a timeless winter look.\n\nSub-Category: Skirts | Season: Fall and Winter | Temperature: 10°C to 20°C (50°F to 68°F) | Slot: Only",

            Price:       79.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/101_az0zfs.jpg",

            Stock:       45

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000098"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000098"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000098"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"098 - Cozy and stylish navy blue teddy-fleece jacket featuring a warm mock-neck collar and a unique asy...",

            Description: @"Cozy and stylish navy blue teddy-fleece jacket featuring a warm mock-neck collar and a unique asymmetrical button-front closure. Designed with a relaxed boxy fit, dropped shoulders, and practical side welt pockets, making it a perfect cozy essential for modern winter street style.\n\nSub-Category: Jackets & Outerwear | Season: Winter | Temperature: 5°C to 15°C (41°F to 59°F) | Slot: Only",

            Price:       249.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/102_q9hzfp.jpg",

            Stock:       80

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000099"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000099"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000099"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"099 - versatile navy blue zip-up jacket crafted from a premium textured knit fabric, featuring a classi...",

            Description: @"A versatile navy blue zip-up jacket crafted from a premium textured knit fabric, featuring a classic pointed collar, two oversized front utility pockets with flap closures, and ribbed cuffs for a structured yet comfortable everyday fit.\n\nSub-Category: Jackets & Outerwear | Season: Fall and Winter | Temperature: 10°C to 20°C (50°F to 68°F) | Slot: Only",

            Price:       219.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/103_iuiank.jpg",

            Stock:       35

        ),

        new(

            ProductId:   new("cccccccc-cccc-cccc-cccc-cccc00000100"),

            ImageId:     new("dddddddd-dddd-dddd-dddd-dddd00000100"),

            InventoryId: new("eeeeeeee-eeee-eeee-eeee-eeee00000100"),

            CategoryId:  new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004"),

            Name:        @"100 - modern olive green jacket crafted from a cozy textured wool-blend fabric, featuring a sharp point...",

            Description: @"A modern olive green jacket crafted from a cozy textured wool-blend fabric, featuring a sharp pointed collar, a sleek silver-tone front zipper closure, and two large functional patch pockets in a relaxed, boxy silhouette.\n\nSub-Category: Jackets & Outerwear | Season: Fall and Winter | Temperature: 8°C to 18°C (46°F to 64°F) | Slot: Only",

            Price:       179.99m,

            ImageUrl:    "https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/104_n3lmnz.jpg",

            Stock:       55

        ),

    };



    // ── Private Data Record ─────────────────────────────────────────────────



    private sealed record ProductSeedRow(

        Guid ProductId,

        Guid ImageId,

        Guid InventoryId,

        Guid CategoryId,

        string Name,

        string Description,

        decimal Price,

        string ImageUrl,

        int Stock

    )

    {

        /// <summary>

        /// External model ID used by the AI style-recommendation model.

        /// Derived from the Cloudinary image filename: "https://.../78_y3ppkj.jpg" → "78_y3ppkj"

        /// </summary>

        public string ModelId =>

            System.IO.Path.GetFileNameWithoutExtension(ImageUrl.Split('/')[^1]);

    }

}