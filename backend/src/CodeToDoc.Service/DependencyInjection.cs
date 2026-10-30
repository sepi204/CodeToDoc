using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToDoc.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        return services;
    }
}
