using System.Security.Cryptography;
using System.Text;
using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Database;

public static class DbSeeder
{
    private const string DevelopmentAdminEmail = "admin@commerce.local";
    private const string DevelopmentAdminPassword = "Admin123!";

    // Product IDs
    private static readonly Guid DellXps13Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GalaxyS24Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PlayStation5Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LgOledTvId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid MxKeysComboId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid MacBookAirM2Id = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid IPhone15ProId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Pixel8aId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid SamsungFrameTVId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid NintendoSwitchOLEDId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SamsungWasherId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid LGMicrowaveId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid RoombaJ7Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    // Additional product IDs (grouped by category)
    
    // Mobiles (9 more needed to reach 11: already have iPhone15Pro, Pixel8a)
    private static readonly Guid GalaxyS24UltraId = Guid.Parse("e0000001-0001-0001-0001-000000000001");
    private static readonly Guid OnePlus12Id = Guid.Parse("e0000002-0002-0002-0002-000000000002");
    private static readonly Guid Xiaomi14Id = Guid.Parse("e0000003-0003-0003-0003-000000000003");
    private static readonly Guid AsusZenfone11Id = Guid.Parse("e0000004-0004-0004-0004-000000000004");
    private static readonly Guid SonyXperia1VId = Guid.Parse("e0000005-0005-0005-0005-000000000005");
    private static readonly Guid MotorolaEdge50Id = Guid.Parse("e0000006-0006-0006-0006-000000000006");
    private static readonly Guid NothingPhone2aId = Guid.Parse("e0000007-0007-0007-0007-000000000007");
    private static readonly Guid HonorMagic6Id = Guid.Parse("e0000008-0008-0008-0008-000000000008");
    private static readonly Guid Realme12ProId = Guid.Parse("e0000009-0009-0009-0009-000000000009");

    // Laptops (9 more needed: have DellXps13, MacBookAirM2)
    private static readonly Guid ThinkPadX1CarbonId = Guid.Parse("f0000001-0001-0001-0001-000000000001");
    private static readonly Guid SurfaceLaptop6Id = Guid.Parse("f0000002-0002-0002-0002-000000000002");
    private static readonly Guid HPEnvy16Id = Guid.Parse("f0000003-0003-0003-0003-000000000003");
    private static readonly Guid AsusZenbookS13Id = Guid.Parse("f0000004-0004-0004-0004-000000000004");
    private static readonly Guid AcerSwiftGo14Id = Guid.Parse("f0000005-0005-0005-0005-000000000005");
    private static readonly Guid RazerBlade15Id = Guid.Parse("f0000006-0006-0006-0006-000000000006");
    private static readonly Guid FrameworkLaptop13Id = Guid.Parse("f0000007-0007-0007-0007-000000000007");
    private static readonly Guid SamsungGalaxyBook4Id = Guid.Parse("f0000008-0008-0008-0008-000000000008");
    private static readonly Guid MSIThinGF63Id = Guid.Parse("f0000009-0009-0009-0009-000000000009");

    // Televisions (9 more needed: have LgOledTv, SamsungFrameTV)
    private static readonly Guid SonyBraviaX90LId = Guid.Parse("a0000001-0001-0001-0001-000000000001");
    private static readonly Guid TCLC745Id = Guid.Parse("a0000002-0002-0002-0002-000000000002");
    private static readonly Guid HisenseU8KId = Guid.Parse("a0000003-0003-0003-0003-000000000003");
    private static readonly Guid PhilipsOLED808Id = Guid.Parse("a0000004-0004-0004-0004-000000000004");
    private static readonly Guid PanasonicMZ2000Id = Guid.Parse("a0000005-0005-0005-0005-000000000005");
    private static readonly Guid VizioMQuantumId = Guid.Parse("a0000006-0006-0006-0006-000000000006");
    private static readonly Guid SharpAquosXLEDId = Guid.Parse("a0000007-0007-0007-0007-000000000007");
    private static readonly Guid ToshibaFireTVId = Guid.Parse("a0000008-0008-0008-0008-000000000008");
    private static readonly Guid SkyworthQ55Id = Guid.Parse("a0000009-0009-0009-0009-000000000009");

    // Games (9 more needed: have PlayStation5, NintendoSwitchOLED)
    private static readonly Guid XboxSeriesXId = Guid.Parse("b0000001-0001-0001-0001-000000000001");
    private static readonly Guid SteamDeckOLEDId = Guid.Parse("b0000002-0002-0002-0002-000000000002");
    private static readonly Guid DualSenseEdgeId = Guid.Parse("b0000003-0003-0003-0003-000000000003");
    private static readonly Guid ZeldaTearsKingdomId = Guid.Parse("b0000004-0004-0004-0004-000000000004");
    private static readonly Guid MarioWonderId = Guid.Parse("b0000005-0005-0005-0005-000000000005");
    private static readonly Guid EldenRingId = Guid.Parse("b0000006-0006-0006-0006-000000000006");
    private static readonly Guid BaldursGate3Id = Guid.Parse("b0000007-0007-0007-0007-000000000007");
    private static readonly Guid Cyberpunk2077Id = Guid.Parse("b0000008-0008-0008-0008-000000000008");
    private static readonly Guid XboxEliteControllerId = Guid.Parse("b0000009-0009-0009-0009-000000000009");

    // Appliances (9 more needed: have SamsungWasher, LGMicrowave)
    private static readonly Guid DysonV15Id = Guid.Parse("c0000001-0001-0001-0001-000000000001");
    private static readonly Guid BoschDishwasherId = Guid.Parse("c0000002-0002-0002-0002-000000000002");
    private static readonly Guid WhirlpoolFridgeId = Guid.Parse("c0000003-0003-0003-0003-000000000003");
    private static readonly Guid NespressoVertuoId = Guid.Parse("c0000004-0004-0004-0004-000000000004");
    private static readonly Guid InstantPotProId = Guid.Parse("c0000005-0005-0005-0005-000000000005");
    private static readonly Guid BrevilleToasterId = Guid.Parse("c0000006-0006-0006-0006-000000000006");
    private static readonly Guid KitchenAidMixerId = Guid.Parse("c0000007-0007-0007-0007-000000000007");
    private static readonly Guid DeLonghiMagnificaId = Guid.Parse("c0000008-0008-0008-0008-000000000008");
    private static readonly Guid LGWashtowerId = Guid.Parse("c0000009-0009-0009-0009-000000000009");

    // Electronics (10 more needed: have GalaxyS24)
    private static readonly Guid iPadAirM2Id = Guid.Parse("d0000001-0001-0001-0001-000000000001");
    private static readonly Guid GalaxyWatch6Id = Guid.Parse("d0000002-0002-0002-0002-000000000002");
    private static readonly Guid AirPodsPro2Id = Guid.Parse("d0000003-0003-0003-0003-000000000003");
    private static readonly Guid SonyWH1000XM5Id = Guid.Parse("d0000004-0004-0004-0004-000000000004");
    private static readonly Guid KindlePaperwhiteId = Guid.Parse("d0000005-0005-0005-0005-000000000005");
    private static readonly Guid GoProHero12Id = Guid.Parse("d0000006-0006-0006-0006-000000000006");
    private static readonly Guid SamsungT7SSDId = Guid.Parse("d0000007-0007-0007-0007-000000000007");
    private static readonly Guid AnkerPowerBankId = Guid.Parse("d0000008-0008-0008-0008-000000000008");
    private static readonly Guid RokuUltraId = Guid.Parse("d0000009-0009-0009-0009-000000000009");
    private static readonly Guid AppleTV4KId = Guid.Parse("d0000010-0010-0010-0010-000000000010");

    // Home (10 more needed: have RoombaJ7)
    private static readonly Guid PhilipsHueStarterId = Guid.Parse("e1000001-0001-0001-0001-000000000001");
    private static readonly Guid DysonPurifierHotCoolId = Guid.Parse("e1000002-0002-0002-0002-000000000002");
    private static readonly Guid IkeaBillyBookcaseId = Guid.Parse("e1000003-0003-0003-0003-000000000003");
    private static readonly Guid TempurPedicPillowId = Guid.Parse("e1000004-0004-0004-0004-000000000004");
    private static readonly Guid VitamixA3500Id = Guid.Parse("e1000005-0005-0005-0005-000000000005");
    private static readonly Guid NespressoMilkFrotherId = Guid.Parse("e1000006-0006-0006-0006-000000000006");
    private static readonly Guid LeCreusetDutchOvenId = Guid.Parse("e1000007-0007-0007-0007-000000000007");
    private static readonly Guid AmazonEchoStudioId = Guid.Parse("e1000008-0008-0008-0008-000000000008");
    private static readonly Guid GoogleNestHubMaxId = Guid.Parse("e1000009-0009-0009-0009-000000000009");
    private static readonly Guid MieleCanisterVacuumId = Guid.Parse("e1000010-0010-0010-0010-000000000010");

    // Other (10 more needed: have MxKeysCombo)
    private static readonly Guid AppleMagicKeyboardId = Guid.Parse("f1000001-0001-0001-0001-000000000001");
    private static readonly Guid JBLCharge5Id = Guid.Parse("f1000002-0002-0002-0002-000000000002");
    private static readonly Guid LogitechC920WebcamId = Guid.Parse("f1000003-0003-0003-0003-000000000003");
    private static readonly Guid AnkerUSBCableId = Guid.Parse("f1000004-0004-0004-0004-000000000004");
    private static readonly Guid UGreenDockId = Guid.Parse("f1000005-0005-0005-0005-000000000005");
    private static readonly Guid SanDiskExtremeProId = Guid.Parse("f1000006-0006-0006-0006-000000000006");
    private static readonly Guid RazerDeathAdderId = Guid.Parse("f1000007-0007-0007-0007-000000000007");
    private static readonly Guid SecretLabChairId = Guid.Parse("f1000008-0008-0008-0008-000000000008");
    private static readonly Guid ElgatoStreamDeckId = Guid.Parse("f1000009-0009-0009-0009-000000000009");
    private static readonly Guid HyperXCloudAlphaId = Guid.Parse("f1000010-0010-0010-0010-000000000010");

    // =============================================================================
    public static async Task SeedAsync(
        AppDbContext context,
        bool seedDevelopmentAdmin = false,
        bool resetDemoCatalog = false)
    {
        if (seedDevelopmentAdmin)
            await SeedDevelopmentAdminAsync(context);

        await SeedProductsAsync(context, resetDemoCatalog);
    }

    private static async Task SeedProductsAsync(AppDbContext context, bool resetDemoCatalog)
    {
        if (!resetDemoCatalog && await context.Products.IgnoreQueryFilters().AnyAsync())
            return;

        await using var transaction = await context.Database.BeginTransactionAsync();

        if (resetDemoCatalog)
            await ResetDemoCatalogAsync(context);

        var seededAt = DateTimeOffset.UtcNow;
        context.Products.AddRange(CreateSeedProducts(seededAt));
        context.ProductImages.AddRange(CreateSeedProductImages(seededAt));

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task SeedDevelopmentAdminAsync(AppDbContext context)
    {
        var adminExists = await context.Users
            .AnyAsync(u => u.Email == DevelopmentAdminEmail || u.Role == UserRole.Admin);

        if (adminExists)
            return;

        var admin = User.Create(
            name: "Development Admin",
            email: DevelopmentAdminEmail,
            rawPassword: DevelopmentAdminPassword);

        admin.PromoteToAdmin();
        context.Users.Add(admin);

        await context.SaveChangesAsync();
    }

    private static async Task ResetDemoCatalogAsync(AppDbContext context)
    {
        await context.EmailNotifications.ExecuteDeleteAsync();
        await context.WebhookEvents.ExecuteDeleteAsync();
        await context.Payments.ExecuteDeleteAsync();
        await context.Orders.ExecuteDeleteAsync();
        await context.CartItems.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Ratings.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.ProductImages.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Products.IgnoreQueryFilters().ExecuteDeleteAsync();
    }

    // =============================================================================
    // Products
    // =============================================================================
    private static IEnumerable<Product> CreateSeedProducts(DateTimeOffset createdAt) =>
    [
        // -------------------------------------------------------------------------
        // MOBILES (11)
        // -------------------------------------------------------------------------
        CreateProduct(IPhone15ProId, "Apple iPhone 15 Pro", "Pro-level iPhone with A17 Pro chip, titanium design, and advanced 48MP camera.", 999m, 18, Category.Mobiles, 4.6m, 120, createdAt,
            new("Display", "6.1-inch Super Retina XDR"), new("Chip", "A17 Pro"), new("Camera", "48MP Main"), new("Storage", "128GB")),
        CreateProduct(Pixel8aId, "Google Pixel 8a", "Affordable Pixel with Tensor G3, AI-powered camera, and 120Hz display.", 499m, 20, Category.Mobiles, 4.4m, 75, createdAt,
            new("Display", "6.1-inch OLED 120Hz"), new("Chip", "Tensor G3"), new("Camera", "64MP wide"), new("Storage", "128GB")),
        CreateProduct(GalaxyS24UltraId, "Samsung Galaxy S24 Ultra", "Ultimate Android with built-in S Pen, 200MP camera, and titanium frame.", 1299m, 10, Category.Mobiles, 4.7m, 95, createdAt,
            new("Display", "6.8-inch Dynamic AMOLED"), new("Chip", "Snapdragon 8 Gen 3"), new("Camera", "200MP quad"), new("S Pen", "Included")),
        CreateProduct(OnePlus12Id, "OnePlus 12", "Flagship killer with Hasselblad cameras, 100W charging, and smooth OxygenOS.", 799m, 15, Category.Mobiles, 4.4m, 88, createdAt,
            new("Display", "6.82-inch LTPO AMOLED"), new("Chip", "Snapdragon 8 Gen 3"), new("Charging", "100W wired"), new("Camera", "50MP triple")),
        CreateProduct(Xiaomi14Id, "Xiaomi 14", "Compact flagship with Leica optics, Snapdragon 8 Gen 3, and 90W charging.", 749m, 12, Category.Mobiles, 4.3m, 67, createdAt,
            new("Display", "6.36-inch LTPO OLED"), new("Chip", "Snapdragon 8 Gen 3"), new("Camera", "50MP Leica triple"), new("Storage", "256GB")),
        CreateProduct(AsusZenfone11Id, "ASUS Zenfone 11 Ultra", "Gaming-focused phone with gimbal-stabilized camera and large battery.", 699m, 14, Category.Mobiles, 4.2m, 52, createdAt,
            new("Display", "6.78-inch AMOLED 144Hz"), new("Chip", "Snapdragon 8 Gen 3"), new("Battery", "5500 mAh"), new("Camera", "50MP gimbal")),
        CreateProduct(SonyXperia1VId, "Sony Xperia 1 V", "Cinematic 4K display phone with pro-grade camera and headphone jack.", 999m, 8, Category.Mobiles, 4.1m, 44, createdAt,
            new("Display", "6.5-inch 4K HDR OLED"), new("Chip", "Snapdragon 8 Gen 2"), new("Camera", "48MP Exmor T"), new("Jack", "3.5mm")),
        CreateProduct(MotorolaEdge50Id, "Motorola Edge 50 Ultra", "Sleek design with Snapdragon 8s Gen 3, 125W charging, and wooden back.", 899m, 11, Category.Mobiles, 4.3m, 38, createdAt,
            new("Display", "6.7-inch pOLED 144Hz"), new("Chip", "Snapdragon 8s Gen 3"), new("Charging", "125W Turbo"), new("Design", "Wooden back")),
        CreateProduct(NothingPhone2aId, "Nothing Phone (2a)", "Unique transparent design with Glyph interface and clean Android experience.", 399m, 25, Category.Mobiles, 4.2m, 72, createdAt,
            new("Display", "6.7-inch AMOLED"), new("Chip", "Dimensity 7200 Pro"), new("Glyph", "Light sequences"), new("Storage", "128GB")),
        CreateProduct(HonorMagic6Id, "Honor Magic6 Pro", "AI-enhanced flagship with 180MP periscope zoom and eye-comfort display.", 899m, 9, Category.Mobiles, 4.1m, 31, createdAt,
            new("Display", "6.8-inch LTPO OLED"), new("Chip", "Snapdragon 8 Gen 3"), new("Camera", "180MP periscope"), new("Battery", "5600 mAh")),
        CreateProduct(Realme12ProId, "realme 12 Pro+ 5G", "Mid-range phone with 120x superzoom and vegan leather design.", 329m, 22, Category.Mobiles, 4.0m, 55, createdAt,
            new("Display", "6.7-inch AMOLED 120Hz"), new("Chip", "Snapdragon 7s Gen 2"), new("Camera", "200MP 4x lossless zoom"), new("Design", "Vegan leather")),

        // -------------------------------------------------------------------------
        // LAPTOPS (11)
        // -------------------------------------------------------------------------
        CreateProduct(DellXps13Id, "Dell XPS 13", "Compact premium laptop with a bright display, fast SSD, and all-day portability.", 1299m, 12, Category.Laptops, 4.6m, 84, createdAt,
            new("CPU", "Intel Core Ultra 7"), new("RAM", "16GB"), new("Storage", "512GB SSD")),
        CreateProduct(MacBookAirM2Id, "Apple MacBook Air M2 (15-inch)", "Thin fanless laptop with M2 chip and Liquid Retina display.", 1299m, 10, Category.Laptops, 4.7m, 95, createdAt,
            new("CPU", "Apple M2"), new("RAM", "8GB"), new("Storage", "256GB"), new("Display", "15.3-inch")),
        CreateProduct(ThinkPadX1CarbonId, "Lenovo ThinkPad X1 Carbon Gen 11", "Business ultrabook with MIL-STD durability, 14-inch 2.8K OLED, and long battery.", 1499m, 7, Category.Laptops, 4.5m, 70, createdAt,
            new("CPU", "Intel Core i7-1365U"), new("RAM", "16GB"), new("Storage", "512GB SSD"), new("Display", "14-inch 2.8K OLED")),
        CreateProduct(SurfaceLaptop6Id, "Microsoft Surface Laptop 6", "Versatile 15-inch laptop with Snapdragon X Elite and Copilot+ PC features.", 1299m, 9, Category.Laptops, 4.3m, 62, createdAt,
            new("CPU", "Snapdragon X Elite"), new("RAM", "16GB"), new("Storage", "256GB SSD"), new("Battery", "Up to 20 hours")),
        CreateProduct(HPEnvy16Id, "HP Envy 16", "Creator laptop with 16-inch 4K OLED, Intel Core i9, and RTX 4060.", 1699m, 5, Category.Laptops, 4.4m, 41, createdAt,
            new("CPU", "Intel Core i9-13900H"), new("GPU", "RTX 4060"), new("RAM", "32GB"), new("Display", "16-inch 4K OLED")),
        CreateProduct(AsusZenbookS13Id, "ASUS Zenbook S 13 OLED", "Ultralight 1kg laptop with OLED panel and AMD Ryzen 7 7840U.", 1199m, 8, Category.Laptops, 4.3m, 55, createdAt,
            new("CPU", "AMD Ryzen 7 7840U"), new("Weight", "1 kg"), new("Display", "13.3-inch OLED"), new("Storage", "512GB SSD")),
        CreateProduct(AcerSwiftGo14Id, "Acer Swift Go 14", "Affordable 14-inch laptop with 2.8K OLED and Intel Core Ultra 7.", 799m, 14, Category.Laptops, 4.2m, 67, createdAt,
            new("CPU", "Intel Core Ultra 7 155H"), new("Display", "14-inch 2.8K OLED"), new("RAM", "16GB"), new("Storage", "512GB SSD")),
        CreateProduct(RazerBlade15Id, "Razer Blade 15", "Gaming and creator laptop with 15.6-inch QHD 240Hz and RTX 4070.", 2199m, 4, Category.Laptops, 4.6m, 48, createdAt,
            new("CPU", "Intel Core i7-13800H"), new("GPU", "RTX 4070"), new("Display", "15.6-inch QHD 240Hz"), new("RAM", "16GB")),
        CreateProduct(FrameworkLaptop13Id, "Framework Laptop 13 (DIY Edition)", "Modular, repairable laptop with hot-swappable expansion cards.", 849m, 6, Category.Laptops, 4.5m, 39, createdAt,
            new("CPU", "Intel Core i5-1340P (configurable)"), new("RAM", "Slots (user provided)"), new("Expansion", "4 hot-swap bays"), new("Display", "13.5-inch 3:2")),
        CreateProduct(SamsungGalaxyBook4Id, "Samsung Galaxy Book4 Ultra", "Flagship 16-inch laptop with dynamic AMOLED 2X and RTX 4070.", 2399m, 3, Category.Laptops, 4.4m, 22, createdAt,
            new("CPU", "Intel Core i9-13900H"), new("GPU", "RTX 4070"), new("Display", "16-inch 3K AMOLED"), new("RAM", "32GB")),
        CreateProduct(MSIThinGF63Id, "MSI Thin GF63 (2024)", "Budget gaming laptop with 144Hz display and RTX 4050.", 899m, 11, Category.Laptops, 4.1m, 73, createdAt,
            new("CPU", "Intel Core i5-12450H"), new("GPU", "RTX 4050"), new("Display", "15.6-inch FHD 144Hz"), new("Storage", "512GB SSD")),

        // -------------------------------------------------------------------------
        // TELEVISIONS (11)
        // -------------------------------------------------------------------------
        CreateProduct(LgOledTvId, "LG OLED C3 55-inch TV", "OLED television with deep contrast, low input lag, and HDR.", 1399m, 8, Category.Televisions, 4.6m, 97, createdAt,
            new("Display", "55 inch OLED"), new("Refresh", "120Hz"), new("HDR", "Dolby Vision")),
        CreateProduct(SamsungFrameTVId, "Samsung 65\" The Frame QLED TV", "Lifestyle TV that turns into art; customizable bezels.", 1999m, 5, Category.Televisions, 4.5m, 45, createdAt,
            new("Display", "65-inch QLED 4K"), new("Refresh", "120Hz"), new("Art Mode", "Yes"), new("HDR", "Quantum HDR")),
        CreateProduct(SonyBraviaX90LId, "Sony Bravia X90L 65\"", "Full Array LED TV with Cognitive Processor XR and Google TV.", 1299m, 6, Category.Televisions, 4.5m, 83, createdAt,
            new("Display", "65-inch 4K HDR LED"), new("Refresh", "120Hz"), new("Processor", "Cognitive XR"), new("HDR", "Dolby Vision")),
        CreateProduct(TCLC745Id, "TCL C745 55\" QLED Gaming TV", "High-value gaming TV with 144Hz VRR, Game Master 2.0, and HDR.", 699m, 12, Category.Televisions, 4.4m, 61, createdAt,
            new("Display", "55-inch QLED 4K"), new("Refresh", "144Hz VRR"), new("HDR", "Dolby Vision IQ"), new("Gaming", "Game Master 2.0")),
        CreateProduct(HisenseU8KId, "Hisense U8K 65\" Mini-LED", "Bright mini-LED TV with Google TV, IMAX Enhanced, and 2.1.2 audio.", 999m, 7, Category.Televisions, 4.3m, 52, createdAt,
            new("Display", "65-inch Mini-LED"), new("Refresh", "144Hz"), new("Audio", "2.1.2 channel"), new("HDR", "Dolby Vision")),
        CreateProduct(PhilipsOLED808Id, "Philips OLED+808 55\"", "OLED with 4-sided Ambilight, 120Hz, and P5 AI processing.", 1499m, 4, Category.Televisions, 4.4m, 38, createdAt,
            new("Display", "55-inch OLED"), new("Refresh", "120Hz"), new("Ambilight", "4-sided"), new("HDR", "Dolby Vision")),
        CreateProduct(PanasonicMZ2000Id, "Panasonic MZ2000 65\" OLED", "Cinema-grade OLED with Master HDR OLED panel and Technics-tuned audio.", 1999m, 3, Category.Televisions, 4.7m, 29, createdAt,
            new("Display", "65-inch Master OLED"), new("Refresh", "120Hz"), new("Audio", "Technics tuned"), new("HDR", "Dolby Vision IQ")),
        CreateProduct(VizioMQuantumId, "VIZIO M-Series Quantum 50\"", "Budget-friendly 4K Smart TV with Quantum Color and V-Gaming engine.", 449m, 15, Category.Televisions, 4.2m, 94, createdAt,
            new("Display", "50-inch QLED 4K"), new("Refresh", "60Hz"), new("HDR", "Dolby Vision"), new("Smart", "SmartCast")),
        CreateProduct(SharpAquosXLEDId, "Sharp Aquos XLED 65\"", "Japan-engineered mini-LED with Deep Chroma Display and 120Hz.", 1199m, 5, Category.Televisions, 4.1m, 33, createdAt,
            new("Display", "65-inch XLED mini-LED"), new("Refresh", "120Hz"), new("HDR", "Dolby Vision"), new("Processor", "Revolution")),
        CreateProduct(ToshibaFireTVId, "Toshiba C350 Fire TV 55\"", "Amazon Fire TV built-in, 4K HDR, and DTS Virtual:X.", 379m, 18, Category.Televisions, 4.0m, 120, createdAt,
            new("Display", "55-inch 4K LED"), new("Refresh", "60Hz"), new("HDR", "HDR10"), new("Smart", "Fire TV")),
        CreateProduct(SkyworthQ55Id, "Skyworth Q55 55\" QLED", "Affordable QLED with Google TV, Dolby Vision, and far-field mics.", 499m, 14, Category.Televisions, 4.1m, 44, createdAt,
            new("Display", "55-inch QLED"), new("Refresh", "60Hz"), new("HDR", "Dolby Vision"), new("Smart", "Google TV")),

        // -------------------------------------------------------------------------
        // GAMES (11)
        // -------------------------------------------------------------------------
        CreateProduct(PlayStation5Id, "Sony PlayStation 5 Slim", "Current-gen console with 1TB SSD, 4K gaming, DualSense controller.", 499m, 15, Category.Games, 4.8m, 215, createdAt,
            new("Storage", "1TB SSD"), new("Resolution", "Up to 4K"), new("Controller", "DualSense")),
        CreateProduct(NintendoSwitchOLEDId, "Nintendo Switch – OLED Model", "Hybrid console with 7-inch OLED screen, improved kickstand.", 349m, 25, Category.Games, 4.7m, 180, createdAt,
            new("Display", "7-inch OLED"), new("Storage", "64GB"), new("Battery", "4.5–9 hours")),
        CreateProduct(XboxSeriesXId, "Xbox Series X", "Most powerful Xbox with 1TB SSD, 4K 120fps, and Quick Resume.", 499m, 12, Category.Games, 4.7m, 198, createdAt,
            new("Storage", "1TB SSD"), new("Resolution", "Up to 8K"), new("Performance", "12 TFLOPS"), new("Controller", "Xbox Wireless")),
        CreateProduct(SteamDeckOLEDId, "Steam Deck OLED 1TB", "Handheld gaming PC with 7.4-inch HDR OLED and custom APU.", 649m, 9, Category.Games, 4.5m, 145, createdAt,
            new("Display", "7.4-inch HDR OLED"), new("Storage", "1TB NVMe SSD"), new("OS", "SteamOS"), new("Battery", "3-12 hours")),
        CreateProduct(DualSenseEdgeId, "DualSense Edge Wireless Controller", "Pro controller with customizable profiles, back buttons, and swappable sticks.", 199m, 20, Category.Games, 4.4m, 87, createdAt,
            new("Type", "Wireless pro"), new("Compatibility", "PS5/PC"), new("Features", "Back buttons, stick modules")),
        CreateProduct(ZeldaTearsKingdomId, "The Legend of Zelda: Tears of the Kingdom (Switch)", "Open-air adventure with limitless creativity, sky islands, and depths.", 69m, 30, Category.Games, 4.9m, 340, createdAt,
            new("Platform", "Nintendo Switch"), new("Genre", "Action-Adventure"), new("Players", "Single")),
        CreateProduct(MarioWonderId, "Super Mario Bros. Wonder (Switch)", "Classic 2D Mario with a new Wonder Flower twist and multiplayer.", 59m, 28, Category.Games, 4.7m, 211, createdAt,
            new("Platform", "Nintendo Switch"), new("Genre", "Platformer"), new("Players", "1-4")),
        CreateProduct(EldenRingId, "Elden Ring (PS5)", "Epic fantasy RPG from FromSoftware; explore the Lands Between.", 59m, 22, Category.Games, 4.8m, 400, createdAt,
            new("Platform", "PS5"), new("Genre", "Action RPG"), new("Players", "1-3")),
        CreateProduct(BaldursGate3Id, "Baldur's Gate 3 (PS5)", "Acclaimed RPG with deep storytelling, tactical combat, and co-op.", 69m, 18, Category.Games, 4.9m, 310, createdAt,
            new("Platform", "PS5"), new("Genre", "RPG"), new("Players", "1-4 split-screen")),
        CreateProduct(Cyberpunk2077Id, "Cyberpunk 2077: Ultimate Edition (Xbox)", "Open-world RPG in Night City, includes Phantom Liberty expansion.", 69m, 17, Category.Games, 4.4m, 225, createdAt,
            new("Platform", "Xbox Series X"), new("Genre", "Action RPG"), new("Edition", "Ultimate")),
        CreateProduct(XboxEliteControllerId, "Xbox Elite Wireless Controller Series 2", "Customizable controller with tension-adjustable thumbsticks and rubberized grip.", 179m, 15, Category.Games, 4.5m, 102, createdAt,
            new("Type", "Wireless pro"), new("Compatibility", "Xbox/PC"), new("Features", "Adjustable tension, paddles")),

        // -------------------------------------------------------------------------
        // APPLIANCES (11)
        // -------------------------------------------------------------------------
        CreateProduct(SamsungWasherId, "Samsung 5.0 cu. ft. Smart Front Load Washer", "Wi‑Fi washer with AI fabric care, steam, and super speed.", 799m, 7, Category.Appliances, 4.3m, 33, createdAt,
            new("Capacity", "5.0 cu. ft."), new("Features", "Wi‑Fi, Steam"), new("Cycles", "12")),
        CreateProduct(LGMicrowaveId, "LG NeoChef 1.5 cu. ft. Countertop Microwave", "Smart Inverter technology, even cooking, easy-clean interior.", 199m, 30, Category.Appliances, 4.2m, 50, createdAt,
            new("Capacity", "1.5 cu. ft."), new("Power", "1200W"), new("Technology", "Smart Inverter")),
        CreateProduct(DysonV15Id, "Dyson V15 Detect Cordless Vacuum", "Laser reveals microscopic dust, piezo sensor counts particles.", 749m, 10, Category.Appliances, 4.6m, 112, createdAt,
            new("Type", "Cordless stick"), new("Runtime", "60 min"), new("Bin", "0.76L"), new("Filtration", "HEPA")),
        CreateProduct(BoschDishwasherId, "Bosch 800 Series Dishwasher", "Quietest brand, CrystalDry technology, and flexible third rack.", 1299m, 5, Category.Appliances, 4.5m, 68, createdAt,
            new("Noise", "40 dBA"), new("Capacity", "16 place settings"), new("Drying", "CrystalDry"), new("Rack", "3rd rack")),
        CreateProduct(WhirlpoolFridgeId, "Whirlpool 25 cu. ft. French Door Refrigerator", "Spacious fridge with adaptive defrost, fingerprint-resistant stainless.", 1599m, 4, Category.Appliances, 4.3m, 45, createdAt,
            new("Capacity", "25 cu. ft."), new("Type", "French door"), new("Finish", "Fingerprint resistant")),
        CreateProduct(NespressoVertuoId, "Nespresso Vertuo Next Deluxe", "Creates perfect coffee or espresso with Centrifusion technology.", 179m, 25, Category.Appliances, 4.4m, 85, createdAt,
            new("Type", "Single-serve"), new("Drinks", "5 sizes"), new("Pod", "Vertuo"), new("Feature", "Bluetooth")),
        CreateProduct(InstantPotProId, "Instant Pot Pro 10-in-1", "10-in-1 multicooker with advanced steam release and 1200W.", 149m, 22, Category.Appliances, 4.5m, 145, createdAt,
            new("Capacity", "6 quarts"), new("Programs", "10"), new("Wattage", "1200W"), new("Material", "Stainless")),
        CreateProduct(BrevilleToasterId, "Breville Bit More Toaster", "Extra-wide slots, motorized lift, and 'A Bit More' button.", 99m, 35, Category.Appliances, 4.4m, 78, createdAt,
            new("Slots", "Extra wide"), new("Features", "Motorized lift"), new("Settings", "Variable browning")),
        CreateProduct(KitchenAidMixerId, "KitchenAid Artisan Series Tilt-Head Stand Mixer", "Iconic 5-quart mixer with 10 speeds, available in many colors.", 449m, 8, Category.Appliances, 4.7m, 210, createdAt,
            new("Capacity", "5 quarts"), new("Speeds", "10"), new("Attachments", "Flat beater, dough hook, whisk")),
        CreateProduct(DeLonghiMagnificaId, "De'Longhi Magnifica Evo", "Fully automatic espresso machine with LatteCrema frother.", 799m, 6, Category.Appliances, 4.3m, 54, createdAt,
            new("Type", "Bean-to-cup"), new("Drinks", "Espresso, lungo"), new("Frother", "LatteCrema")),
        CreateProduct(LGWashtowerId, "LG WashTower 4.5 cu. ft. Single Unit", "Space-saving washer/dryer combo with AI and center control panel.", 1999m, 3, Category.Appliances, 4.4m, 27, createdAt,
            new("Capacity", "4.5 cu. ft. wash / 7.4 dry"), new("Venting", "Vented"), new("AI", "Sensor dry/wash")),

        // -------------------------------------------------------------------------
        // ELECTRONICS (11)
        // -------------------------------------------------------------------------
        CreateProduct(GalaxyS24Id, "Samsung Galaxy S24", "Flagship Android with AMOLED, fast performance, versatile camera.", 899m, 24, Category.Electronics, 4.7m, 132, createdAt,
            new("Display", "6.2 inch AMOLED"), new("Storage", "256GB"), new("Camera", "50MP triple")),
        CreateProduct(iPadAirM2Id, "Apple iPad Air (M2, 2024)", "11-inch Liquid Retina, M2 chip, and support for Apple Pencil Pro.", 599m, 16, Category.Electronics, 4.6m, 101, createdAt,
            new("Display", "11-inch Liquid Retina"), new("Chip", "M2"), new("Pencil", "Pro support"), new("Storage", "128GB")),
        CreateProduct(GalaxyWatch6Id, "Samsung Galaxy Watch6 Classic", "Rotating bezel smartwatch with Wear OS, sleep tracking, and ECG.", 399m, 12, Category.Electronics, 4.4m, 82, createdAt,
            new("Display", "1.5-inch Super AMOLED"), new("OS", "Wear OS"), new("Battery", "40h"), new("Sensors", "ECG, BioActive")),
        CreateProduct(AirPodsPro2Id, "Apple AirPods Pro (2nd gen)", "Active noise cancellation, spatial audio, and USB-C MagSafe case.", 249m, 30, Category.Electronics, 4.7m, 290, createdAt,
            new("Type", "True wireless"), new("ANC", "Adaptive"), new("Battery", "6h (30h total)"), new("Chip", "H2")),
        CreateProduct(SonyWH1000XM5Id, "Sony WH-1000XM5", "Industry-leading noise canceling headphones with exceptional comfort.", 349m, 14, Category.Electronics, 4.7m, 195, createdAt,
            new("Type", "Over-ear"), new("ANC", "Yes, QN1"), new("Battery", "30h"), new("Weight", "250g")),
        CreateProduct(KindlePaperwhiteId, "Kindle Paperwhite (11th gen)", "Waterproof e-reader with 6.8\" glare-free display, adjustable warm light.", 149m, 40, Category.Electronics, 4.6m, 170, createdAt,
            new("Display", "6.8-inch, 300 ppi"), new("Storage", "16GB"), new("Waterproof", "IPX8"), new("Light", "Warm adjustable")),
        CreateProduct(GoProHero12Id, "GoPro HERO12 Black", "5.3K video, HyperSmooth 6.0, and extended battery life.", 399m, 15, Category.Electronics, 4.4m, 88, createdAt,
            new("Video", "5.3K60"), new("Stabilization", "HyperSmooth 6.0"), new("Waterproof", "33ft"), new("Battery", "Enduro")),
        CreateProduct(SamsungT7SSDId, "Samsung T7 Shield 2TB", "Rugged portable SSD with IP65 rating, 1050 MB/s read speed.", 159m, 20, Category.Electronics, 4.7m, 93, createdAt,
            new("Capacity", "2TB"), new("Speed", "1050 MB/s"), new("Durability", "IP65, 3m drop"), new("Connectivity", "USB-C")),
        CreateProduct(AnkerPowerBankId, "Anker 737 Power Bank (24,000mAh)", "140W fast charging power bank with smart display, can charge laptops.", 99m, 25, Category.Electronics, 4.5m, 115, createdAt,
            new("Capacity", "24000 mAh"), new("Output", "140W total"), new("Ports", "2x USB-C, 1x USB-A"), new("Display", "Smart display")),
        CreateProduct(RokuUltraId, "Roku Ultra (2024)", "Flagship streaming player with Dolby Vision, Wi‑Fi 6, and voice remote.", 99m, 20, Category.Electronics, 4.5m, 67, createdAt,
            new("Resolution", "4K HDR"), new("HDR", "Dolby Vision"), new("Connectivity", "Wi‑Fi 6, Ethernet"), new("Voice", "Hands-free")),
        CreateProduct(AppleTV4KId, "Apple TV 4K (3rd gen)", "A15 Bionic chip, HDR10+, and Siri Remote with USB-C.", 149m, 18, Category.Electronics, 4.6m, 130, createdAt,
            new("Chip", "A15 Bionic"), new("Resolution", "4K HDR"), new("HDR", "HDR10+, Dolby Vision"), new("Remote", "Siri USB-C")),

        // -------------------------------------------------------------------------
        // HOME (11)
        // -------------------------------------------------------------------------
        CreateProduct(RoombaJ7Id, "iRobot Roomba j7 Self‑Emptying Robot Vacuum", "Smart vacuum with PrecisionVision, obstacle avoidance, auto‑disposal.", 599m, 12, Category.Home, 4.4m, 60, createdAt,
            new("Navigation", "PrecisionVision"), new("Base", "Automatic disposal"), new("Runtime", "120 min")),
        CreateProduct(PhilipsHueStarterId, "Philips Hue White & Color Ambiance Starter Kit", "3 smart bulbs + bridge; control with app or voice, 16M colors.", 149m, 25, Category.Home, 4.5m, 140, createdAt,
            new("Bulbs", "3x A19 10W"), new("Bridge", "Included"), new("Colors", "16 million"), new("Voice", "Alexa/Google")),
        CreateProduct(DysonPurifierHotCoolId, "Dyson Purifier Hot+Cool Gen1", "Purifies, heats, and cools; sealed HEPA captures 99.97% of particles.", 569m, 9, Category.Home, 4.5m, 82, createdAt,
            new("Filtration", "HEPA H13"), new("Heating", "Yes"), new("Cooling", "Yes"), new("Oscillation", "350°")),
        CreateProduct(IkeaBillyBookcaseId, "IKEA BILLY / OXBERG Bookcase", "Classic 31.5\" wide bookcase with glass doors, white finish.", 119m, 18, Category.Home, 4.3m, 205, createdAt,
            new("Width", "31.5\""), new("Height", "79.5\""), new("Color", "White"), new("Doors", "Glass")),
        CreateProduct(TempurPedicPillowId, "Tempur-Pedic TEMPUR-Cloud Pillow", "Adaptable memory foam pillow, medium-firm, with washable cover.", 89m, 40, Category.Home, 4.3m, 173, createdAt,
            new("Material", "TEMPUR foam"), new("Firmness", "Medium-firm"), new("Cover", "Washable"), new("Size", "Standard")),
        CreateProduct(VitamixA3500Id, "Vitamix A3500 Smart Blender", "Premium blender with touchscreen, wireless connectivity, and self-cleaning.", 649m, 7, Category.Home, 4.7m, 98, createdAt,
            new("Motor", "2.2 HP"), new("Capacity", "64 oz"), new("Presets", "5 + variable"), new("Connectivity", "Wi‑Fi")),
        CreateProduct(NespressoMilkFrotherId, "Nespresso Aeroccino 4 Milk Frother", "Hot & cold milk frothing, dishwasher-safe, for cappuccinos and lattes.", 79m, 35, Category.Home, 4.4m, 132, createdAt,
            new("Type", "Milk frother"), new("Hot", "Yes"), new("Cold", "Yes"), new("Clean", "Dishwasher safe")),
        CreateProduct(LeCreusetDutchOvenId, "Le Creuset Signature Enameled Cast Iron Dutch Oven", "5.5 qt iconic pot, excellent heat retention, many colors.", 399m, 11, Category.Home, 4.8m, 222, createdAt,
            new("Capacity", "5.5 quarts"), new("Material", "Enameled cast iron"), new("Oven safe", "500°F"), new("Colors", "Multiple")),
        CreateProduct(AmazonEchoStudioId, "Amazon Echo Studio", "High-fidelity smart speaker with spatial audio and built-in smart home hub.", 199m, 15, Category.Home, 4.4m, 87, createdAt,
            new("Audio", "5 speakers, Dolby Atmos"), new("Voice", "Alexa"), new("Smart Hub", "Zigbee, Matter"), new("Streaming", "HD/Ultra HD")),
        CreateProduct(GoogleNestHubMaxId, "Google Nest Hub Max", "10-inch smart display with Nest Cam, Google Assistant, and YouTube TV.", 229m, 13, Category.Home, 4.5m, 74, createdAt,
            new("Display", "10-inch touchscreen"), new("Camera", "6.5MP with face match"), new("Audio", "Stereo speakers"), new("Voice", "Google Assistant")),
        CreateProduct(MieleCanisterVacuumId, "Miele Complete C3 Calima Canister Vacuum", "German-engineered with HEPA AirClean filter, 6 suction settings.", 749m, 6, Category.Home, 4.7m, 49, createdAt,
            new("Type", "Canister"), new("Filtration", "HEPA AirClean"), new("Settings", "6 suction levels"), new("Warranty", "5 years")),

        // -------------------------------------------------------------------------
        // OTHER (11)
        // -------------------------------------------------------------------------
        CreateProduct(MxKeysComboId, "Logitech MX Keys S Combo", "Wireless keyboard & mouse bundle for productivity, backlit keys.", 199m, 30, Category.Other, 4.4m, 61, createdAt,
            new("Keyboard", "Backlit low-profile"), new("Mouse", "MX Master 3S"), new("Connectivity", "Bluetooth / USB")),
        CreateProduct(AppleMagicKeyboardId, "Apple Magic Keyboard with Touch ID", "Wireless keyboard with Touch ID, numeric keypad, compatible with M-series Macs.", 179m, 20, Category.Other, 4.5m, 58, createdAt,
            new("Type", "Wireless"), new("Touch ID", "Yes"), new("Compatibility", "Apple Silicon Macs"), new("Layout", "Full")),
        CreateProduct(JBLCharge5Id, "JBL Charge 5", "Portable Bluetooth speaker with powerful sound, built-in powerbank.", 179m, 22, Category.Other, 4.6m, 169, createdAt,
            new("Type", "Portable speaker"), new("Battery", "20h"), new("Waterproof", "IP67"), new("Powerbank", "Yes")),
        CreateProduct(LogitechC920WebcamId, "Logitech C920s Pro HD Webcam", "Full HD 1080p webcam with privacy shutter and dual mics.", 69m, 35, Category.Other, 4.4m, 240, createdAt,
            new("Resolution", "1080p 30fps"), new("Mic", "Dual stereo"), new("Shutter", "Privacy")),
        CreateProduct(AnkerUSBCableId, "Anker PowerLine III USB-C Cable (6ft, 2-pack)", "Durable 100W fast-charging cables, reinforced with Kevlar.", 14m, 50, Category.Other, 4.6m, 310, createdAt,
            new("Length", "6ft"), new("Power", "100W"), new("Pack", "2"), new("Material", "Kevlar")),
        CreateProduct(UGreenDockId, "UGREEN Revodok Pro USB-C Hub", "10-in-1 hub with 4K HDMI, SD card slot, 100W PD, and Ethernet.", 59m, 28, Category.Other, 4.3m, 95, createdAt,
            new("Ports", "10"), new("HDMI", "4K@60Hz"), new("Ethernet", "Gigabit"), new("Power", "100W PD")),
        CreateProduct(SanDiskExtremeProId, "SanDisk Extreme PRO 256GB SD Card", "UHS‑I, 200MB/s read, perfect for 4K video and burst photography.", 39m, 45, Category.Other, 4.7m, 280, createdAt,
            new("Capacity", "256GB"), new("Speed", "200MB/s read"), new("Class", "U3, V30"), new("Format", "SDXC")),
        CreateProduct(RazerDeathAdderId, "Razer DeathAdder V3", "Ultra-lightweight 59g esports mouse with 30K optical sensor.", 69m, 25, Category.Other, 4.5m, 152, createdAt,
            new("Type", "Wired gaming mouse"), new("Sensor", "Focus Pro 30K"), new("Weight", "59g"), new("Switches", "Optical Gen-3")),
        CreateProduct(SecretLabChairId, "Secretlab TITAN Evo 2024", "Premium gaming/office chair with adjustable lumbar, magnetic head pillow.", 549m, 5, Category.Other, 4.6m, 112, createdAt,
            new("Material", "SoftWeave Plus"), new("Lumbar", "4-way built-in"), new("Recline", "165°"), new("Warranty", "5 years")),
        CreateProduct(ElgatoStreamDeckId, "Elgato Stream Deck MK.2", "15 customizable LCD keys to control apps, streaming, and macros.", 149m, 18, Category.Other, 4.5m, 88, createdAt,
            new("Keys", "15 LCD"), new("Functions", "Customizable"), new("Integration", "Twitch, OBS, etc.")),
        CreateProduct(HyperXCloudAlphaId, "HyperX Cloud Alpha Wireless", "300‑hour battery wireless gaming headset with DTS:X and memory foam.", 199m, 10, Category.Other, 4.4m, 76, createdAt,
            new("Type", "Wireless over-ear"), new("Battery", "300h"), new("Audio", "DTS Headphone:X"), new("Mic", "Detachable"))
    ];

    // =============================================================================
    // Product Images (3 per product, 88*3 = 264 images)
    // =============================================================================
    private static IEnumerable<ProductImage> CreateSeedProductImages(DateTimeOffset createdAt)
    {
        // Helper arrays to reduce repetition
        var products = new (Guid id, string label, Guid imgBase)[]
        {
            // MOBILES
            (IPhone15ProId, "iPhone+15+Pro", Guid.Parse("77777777-7777-7777-7777-000000000000")),
            (Pixel8aId, "Pixel+8a", Guid.Parse("88888888-8888-8888-8888-000000000000")),
            (GalaxyS24UltraId, "Galaxy+S24+Ultra", Guid.Parse("e0000001-0001-0001-0001-000000000000")),
            (OnePlus12Id, "OnePlus+12", Guid.Parse("e0000002-0002-0002-0002-000000000000")),
            (Xiaomi14Id, "Xiaomi+14", Guid.Parse("e0000003-0003-0003-0003-000000000000")),
            (AsusZenfone11Id, "Zenfone+11", Guid.Parse("e0000004-0004-0004-0004-000000000000")),
            (SonyXperia1VId, "Xperia+1+V", Guid.Parse("e0000005-0005-0005-0005-000000000000")),
            (MotorolaEdge50Id, "Edge+50+Ultra", Guid.Parse("e0000006-0006-0006-0006-000000000000")),
            (NothingPhone2aId, "Nothing+Phone+2a", Guid.Parse("e0000007-0007-0007-0007-000000000000")),
            (HonorMagic6Id, "Honor+Magic6+Pro", Guid.Parse("e0000008-0008-0008-0008-000000000000")),
            (Realme12ProId, "Realme+12+Pro", Guid.Parse("e0000009-0009-0009-0009-000000000000")),

            // LAPTOPS
            (DellXps13Id, "Dell+XPS+13", Guid.Parse("11111111-1111-1111-1111-000000000000")),
            (MacBookAirM2Id, "MacBook+Air+M2", Guid.Parse("66666666-6666-6666-6666-000000000000")),
            (ThinkPadX1CarbonId, "ThinkPad+X1+Carbon", Guid.Parse("f0000001-0001-0001-0001-000000000000")),
            (SurfaceLaptop6Id, "Surface+Laptop+6", Guid.Parse("f0000002-0002-0002-0002-000000000000")),
            (HPEnvy16Id, "HP+Envy+16", Guid.Parse("f0000003-0003-0003-0003-000000000000")),
            (AsusZenbookS13Id, "Zenbook+S13", Guid.Parse("f0000004-0004-0004-0004-000000000000")),
            (AcerSwiftGo14Id, "Swift+Go+14", Guid.Parse("f0000005-0005-0005-0005-000000000000")),
            (RazerBlade15Id, "Razer+Blade+15", Guid.Parse("f0000006-0006-0006-0006-000000000000")),
            (FrameworkLaptop13Id, "Framework+Laptop+13", Guid.Parse("f0000007-0007-0007-0007-000000000000")),
            (SamsungGalaxyBook4Id, "Galaxy+Book4+Ultra", Guid.Parse("f0000008-0008-0008-0008-000000000000")),
            (MSIThinGF63Id, "MSI+Thin+GF63", Guid.Parse("f0000009-0009-0009-0009-000000000000")),

            // TELEVISIONS
            (LgOledTvId, "LG+OLED+C3", Guid.Parse("44444444-4444-4444-4444-000000000000")),
            (SamsungFrameTVId, "The+Frame+TV", Guid.Parse("99999999-9999-9999-9999-000000000000")),
            (SonyBraviaX90LId, "Bravia+X90L", Guid.Parse("a0000001-0001-0001-0001-000000000000")),
            (TCLC745Id, "TCL+C745", Guid.Parse("a0000002-0002-0002-0002-000000000000")),
            (HisenseU8KId, "Hisense+U8K", Guid.Parse("a0000003-0003-0003-0003-000000000000")),
            (PhilipsOLED808Id, "Philips+OLED808", Guid.Parse("a0000004-0004-0004-0004-000000000000")),
            (PanasonicMZ2000Id, "Panasonic+MZ2000", Guid.Parse("a0000005-0005-0005-0005-000000000000")),
            (VizioMQuantumId, "Vizio+M+Quantum", Guid.Parse("a0000006-0006-0006-0006-000000000000")),
            (SharpAquosXLEDId, "Sharp+Aquos+XLED", Guid.Parse("a0000007-0007-0007-0007-000000000000")),
            (ToshibaFireTVId, "Toshiba+Fire+TV", Guid.Parse("a0000008-0008-0008-0008-000000000000")),
            (SkyworthQ55Id, "Skyworth+Q55", Guid.Parse("a0000009-0009-0009-0009-000000000000")),

            // GAMES
            (PlayStation5Id, "PlayStation+5", Guid.Parse("33333333-3333-3333-3333-000000000000")),
            (NintendoSwitchOLEDId, "Switch+OLED", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000000")),
            (XboxSeriesXId, "Xbox+Series+X", Guid.Parse("b0000001-0001-0001-0001-000000000000")),
            (SteamDeckOLEDId, "Steam+Deck+OLED", Guid.Parse("b0000002-0002-0002-0002-000000000000")),
            (DualSenseEdgeId, "DualSense+Edge", Guid.Parse("b0000003-0003-0003-0003-000000000000")),
            (ZeldaTearsKingdomId, "Zelda+TOTK", Guid.Parse("b0000004-0004-0004-0004-000000000000")),
            (MarioWonderId, "Mario+Wonder", Guid.Parse("b0000005-0005-0005-0005-000000000000")),
            (EldenRingId, "Elden+Ring", Guid.Parse("b0000006-0006-0006-0006-000000000000")),
            (BaldursGate3Id, "Baldurs+Gate+3", Guid.Parse("b0000007-0007-0007-0007-000000000000")),
            (Cyberpunk2077Id, "Cyberpunk+2077", Guid.Parse("b0000008-0008-0008-0008-000000000000")),
            (XboxEliteControllerId, "Xbox+Elite+Series2", Guid.Parse("b0000009-0009-0009-0009-000000000000")),

            // APPLIANCES
            (SamsungWasherId, "Samsung+Washer", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000000")),
            (LGMicrowaveId, "LG+Microwave", Guid.Parse("cccccccc-cccc-cccc-cccc-000000000000")),
            (DysonV15Id, "Dyson+V15", Guid.Parse("c0000001-0001-0001-0001-000000000000")),
            (BoschDishwasherId, "Bosch+Dishwasher", Guid.Parse("c0000002-0002-0002-0002-000000000000")),
            (WhirlpoolFridgeId, "Whirlpool+Fridge", Guid.Parse("c0000003-0003-0003-0003-000000000000")),
            (NespressoVertuoId, "Nespresso+Vertuo", Guid.Parse("c0000004-0004-0004-0004-000000000000")),
            (InstantPotProId, "Instant+Pot+Pro", Guid.Parse("c0000005-0005-0005-0005-000000000000")),
            (BrevilleToasterId, "Breville+Toaster", Guid.Parse("c0000006-0006-0006-0006-000000000000")),
            (KitchenAidMixerId, "KitchenAid+Mixer", Guid.Parse("c0000007-0007-0007-0007-000000000000")),
            (DeLonghiMagnificaId, "DeLonghi+Magnifica", Guid.Parse("c0000008-0008-0008-0008-000000000000")),
            (LGWashtowerId, "LG+WashTower", Guid.Parse("c0000009-0009-0009-0009-000000000000")),

            // ELECTRONICS
            (GalaxyS24Id, "Galaxy+S24", Guid.Parse("22222222-2222-2222-2222-000000000000")),
            (iPadAirM2Id, "iPad+Air+M2", Guid.Parse("d0000001-0001-0001-0001-000000000000")),
            (GalaxyWatch6Id, "Galaxy+Watch6", Guid.Parse("d0000002-0002-0002-0002-000000000000")),
            (AirPodsPro2Id, "AirPods+Pro+2", Guid.Parse("d0000003-0003-0003-0003-000000000000")),
            (SonyWH1000XM5Id, "WH-1000XM5", Guid.Parse("d0000004-0004-0004-0004-000000000000")),
            (KindlePaperwhiteId, "Kindle+Paperwhite", Guid.Parse("d0000005-0005-0005-0005-000000000000")),
            (GoProHero12Id, "HERO12+Black", Guid.Parse("d0000006-0006-0006-0006-000000000000")),
            (SamsungT7SSDId, "T7+Shield+SSD", Guid.Parse("d0000007-0007-0007-0007-000000000000")),
            (AnkerPowerBankId, "Anker+737", Guid.Parse("d0000008-0008-0008-0008-000000000000")),
            (RokuUltraId, "Roku+Ultra", Guid.Parse("d0000009-0009-0009-0009-000000000000")),
            (AppleTV4KId, "Apple+TV+4K", Guid.Parse("d0000010-0010-0010-0010-000000000000")),

            // HOME
            (RoombaJ7Id, "Roomba+j7", Guid.Parse("dddddddd-dddd-dddd-dddd-000000000000")),
            (PhilipsHueStarterId, "Philips+Hue", Guid.Parse("e1000001-0001-0001-0001-000000000000")),
            (DysonPurifierHotCoolId, "Dyson+Purifier", Guid.Parse("e1000002-0002-0002-0002-000000000000")),
            (IkeaBillyBookcaseId, "BILLY+Bookcase", Guid.Parse("e1000003-0003-0003-0003-000000000000")),
            (TempurPedicPillowId, "TEMPUR+Pillow", Guid.Parse("e1000004-0004-0004-0004-000000000000")),
            (VitamixA3500Id, "Vitamix+A3500", Guid.Parse("e1000005-0005-0005-0005-000000000000")),
            (NespressoMilkFrotherId, "Aeroccino+4", Guid.Parse("e1000006-0006-0006-0006-000000000000")),
            (LeCreusetDutchOvenId, "Le+Creuset+Pot", Guid.Parse("e1000007-0007-0007-0007-000000000000")),
            (AmazonEchoStudioId, "Echo+Studio", Guid.Parse("e1000008-0008-0008-0008-000000000000")),
            (GoogleNestHubMaxId, "Nest+Hub+Max", Guid.Parse("e1000009-0009-0009-0009-000000000000")),
            (MieleCanisterVacuumId, "Miele+C3", Guid.Parse("e1000010-0010-0010-0010-000000000000")),

            // OTHER
            (MxKeysComboId, "MX+Keys+S", Guid.Parse("55555555-5555-5555-5555-000000000000")),
            (AppleMagicKeyboardId, "Magic+Keyboard", Guid.Parse("f1000001-0001-0001-0001-000000000000")),
            (JBLCharge5Id, "JBL+Charge+5", Guid.Parse("f1000002-0002-0002-0002-000000000000")),
            (LogitechC920WebcamId, "C920s", Guid.Parse("f1000003-0003-0003-0003-000000000000")),
            (AnkerUSBCableId, "Anker+Cable", Guid.Parse("f1000004-0004-0004-0004-000000000000")),
            (UGreenDockId, "UGREEN+Dock", Guid.Parse("f1000005-0005-0005-0005-000000000000")),
            (SanDiskExtremeProId, "SanDisk+SD", Guid.Parse("f1000006-0006-0006-0006-000000000000")),
            (RazerDeathAdderId, "DeathAdder+V3", Guid.Parse("f1000007-0007-0007-0007-000000000000")),
            (SecretLabChairId, "Secretlab+Titan", Guid.Parse("f1000008-0008-0008-0008-000000000000")),
            (ElgatoStreamDeckId, "Stream+Deck+MK2", Guid.Parse("f1000009-0009-0009-0009-000000000000")),
            (HyperXCloudAlphaId, "Cloud+Alpha", Guid.Parse("f1000010-0010-0010-0010-000000000000")),
        };

        // Generate 3 images per product: primary + 2 alternates
        foreach (var (productId, label, imgBase) in products)
        {
            yield return CreateProductImage(
                IncrementGuid(imgBase, 1),
                productId,
                $"https://placehold.co/600x600/png?text={label}",
                isPrimary: true,
                displayOrder: 0,
                createdAt);
            yield return CreateProductImage(
                IncrementGuid(imgBase, 2),
                productId,
                $"https://placehold.co/600x600/png?text={label}+Angle",
                isPrimary: false,
                displayOrder: 1,
                createdAt);
            yield return CreateProductImage(
                IncrementGuid(imgBase, 3),
                productId,
                $"https://placehold.co/600x600/png?text={label}+Side",
                isPrimary: false,
                displayOrder: 2,
                createdAt);
        }
    }

    // Helper to increment a GUID by a value in the last segment (keeps rest unchanged)
    private static Guid IncrementGuid(Guid baseGuid, int increment)
    {
        var bytes = baseGuid.ToByteArray();
        // Increment the last 4 bytes as a little-endian integer
        int last = BitConverter.ToInt32(bytes, bytes.Length - 4);
        last += increment;
        BitConverter.GetBytes(last).CopyTo(bytes, bytes.Length - 4);
        return new Guid(bytes);
    }

    // =============================================================================
    // Core helper methods
    // =============================================================================
    private static Product CreateProduct(
        Guid id,
        string name,
        string description,
        decimal price,
        int stockQuantity,
        Category category,
        decimal averageRating,
        int ratingCount,
        DateTimeOffset createdAt,
        params ProductSpecification[] specifications)
    {
        return new Product
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            StockQuantity = stockQuantity,
            Category = category,
            Specifications = specifications.ToList(),
            AverageRating = averageRating,
            RatingCount = ratingCount,
            IsDeleted = false,
            CreatedAt = createdAt
        };
    }

    private static ProductImage CreateProductImage(
        Guid id,
        Guid productId,
        string imageUrl,
        bool isPrimary,
        int displayOrder,
        DateTimeOffset createdAt)
    {
        return new ProductImage
        {
            Id = id,
            ProductId = productId,
            ImageUrl = imageUrl,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
            ContentHash = CreateContentHash(imageUrl),
            CreatedAt = createdAt
        };
    }

    private static string CreateContentHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}