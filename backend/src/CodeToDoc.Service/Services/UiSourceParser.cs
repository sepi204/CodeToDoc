using System.Text;
using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Deterministic, regex-based parser for the supported static UI subset of
/// TSX/JSX/HTML files. It is intentionally not a full AST and never executes code.
/// </summary>
public sealed class UiSourceParser : IUiSourceParser
{
    private static readonly Regex ImportRegex = new(
        @"import\s+(.+?)\s+from\s+['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    private static readonly Regex FormRegex = new(
        @"<form\b([^>]*)>(.*?)</form\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex InputRegex = new(
        @"<input\b([^>]*?)/?>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TextareaRegex = new(
        @"<textarea\b([^>]*)>(.*?)</textarea\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SelectRegex = new(
        @"<select\b([^>]*)>(.*?)</select\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OptionRegex = new(
        @"<option\b([^>]*)>(.*?)</option\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ButtonRegex = new(
        @"<button\b([^>]*)>(.*?)</button\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex LabelRegex = new(
        @"<label\b([^>]*)>(.*?)</label\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex LinkRegex = new(
        @"<(a|Link)\b([^>]*)>(.*?)</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ThRegex = new(
        @"<th\b([^>]*)>(.*?)</th\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TableRegex = new(
        @"<table\b([^>]*)>(.*?)</table\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CaptionRegex = new(
        @"<caption\b[^>]*>(.*?)</caption\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex H1Regex = new(
        @"<h1\b[^>]*>(.*?)</h1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex FetchRegex = new(
        @"fetch\(\s*(""[^""]*""|'[^']*'|`[^`]*`|[^,)]+)\s*(?:,\s*\{([^}]*)\})?\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex RouterPushRegex = new(
        @"router\.push\(\s*(""[^""]*""|'[^']*'|`[^`]*`|[^)]+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex MethodRegex = new(
        @"method\s*:\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private int _elementCounter;
    private int _evidenceCounter;
    private string _repositoryId = string.Empty;
    private string _filePath = string.Empty;
    private string _fileHash = string.Empty;
    private Dictionary<string, List<UiValidationRule>> _schemaRules = new(StringComparer.OrdinalIgnoreCase);

    public ParsedUiSource Parse(SourceFile file, FrontendProject project)
    {
        _elementCounter = 0;
        _evidenceCounter = 0;
        _repositoryId = project.Id;
        _filePath = file.RelativePath.Replace('\\', '/');
        _fileHash = ShortHash(_filePath);

        var source = new ParsedUiSource();
        var content = file.Content;

        _schemaRules = BuildSchemaRules(content);

        source.Title = ExtractTitle(content);
        source.Imports.AddRange(ParseImports(content));
        source.Fetches.AddRange(ParseFetches(content));
        source.RouterPushes.AddRange(ParseRouterPushes(content));
        ParseForms(content, source);
        ParseStandaloneFields(content, source);
        ParseStandaloneButtons(content, source);
        ParseLinks(content, source);
        ParseTables(content, source);

        return source;
    }

    // ---- Evidence ----

    private string AddEvidence(ParsedUiSource source, EvidenceKind kind, int line) =>
        AddEvidence(source, kind, line, line);

    private string AddEvidence(ParsedUiSource source, EvidenceKind kind, int startLine, int endLine)
    {
        var id = $"ev_{_repositoryId}_{_fileHash}_{_evidenceCounter++}";
        source.Evidence.Add(new UiEvidence
        {
            Id = id,
            RepositoryId = _repositoryId,
            FilePath = _filePath,
            StartLine = startLine,
            EndLine = endLine,
            Kind = kind,
        });
        return id;
    }

    // ---- Imports ----

    private static List<ParsedImport> ParseImports(string content)
    {
        var imports = new List<ParsedImport>();
        foreach (Match match in ImportRegex.Matches(content))
        {
            var clause = match.Groups[1].Value.Trim();
            var modulePath = match.Groups[2].Value;
            string? defaultImport = null;
            var named = new List<string>();

            var namedMatch = Regex.Match(clause, @"\{([^}]*)\}");
            if (namedMatch.Success)
            {
                named = namedMatch.Groups[1].Value
                    .Split(',')
                    .Select(part => part.Trim().Split(' ', '\t')[0].Trim())
                    .Where(name => name.Length > 0)
                    .ToList();
                clause = clause.Replace(namedMatch.Value, string.Empty).Trim().TrimEnd(',').Trim();
            }

            var defaultMatch = Regex.Match(clause, @"^([\w$]+)$");
            if (defaultMatch.Success)
            {
                defaultImport = defaultMatch.Groups[1].Value;
            }

            imports.Add(new ParsedImport
            {
                ModulePath = modulePath,
                DefaultImport = defaultImport,
                NamedImports = named,
                Line = LineOf(content, match.Index),
            });
        }

        return imports;
    }

    // ---- Title ----

    private static string? ExtractTitle(string content)
    {
        var match = H1Regex.Match(content);
        if (!match.Success)
        {
            return null;
        }

        var text = StripTags(match.Groups[1].Value).Trim();
        return text.Length == 0 ? null : text;
    }

    // ---- Forms ----

    private void ParseForms(string content, ParsedUiSource source)
    {
        foreach (Match formMatch in FormRegex.Matches(content))
        {
            var attributes = ParseAttributes(formMatch.Groups[1].Value);
            var formBody = formMatch.Groups[2].Value;
            var formLine = LineOf(content, formMatch.Index);

            var form = new UiForm
            {
                Id = $"form_{Sanitize(attributes.GetValueOrDefault("name"))}",
                Name = attributes.GetValueOrDefault("name"),
                SubmitHandler = ExtractExpression(attributes.GetValueOrDefault("onSubmit")),
            };
            form.EvidenceIds.Add(AddEvidence(source, EvidenceKind.Form, formLine));

            var labels = ExtractLabels(formBody, content, formMatch.Index, source);
            var fields = ExtractFields(formBody, content, formMatch.Index, labels, source);

            foreach (var field in fields)
            {
                form.Fields.Add(field);
            }

            // Buttons inside the form
            foreach (Match buttonMatch in ButtonRegex.Matches(formBody))
            {
                var button = BuildButton(buttonMatch, attributes, form, source, formMatch.Index, content);
                if (button is not null)
                {
                    source.Actions.Add(button);
                    if (button.Kind == ActionKind.SubmitForm)
                    {
                        form.SubmitActionIds.Add(button.Id);
                        button.FormId = form.Id;
                    }
                }
            }

            source.Forms.Add(form);
        }
    }

    private List<(string? HtmlFor, string? AriaLabel, string Text)> ExtractLabels(
        string body, string fullContent, int bodyOffset, ParsedUiSource source)
    {
        var labels = new List<(string?, string?, string)>();
        foreach (Match labelMatch in LabelRegex.Matches(body))
        {
            var attributes = ParseAttributes(labelMatch.Groups[1].Value);
            var text = StripTags(labelMatch.Groups[2].Value).Trim();
            var htmlFor = attributes.GetValueOrDefault("htmlFor") ?? attributes.GetValueOrDefault("for");
            var ariaLabel = attributes.GetValueOrDefault("aria-label");
            labels.Add((htmlFor, ariaLabel, text));

            AddEvidence(source, EvidenceKind.Field, LineOf(fullContent, bodyOffset + labelMatch.Index));
        }

        return labels;
    }

    private List<UiField> ExtractFields(
        string body, string fullContent, int bodyOffset,
        List<(string? HtmlFor, string? AriaLabel, string Text)> labels,
        ParsedUiSource source)
    {
        var fields = new List<UiField>();

        foreach (Match inputMatch in InputRegex.Matches(body))
        {
            var field = BuildInputField(inputMatch.Groups[1].Value, inputMatch.Groups[2].Value, "input",
                labels, source, LineOf(fullContent, bodyOffset + inputMatch.Index));
            fields.Add(field);
        }

        foreach (Match textareaMatch in TextareaRegex.Matches(body))
        {
            var field = BuildInputField(textareaMatch.Groups[1].Value, textareaMatch.Groups[2].Value, "textarea",
                labels, source, LineOf(fullContent, bodyOffset + textareaMatch.Index));
            field.InputType = InputKind.Textarea;
            fields.Add(field);
        }

        foreach (Match selectMatch in SelectRegex.Matches(body))
        {
            var field = BuildInputField(selectMatch.Groups[1].Value, selectMatch.Groups[2].Value, "select",
                labels, source, LineOf(fullContent, bodyOffset + selectMatch.Index));
            field.InputType = InputKind.Select;
            field.Options = ExtractOptions(selectMatch.Groups[2].Value);
            fields.Add(field);
        }

        return fields;
    }

    private List<UiOption> ExtractOptions(string selectBody)
    {
        var options = new List<UiOption>();
        foreach (Match optionMatch in OptionRegex.Matches(selectBody))
        {
            var attributes = ParseAttributes(optionMatch.Groups[1].Value);
            options.Add(new UiOption
            {
                Value = attributes.GetValueOrDefault("value") ?? string.Empty,
                Label = StripTags(optionMatch.Groups[2].Value).Trim(),
            });
        }

        return options;
    }

    private UiField BuildInputField(
        string attributesText, string rawValue, string component,
        List<(string? HtmlFor, string? AriaLabel, string Text)> labels,
        ParsedUiSource source, int line)
    {
        var attributes = ParseAttributes(attributesText);

        var id = attributes.GetValueOrDefault("id");
        var name = attributes.GetValueOrDefault("name") ?? ExtractRhfName(attributesText);
        var placeholder = attributes.GetValueOrDefault("placeholder");
        var ariaLabel = attributes.GetValueOrDefault("aria-label");

        var required = attributes.ContainsKey("required")
            ? (attributes["required"] == "false" ? false : true)
            : (bool?)null;
        var disabled = attributes.ContainsKey("disabled")
            ? (attributes["disabled"] == "false" ? false : true)
            : (bool?)null;

        var field = new UiField
        {
            Id = $"field_{Sanitize(name)}",
            Name = name,
            Component = component,
            InputType = MapInputType(attributes.GetValueOrDefault("type"), component),
            Label = ResolveLabel(id, name, ariaLabel, labels),
            Placeholder = placeholder,
            Required = required,
            Visibility = BuildVisibility(attributes, disabled),
        };

        field.EvidenceIds.Add(AddEvidence(source, EvidenceKind.Field, line));

        if (required == true)
        {
            field.ValidationRules.Add(new UiValidationRule { Kind = "required", Value = true, EvidenceIds = { field.EvidenceIds.Last() } });
        }

        AddNativeRules(field, attributes);
        AddSchemaRules(field, name);
        return field;
    }

    private void AddSchemaRules(UiField field, string? name)
    {
        if (name is null || !_schemaRules.TryGetValue(name, out var schemaRules))
        {
            return;
        }

        foreach (var rule in schemaRules)
        {
            field.ValidationRules.Add(rule);

            if (rule.Kind == "required" || (rule.Kind == "minLength" && rule.Value is int minValue && minValue >= 1))
            {
                field.Required = true;
            }
        }
    }

    private static void AddNativeRules(UiField field, Dictionary<string, string?> attributes)
    {
        if (attributes.TryGetValue("minLength", out var minLength) && int.TryParse(minLength, out var minValue))
        {
            field.ValidationRules.Add(new UiValidationRule { Kind = "minLength", Value = minValue });
        }

        if (attributes.TryGetValue("maxLength", out var maxLength) && int.TryParse(maxLength, out var maxValue))
        {
            field.ValidationRules.Add(new UiValidationRule { Kind = "maxLength", Value = maxValue });
        }

        if (attributes.TryGetValue("pattern", out var pattern) && pattern is not null)
        {
            field.ValidationRules.Add(new UiValidationRule { Kind = "pattern", Value = pattern });
        }
    }

    private static Dictionary<string, List<UiValidationRule>> BuildSchemaRules(string content)
    {
        var rules = new Dictionary<string, List<UiValidationRule>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, fieldRules) in ParseZodRules(content))
        {
            rules[name] = fieldRules;
        }

        foreach (var (name, fieldRules) in ParseRhfRules(content))
        {
            if (rules.TryGetValue(name, out var existing))
            {
                existing.AddRange(fieldRules);
            }
            else
            {
                rules[name] = fieldRules;
            }
        }

        return rules;
    }

    private static Dictionary<string, List<UiValidationRule>> ParseZodRules(string content)
    {
        var rules = new Dictionary<string, List<UiValidationRule>>(StringComparer.OrdinalIgnoreCase);

        foreach (Match fieldMatch in Regex.Matches(content, @"(\w+)\s*:\s*z\.(?:string|number|boolean)\(\)([^,}]*)"))
        {
            var name = fieldMatch.Groups[1].Value;
            var modifiers = fieldMatch.Groups[2].Value;
            var fieldRules = new List<UiValidationRule>();

            if (Regex.IsMatch(modifiers, @"\.email\(\)"))
            {
                fieldRules.Add(new UiValidationRule { Kind = "email", Value = true });
            }

            var min = Regex.Match(modifiers, @"\.min\((\d+)\)");
            if (min.Success)
            {
                fieldRules.Add(new UiValidationRule { Kind = "minLength", Value = int.Parse(min.Groups[1].Value) });
            }

            var max = Regex.Match(modifiers, @"\.max\((\d+)\)");
            if (max.Success)
            {
                fieldRules.Add(new UiValidationRule { Kind = "maxLength", Value = int.Parse(max.Groups[1].Value) });
            }

            if (fieldRules.Count > 0)
            {
                rules[name] = fieldRules;
            }
        }

        return rules;
    }

    private static Dictionary<string, List<UiValidationRule>> ParseRhfRules(string content)
    {
        var rules = new Dictionary<string, List<UiValidationRule>>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(content, @"register\(\s*[""'](\w+)[""']\s*,\s*\{([^}]*)\}"))
        {
            var name = match.Groups[1].Value;
            var options = match.Groups[2].Value;
            var fieldRules = new List<UiValidationRule>();

            if (Regex.IsMatch(options, @"\brequired\s*:\s*true"))
            {
                fieldRules.Add(new UiValidationRule { Kind = "required", Value = true });
            }

            var min = Regex.Match(options, @"\bminLength\s*:\s*(\d+)");
            if (min.Success)
            {
                fieldRules.Add(new UiValidationRule { Kind = "minLength", Value = int.Parse(min.Groups[1].Value) });
            }

            var max = Regex.Match(options, @"\bmaxLength\s*:\s*(\d+)");
            if (max.Success)
            {
                fieldRules.Add(new UiValidationRule { Kind = "maxLength", Value = int.Parse(max.Groups[1].Value) });
            }

            if (fieldRules.Count > 0)
            {
                rules[name] = fieldRules;
            }
        }

        return rules;
    }

    private static string? ResolveLabel(
        string? id, string? name, string? ariaLabel,
        List<(string? HtmlFor, string? AriaLabel, string Text)> labels)
    {
        if (!string.IsNullOrEmpty(ariaLabel))
        {
            return ariaLabel;
        }

        foreach (var label in labels)
        {
            if (label.HtmlFor is not null && (label.HtmlFor == id || label.HtmlFor == name))
            {
                return label.Text;
            }
        }

        return null;
    }

    private static UiVisibility BuildVisibility(Dictionary<string, string?> attributes, bool? disabled)
    {
        if (disabled == true)
        {
            return new UiVisibility { State = VisibilityState.Hidden };
        }

        return new UiVisibility { State = VisibilityState.Always };
    }

    private UiAction? BuildButton(
        Match buttonMatch, Dictionary<string, string?> formAttributes,
        UiForm form, ParsedUiSource source, int formOffset, string fullContent)
    {
        var attributes = ParseAttributes(buttonMatch.Groups[1].Value);
        var label = StripTags(buttonMatch.Groups[2].Value).Trim();
        var line = LineOf(fullContent, formOffset + buttonMatch.Index);

        var type = attributes.GetValueOrDefault("type");
        var actionKind = type == "button" ? ActionKind.Unknown : ActionKind.SubmitForm;

        var onClick = ExtractExpression(attributes.GetValueOrDefault("onClick"));

        var action = new UiAction
        {
            Id = $"action_{Sanitize(label)}",
            Label = label,
            ElementType = "button",
            Kind = actionKind,
            HandlerExpression = actionKind == ActionKind.SubmitForm ? form.SubmitHandler : onClick,
        };
        action.EvidenceIds.Add(AddEvidence(source, EvidenceKind.Action, line));

        return action;
    }

    private void ParseStandaloneFields(string content, ParsedUiSource source)
    {
        foreach (Match inputMatch in InputRegex.Matches(content))
        {
            if (IsInsideForm(content, inputMatch.Index))
            {
                continue;
            }

            var field = BuildInputField(
                inputMatch.Groups[1].Value, inputMatch.Groups[2].Value, "input",
                new List<(string?, string?, string)>(),
                source, LineOf(content, inputMatch.Index));
            source.StandaloneFields.Add(field);
        }
    }

    private void ParseStandaloneButtons(string content, ParsedUiSource source)
    {
        foreach (Match buttonMatch in ButtonRegex.Matches(content))
        {
            if (IsInsideForm(content, buttonMatch.Index))
            {
                continue;
            }

            var attributes = ParseAttributes(buttonMatch.Groups[1].Value);
            var label = StripTags(buttonMatch.Groups[2].Value).Trim();
            var action = new UiAction
            {
                Id = $"action_{Sanitize(label)}",
                Label = label,
                ElementType = "button",
                Kind = ActionKind.Unknown,
                HandlerExpression = ExtractExpression(attributes.GetValueOrDefault("onClick")),
            };
            action.EvidenceIds.Add(AddEvidence(source, EvidenceKind.Action, LineOf(content, buttonMatch.Index)));
            source.Actions.Add(action);
        }
    }

    private void ParseLinks(string content, ParsedUiSource source)
    {
        foreach (Match linkMatch in LinkRegex.Matches(content))
        {
            if (IsInsideForm(content, linkMatch.Index))
            {
                continue;
            }

            var attributes = ParseAttributes(linkMatch.Groups[2].Value);
            var label = StripTags(linkMatch.Groups[3].Value).Trim();
            var href = attributes.GetValueOrDefault("href");

            var action = new UiAction
            {
                Id = $"action_{Sanitize(label)}",
                Label = label,
                ElementType = linkMatch.Groups[1].Value,
                Kind = ActionKind.Navigate,
                NavigationTarget = href,
            };
            action.EvidenceIds.Add(AddEvidence(source, EvidenceKind.Action, LineOf(content, linkMatch.Index)));

            source.Actions.Add(action);
            source.Navigation.Add(new NavigationItem
            {
                Id = $"nav_{Sanitize(label)}",
                Label = label,
                Target = href,
                EvidenceIds = { action.EvidenceIds.Last() },
            });
        }
    }

    private void ParseTables(string content, ParsedUiSource source)
    {
        foreach (Match tableMatch in TableRegex.Matches(content))
        {
            var line = LineOf(content, tableMatch.Index);
            var table = new UiTable { Id = $"table_{_elementCounter++}" };
            table.EvidenceIds.Add(AddEvidence(source, EvidenceKind.Table, line));

            var caption = CaptionRegex.Match(tableMatch.Groups[2].Value);
            if (caption.Success)
            {
                table.Caption = StripTags(caption.Groups[1].Value).Trim();
            }

            foreach (Match thMatch in ThRegex.Matches(tableMatch.Groups[2].Value))
            {
                var attributes = ParseAttributes(thMatch.Groups[1].Value);
                table.Columns.Add(new UiTableColumn
                {
                    Label = StripTags(thMatch.Groups[2].Value).Trim(),
                    Accessor = attributes.GetValueOrDefault("data-accessor") ?? attributes.GetValueOrDefault("accessor"),
                    EvidenceIds = { AddEvidence(source, EvidenceKind.Table, LineOf(content, tableMatch.Index + thMatch.Index)) },
                });
            }

            source.Tables.Add(table);
        }
    }

    private List<ParsedFetch> ParseFetches(string content)
    {
        var fetches = new List<ParsedFetch>();
        foreach (Match match in FetchRegex.Matches(content))
        {
            var urlArg = match.Groups[1].Value.Trim();
            var options = match.Groups[2].Value;

            string? method = null;
            var methodMatch = MethodRegex.Match(options);
            if (methodMatch.Success)
            {
                method = methodMatch.Groups[1].Value.ToUpperInvariant();
            }

            fetches.Add(new ParsedFetch
            {
                Method = method,
                Url = IsLiteral(urlArg) ? Unquote(urlArg) : null,
                UrlExpression = IsLiteral(urlArg) ? null : urlArg,
                Line = LineOf(content, match.Index),
            });
        }

        return fetches;
    }

    private List<ParsedRouterPush> ParseRouterPushes(string content)
    {
        var pushes = new List<ParsedRouterPush>();
        foreach (Match match in RouterPushRegex.Matches(content))
        {
            var arg = match.Groups[1].Value.Trim();
            pushes.Add(new ParsedRouterPush
            {
                Target = IsLiteral(arg) ? Unquote(arg) : null,
                Expression = IsLiteral(arg) ? null : arg,
                Line = LineOf(content, match.Index),
            });
        }

        return pushes;
    }

    // ---- helpers ----

    private static Dictionary<string, string?> ParseAttributes(string tag)
    {
        var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(tag, @"([\w:.-]+)(?:\s*=\s*(""([^""]*)""|'([^']*)'|\{([^}]*)\}))?"))
        {
            var name = match.Groups[1].Value;
            var value = match.Groups[3].Success ? match.Groups[3].Value
                : match.Groups[4].Success ? match.Groups[4].Value
                : match.Groups[5].Success ? match.Groups[5].Value
                : null;
            attributes[name] = value;
        }

        return attributes;
    }

    private static string? ExtractExpression(string? value) => value;

    private static string? ExtractRhfName(string attributesText)
    {
        var match = Regex.Match(attributesText, @"register\(\s*[""'](\w+)[""']");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static InputKind MapInputType(string? type, string component)
    {
        if (component == "textarea") return InputKind.Textarea;
        if (component == "select") return InputKind.Select;

        return type?.ToLowerInvariant() switch
        {
            "email" => InputKind.Email,
            "password" => InputKind.Password,
            "number" => InputKind.Number,
            "checkbox" => InputKind.Checkbox,
            "radio" => InputKind.Radio,
            "file" => InputKind.File,
            "date" => InputKind.Date,
            null or "" or "text" => InputKind.Text,
            _ => InputKind.Unknown,
        };
    }

    private static bool IsLiteral(string value)
    {
        var trimmed = value.Trim();
        return (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            || (trimmed.StartsWith('\'') && trimmed.EndsWith('\''));
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static bool IsInsideForm(string content, int index)
    {
        var before = content[..index];
        var lastFormOpen = before.LastIndexOf("<form", StringComparison.OrdinalIgnoreCase);
        var lastFormClose = before.LastIndexOf("</form", StringComparison.OrdinalIgnoreCase);
        return lastFormOpen > lastFormClose;
    }

    private static string StripTags(string value)
    {
        var text = Regex.Replace(value, @"<[^>]*>", string.Empty);
        text = Regex.Replace(text, @"\{(.+?)\}", "$1");
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static int LineOf(string content, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "anon";
        }

        var builder = new StringBuilder();
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        var result = builder.ToString().Trim('_');
        return result.Length == 0 ? "anon" : result;
    }

    private static string ShortHash(string value)
    {
        uint hash = 2166136261;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash.ToString("x8");
    }
}
