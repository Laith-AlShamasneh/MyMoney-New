using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Shared.Enums.System;

namespace Infrastructure.Services.Email;

internal sealed class EmailTemplateService(
    IWebHostEnvironment environment,
    IMemoryCache        cache) : IEmailTemplateService
{
    private const string CacheKey = "email-templates";

    public async Task<(string Subject, string HtmlBody)> RenderAsync(
        string                     templateKey,
        SystemLanguages            language,
        Dictionary<string, string> placeholders,
        CancellationToken          ct = default)
    {
        var templates = await LoadTemplatesAsync(ct);

        if (!templates.TryGetValue(templateKey, out var template))
            throw new InvalidOperationException($"Email template '{templateKey}' not found.");

        var isArabic = language == SystemLanguages.Arabic;

        var subject = isArabic ? template.SubjectAr : template.SubjectEn;
        var body    = isArabic ? template.BodyAr    : template.BodyEn;

        subject = ApplyPlaceholders(subject, placeholders);
        body    = ApplyPlaceholders(body,    placeholders);

        var htmlBody = WrapInBaseLayout(body, isArabic);

        return (subject, htmlBody);
    }

    private async Task<Dictionary<string, EmailTemplate>> LoadTemplatesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out Dictionary<string, EmailTemplate>? cached) && cached is not null)
            return cached;

        var path = Path.Combine(environment.WebRootPath, "resources", "email-templates.json");
        await using var stream   = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var templates = await JsonSerializer.DeserializeAsync<Dictionary<string, EmailTemplate>>(stream, cancellationToken: ct)
            ?? [];

        cache.Set(CacheKey, templates, TimeSpan.FromHours(12));
        return templates;
    }

    private static string ApplyPlaceholders(string template, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
            template = template.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return template;
    }

    private static string WrapInBaseLayout(string body, bool isArabic)
    {
        var dir    = isArabic ? "rtl" : "ltr";
        var lang   = isArabic ? "ar"  : "en";
        var align  = isArabic ? "right" : "left";
        var year   = DateTime.UtcNow.Year;
        var footer = isArabic
            ? $"&copy; {year} MyMoney &mdash; جميع الحقوق محفوظة"
            : $"&copy; {year} MyMoney &mdash; All rights reserved";

        return $$"""
            <!DOCTYPE html>
            <html lang="{{lang}}" dir="{{dir}}">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>MyMoney</title>
            </head>
            <body style="margin:0;padding:0;background:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:40px 16px;">
                <tr>
                  <td align="center">
                    <table width="100%" style="max-width:600px;" cellpadding="0" cellspacing="0">
                      <!-- Header -->
                      <tr>
                        <td style="background:#2563eb;border-radius:12px 12px 0 0;padding:24px 32px;text-align:center;">
                          <span style="color:#fff;font-size:22px;font-weight:700;letter-spacing:-0.5px;">MyMoney</span>
                        </td>
                      </tr>
                      <!-- Card body -->
                      <tr>
                        <td style="background:#fff;padding:32px;text-align:{{align}};border-left:1px solid #e5e7eb;border-right:1px solid #e5e7eb;">
                          {{body}}
                        </td>
                      </tr>
                      <!-- Footer -->
                      <tr>
                        <td style="background:#f9fafb;border-radius:0 0 12px 12px;border:1px solid #e5e7eb;border-top:none;padding:20px 32px;text-align:center;color:#9ca3af;font-size:13px;">
                          {{footer}}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private sealed record EmailTemplate(
        string SubjectEn,
        string SubjectAr,
        string BodyEn,
        string BodyAr);
}
