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

    // Commerce.Application/Services/Email/Templates/EmailTemplateRenderer.cs
    // Replace RenderOrderConfirmation — everything else stays the same

    private (string Subject, string HtmlBody) RenderOrderConfirmation(
        Dictionary<string, string> data)
    {
        var orderNumber = data.GetValueOrDefault("OrderNumber", "N/A");
        var totalAmount = data.GetValueOrDefault("TotalAmount", "0.00");
        var customerName = data.GetValueOrDefault("CustomerName", "Valued Customer");
        var orderId = data.GetValueOrDefault("OrderId", "");
        var paymentMethod = data.GetValueOrDefault("PaymentMethod", "Card");
        var paymentStatus = data.GetValueOrDefault("PaymentStatus", "Paid");
        var paymentStatusBackground = paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)
            ? "#dcfce7"
            : "#fef3c7";
        var paymentStatusColor = paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)
            ? "#16a34a"
            : "#92400e";
        var orderUrl = $"{_settings.FrontendBaseUrl}/orders/{orderId}";
        var subject = $"Order Confirmed – {orderNumber}";

        // Deserialize items — gracefully fall back to empty list if key is missing
        List<OrderLineItemData> items = [];
        
        if (data.TryGetValue("Items", out var itemsJson))
            items = JsonSerializer.Deserialize<List<OrderLineItemData>>(itemsJson) ?? [];


        var itemsHtml = BuildItemsTableHtml(items);

        var html = $"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                      <meta charset="UTF-8"/>
                      <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
                      <title>Order Confirmed</title>
                    </head>
                    <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">

                      <div style="display:none;max-height:0;overflow:hidden;">
                        Your order {orderNumber} has been confirmed. Total: ${totalAmount}
                      </div>

                      <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f5;padding:40px 0;">
                        <tr>
                          <td align="center">
                            <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;">

                              <!-- Header -->
                              <tr>
                                <td style="background-color:#1a1a2e;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                                  <h1 style="margin:0;font-size:28px;font-weight:700;color:#ffffff;letter-spacing:-0.5px;">
                                    🛍️ Commerce
                                  </h1>
                                  <p style="margin:8px 0 0;font-size:14px;color:#a0a0b8;">
                                    Your order is confirmed
                                  </p>
                                </td>
                              </tr>

                              <!-- Success banner -->
                              <tr>
                                <td style="background-color:#16a34a;padding:16px 40px;text-align:center;">
                                  <p style="margin:0;font-size:15px;font-weight:600;color:#ffffff;">
                                    ✅ &nbsp; We are processing your order
                                  </p>
                                </td>
                              </tr>

                              <!-- Body -->
                              <tr>
                                <td style="background-color:#ffffff;padding:36px 40px;">

                                  <p style="margin:0 0 28px;font-size:16px;color:#374151;">
                                    Hi <strong>{HtmlEncode(customerName)}</strong>, thank you for your purchase!
                                  </p>

                                  <!-- ── Items table ─────────────────────────────────── -->
                                  {itemsHtml}

                                  <!-- ── Order summary card ─────────────────────────── -->
                                  <table width="100%" cellpadding="0" cellspacing="0"
                                         style="background-color:#f9fafb;border:1px solid #e5e7eb;
                                                border-radius:8px;margin-bottom:28px;">
                                    <tr>
                                      <td style="padding:20px 24px;">
                                        <p style="margin:0 0 14px;font-size:11px;font-weight:700;color:#6b7280;
                                                  letter-spacing:0.08em;text-transform:uppercase;">
                                          Order Summary
                                        </p>
                                        <table width="100%" cellpadding="0" cellspacing="0">
                                          <tr>
                                            <td style="padding:7px 0;font-size:14px;color:#374151;">Order number</td>
                                            <td align="right" style="padding:7px 0;font-size:14px;font-weight:600;color:#111827;">
                                              {orderNumber}
                                            </td>
                                          </tr>
                                          <tr>
                                            <td colspan="2"><hr style="border:none;border-top:1px solid #e5e7eb;margin:2px 0;"/></td>
                                          </tr>
                                          <tr>
                                            <td style="padding:7px 0;font-size:14px;color:#374151;">Payment method</td>
                                            <td align="right" style="padding:7px 0;font-size:14px;font-weight:600;color:#111827;">
                                              {HtmlEncode(paymentMethod)}
                                            </td>
                                          </tr>
                                          <tr>
                                            <td colspan="2"><hr style="border:none;border-top:1px solid #e5e7eb;margin:2px 0;"/></td>
                                          </tr>
                                          <tr>
                                            <td style="padding:7px 0;font-size:15px;color:#374151;font-weight:600;">Order total</td>
                                            <td align="right" style="padding:7px 0;font-size:18px;font-weight:700;color:#1a1a2e;">
                                              ${totalAmount}
                                            </td>
                                          </tr>
                                          <tr>
                                            <td colspan="2"><hr style="border:none;border-top:1px solid #e5e7eb;margin:2px 0;"/></td>
                                          </tr>
                                          <tr>
                                            <td style="padding:7px 0;font-size:14px;color:#374151;">Payment status</td>
                                            <td align="right" style="padding:7px 0;">
                                              <span style="display:inline-block;background-color:{paymentStatusBackground};color:{paymentStatusColor};
                                                           font-size:12px;font-weight:700;padding:4px 12px;border-radius:9999px;">
                                                {HtmlEncode(paymentStatus)}
                                              </span>
                                            </td>
                                          </tr>
                                        </table>
                                      </td>
                                    </tr>
                                  </table>

                                  <!-- CTA -->
                                  <table width="100%" cellpadding="0" cellspacing="0">
                                    <tr>
                                      <td align="center" style="padding-bottom:28px;">
                                        <a href="{orderUrl}"
                                           style="display:inline-block;background-color:#1a1a2e;color:#ffffff;
                                                  font-size:15px;font-weight:600;text-decoration:none;
                                                  padding:14px 36px;border-radius:8px;">
                                          View Order Details →
                                        </a>
                                      </td>
                                    </tr>
                                  </table>

                                  <p style="margin:0;font-size:13px;color:#6b7280;line-height:1.6;">
                                    Questions?
                                    Visit our <a href="{_settings.FrontendBaseUrl}/support"
                                                 style="color:#1a1a2e;">support center</a>.
                                  </p>

                                </td>
                              </tr>

                              <!-- Footer -->
                              <tr>
                                <td style="background-color:#f9fafb;border-radius:0 0 12px 12px;
                                           padding:20px 40px;text-align:center;border-top:1px solid #e5e7eb;">
                                  <p style="margin:0;font-size:12px;color:#9ca3af;">
                                    Commerce Inc. · 123 Store Street · Cairo, EG
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

    /// <summary>
    /// Builds the items section. Each row shows the product image (or a
    /// placeholder), name, unit price × quantity, and line total.
    /// Uses tables throughout — Outlook does not support flexbox or grid.
    /// </summary>
    private static string BuildItemsTableHtml(List<OrderLineItemData> items)
    {
        if (items.Count == 0)
            return string.Empty;

        var rows = new StringBuilder();

        foreach (var item in items)
        {
            var productName = HtmlEncode(item.ProductName);

            // Placeholder shown when no image URL is stored
            var imageCell = !string.IsNullOrWhiteSpace(item.ImageUrl)
                ? $"""
                   <img src="{HtmlEncode(item.ImageUrl)}" alt="{productName}"
                        width="64" height="64"
                        style="width:64px;height:64px;border-radius:6px;
                               display:block;border:0;outline:none;text-decoration:none;"/>
                   """
                : """
                  <table role="presentation" width="64" height="64" cellpadding="0" cellspacing="0"
                         style="width:64px;height:64px;background-color:#f3f4f6;border-radius:6px;border-collapse:collapse;">
                    <tr>
                      <td width="64" height="64" align="center" valign="middle"
                          style="width:64px;height:64px;font-size:24px;line-height:64px;text-align:center;">
                        🛍️
                      </td>
                    </tr>
                  </table>
                  """;

            rows.Append($"""
                         <tr>
                           <!-- Image -->
                           <td style="padding:14px 0;vertical-align:top;width:80px;">
                             {imageCell}
                           </td>

                           <!-- Name + unit price -->
                           <td style="padding:14px 12px;vertical-align:top;">
                             <p style="margin:0 0 4px;font-size:14px;font-weight:600;color:#111827;
                                       line-height:1.4;">
                               {productName}
                             </p>
                             <p style="margin:0;font-size:13px;color:#6b7280;">
                               ${item.UnitPrice:F2} &times; {item.Quantity}
                             </p>
                           </td>

                           <!-- Line total -->
                           <td align="right" style="padding:14px 0;vertical-align:top;white-space:nowrap;">
                             <p style="margin:0;font-size:14px;font-weight:700;color:#111827;">
                               ${item.LineTotal:F2}
                             </p>
                           </td>
                         </tr>

                         <!-- Divider -->
                         <tr>
                           <td colspan="3">
                             <hr style="border:none;border-top:1px solid #f3f4f6;margin:0;"/>
                           </td>
                         </tr>
                         """);
        }

        return $"""
                <table width="100%" cellpadding="0" cellspacing="0"
                       style="border:1px solid #e5e7eb;border-radius:8px;
                              margin-bottom:24px;border-collapse:collapse;">
                  <tr>
                    <td colspan="3" style="padding:14px 16px 10px;background-color:#f9fafb;
                                           border-radius:8px 8px 0 0;border-bottom:1px solid #e5e7eb;">
                      <p style="margin:0;font-size:11px;font-weight:700;color:#6b7280;
                                letter-spacing:0.08em;text-transform:uppercase;">
                        Items Ordered
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td colspan="3" style="padding:0 16px;">
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
        var subject = "Reset your password";

        var html = $"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                      <meta charset="UTF-8"/>
                      <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
                      <title>Reset your password</title>
                    </head>
                    <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">

                      <div style="display:none;max-height:0;overflow:hidden;">
                        Reset your Commerce password — link expires in {expiresIn}
                      </div>

                      <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f5;padding:40px 0;">
                        <tr>
                          <td align="center">
                            <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;">

                              <!-- Header -->
                              <tr>
                                <td style="background-color:#1a1a2e;border-radius:12px 12px 0 0;padding:32px 40px;text-align:center;">
                                  <h1 style="margin:0;font-size:28px;font-weight:700;color:#ffffff;letter-spacing:-0.5px;">
                                    🛍️ Commerce
                                  </h1>
                                </td>
                              </tr>

                              <!-- Body -->
                              <tr>
                                <td style="background-color:#ffffff;padding:40px;">
                                  
                                  <!-- Lock icon -->
                                  <div style="text-align:center;margin-bottom:24px;">
                                    <div style="display:inline-block;background-color:#fef3c7;border-radius:50%;
                                                width:64px;height:64px;line-height:64px;font-size:32px;">
                                      🔒
                                    </div>
                                  </div>

                                  <h2 style="margin:0 0 12px;font-size:22px;font-weight:700;
                                             color:#111827;text-align:center;">
                                    Reset your password
                                  </h2>

                                  <p style="margin:0 0 32px;font-size:15px;color:#374151;
                                            text-align:center;line-height:1.6;">
                                    We received a request to reset your password.
                                    Click the button below — this link expires in
                                    <strong>{expiresIn}</strong>.
                                  </p>

                                  <!-- CTA button -->
                                  <table width="100%" cellpadding="0" cellspacing="0">
                                    <tr>
                                      <td align="center" style="padding-bottom:32px;">
                                        <a href="{resetUrl}"
                                           style="display:inline-block;background-color:#dc2626;color:#ffffff;
                                                  font-size:15px;font-weight:600;text-decoration:none;
                                                  padding:14px 36px;border-radius:8px;">
                                          Reset Password
                                        </a>
                                      </td>
                                    </tr>
                                  </table>

                                  <!-- Security note -->
                                  <table width="100%" cellpadding="0" cellspacing="0"
                                         style="background-color:#fef9ec;border:1px solid #fde68a;
                                                border-radius:8px;margin-bottom:24px;">
                                    <tr>
                                      <td style="padding:16px 20px;">
                                        <p style="margin:0;font-size:13px;color:#92400e;line-height:1.5;">
                                          ⚠️ &nbsp;<strong>Didn't request this?</strong>
                                          You can safely ignore this email.
                                          Your password will <em>not</em> change unless you click the button above.
                                        </p>
                                      </td>
                                    </tr>
                                  </table>

                                  <!-- Fallback URL -->
                                  <p style="margin:0;font-size:12px;color:#9ca3af;line-height:1.6;">
                                    If the button doesn't work, copy and paste this URL into your browser:
                                    <br/>
                                    <span style="color:#6b7280;word-break:break-all;">{resetUrl}</span>
                                  </p>

                                </td>
                              </tr>

                              <!-- Footer -->
                              <tr>
                                <td style="background-color:#f9fafb;border-radius:0 0 12px 12px;
                                           padding:24px 40px;text-align:center;
                                           border-top:1px solid #e5e7eb;">
                                  <p style="margin:0;font-size:12px;color:#d1d5db;">
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
