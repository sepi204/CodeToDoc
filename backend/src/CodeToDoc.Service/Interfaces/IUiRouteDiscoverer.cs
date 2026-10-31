using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IUiRouteDiscoverer
{
    List<DiscoveredRoute> Discover(FrontendProject project);
}
