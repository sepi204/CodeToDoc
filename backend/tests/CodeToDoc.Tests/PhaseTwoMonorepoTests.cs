using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeToDoc.Tests;

public class PhaseTwoMonorepoTests : IDisposable
{
    private readonly string _tempRoot = TestHelpers.CreateTempRoot();

    public void Dispose() => TestHelpers.DeleteDirectory(_tempRoot);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private WebApplicationFactory<global::Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["PhaseOneProcessingOptions:TempRootPath"] = _tempRoot,
        };

        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            });
    }

    [Fact]
    public async Task Monorepo_AstroRoot_NestJsApi_Detected()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(
            ("MovieToon/package.json", """{"dependencies":{"astro":"4.0.0","react":"18.2.0"}}"""),
            ("MovieToon/astro.config.mjs", """export default {}"""),
            ("MovieToon/src/pages/index.astro", """<html><body>catalog</body></html>"""),
            ("MovieToon/src/pages/movies/[id].astro", """<html>detail</html>"""),
            ("MovieToon/api/package.json", """{"dependencies":{"@nestjs/common":"10.0.0","@nestjs/core":"10.0.0","typeorm":"0.3.0","pg":"8.11.0","@nestjs/jwt":"10.0.0"}}"""),
            ("MovieToon/api/src/movies/movies.controller.ts", """
                import { Controller, Get, Post, Body, UseGuards } from '@nestjs/common';
                @Controller('movies')
                @UseGuards(JwtAuthGuard)
                export class MoviesController {
                  @Get()
                  findAll() { return []; }
                  @Post()
                  create(@Body() dto: CreateMovieDto) { return null; }
                }
                """),
            ("MovieToon/api/src/movies/dto/create-movie.dto.ts", """
                import { IsNotEmpty } from 'class-validator';
                export class CreateMovieDto {
                  @IsNotEmpty()
                  title: string;
                }
                """),
            ("MovieToon/api/src/movies/movie.entity.ts", """
                import { Entity, Column } from 'typeorm';
                @Entity('movies')
                export class Movie {
                  @Column()
                  title: string;
                }
                """));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zip);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "archives", "movietoon.zip");

        using var response = await client.PostAsync("/api/projects/process-technical", form);
        response.EnsureSuccessStatusCode();
        var skeleton = JsonSerializer.Deserialize<TechnicalSkeleton>(
            await response.Content.ReadAsStringAsync(), JsonOptions)!;

        Assert.Contains(skeleton.Endpoints, e => e.Path == "/movies" && e.Method == "GET");
        Assert.Contains(skeleton.Endpoints, e => e.Path == "/movies" && e.Method == "POST");
        Assert.Contains(skeleton.Projects, p => p.Framework == "NestJS");
        Assert.Contains(skeleton.Projects, p => p.Framework == "Astro");
        Assert.Contains(skeleton.Entities, e => e.Name == "Movie");
        Assert.Contains(skeleton.Dependencies, d => d.Name == "@nestjs/common");
        Assert.Contains("NestJS", skeleton.Summary.Framework);
    }

    [Fact]
    public async Task Synthesis_Monorepo_Produces_Persian_Docs()
    {
        var config = new Dictionary<string, string?>
        {
            ["PhaseOneProcessingOptions:TempRootPath"] = _tempRoot,
            ["LlmOptions:Provider"] = "Fake",
            ["LlmOptions:Model"] = "fake-model",
        };

        using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            });
        using var client = factory.CreateClient();

        var zip = TestHelpers.CreateZip(
            ("MovieToon/package.json", """{"dependencies":{"astro":"4.0.0"}}"""),
            ("MovieToon/src/pages/index.astro", """<html>catalog <form><input name="q" /></form></html>"""),
            ("MovieToon/api/package.json", """{"dependencies":{"@nestjs/common":"10.0.0"}}"""),
            ("MovieToon/api/src/app.controller.ts", """
                import { Controller, Get } from '@nestjs/common';
                @Controller('health')
                export class AppController {
                  @Get()
                  check() { return 'ok'; }
                }
                """));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zip);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "archives", "movietoon.zip");

        using var response = await client.PostAsync("/api/projects/process-synthesis", form);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("technical_document.md", body);
    }
}
