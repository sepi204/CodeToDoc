using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToDoc.Service.Services;

public sealed class CSharpAstParser : ICSharpAstParser
{
    private static readonly HashSet<string> HttpVerbAttributes = new(StringComparer.Ordinal)
    {
        "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch",
    };

    public List<EndpointModel> Parse(IReadOnlyList<SourceFile> sourceFiles)
    {
        var dtoMap = BuildDtoMap(sourceFiles);
        var endpoints = new List<EndpointModel>();

        foreach (var file in sourceFiles)
        {
            if (!file.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(file.Content);
            var root = tree.GetRoot();

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsController(classDeclaration))
                {
                    continue;
                }

                endpoints.AddRange(ParseController(classDeclaration, dtoMap));
            }
        }

        return endpoints;
    }

    private static bool IsController(ClassDeclarationSyntax classDeclaration)
    {
        var bases = classDeclaration.BaseList?.Types.Select(t => t.ToString()).ToList() ?? new List<string>();
        if (bases.Any(b => b.Split('.').Last() is "ControllerBase" or "Controller"))
        {
            return true;
        }

        return HasAttribute(classDeclaration, "ApiController");
    }

    private static List<EndpointModel> ParseController(
        ClassDeclarationSyntax classDeclaration,
        Dictionary<string, ClassDeclarationSyntax> dtoMap)
    {
        var controllerName = classDeclaration.Identifier.Text;
        var classRoute = GetClassRoute(classDeclaration);
        var classAuth = GetAuth(classDeclaration);

        var endpoints = new List<EndpointModel>();

        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var verbAttribute = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => HttpVerbAttributes.Contains(AttributeName(a)));

            if (verbAttribute is null)
            {
                continue;
            }

            var methodAuth = GetAuth(method);
            var auth = methodAuth.HasAuth ? methodAuth : classAuth;

            var httpMethod = AttributeName(verbAttribute) switch
            {
                "HttpGet" => "GET",
                "HttpPost" => "POST",
                "HttpPut" => "PUT",
                "HttpDelete" => "DELETE",
                "HttpPatch" => "PATCH",
                _ => "GET",
            };

            var actionRoute = GetRouteTemplate(verbAttribute);

            endpoints.Add(new EndpointModel
            {
                Path = CombineRoute(classRoute, actionRoute, controllerName),
                Method = httpMethod,
                ControllerName = controllerName,
                ActionName = method.Identifier.Text,
                AuthRequired = auth.AuthRequired,
                Roles = auth.Roles,
                RequestDto = GetRequestDto(method, dtoMap),
                ResponseDto = GetResponseDto(method, dtoMap),
            });
        }

        return endpoints;
    }

    private static string? GetClassRoute(ClassDeclarationSyntax classDeclaration)
    {
        var routeAttribute = classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => AttributeName(a) is "Route" or "RouteAttribute");

        return routeAttribute is null ? null : GetRouteTemplate(routeAttribute);
    }

    private static string? GetRouteTemplate(AttributeSyntax routeAttribute)
    {
        var arguments = routeAttribute.ArgumentList?.Arguments;
        if (arguments is null || arguments.Value.Count == 0)
        {
            return null;
        }

        var expression = arguments.Value[0].Expression;
        if (expression is LiteralExpressionSyntax literal && literal.Token.Value is string value)
        {
            return value;
        }

        return expression.ToString().Trim('"');
    }

    private static string CombineRoute(string? classRoute, string? actionRoute, string controllerName)
    {
        var controller = controllerName;
        if (controller.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            controller = controller[..^"Controller".Length];
        }

        var controllerSegment = char.ToLowerInvariant(controller[0]) + controller[1..];

        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(classRoute))
        {
            segments.Add(classRoute.Replace("[controller]", controllerSegment).Trim('/'));
        }

        if (!string.IsNullOrWhiteSpace(actionRoute))
        {
            segments.Add(actionRoute.Trim('/'));
        }

        return "/" + string.Join("/", segments.Where(s => s.Length > 0));
    }

    private static AuthInfo GetAuth(MemberDeclarationSyntax member)
    {
        var attributes = member.AttributeLists.SelectMany(al => al.Attributes).ToList();

        if (attributes.Any(a => AttributeName(a) == "AllowAnonymous"))
        {
            return new AuthInfo { HasAuth = true, AuthRequired = false };
        }

        var authorize = attributes.FirstOrDefault(a => AttributeName(a) == "Authorize");
        if (authorize is not null)
        {
            return new AuthInfo { HasAuth = true, AuthRequired = true, Roles = ExtractRoles(authorize) };
        }

        return new AuthInfo { HasAuth = false, AuthRequired = false };
    }

    private static List<string> ExtractRoles(AttributeSyntax authorize)
    {
        var roles = new List<string>();
        var arguments = authorize.ArgumentList?.Arguments;
        if (arguments is null)
        {
            return roles;
        }

        foreach (var argument in arguments.Value)
        {
            var argumentName = argument.NameEquals?.Name.Identifier.Text
                ?? argument.NameColon?.Name.Identifier.Text;

            if (argumentName != "Roles")
            {
                continue;
            }

            var strings = argument.Expression.DescendantNodesAndSelf()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.Token.Value is string)
                .Select(l => (string)l.Token.Value!);

            foreach (var value in strings)
            {
                foreach (var role in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!roles.Contains(role))
                    {
                        roles.Add(role);
                    }
                }
            }
        }

        return roles;
    }

    private static DtoModel? GetRequestDto(
        MethodDeclarationSyntax method,
        Dictionary<string, ClassDeclarationSyntax> dtoMap)
    {
        var fromBody = method.ParameterList.Parameters.FirstOrDefault(p =>
            p.AttributeLists.SelectMany(al => al.Attributes).Any(a => AttributeName(a) == "FromBody"));

        if (fromBody?.Type is null)
        {
            return null;
        }

        var typeName = fromBody.Type.ToString().TrimEnd('?');
        if (!dtoMap.TryGetValue(typeName, out var classDeclaration))
        {
            return new DtoModel { TypeName = typeName };
        }

        return new DtoModel { TypeName = typeName, Properties = ExtractProperties(classDeclaration) };
    }

    private static ResponseModel? GetResponseDto(
        MethodDeclarationSyntax method,
        Dictionary<string, ClassDeclarationSyntax> dtoMap)
    {
        var returnTypeText = method.ReturnType.ToString().Trim();

        var taskMatch = Regex.Match(returnTypeText, @"^(?:Task|ValueTask)<(.+)>$");
        if (taskMatch.Success)
        {
            returnTypeText = taskMatch.Groups[1].Value.Trim();
        }

        string? typeName = null;
        if (returnTypeText.StartsWith("ActionResult<") && returnTypeText.EndsWith(">"))
        {
            typeName = returnTypeText["ActionResult<".Length..^1].TrimEnd('?');
        }
        else if (returnTypeText.StartsWith("IActionResult<") && returnTypeText.EndsWith(">"))
        {
            typeName = returnTypeText["IActionResult<".Length..^1].TrimEnd('?');
        }
        else if (returnTypeText is "IActionResult" or "ActionResult" or "void" or "Task")
        {
            typeName = null;
        }
        else
        {
            typeName = returnTypeText.TrimEnd('?');
        }

        var properties = (typeName is not null && dtoMap.TryGetValue(typeName, out var classDeclaration))
            ? ExtractProperties(classDeclaration)
            : new List<PropertyModel>();

        return new ResponseModel
        {
            TypeName = typeName,
            StatusCode = InferStatusCode(method),
            Properties = properties,
        };
    }

    private static int InferStatusCode(MethodDeclarationSyntax method)
    {
        var body = (method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? string.Empty);

        if (body.Contains("CreatedAtAction") || body.Contains("CreatedAtRoute") || body.Contains("Created(")) return 201;
        if (body.Contains("NoContent")) return 204;
        if (body.Contains("BadRequest")) return 400;
        if (body.Contains("NotFound")) return 404;

        return 200;
    }

    private static List<PropertyModel> ExtractProperties(ClassDeclarationSyntax classDeclaration)
    {
        var properties = new List<PropertyModel>();

        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            var rules = new List<string>();
            var isRequired = false;

            foreach (var attribute in property.AttributeLists.SelectMany(al => al.Attributes))
            {
                switch (AttributeName(attribute))
                {
                    case "Required":
                        isRequired = true;
                        rules.Add("required");
                        break;
                    case "EmailAddress":
                        rules.Add("valid email");
                        break;
                    case "StringLength":
                        rules.Add($"maxLength({GetAttributeArgument(attribute, 0)})");
                        break;
                    case "MaxLength":
                        rules.Add($"maxLength({GetAttributeArgument(attribute, 0)})");
                        break;
                    case "MinLength":
                        rules.Add($"minLength({GetAttributeArgument(attribute, 0)})");
                        break;
                    case "Url":
                        rules.Add("valid url");
                        break;
                    case "Range":
                        rules.Add($"range({GetAttributeArgument(attribute, 0)},{GetAttributeArgument(attribute, 1)})");
                        break;
                }
            }

            properties.Add(new PropertyModel
            {
                Name = property.Identifier.Text,
                Type = MapType(property.Type),
                IsRequired = isRequired,
                Rules = rules,
            });
        }

        return properties;
    }

    private static string GetAttributeArgument(AttributeSyntax attribute, int index)
    {
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null || index >= arguments.Value.Count)
        {
            return "?";
        }

        return arguments.Value[index].Expression.ToString();
    }

    private static string MapType(TypeSyntax type)
    {
        var text = type.ToString().Trim();

        if (text.EndsWith("[]"))
        {
            return "array";
        }

        if (Regex.IsMatch(text, @"^(?:List|IEnumerable|ICollection|IReadOnlyList|IList|HashSet|Array)<.+>$"))
        {
            return "array";
        }

        text = text.TrimEnd('?');

        return text.ToLowerInvariant() switch
        {
            "string" or "system.string" => "string",
            "int" or "int32" or "system.int32" => "int",
            "long" or "int64" or "system.int64" => "long",
            "double" or "system.double" => "double",
            "decimal" or "system.decimal" => "decimal",
            "float" or "single" or "system.single" => "float",
            "bool" or "boolean" or "system.boolean" => "bool",
            "guid" or "system.guid" => "guid",
            "datetime" or "system.datetime" => "datetime",
            "datetimeoffset" or "system.datetimeoffset" => "datetimeoffset",
            "object" or "system.object" => "object",
            _ => text,
        };
    }

    private static Dictionary<string, ClassDeclarationSyntax> BuildDtoMap(IReadOnlyList<SourceFile> sourceFiles)
    {
        var map = new Dictionary<string, ClassDeclarationSyntax>(StringComparer.Ordinal);

        foreach (var file in sourceFiles)
        {
            if (!file.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(file.Content);
            var root = tree.GetRoot();

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                map[classDeclaration.Identifier.Text] = classDeclaration;
            }
        }

        return map;
    }

    private static string AttributeName(AttributeSyntax attribute) =>
        attribute.Name.ToString().Split('.').Last();

    private static bool HasAttribute(MemberDeclarationSyntax member, string attributeName) =>
        member.AttributeLists.SelectMany(al => al.Attributes).Any(a => AttributeName(a) == attributeName);

    private sealed class AuthInfo
    {
        public bool HasAuth { get; init; }
        public bool AuthRequired { get; init; }
        public List<string> Roles { get; init; } = new();
    }
}
