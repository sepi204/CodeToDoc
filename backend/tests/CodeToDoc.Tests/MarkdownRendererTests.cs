using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class MarkdownRendererTests
{
    private static TechnicalModuleResponse TechnicalResponse() => new()
    {
        Overview = { new SynthesisStatement { Text = "Invoice management module.", Certainty = "derived" } },
        Technologies = { new SynthesisStatement { Text = "ASP.NET Core, Entity Framework Core.", Certainty = "explicit" } },
        Authentication = { new SynthesisStatement { Text = "JWT Bearer authentication.", Certainty = "explicit" } },
        Limitations = { "Runtime versions were not detected." },
    };

    private static SynthesisPayload TechnicalPayload() => new()
    {
        DocumentType = "technical",
        ModuleId = "invoices",
        Summary = "ASP.NET Core, ORM: Entity Framework Core, Database: PostgreSQL",
        Endpoints =
        {
            new PayloadEndpoint
            {
                EndpointId = "ep_0", Method = "POST", Path = "/api/invoices", ControllerName = "InvoicesController",
                AuthRequired = true, Roles = { "Admin" },
                RequestFields = { new PayloadField { Name = "CustomerId", Type = "string", IsRequired = true } },
                ResponseFields = { new PayloadField { Name = "Id", Type = "guid" } },
                PersistenceKind = "evidence", PersistenceOperation = "create", PersistenceEntity = "Invoice",
            },
        },
    };

    [Fact]
    public void TechnicalEndpointTableRendered()
    {
        var markdown = MarkdownRenderer.RenderTechnical(TechnicalPayload(), TechnicalResponse(), partial: false);

        Assert.Contains("POST /api/invoices", markdown);
        Assert.Contains("CustomerId", markdown);
        Assert.Contains("Admin", markdown);
        Assert.Contains("Entity Framework Core", markdown);
    }

    [Fact]
    public void UnknownResponseModelIsNotInvented()
    {
        var payload = TechnicalPayload();
        payload.Endpoints[0].ResponseFields.Clear();

        var markdown = MarkdownRenderer.RenderTechnical(payload, TechnicalResponse(), partial: false);

        Assert.DoesNotContain("Response fields", markdown);
    }

    [Fact]
    public void PartialStatusCreatesLimitationsNotice()
    {
        var markdown = MarkdownRenderer.RenderTechnical(TechnicalPayload(), TechnicalResponse(), partial: true);

        Assert.Contains("partial", markdown);
    }

    [Fact]
    public void PersianUserGuideContentPreserved()
    {
        var payload = new SynthesisPayload
        {
            DocumentType = "userGuide",
            Pages =
            {
                new PayloadPage
                {
                    PageId = "page_1", Route = "/invoices/new", Title = "صدور فاکتور",
                    Forms = { new PayloadForm { FormId = "form_1", Fields = { new PayloadField { Name = "customerId", Label = "شناسه مشتری", IsRequired = true } } } },
                },
            },
        };
        var response = new UserGuideModuleResponse
        {
            Pages = { new UserGuidePageSection { PageId = "page_1", DisplayTitle = "صدور فاکتور", Forms = { new UserGuideFormSection { FormId = "form_1", Steps = { new UserGuideStep { Order = 1, Text = "فیلد «شناسه مشتری» را تکمیل کنید." } } } } } },
        };

        var markdown = MarkdownRenderer.RenderUserGuide(payload, response, partial: false);

        Assert.Contains("شناسه مشتری", markdown);
        Assert.Contains("صدور فاکتور", markdown);
    }

    [Fact]
    public void MarkdownTableDelimitersEscaped()
    {
        var response = TechnicalResponse();
        response.Overview[0].Text = "Handles invoices | payments";

        var markdown = MarkdownRenderer.RenderTechnical(TechnicalPayload(), response, partial: false);

        Assert.Contains("Handles invoices \\| payments", markdown);
    }

    [Fact]
    public void StableInputProducesStableMarkdown()
    {
        var first = MarkdownRenderer.RenderTechnical(TechnicalPayload(), TechnicalResponse(), partial: false);
        var second = MarkdownRenderer.RenderTechnical(TechnicalPayload(), TechnicalResponse(), partial: false);

        Assert.Equal(first, second);
    }
}
