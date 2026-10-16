using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;

public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("STOCKPILOT_BROWSER") != "1")
            Skip = "Set STOCKPILOT_BROWSER=1 after installing the matching Playwright Chromium runtime.";
    }
}
[Collection("database")]
public sealed class BrowserTests(Fixture f)
{
    [BrowserFact]
    public async Task PurchasePosSettlementAndMobileInvoiceWorkInChromium()
    {
        using (var scope = f.Factory.Services.CreateScope())
        {
            var s = scope.ServiceProvider.GetRequiredService<BusinessService>();
            var a = f.Users[Roles.Admin];
            product = await InventoryTests.Product(s, a);
            sku = (await s.Product(a, product)).Sku;
            supplier = await s.SavePartner(a, "Supplier", null, new PartnerInput("Browser supplier"));
        }
        await using var server = new AppFactory(f);
        server.UseKestrel(0);
        using var client = server.CreateClient();
        var origin = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new()
            {
                Width = 1440,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await page.GotoAsync(origin + "/Account/Login");
        await page.GetByLabel("Email", new()
        {
            Exact = true
        }).FillAsync("Admin@stockpilot.test");
        await page.GetByLabel("Password", new()
        {
            Exact = true
        }).FillAsync(f.Password);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Sign in",
            Exact = true
        }).ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await Capture(page, "dashboard-desktop");
        await page.GotoAsync(origin + "/Purchases/Create");
        await SearchAndAdd(page);
        await page.GetByLabel("Quantity", new()
        {
            Exact = true
        }).FillAsync("2");
        await page.GetByLabel("Unit cost", new()
        {
            Exact = true
        }).FillAsync("3");
        await page.Locator("#partner-query").FillAsync("Browser supplier");
        await page.Locator("#partner-search").ClickAsync();
        await page.Locator("#partner option").Filter(new()
        {
            HasText = "Browser supplier"
        }).WaitForAsync(new()
        {
            State = WaitForSelectorState.Attached
        });
        await page.Locator("#partner").SelectOptionAsync(supplier.ToString());
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Create draft for review"
        }).ClickAsync();
        await page.WaitForURLAsync("**/Purchases/Details/*");
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Receive purchase",
            Exact = true
        }).ClickAsync();
        await page.GetByText("Received", new()
        {
            Exact = true
        }).WaitForAsync();
        await page.GotoAsync(origin + "/Sales/Create");
        await SearchAndAdd(page);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Create draft for review"
        }).ClickAsync();
        await page.WaitForURLAsync("**/Sales/Details/*");
        var saleId = Guid.Parse(page.Url.Split('/').Last());
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Confirm sale",
            Exact = true
        }).ClickAsync();
        await page.GetByText("Completed", new()
        {
            Exact = true
        }).WaitForAsync();
        await page.GetByLabel("Amount", new()
        {
            Exact = true
        }).FillAsync("10");
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Record payment",
            Exact = true
        }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Payment history"
        }).WaitForAsync();
        await Capture(page, "invoice-desktop");
        await page.SetViewportSizeAsync(390, 844);
        await Capture(page, "invoice-mobile");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        Assert.Empty(errors);
        using var verify = f.Factory.Services.CreateScope();
        var service = verify.ServiceProvider.GetRequiredService<BusinessService>();
        Assert.Equal(1, (await service.Product(f.Users[Roles.Admin], product)).Stock);
        Assert.Equal("Paid", (await service.Document(f.Users[Roles.Admin], saleId)).PaymentState);
    }
    private Guid product, supplier; private string sku = "";
    private async Task SearchAndAdd(IPage page)
    {
        await page.Locator("#query").FillAsync(sku);
        await page.Locator("#product-search").GetByRole(AriaRole.Button, new()
        {
            Name = "Search",
            Exact = true
        }).ClickAsync();
        await page.Locator("#search-results").GetByRole(AriaRole.Button, new()
        {
            Name = "Add",
            Exact = true
        }).ClickAsync();
    }
    private static async Task Capture(IPage page, string name)
    {
        var dir = Environment.GetEnvironmentVariable("STOCKPILOT_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(dir, name + ".png"),
                FullPage = true
            });
        }
    }
}
