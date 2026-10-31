using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IUiScenarioExtractor
{
    List<UiScenario> Extract(ParsedUiSource source, IReadOnlyList<UiAction> pageActions);
}
