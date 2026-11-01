using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class ArchitectureInferenceEngine : IArchitectureInferenceEngine
{
    public ArchitectureInferenceResult Infer(ProjectManifest manifest)
    {
        var dependencyNames = manifest.Dependencies
            .Select(d => d.Name.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summary = new ArchitectureSummary
        {
            PrimaryLanguage = manifest.PrimaryLanguage,
            Framework = DetectFramework(manifest, dependencyNames),
            Orm = FirstMatch(dependencyNames, DetectOrm),
            Database = FirstMatch(dependencyNames, DetectDatabase),
            Auth = FirstMatch(dependencyNames, DetectAuth),
        };

        var dependencies = manifest.Dependencies
            .Select(d => new DependencyEntry
            {
                Name = d.Name,
                Version = d.Version,
                Category = Categorize(d.Name),
            })
            .ToList();

        return new ArchitectureInferenceResult { Summary = summary, Dependencies = dependencies };
    }

    private static string? DetectFramework(ProjectManifest manifest, HashSet<string> dependencyNames)
    {
        if (manifest.FrameworkHints.Contains("aspnetcore"))
        {
            return "ASP.NET Core";
        }

        if (dependencyNames.Contains("next")) return "Next.js";
        if (dependencyNames.Contains("nuxt")) return "Nuxt";
        if (dependencyNames.Contains("astro")) return "Astro";
        if (dependencyNames.Contains("@nestjs/core") || dependencyNames.Contains("@nestjs/common")) return "NestJS";
        if (dependencyNames.Contains("react")) return "React";
        if (dependencyNames.Contains("express")) return "Express";
        if (dependencyNames.Contains("fastify") || dependencyNames.Contains("@fastify/static")) return "Fastify";
        if (dependencyNames.Contains("fastapi")) return "FastAPI";
        if (dependencyNames.Contains("flask")) return "Flask";
        if (dependencyNames.Contains("django")) return "Django";
        if (dependencyNames.Contains("vue")) return "Vue";
        if (dependencyNames.Contains("@angular/core")) return "Angular";

        if (manifest.PrimaryLanguage == "C#") return "ASP.NET Core";
        if (manifest.PrimaryLanguage == "Go") return "Go";
        if (manifest.PrimaryLanguage == "Rust") return "Rust";

        return null;
    }

    private static string? DetectOrm(string name)
    {
        if (name.Contains("entityframeworkcore")) return "Entity Framework Core";
        if (name is "prisma" or "@prisma/client") return "Prisma";
        if (name == "sqlalchemy") return "SQLAlchemy";
        if (name == "dapper") return "Dapper";
        if (name == "mongoose") return "Mongoose";
        if (name == "typeorm") return "TypeORM";
        if (name == "sequelize") return "Sequelize";
        if (name.Contains("mikro-orm")) return "MikroORM";
        return null;
    }

    private static string? DetectDatabase(string name)
    {
        if (name.Contains("npgsql")) return "PostgreSQL";
        if (name.Contains("sqlserver") || name.Contains("sqlclient")) return "SQL Server";
        if (name.Contains("sqlite")) return "SQLite";
        if (name.Contains("pomelo") || name.Contains("mysql")) return "MySQL";
        if (name.Contains("mongo")) return "MongoDB";
        if (name is "psycopg2" or "psycopg" or "asyncpg" or "pg") return "PostgreSQL";
        if (name is "mysql2" or "mysql") return "MySQL";
        return null;
    }

    private static string? DetectAuth(string name)
    {
        if (name.Contains("jwtbearer")) return "JWT Bearer";
        if (name is "jsonwebtoken" or "jose") return "JWT Bearer";
        if (name is "@nestjs/jwt" or "@nestjs/passport" or "passport" or "passport-jwt" or "passport-local") return "JWT Bearer (Passport/Guards)";
        if (name.Contains("aspnetcore.identity")) return "ASP.NET Core Identity";
        if (name.Contains("identityserver")) return "OpenID Connect";
        if (name.StartsWith("duende")) return "OpenID Connect";
        if (name is "next-auth" or "@auth/core") return "NextAuth.js";
        if (name.StartsWith("passport")) return "Passport JWT";
        return null;
    }

    private static string Categorize(string name)
    {
        var n = name.ToLowerInvariant();

        if (n.Contains("entityframeworkcore") || n is "dapper" or "sqlalchemy" or "prisma" or "@prisma/client" or "mongoose" or "typeorm" or "sequelize")
        {
            return "ORM";
        }

        if (n.Contains("npgsql") || n.Contains("sqlclient") || n.Contains("mysql") || n.Contains("mongo") ||
            n is "psycopg2" or "psycopg" or "asyncpg" or "pg" or "mysql2")
        {
            return "DatabaseDriver";
        }

        if (n.Contains("jwtbearer") || n.Contains("identity") || n.Contains("identityserver") || n.StartsWith("duende") ||
            n is "jsonwebtoken" or "jose" or "next-auth" or "@auth/core" || n.StartsWith("passport"))
        {
            return "Authentication";
        }

        if (n is "next" or "nuxt" or "astro" or "react" or "react-dom" or "vue" or "@angular/core" or "express" or "fastify" or "@nestjs/core" or "@nestjs/common" or "fastapi" or "flask" or "django")
        {
            return "Framework";
        }

        return "Dependency";
    }

    private static string? FirstMatch(HashSet<string> names, Func<string, string?> detector)
    {
        foreach (var name in names)
        {
            var result = detector(name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
