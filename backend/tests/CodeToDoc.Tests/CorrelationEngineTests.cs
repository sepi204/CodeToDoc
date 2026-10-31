using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class CorrelationEngineTests
{
    private readonly CorrelationEngine _engine = new();

    private static EndpointModel Endpoint(string method, string path, string controller, string action, DtoModel? dto = null, bool authRequired = false) =>
        new()
        {
            Method = method,
            Path = path,
            ControllerName = controller,
            ActionName = action,
            RequestDto = dto,
            AuthRequired = authRequired,
        };

    private static DtoModel Dto(params (string Name, bool Required)[] props) =>
        new()
        {
            TypeName = "Request",
            Properties = props.Select(p => new PropertyModel { Name = p.Name, IsRequired = p.Required }).ToList(),
        };

    private static UiAction Action(params UiApiReference[] refs) =>
        new() { Id = "action_1", Label = "ثبت", Kind = ActionKind.SubmitForm, FormId = "form_1", ApiReferences = refs.ToList() };

    private static UiApiReference Ref(string method, string url) =>
        new() { Method = method, Url = url };

    private static UiPage Page(string route, UiAction action, params UiForm[] forms) =>
        new() { Id = "page_1", RepositoryId = "frontend", Route = route, Actions = new List<UiAction> { action }, Forms = forms.ToList() };

    private static UiForm Form(params UiField[] fields) =>
        new() { Id = "form_1", Fields = fields.ToList() };

    private static UiField Field(string name, bool? required = null) =>
        new() { Id = $"field_{name}", Name = name, Required = required };

    private CorrelationSkeleton Correlate(TechnicalSkeleton technical, UiSkeleton ui, params SourceFile[] sourceFiles) =>
        _engine.Correlate(technical, ui, sourceFiles, "job-1");

    [Fact]
    public void PathNormalizationStripsDomainAndQuery()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel> { Endpoint("POST", "/api/invoices", "InvoicesController", "Create") },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices/new", Action(Ref("POST", "http://localhost:5000/api/invoices?page=1"))) },
        };

        var result = Correlate(technical, ui);

        Assert.Single(result.Correlations);
        Assert.Equal("/api/invoices", result.Correlations[0].Backend.PathTemplate);
    }

    [Fact]
    public void TemplateMatchingDynamicSegments()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel> { Endpoint("GET", "/api/invoices/{id}", "InvoicesController", "Get") },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices", Action(Ref("GET", "/api/invoices/123"))) },
        };

        var result = Correlate(technical, ui);

        Assert.Single(result.Correlations);
        Assert.Equal("/api/invoices/{id}", result.Correlations[0].Backend.PathTemplate);
    }

    [Fact]
    public void ControllerPlaceholderExpansion()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel> { Endpoint("POST", "/api/[controller]", "InvoicesController", "Create") },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices/new", Action(Ref("POST", "/api/invoices"))) },
        };

        var result = Correlate(technical, ui);

        Assert.Single(result.Correlations);
        Assert.Equal("/api/invoices", result.Correlations[0].Backend.PathTemplate);
    }

    [Fact]
    public void ConfidenceScoringAtLeastPointSevenForExactMatch()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel> { Endpoint("POST", "/api/invoices", "InvoicesController", "Create") },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices/new", Action(Ref("POST", "/api/invoices"))) },
        };

        var result = Correlate(technical, ui);

        var correlation = Assert.Single(result.Correlations);
        Assert.True(correlation.Confidence >= 0.7, $"confidence {correlation.Confidence} should be >= 0.7");
    }

    [Fact]
    public void MultiMatchAmbiguityProducesMultipleCandidates()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel>
            {
                Endpoint("POST", "/api/invoices", "InvoicesController", "Create"),
                Endpoint("POST", "/api/invoices", "LegacyInvoicesController", "CreateLegacy"),
            },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices/new", Action(Ref("POST", "/api/invoices"))) },
        };

        var result = Correlate(technical, ui);

        Assert.Equal(2, result.Correlations.Count);
        Assert.Contains(result.Diagnostics, d => d.Code == "corr.ambiguous");
    }

    [Fact]
    public void FieldMappingCaseAndSnakeTransform()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel>
            {
                Endpoint("POST", "/api/invoices", "InvoicesController", "Create",
                    Dto(("CustomerId", true), ("TotalAmount", true))),
            },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage>
            {
                Page("/invoices/new",
                    Action(Ref("POST", "/api/invoices")),
                    Form(Field("customerId", required: true), Field("total_amount", required: true))),
            },
        };

        var result = Correlate(technical, ui);

        var correlation = Assert.Single(result.Correlations);
        Assert.Contains(correlation.FieldMapping.Mappings, m => m.UiFieldName == "customerId" && m.DtoProperty == "CustomerId");
        Assert.Contains(correlation.FieldMapping.Mappings, m => m.UiFieldName == "total_amount" && m.DtoProperty == "TotalAmount");
    }

    [Fact]
    public void MissingRequiredDtoPropertyDetected()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel>
            {
                Endpoint("POST", "/api/invoices", "InvoicesController", "Create",
                    Dto(("CustomerId", true), ("IssueDate", true))),
            },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage>
            {
                Page("/invoices/new", Action(Ref("POST", "/api/invoices")), Form(Field("customerId", required: true))),
            },
        };

        var result = Correlate(technical, ui);

        var correlation = Assert.Single(result.Correlations);
        Assert.Contains(correlation.FieldMapping.MissingRequiredOnUi, m => m.DtoProperty == "IssueDate");
    }

    [Fact]
    public void DoesNotInferUiRequiredWhenOnlyBackendRequires()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel>
            {
                Endpoint("POST", "/api/invoices", "InvoicesController", "Create", Dto(("CustomerId", true))),
            },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage>
            {
                Page("/invoices/new", Action(Ref("POST", "/api/invoices")), Form(Field("customerId", required: null))),
            },
        };

        var result = Correlate(technical, ui);

        var correlation = Assert.Single(result.Correlations);
        Assert.Contains("CustomerId", correlation.FieldMapping.RequiredOnBackendButNotMarkedRequiredOnUi);
        Assert.Null(ui.Pages[0].Forms[0].Fields[0].Required);
    }

    [Fact]
    public void NoBackendEndpointsReturnsNotApplicable()
    {
        var technical = new TechnicalSkeleton { Endpoints = new List<EndpointModel>() };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/x", Action(Ref("POST", "/api/x"))) },
        };

        var result = Correlate(technical, ui);

        Assert.Equal(CorrelationStatus.NotApplicable, result.Status);
        Assert.Empty(result.Correlations);
    }

    [Fact]
    public void PersistenceEvidenceFromDbContextUsage()
    {
        var source = new SourceFile
        {
            RelativePath = "backend/Controllers/InvoicesController.cs",
            Content = """
                public class InvoicesController : ControllerBase
                {
                    [HttpPost]
                    public IActionResult Create([FromBody] CreateInvoiceRequest request)
                    {
                        _db.Invoices.Add(new Invoice { Id = Guid.NewGuid() });
                        _db.SaveChanges();
                        return Ok();
                    }
                }
                """,
        };
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel> { Endpoint("POST", "/api/invoices", "InvoicesController", "Create") },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices/new", Action(Ref("POST", "/api/invoices"))) },
        };

        var result = Correlate(technical, ui, source);

        var correlation = Assert.Single(result.Correlations);
        Assert.Equal(PersistenceKind.Evidence, correlation.Persistence.Kind);
        Assert.Equal("create", correlation.Persistence.Operation);
        Assert.Equal("Invoice", correlation.Persistence.EntityType);
    }

    [Fact]
    public void NoAbsolutePathsInOutput()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = new List<EndpointModel> { Endpoint("POST", "/api/invoices", "InvoicesController", "Create") },
        };
        var ui = new UiSkeleton
        {
            Pages = new List<UiPage> { Page("/invoices/new", Action(Ref("POST", "/api/invoices"))) },
        };

        var result = Correlate(technical, ui);
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.DoesNotContain("C:\\", json);
        Assert.DoesNotContain("/home/", json);
    }
}
