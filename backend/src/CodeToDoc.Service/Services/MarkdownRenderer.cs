using System.Text;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Deterministic Markdown renderer. It renders document structure from the
/// validated structured skeleton payload and the validated LLM narrative response.
/// </summary>
public static class MarkdownRenderer
{
    public static string RenderTechnical(SynthesisPayload payload, TechnicalModuleResponse response, bool partial)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# مشخصات فنی (Technical Specification)");
        builder.AppendLine();

        builder.AppendLine("## ۱. معماری سیستم");
        foreach (var statement in response.Architecture)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        var frameworks = payload.Projects.Select(p => p.Framework).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (frameworks.Count > 0)
        {
            builder.AppendLine($"- تفکیک پروژه‌ها: {Escape(string.Join(" + ", frameworks))}.");
            foreach (var proj in payload.Projects)
            {
                var root = string.IsNullOrWhiteSpace(proj.Root) ? "ریشه" : Escape(proj.Root);
                builder.AppendLine($"- پروژه `{root}`: {Escape(proj.Framework)}.");
            }
        }

        foreach (var statement in response.Overview)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        if (response.Overview.Count == 0 && response.Architecture.Count == 0)
        {
            builder.AppendLine("- معماری از روی شواهد شناسایی شد؛ جزئیات تکمیلی در بخش‌های بعد آمده است.");
        }

        builder.AppendLine();
        builder.AppendLine("## ۲. تکنولوژی‌ها");
        foreach (var statement in response.Technologies)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Summary))
        {
            builder.AppendLine($"- {Escape(payload.Summary)}");
        }

        if (payload.Dependencies.Count > 0)
        {
            builder.AppendLine($"- وابستگی‌های کلیدی: {Escape(string.Join("، ", payload.Dependencies.Take(30)))}.");
        }

        if (payload.Entities.Count > 0)
        {
            builder.AppendLine("- مدل‌های داده (Entities):");
            foreach (var entity in payload.Entities.Take(50))
            {
                var props = entity.Properties.Count > 0
                    ? string.Join("، ", entity.Properties.Select(p => $"`{p.Name}` ({p.Type})"))
                    : "بدون فیلد ثبت‌شده";
                builder.AppendLine($"  - `{Escape(entity.Name)}`: {props}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## ۳. احراز هویت و دسترسی");
        foreach (var statement in response.Authentication)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        var authEndpoints = payload.Endpoints.Where(e => e.AuthRequired).ToList();
        foreach (var endpoint in authEndpoints)
        {
            var roles = endpoint.Roles.Count > 0 ? $" (نقش‌ها: {string.Join("، ", endpoint.Roles)})" : string.Empty;
            builder.AppendLine($"- {endpoint.Method} {endpoint.Path}: نیازمند احراز هویت{roles}.");
        }

        if (response.Authentication.Count == 0 && authEndpoints.Count == 0)
        {
            builder.AppendLine("- شواهدی از الزام احراز هویت شناسایی نشد (در صورت وجود Guards، در جدول API مشخص شده است).");
        }

        builder.AppendLine();
        builder.AppendLine("## ۴. پیش‌نیازهای نصب و اجرا");
        foreach (var statement in response.Prerequisites)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        if (response.Prerequisites.Count == 0)
        {
            builder.AppendLine("- نودجی‌اس ۲۰ و مدیر بسته (npm/pnpm)؛ پایگاه‌داده PostgreSQL؛ متغیرهای محیطی `DATABASE_URL`، `JWT_SECRET` و `API_URL` را تنظیم کنید.");
            builder.AppendLine("- نصب وابستگی‌ها در ریشه فرانت‌اند و پوشه `api`؛ سپس اجرای سرویس API و فرانت‌اند به‌صورت جداگانه.");
        }

        builder.AppendLine();
        builder.AppendLine("## ۵. مرجع کامل REST API");
        if (payload.Endpoints.Count == 0)
        {
            builder.AppendLine("- در این نسخه اندپوینتی استخراج نشد؛ اما وابستگی‌ها و پروژه‌های شناسایی‌شده در بخش‌های بالا آمده است.");
        }
        else
        {
            builder.AppendLine("| Method | Path | Controller | Auth | شرح |");
            builder.AppendLine("|---|---|---|---|---|");
            var byId = response.Endpoints.ToDictionary(e => e.EndpointId, StringComparer.OrdinalIgnoreCase);
            foreach (var endpoint in payload.Endpoints)
            {
                var key = payload.Endpoints.IndexOf(endpoint);
                var id = $"ep_{key}";
                byId.TryGetValue(id, out var section);
                var desc = section?.Summary ?? $"{endpoint.Method} {endpoint.Path}";
                var auth = endpoint.AuthRequired ? "دارد" : "—";
                builder.AppendLine($"| {endpoint.Method} | `{endpoint.Path}` | {Escape(endpoint.ControllerName ?? "—")} | {auth} | {Escape(desc)} |");
            }

            builder.AppendLine();
            foreach (var endpoint in payload.Endpoints)
            {
                builder.AppendLine($"### {endpoint.Method} {endpoint.Path}");
                if (!string.IsNullOrWhiteSpace(endpoint.ControllerName))
                {
                    builder.AppendLine($"- کنترلر: `{endpoint.ControllerName}`" + (string.IsNullOrWhiteSpace(endpoint.ActionName) ? string.Empty : $" / اکشن `{endpoint.ActionName}`"));
                }

                if (endpoint.AuthRequired)
                {
                    var roles = endpoint.Roles.Count > 0 ? $" ({string.Join(", ", endpoint.Roles)})" : string.Empty;
                    builder.AppendLine($"- احراز هویت: لازم است{roles}");
                }

                if (endpoint.RequestFields.Count > 0)
                {
                    builder.AppendLine("- فیلدهای درخواست:");
                    foreach (var field in endpoint.RequestFields)
                    {
                        builder.AppendLine($"  - `{field.Name}` ({field.Type}{(field.IsRequired ? "، الزامی" : string.Empty)})");
                    }
                    builder.AppendLine("- نمونه درخواست:");
                    builder.AppendLine("```json");
                    builder.AppendLine(BuildSampleJson(endpoint.RequestFields));
                    builder.AppendLine("```");
                }

                if (endpoint.ResponseFields.Count > 0)
                {
                    builder.AppendLine("- فیلدهای پاسخ:");
                    foreach (var field in endpoint.ResponseFields)
                    {
                        builder.AppendLine($"  - `{field.Name}` ({field.Type})");
                    }
                    builder.AppendLine("- نمونه پاسخ:");
                    builder.AppendLine("```json");
                    builder.AppendLine(BuildSampleJson(endpoint.ResponseFields));
                    builder.AppendLine("```");
                }

                if (!string.IsNullOrWhiteSpace(endpoint.PersistenceKind))
                {
                    builder.AppendLine($"- ماندگاری: {Escape(endpoint.PersistenceKind)}.");
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("## ۶. محدودیت‌های تحلیل");
        foreach (var limitation in response.Limitations)
        {
            builder.AppendLine($"- {Escape(limitation)}");
        }

        if (partial)
        {
            builder.AppendLine("- این سند به‌صورت جزئی تولید شده است؛ برخی رفتارها از روی تحلیل استاتیک قابل تشخیص نبود.");
        }

        return builder.ToString().TrimEnd() + "\n";
    }

    private static string BuildSampleJson(List<Models.PayloadField> fields)
    {
        var parts = fields.Take(10).Select(f => $"  \"{f.Name}\": \"<{f.Type ?? "string"}>\"");
        return "{\n" + string.Join(",\n", parts) + "\n}";
    }

    public static string RenderUserGuide(SynthesisPayload payload, UserGuideModuleResponse response, bool partial)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# راهنمای کاربر");
        builder.AppendLine();

        builder.AppendLine("## معرفی");
        foreach (var statement in response.Overview)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        builder.AppendLine();
        builder.AppendLine("## فهرست بخش‌ها");
        foreach (var page in response.Pages)
        {
            builder.AppendLine($"- {Escape(page.DisplayTitle)}");
        }

        foreach (var page in response.Pages)
        {
            builder.AppendLine();
            builder.AppendLine($"## {Escape(page.DisplayTitle)}");

            if (page.Introduction.Count > 0)
            {
                foreach (var intro in page.Introduction)
                {
                    builder.AppendLine();
                    builder.AppendLine(Escape(intro.Text));
                }
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine($"این بخش مربوط به «{Escape(page.DisplayTitle)}» است. در ادامه مراحل کار و نتیجه هر اقدام توضیح داده شده است.");
            }

            foreach (var form in page.Forms)
            {
                if (form.Steps.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("### مراحل انجام کار");
                    foreach (var step in form.Steps.OrderBy(s => s.Order))
                    {
                        builder.AppendLine($"{step.Order}. {Escape(step.Text)}");
                    }
                }

                if (form.Outcomes.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("### نتیجه");
                    foreach (var outcome in form.Outcomes)
                    {
                        builder.AppendLine($"- {Escape(outcome.Text)}");
                    }
                }
            }

            foreach (var action in page.Actions)
            {
                if (!string.IsNullOrWhiteSpace(action.Outcome))
                {
                    builder.AppendLine($"- دکمه «{Escape(action.Label)}»: {Escape(action.Outcome)}");
                }
                else
                {
                    builder.AppendLine($"- دکمه «{Escape(action.Label)}»");
                }
            }

            foreach (var table in page.Tables)
            {
                if (table.Columns.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("### جدول‌ها");
                    builder.AppendLine("- ستون‌ها: " + string.Join("، ", table.Columns.Select(Escape)));
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## نکات و محدودیت‌ها");
        foreach (var limitation in response.Limitations)
        {
            builder.AppendLine($"- {Escape(limitation)}");
        }

        if (partial)
        {
            builder.AppendLine("- این راهنما ناقص است؛ برخی رفتارها قابل تشخیص نبود.");
        }

        return builder.ToString().TrimEnd() + "\n";
    }

    private static string Escape(string? text) => (text ?? string.Empty).Replace("|", "\\|");
}
