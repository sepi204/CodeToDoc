using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class NestJsControllerParserTests
{
    [Fact]
    public void Parses_Controller_Prefix_Method_Routes_Guards_Dto_Entity()
    {
        var files = new List<SourceFile>
        {
            new()
            {
                RelativePath = "MovieToon/api/src/movies/movies.controller.ts",
                Content = """
                    import { Controller, Get, Post, Body, Param, UseGuards } from '@nestjs/common';
                    import { Roles } from './roles.decorator';

                    @Controller('movies')
                    @UseGuards(JwtAuthGuard)
                    export class MoviesController {
                      @Get()
                      findAll(): Promise<Movie[]> { return []; }

                      @Get(':id')
                      findOne(@Param('id') id: string) { return null; }

                      @Post()
                      @Roles('Admin')
                      create(@Body() dto: CreateMovieDto) { return null; }
                    }
                    """,
            },
            new()
            {
                RelativePath = "MovieToon/api/src/movies/dto/create-movie.dto.ts",
                Content = """
                    import { IsNotEmpty, IsString, IsOptional } from 'class-validator';
                    export class CreateMovieDto {
                      @IsNotEmpty()
                      @IsString()
                      title: string;
                      @IsOptional()
                      description?: string;
                    }
                    """,
            },
            new()
            {
                RelativePath = "MovieToon/api/src/movies/movie.entity.ts",
                Content = """
                    import { Entity, Column, PrimaryGeneratedColumn } from 'typeorm';
                    @Entity('movies')
                    export class Movie {
                      @PrimaryGeneratedColumn()
                      id: number;
                      @Column()
                      title: string;
                    }
                    """,
            },
        };

        var endpoints = NestJsControllerParser.Parse(files, out var entities);

        Assert.Equal(3, endpoints.Count);
        Assert.Contains(endpoints, e => e.Path == "/movies" && e.Method == "GET");
        Assert.Contains(endpoints, e => e.Path == "/movies/:id" && e.Method == "GET");
        var post = Assert.Single(endpoints, e => e.Method == "POST" && e.Path == "/movies");
        Assert.Equal("MoviesController", post.ControllerName);
        Assert.True(post.AuthRequired);
        Assert.Contains("Admin", post.Roles);
        Assert.NotNull(post.RequestDto);
        Assert.Equal("CreateMovieDto", post.RequestDto.TypeName);
        Assert.Contains(post.RequestDto.Properties, p => p.Name == "title" && p.IsRequired);
        Assert.Contains(entities, e => e.Name == "Movie");
    }
}
