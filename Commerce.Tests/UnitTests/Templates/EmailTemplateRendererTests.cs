using System.Text.Json;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Email.Templates;
using Commerce.Application.Settings;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Commerce.Tests.UnitTests.Templates;

public class EmailTemplateRendererTests
{
    private static EmailTemplateRenderer CreateRenderer() =>
        new(Options.Create(new EmailSettings
        {
            FromAddress = "noreply@commerce.com",
            FromName = "Commerce",
            FrontendBaseUrl = "https://app.commerce.com"
        }));

    private static Dictionary<string, string> OrderConfirmationData(
        string orderNumber = "000000001",
        string customerName = "John Doe",
        string totalAmount = "99.99",
        string? orderId = null,
        string paymentMethod = "Card",
        string paymentStatus = "Paid",
        List<OrderLineItemData>? items = null) =>
        new()
        {
            ["CustomerName"] = customerName,
            ["OrderNumber"] = orderNumber,
            ["OrderId"] = orderId ?? Guid.NewGuid().ToString(),
            ["TotalAmount"] = totalAmount,
            ["PaymentMethod"] = paymentMethod,
            ["PaymentStatus"] = paymentStatus,
            ["Items"] = JsonSerializer.Serialize(items ?? [])
        };

    // ── OrderConfirmation ─────────────────────────────────────────────────────

    [Fact]
    public void RenderOrderConfirmation_ShouldContainOrderNumberInSubjectAndBody()
    {
        var renderer = CreateRenderer();

        var (subject, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(orderNumber: "000000042"));

        subject.ShouldContain("000000042");
        html.ShouldContain("000000042");
    }

    [Fact]
    public void RenderOrderConfirmation_ShouldGreetCustomerByName()
    {
        var renderer = CreateRenderer();

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(customerName: "Jane Smith"));

        html.ShouldContain("Jane Smith");
    }

    [Fact]
    public void RenderOrderConfirmation_ShouldContainTotalAmount()
    {
        var renderer = CreateRenderer();

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(totalAmount: "249.99"));

        html.ShouldContain("249.99");
    }

    [Fact]
    public void RenderOrderConfirmation_ShouldContainOrderDetailLink()
    {
        var orderId = Guid.NewGuid().ToString();
        var renderer = CreateRenderer();

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(orderId: orderId));

        html.ShouldContain($"https://app.commerce.com/orders/{orderId}");
    }

    [Fact]
    public void RenderOrderConfirmation_WithLineItems_ShouldContainProductNames()
    {
        var renderer = CreateRenderer();
        var items = new List<OrderLineItemData>
        {
            new("Sony WH-1000XM5", null, 349.99m, 1),
            new("Nike Air Max", "https://img.example.com/shoe.jpg", 89.99m, 2)
        };

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(items: items));

        html.ShouldContain("Sony WH-1000XM5");
        html.ShouldContain("Nike Air Max");
    }

    [Fact]
    public void RenderOrderConfirmation_WithLineItems_ShouldContainLineTotals()
    {
        var renderer = CreateRenderer();
        var items = new List<OrderLineItemData>
        {
            new("Headphones", null, 100m, 3) // line total = $300.00
        };

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(items: items));

        html.ShouldContain("300.00");
    }

    [Fact]
    public void RenderOrderConfirmation_ShouldRenderPaymentMethodAndStatus()
    {
        var renderer = CreateRenderer();

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(
                paymentMethod: "Cash on delivery",
                paymentStatus: "Awaiting payment"));

        html.ShouldContain("Payment Method");
        html.ShouldContain("Cash on delivery");
    }

    [Fact]
    public void RenderOrderConfirmation_WithItemImage_ShouldUseEmailSafeImageMarkup()
    {
        var renderer = CreateRenderer();
        var items = new List<OrderLineItemData>
        {
            new("Nike Air Max", "https://img.example.com/shoe.jpg?size=64&fit=crop", 89.99m, 2)
        };

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(items: items));

        html.ShouldContain("width=\"60\" height=\"60\"");
        html.ShouldContain("https://img.example.com/shoe.jpg?size=64&amp;fit=crop");
    }

    [Fact]
    public void RenderOrderConfirmation_WithItemMissingImage_ShouldRenderPlaceholder()
    {
        var renderer = CreateRenderer();
        var items = new List<OrderLineItemData>
        {
            new("No-Image Product", ImageUrl: null, 10m, 1)
        };

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(items: items));

        html.ShouldContain("📦");
        html.ShouldContain("<table role=\"presentation\" width=\"60\" height=\"60\"");
        html.ShouldNotContain("display:flex");
    }


    [Fact]
    public void RenderOrderConfirmation_WhenItemsKeyMissing_ShouldNotThrow()
    {
        var renderer = CreateRenderer();
        var dataWithoutItems = new Dictionary<string, string>
        {
            ["CustomerName"] = "Test",
            ["OrderNumber"] = "000000001",
            ["OrderId"] = Guid.NewGuid().ToString(),
            ["TotalAmount"] = "0.00"
            // No "Items" key
        };

        Should.NotThrow(() =>
            renderer.Render(EmailTemplate.OrderConfirmation, dataWithoutItems));
    }

    [Fact]
    public void RenderOrderConfirmation_ShouldHtmlEncodeProductNames()
    {
        var renderer = CreateRenderer();
        var items = new List<OrderLineItemData>
        {
            new("<script>alert('xss')</script>", null, 10m, 1)
        };

        var (_, html) = renderer.Render(
            EmailTemplate.OrderConfirmation,
            OrderConfirmationData(items: items));

        // Raw script tag must not appear — it must be encoded
        html.ShouldNotContain("<script>alert");
        html.ShouldContain("&lt;script&gt;");
    }

    // ── PasswordReset ─────────────────────────────────────────────────────────

    [Fact]
    public void RenderPasswordReset_SubjectShouldBeFixed()
    {
        var renderer = CreateRenderer();

        var (subject, _) = renderer.Render(EmailTemplate.PasswordReset, new Dictionary<string, string>
        {
            ["ResetUrl"]  = "https://app.commerce.com/reset-password?token=abc",
            ["ExpiresIn"] = "1 hour"
        });

        subject.ShouldBe("Reset Your Password");
    }

    [Fact]
    public void RenderPasswordReset_ShouldContainResetUrl()
    {
        var renderer = CreateRenderer();
        const string resetUrl = "https://app.commerce.com/reset-password?token=tok_xyz_123";

        var (_, html) = renderer.Render(EmailTemplate.PasswordReset, new Dictionary<string, string>
        {
            ["ResetUrl"] = resetUrl,
            ["ExpiresIn"] = "1 hour"
        });

        html.ShouldContain(resetUrl);
    }

    [Fact]
    public void RenderPasswordReset_ShouldContainExpiryNotice()
    {
        var renderer = CreateRenderer();

        var (_, html) = renderer.Render(EmailTemplate.PasswordReset, new Dictionary<string, string>
        {
            ["ResetUrl"] = "https://example.com/reset",
            ["ExpiresIn"] = "1 hour"
        });

        html.ShouldContain("1 hour");
    }

    [Fact]
    public void RenderPasswordReset_ShouldContainSecurityWarning()
    {
        var renderer = CreateRenderer();

        var (_, html) = renderer.Render(EmailTemplate.PasswordReset, new Dictionary<string, string>
        {
            ["ResetUrl"] = "https://example.com/reset",
            ["ExpiresIn"] = "1 hour"
        });

        // Users who didn't request this should be told to ignore the email
        html.ShouldContain("Didn't request this");
    }

    // ── Unknown template ──────────────────────────────────────────────────────

    [Fact]
    public void Render_WithUnknownTemplate_ShouldThrowArgumentOutOfRangeException()
    {
        var renderer = CreateRenderer();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            renderer.Render((EmailTemplate)999, new Dictionary<string, string>()));
    }
}