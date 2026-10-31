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
        builder.AppendLine("# Technical Specification");
        builder.AppendLine();

        builder.AppendLine("## 1. System Overview");
        foreach (var statement in response.Overview)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        if (response.Overview.Count == 0)
        {
            builder.AppendLine("- Not detected.");
        }

        builder.AppendLine();
        builder.AppendLine("## 2. Technology Stack");
        foreach (var statement in response.Technologies)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Summary))
        {
            builder.AppendLine($"- {Escape(payload.Summary)}");
        }

        builder.AppendLine();
        builder.AppendLine("## 3. Authentication and Authorization");
        var authEndpoints = payload.Endpoints.Where(e => e.AuthRequired).ToList();
        foreach (var statement in response.Authentication)
        {
            builder.AppendLine($"- {Escape(statement.Text)}");
        }

        foreach (var endpoint in authEndpoints)
        {
            var roles = endpoint.Roles.Count > 0 ? $" (roles: {string.Join(", ", endpoint.Roles)})" : string.Empty;
            builder.AppendLine($"- {endpoint.Method} {endpoint.Path}: authentication required{roles}.");
        }

        if (response.Authentication.Count == 0 && authEndpoints.Count == 0)
        {
            builder.AppendLine("- No authentication requirements were detected.");
        }

        builder.AppendLine();
        builder.AppendLine("## 4. API Reference");
        foreach (var endpoint in payload.Endpoints)
        {
            builder.AppendLine($"### {endpoint.Method} {endpoint.Path}");
            if (!string.IsNullOrWhiteSpace(endpoint.ControllerName))
            {
                builder.AppendLine($"- Controller: `{endpoint.ControllerName}`");
            }

            if (endpoint.AuthRequired)
            {
                var roles = endpoint.Roles.Count > 0 ? $" ({string.Join(", ", endpoint.Roles)})" : string.Empty;
                builder.AppendLine($"- Authentication: required{roles}");
            }

            if (endpoint.RequestFields.Count > 0)
            {
                builder.AppendLine("- Request fields:");
                foreach (var field in endpoint.RequestFields)
                {
                    builder.AppendLine($"  - `{field.Name}` ({field.Type}{(field.IsRequired ? ", required" : string.Empty)})");
                }
            }

            if (endpoint.ResponseFields.Count > 0)
            {
                builder.AppendLine("- Response fields:");
                foreach (var field in endpoint.ResponseFields)
                {
                    builder.AppendLine($"  - `{field.Name}` ({field.Type})");
                }
            }

            if (!string.IsNullOrWhiteSpace(endpoint.PersistenceKind))
            {
                var persistence = endpoint.PersistenceKind == "evidence"
                    ? $"Persistence evidence: {endpoint.PersistenceOperation} {endpoint.PersistenceEntity}."
                    : $"Persistence (inferred): {endpoint.PersistenceOperation ?? "unknown"}.";
                builder.AppendLine($"- {persistence}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## 5. Analysis Limitations");
        foreach (var limitation in response.Limitations)
        {
            builder.AppendLine($"- {Escape(limitation)}");
        }

        if (partial)
        {
            builder.AppendLine("- This document is partial because static analysis was incomplete.");
        }

        return builder.ToString().TrimEnd() + "\n";
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
