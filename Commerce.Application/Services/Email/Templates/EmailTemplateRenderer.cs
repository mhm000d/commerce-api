using System.Net;
using System.Text;
using System.Text.Json;
using Commerce.Application.Models;
using Commerce.Application.Settings;
using Microsoft.Extensions.Options;

namespace Commerce.Application.Services.Email.Templates;

public class EmailTemplateRenderer(IOptions<EmailSettings> settings)
{
    private readonly EmailSettings _settings = settings.Value;

    public (string Subject, string HtmlBody) Render(
        EmailTemplate template,
        Dictionary<string, string> data)
    {
        return template switch
        {
            EmailTemplate.OrderConfirmation => RenderOrderConfirmation(data),
            EmailTemplate.PasswordReset => RenderPasswordReset(data),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
        };
    }

    // ── Order Confirmation ────────────────────────────────────────────────────

    private (string Subject, string HtmlBody) RenderOrderConfirmation(
        Dictionary<string, string> data)
    {
        var orderNumber = data.GetValueOrDefault("OrderNumber", "N/A");
        var totalAmount = data.GetValueOrDefault("TotalAmount", "0.00");
        var customerName = data.GetValueOrDefault("CustomerName", "Valued Customer");
        var orderId = data.GetValueOrDefault("OrderId", "");
        var paymentMethod = data.GetValueOrDefault("PaymentMethod", "Card");
        var paymentStatus = data.GetValueOrDefault("PaymentStatus", "Paid");
        var orderUrl = $"{_settings.FrontendBaseUrl}/orders/{orderId}";
        var subject = $"Order Confirmation – #{orderNumber}";

        List<OrderLineItemData> items = [];
        if (data.TryGetValue("Items", out var itemsJson))
            items = JsonSerializer.Deserialize<List<OrderLineItemData>>(itemsJson) ?? [];

        var itemsHtml = BuildItemsTableHtml(items);

        var html = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>Order Confirmed</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">

          <!-- Preview Text -->
          <div style="display:none;max-height:0;overflow:hidden;">
            Your order {orderNumber} has been confirmed. Total: ${totalAmount}
          </div>

          <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f5;padding:40px 0;">
            <tr>
              <td align="center">
                <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;">

                  <!-- Header -->
                  <tr>
                    <td style="background:linear-gradient(135deg,#4f46e5,#6366f1);border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                      <h1 style="margin:0;font-size:28px;font-weight:700;color:#ffffff;letter-spacing:-0.5px;">
                        Commerce
                      </h1>
                    </td>
                  </tr>

                  <!-- Body -->
                  <tr>
                    <td style="background-color:#ffffff;padding:36px 40px;">

                      <!-- Greeting -->
                      <p style="margin:0 0 24px;font-size:16px;color:#374151;">
                        Dear <strong>{HtmlEncode(customerName)}</strong>,
                      </p>
                      <p style="margin:0 0 32px;font-size:15px;color:#374151;">
                        Thank you for your order. We are pleased to confirm that your order has been received and is being processed.
                      </p>

                      <!-- Items Table -->
                      {itemsHtml}

                      <!-- Order Summary Card -->
                      <table width="100%" cellpadding="0" cellspacing="0"
                             style="background:linear-gradient(135deg,#f8fafc,#f1f5f9);border:1px solid #e2e8f0;border-radius:10px;margin:0 0 28px;">
                        <tr>
                          <td style="padding:20px 24px;">
                            <p style="margin:0 0 16px;font-size:11px;font-weight:700;color:#64748b;letter-spacing:0.08em;text-transform:uppercase;">
                              Order Summary
                            </p>
                            <table width="100%" cellpadding="0" cellspacing="0">
                              <tr>
                                <td style="padding:8px 0;font-size:14px;color:#475569;">Order Number</td>
                                <td align="right" style="padding:8px 0;font-size:14px;font-weight:600;color:#0f172a;">
                                  #{orderNumber}
                                </td>
                              </tr>
                              <tr>
                                <td colspan="2"><hr style="border:none;border-top:1px solid #e2e8f0;margin:2px 0;" /></td>
                              </tr>
                              <tr>
                                <td style="padding:8px 0;font-size:14px;color:#475569;">Payment Method</td>
                                <td align="right" style="padding:8px 0;font-size:14px;font-weight:600;color:#0f172a;">
                                  {HtmlEncode(paymentMethod)}
                                </td>
                              </tr>
                              <tr>
                                <td colspan="2"><hr style="border:none;border-top:1px solid #e2e8f0;margin:2px 0;" /></td>
                              </tr>
                              <tr>
                                <td style="padding:8px 0;font-size:15px;color:#475569;font-weight:600;">Total</td>
                                <td align="right" style="padding:8px 0;font-size:20px;font-weight:700;color:#4f46e5;">
                                  ${totalAmount}
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                      </table>

                      <!-- CTA -->
                      <table width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td align="center" style="padding-bottom:24px;">
                            <a href="{orderUrl}"
                               style="display:inline-block;background:linear-gradient(135deg,#4f46e5,#6366f1);color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;padding:14px 36px;border-radius:8px;">
                              Track Order
                            </a>
                          </td>
                        </tr>
                      </table>

                      <p style="margin:0;font-size:13px;color:#94a3b8;line-height:1.6;">
                        If you have any questions, please visit our <a href="{_settings.FrontendBaseUrl}/support" style="color:#4f46e5;">help center</a>.
                      </p>

                    </td>
                  </tr>

                  <!-- Footer -->
                  <tr>
                    <td style="background-color:#f8fafc;border-radius:0 0 12px 12px;padding:20px 40px;text-align:center;border-top:1px solid #e2e8f0;">
                      <p style="margin:0;font-size:12px;color:#94a3b8;">
                        Commerce Inc. &middot; 123 Store Street &middot; Cairo, EG
                      </p>
                    </td>
                  </tr>

                </table>
              </td>
            </tr>
          </table>

        </body>
        </html>
        """;

        return (subject, html);
    }

    // ── Items Table Helper ──────────────────────────────────────────────────

    private static string BuildItemsTableHtml(List<OrderLineItemData> items)
    {
        if (items.Count == 0)
            return string.Empty;

        var rows = new StringBuilder();

        foreach (var item in items)
        {
            var productName = HtmlEncode(item.ProductName);

            var imageCell = !string.IsNullOrWhiteSpace(item.ImageUrl)
                ? $"""
                   <img src="{HtmlEncode(item.ImageUrl)}" alt="{productName}"
                        width="60" height="60"
                        style="width:60px;height:60px;border-radius:8px;object-fit:cover;display:block;border:0;outline:none;"/>
                   """
                : """
                   <table role="presentation" width="60" height="60" cellpadding="0" cellspacing="0"
                          style="width:60px;height:60px;background-color:#f1f5f9;border-radius:8px;border-collapse:collapse;">
                     <tr>
                       <td align="center" valign="middle" style="font-size:24px;color:#94a3b8;">📦</td>
                     </tr>
                   </table>
                   """;

            rows.Append($"""
                         <tr>
                           <td style="padding:12px 0;vertical-align:middle;width:72px;">
                             {imageCell}
                           </td>
                           <td style="padding:12px 12px;vertical-align:middle;">
                             <p style="margin:0 0 4px;font-size:14px;font-weight:600;color:#0f172a;line-height:1.4;">
                               {productName}
                             </p>
                             <p style="margin:0;font-size:13px;color:#64748b;">
                               ${item.UnitPrice:F2} &times; {item.Quantity}
                             </p>
                           </td>
                           <td align="right" style="padding:12px 0;vertical-align:middle;white-space:nowrap;">
                             <p style="margin:0;font-size:14px;font-weight:700;color:#0f172a;">
                               ${item.LineTotal:F2}
                             </p>
                           </td>
                         </tr>
                         <tr>
                           <td colspan="3"><hr style="border:none;border-top:1px solid #f1f5f9;margin:0;" /></td>
                         </tr>
                         """);
        }

        return $"""
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="border:1px solid #e2e8f0;border-radius:10px;margin:0 0 28px;border-collapse:collapse;">
                  <tr>
                    <td colspan="3" style="padding:14px 20px;background-color:#f8fafc;border-radius:10px 10px 0 0;border-bottom:1px solid #e2e8f0;">
                      <p style="margin:0;font-size:11px;font-weight:700;color:#64748b;letter-spacing:0.08em;text-transform:uppercase;">
                        Items Ordered
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td colspan="3" style="padding:0 20px;">
                      <table width="100%" cellpadding="0" cellspacing="0">
                        {rows}
                      </table>
                    </td>
                  </tr>
                </table>
                """;
    }

    private static string HtmlEncode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);

    // ── Password Reset ────────────────────────────────────────────────────────

    private (string Subject, string HtmlBody) RenderPasswordReset(
        Dictionary<string, string> data)
    {
        var resetUrl = data.GetValueOrDefault("ResetUrl", "#");
        var expiresIn = data.GetValueOrDefault("ExpiresIn", "1 hour");
        var subject = "Reset Your Password";

        var html = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>Reset Your Password</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">

          <!-- Preview Text -->
          <div style="display:none;max-height:0;overflow:hidden;">
            Reset your Commerce password — link expires in {expiresIn}
          </div>

          <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f5;padding:40px 0;">
            <tr>
              <td align="center">
                <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;">

                  <!-- Header -->
                  <tr>
                    <td style="background:linear-gradient(135deg,#4f46e5,#6366f1);border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                      <h1 style="margin:0;font-size:28px;font-weight:700;color:#ffffff;letter-spacing:-0.5px;">
                        Commerce
                      </h1>
                    </td>
                  </tr>

                  <!-- Body -->
                  <tr>
                    <td style="background-color:#ffffff;padding:40px;">

                      <!-- Padlock icon (inline SVG) -->
                      <div style="text-align:center;margin-bottom:24px;">
                        <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="#4f46e5" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                          <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                          <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                        </svg>
                      </div>

                      <h2 style="margin:0 0 12px;font-size:22px;font-weight:700;color:#0f172a;text-align:center;">
                        Reset Your Password
                      </h2>

                      <p style="margin:0 0 32px;font-size:15px;color:#475569;text-align:center;line-height:1.6;">
                        We received a request to reset your password. Click the button below to set a new one.
                        This link is valid for <strong>{expiresIn}</strong>.
                      </p>

                      <!-- CTA -->
                      <table width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td align="center" style="padding-bottom:32px;">
                            <a href="{resetUrl}"
                               style="display:inline-block;background:linear-gradient(135deg,#4f46e5,#6366f1);color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;padding:14px 36px;border-radius:8px;">
                              Reset Password
                            </a>
                          </td>
                        </tr>
                      </table>

                      <!-- Security Note -->
                      <table width="100%" cellpadding="0" cellspacing="0"
                             style="background-color:#fef9ec;border:1px solid #fde68a;border-radius:8px;margin-bottom:24px;">
                        <tr>
                          <td style="padding:16px 20px;">
                            <p style="margin:0;font-size:13px;color:#92400e;line-height:1.5;">
                              <strong>Didn't request this?</strong>
                              You can safely ignore this email. Your password will not change unless you click the button above.
                            </p>
                          </td>
                        </tr>
                      </table>

                      <!-- Fallback URL -->
                      <p style="margin:0;font-size:12px;color:#94a3b8;line-height:1.6;text-align:center;">
                        If the button doesn't work, copy and paste this URL into your browser:
                        <br />
                        <span style="color:#64748b;word-break:break-all;font-size:13px;">{resetUrl}</span>
                      </p>

                    </td>
                  </tr>

                  <!-- Footer -->
                  <tr>
                    <td style="background-color:#f8fafc;border-radius:0 0 12px 12px;padding:24px 40px;text-align:center;border-top:1px solid #e2e8f0;">
                      <p style="margin:0;font-size:12px;color:#94a3b8;">
                        For security, this link expires in {expiresIn} and can only be used once.
                      </p>
                    </td>
                  </tr>

                </table>
              </td>
            </tr>
          </table>

        </body>
        </html>
        """;

        return (subject, html);
    }
}