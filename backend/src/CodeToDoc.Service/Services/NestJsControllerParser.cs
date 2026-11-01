using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public static class NestJsControllerParser
{
    private static readonly Regex ControllerRegex = new(
        "@Controller\\(\\s*(?:['\"](?<prefix>[^'\"]*)['\"]|(?<obj>\\{[^}]*\\}))?\\s*\\)",
        RegexOptions.IgnoreCase);

    private static readonly Regex ClassRegex = new(
        "export\\s+class\\s+(?<name>\\w+)",
        RegexOptions.IgnoreCase);

    private static readonly Regex MethodDecoratorRegex = new(
        "@(?<verb>Get|Post|Put|Delete|Patch|Options|Head|All)\\(\\s*(?:['\"](?<path>[^'\"]*)['\"])?\\s*\\)",
        RegexOptions.IgnoreCase);

    private static readonly Regex MethodNameRegex = new(
        "(?:async\\s+)?(?<name>[A-Za-z_]\\w*)\\s*\\(",
        RegexOptions.IgnoreCase);

    private static readonly Regex RolesRegex = new(
        "@Roles\\(\\s*(?<roles>[^)]*)\\)",
        RegexOptions.IgnoreCase);

    private static readonly Regex BodyParamRegex = new(
        "@Body\\([^)]*\\)\\s*(?<param>\\w+)\\s*:\\s*(?<dto>\\w+)",
        RegexOptions.IgnoreCase);

    private static readonly Regex ReturnTypeRegex = new(
        "(?<name>[A-Za-z_]\\w*)\\s*\\([^)]*\\)\\s*:\\s*(?:Promise<\\s*(?<ret>\\w+)[^>]*>|(?<ret2>\\w+))",
        RegexOptions.IgnoreCase);

    private static readonly Regex DtoClassRegex = new(
        "export\\s+(?:class|interface)\\s+(?<name>\\w+)",
        RegexOptions.IgnoreCase);

    private static readonly Regex DtoPropRegex = new(
        "(?:^|\\n)\\s*(?:@\\w+(?:\\([^)]*\\))?\\s*)*(?<prop>[A-Za-z_]\\w*)\\s*[?!]?\\s*:\\s*(?<type>[A-Za-z_][\\w<>\\[\\]]*)",
        RegexOptions.IgnoreCase);

    private static readonly Regex EntityRegex = new(
        "@Entity\\(\\s*(?:['\"](?<table>[^'\"]*)['\"])?[^)]*\\)\\s*export\\s+class\\s+(?<name>\\w+)",
        RegexOptions.IgnoreCase);

    public static List<EndpointModel> Parse(IReadOnlyList<SourceFile> sourceFiles, out List<EntityModel> entities)
    {
        entities = new List<EntityModel>();
        var endpoints = new List<EndpointModel>();
        var dtoMap = BuildDtoMap(sourceFiles, entities);

        foreach (var file in sourceFiles)
        {
            if (!file.RelativePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip frontend-only dirs unless they contain controllers.
            if (!file.Content.Contains("@Controller", StringComparison.Ordinal))
            {
                continue;
            }

            endpoints.AddRange(ParseFile(file, dtoMap));
        }

        return endpoints;
    }

    public static List<EndpointModel> Parse(IReadOnlyList<SourceFile> sourceFiles)
        => Parse(sourceFiles, out _);

    private static Dictionary<string, DtoModel> BuildDtoMap(
        IReadOnlyList<SourceFile> sourceFiles, List<EntityModel> entities)
    {
        var map = new Dictionary<string, DtoModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            if (!file.RelativePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Match classMatch in DtoClassRegex.Matches(file.Content))
            {
                var name = classMatch.Groups["name"].Value;
                if (name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("Module", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (map.ContainsKey(name))
                {
                    continue;
                }

                var body = ExtractClassBody(file.Content, classMatch.Index);
                var props = new List<PropertyModel>();
                var propMatches = DtoPropRegex.Matches(body);
                for (var pi = 0; pi < propMatches.Count; pi++)
                {
                    var prop = propMatches[pi];
                    var propName = prop.Groups["prop"].Value;
                    if (propName is "constructor" or "prototype")
                    {
                        continue;
                    }

                    var type = prop.Groups["type"].Value;
                    var nameIdxInBody = prop.Groups["prop"].Index;
                    // Look back up to 400 chars before the property name for decorators.
                    var backStart = Math.Max(0, nameIdxInBody - 400);
                    var window = body.Substring(backStart, nameIdxInBody - backStart);
                    var required = window.Contains("@IsNotEmpty", StringComparison.Ordinal) ||
                                   window.Contains("@IsDefined", StringComparison.Ordinal) ||
                                   window.Contains("@IsString", StringComparison.Ordinal);
                    if (window.Contains("@IsOptional", StringComparison.Ordinal) || type.EndsWith("?"))
                    {
                        required = false;
                    }
                    // Explicit optional marker in source wins.
                    if (body.Substring(Math.Max(0, prop.Index - 80), Math.Min(80, prop.Index)).Contains("?:"))
                    {
                        required = false;
                    }

                    props.Add(new PropertyModel
                    {
                        Name = propName,
                        Type = MapTsType(type),
                        IsRequired = required,
                        Rules = required ? new List<string> { "required" } : new List<string>(),
                    });
                }

                map[name] = new DtoModel { TypeName = name, Properties = props };
            }

            foreach (Match entityMatch in EntityRegex.Matches(file.Content))
            {
                var name = entityMatch.Groups["name"].Value;
                if (entities.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var body = ExtractClassBody(file.Content, entityMatch.Index);
                var props = new List<PropertyModel>();
                foreach (Match prop in DtoPropRegex.Matches(body))
                {
                    props.Add(new PropertyModel
                    {
                        Name = prop.Groups["prop"].Value,
                        Type = MapTsType(prop.Groups["type"].Value),
                    });
                }

                entities.Add(new EntityModel { Name = name, Properties = props, Source = file.RelativePath });
                if (!map.ContainsKey(name))
                {
                    map[name] = new DtoModel { TypeName = name, Properties = props };
                }
            }
        }

        return map;
    }

    private static string ExtractClassBody(string content, int classIndex)
    {
        var open = content.IndexOf('{', classIndex);
        if (open < 0) return string.Empty;
        var depth = 0;
        for (var i = open; i < content.Length && i < open + 20000; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0) return content.Substring(open, i - open + 1);
            }
        }
        return content.Substring(open, Math.Min(8000, content.Length - open));
    }

    private static List<EndpointModel> ParseFile(SourceFile file, Dictionary<string, DtoModel> dtoMap)
    {
        var result = new List<EndpointModel>();
        var content = file.Content;

        var controllerMatch = ControllerRegex.Match(content);
        var prefix = controllerMatch.Success
            ? (controllerMatch.Groups["prefix"].Success ? controllerMatch.Groups["prefix"].Value.Trim() : string.Empty)
            : string.Empty;

        var classMatch = ClassRegex.Match(content);
        var controllerName = classMatch.Success ? classMatch.Groups["name"].Value : Path.GetFileNameWithoutExtension(file.RelativePath);

        var classAuth = content.Contains("@UseGuards", StringComparison.Ordinal) ||
                        content.Contains("@Auth(", StringComparison.Ordinal) ||
                        content.Contains("JwtAuthGuard", StringComparison.Ordinal) ||
                        content.Contains("AuthGuard", StringComparison.Ordinal);
        var classRoles = ExtractRoles(content);

        // Find method decorators in order and pair each with the next method name.
        var decorators = MethodDecoratorRegex.Matches(content);
        foreach (Match dec in decorators)
        {
            var verb = dec.Groups["verb"].Value.ToUpperInvariant();
            if (verb == "ALL") verb = "GET";
            var subPath = dec.Groups["path"].Success ? dec.Groups["path"].Value.Trim() : string.Empty;
            var fullPath = Combine(prefix, subPath);

            var after = content.Substring(dec.Index + dec.Length, Math.Min(600, content.Length - dec.Index - dec.Length));
            var nameMatch = MethodNameRegex.Match(after);
            var actionName = nameMatch.Success ? nameMatch.Groups["name"].Value : null;

            var methodAuth = classAuth ||
                             after.Contains("@UseGuards", StringComparison.Ordinal) ||
                             content.Substring(Math.Max(0, dec.Index - 400), Math.Min(400, dec.Index)).Contains("@UseGuards", StringComparison.Ordinal);
            var methodRoles = ExtractRoles(after);
            var roles = methodRoles.Count > 0 ? methodRoles : classRoles;

            DtoModel? requestDto = null;
            var bodyMatch = BodyParamRegex.Match(after);
            if (bodyMatch.Success && dtoMap.TryGetValue(bodyMatch.Groups["dto"].Value, out var dto))
            {
                requestDto = dto;
            }

            DtoModel? responseDto = null;
            var retMatch = ReturnTypeRegex.Match(after);
            var retName = retMatch.Success
                ? (retMatch.Groups["ret"].Success ? retMatch.Groups["ret"].Value : retMatch.Groups["ret2"].Value)
                : null;
            if (retName is not null && dtoMap.TryGetValue(retName.Trim(), out var resp))
            {
                responseDto = new DtoModel { TypeName = resp.TypeName, Properties = resp.Properties };
            }

            result.Add(new EndpointModel
            {
                Path = fullPath.StartsWith('/') ? fullPath : "/" + fullPath,
                Method = verb,
                ControllerName = controllerName,
                ActionName = actionName,
                AuthRequired = methodAuth,
                Roles = roles,
                RequestDto = requestDto is null ? null : new DtoModel { TypeName = requestDto.TypeName, Properties = requestDto.Properties },
                ResponseDto = responseDto is null ? null : new ResponseModel { TypeName = responseDto.TypeName, Properties = responseDto.Properties.Select(p => new PropertyModel { Name = p.Name, Type = p.Type, IsRequired = p.IsRequired, Rules = p.Rules }).ToList() },
                SourceProject = ProjectRootOf(file.RelativePath),
            });
        }

        return result;
    }

    private static List<string> ExtractRoles(string text)
    {
        var roles = new List<string>();
        foreach (Match m in RolesRegex.Matches(text))
        {
            var raw = m.Groups["roles"].Value;
            foreach (Match q in Regex.Matches(raw, @"['""]([^'""]+)['""]"))
            {
                roles.Add(q.Groups[1].Value);
            }
        }
        return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Combine(string prefix, string sub)
    {
        prefix = (prefix ?? string.Empty).Trim().Trim('/');
        sub = (sub ?? string.Empty).Trim().Trim('/');
        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(sub)) return "/";
        if (string.IsNullOrEmpty(prefix)) return "/" + sub;
        if (string.IsNullOrEmpty(sub)) return "/" + prefix;
        return "/" + prefix + "/" + sub;
    }

    private static string? ProjectRootOf(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        // Heuristic: if path contains api/ as a segment, attribute to that subtree.
        var idx = normalized.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return normalized[..idx].Trim('/');
        }
        return null;
    }

    private static string MapTsType(string ts) => ts switch
    {
        "string" => "string",
        "number" => "number",
        "boolean" => "boolean",
        "Date" => "date-time",
        _ when ts.EndsWith("[]") => $"array<{ts[..^2]}>",
        _ => ts,
    };
}
