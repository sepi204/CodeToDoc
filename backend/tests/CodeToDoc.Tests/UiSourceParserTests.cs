using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class UiSourceParserTests
{
    private readonly UiSourceParser _parser = new();

    private static ParsedUiSource Parse(string content) =>
        new UiSourceParser().Parse(
            new SourceFile { RelativePath = "src/app/page.tsx", Content = content },
            new FrontendProject { Id = "frontend", Framework = "next-app-router" });

    [Fact]
    public void PersianLabelsPreserved()
    {
        var source = Parse("""
            <form onSubmit={handleSubmit}>
              <label htmlFor="lastName">نام خانوادگی</label>
              <input id="lastName" name="lastName" />
            </form>
            """);

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.Equal("نام خانوادگی", field.Label);
    }

    [Fact]
    public void HtmlForLabelResolution()
    {
        var source = Parse("""
            <form>
              <label htmlFor="email">Email</label>
              <input id="email" name="email" type="email" />
            </form>
            """);

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.Equal("Email", field.Label);
        Assert.Equal(InputKind.Email, field.InputType);
    }

    [Fact]
    public void PlaceholderKeptSeparate()
    {
        var source = Parse("""
            <form>
              <label htmlFor="name">Name</label>
              <input id="name" name="name" placeholder="Enter name" />
            </form>
            """);

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.Equal("Name", field.Label);
        Assert.Equal("Enter name", field.Placeholder);
    }

    [Fact]
    public void NativeRequiredFieldDetection()
    {
        var source = Parse("""<form><input name="lastName" required /></form>""");

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.True(field.Required);
        Assert.Contains(field.ValidationRules, r => r.Kind == "required");
    }

    [Fact]
    public void SubmitButtonAssociatedWithForm()
    {
        var source = Parse("""
            <form onSubmit={handleSubmit}>
              <input name="lastName" />
              <button type="submit">ثبت نهایی</button>
            </form>
            """);

        var form = source.Forms[0];
        Assert.Equal("handleSubmit", form.SubmitHandler);

        var action = Assert.Single(source.Actions);
        Assert.Equal("ثبت نهایی", action.Label);
        Assert.Equal(ActionKind.SubmitForm, action.Kind);
        Assert.Contains(action.Id, form.SubmitActionIds);
    }

    [Fact]
    public void ButtonOutsideFormNotAssociated()
    {
        var source = Parse("""<button onClick={doThing}>Do</button>""");

        var action = Assert.Single(source.Actions);
        Assert.Equal(ActionKind.Unknown, action.Kind);
    }

    [Fact]
    public void StaticTableColumns()
    {
        var source = Parse("""
            <table>
              <thead><tr><th>شناسه</th><th>نام</th><th>وضعیت</th></tr></thead>
            </table>
            """);

        var table = Assert.Single(source.Tables);
        Assert.Equal(new[] { "شناسه", "نام", "وضعیت" }, table.Columns.Select(c => c.Label));
    }

    [Fact]
    public void StaticNavigationLinks()
    {
        var source = Parse("""<Link href="/customers/new">مشتری جدید</Link>""");

        var nav = Assert.Single(source.Navigation);
        Assert.Equal("مشتری جدید", nav.Label);
        Assert.Equal("/customers/new", nav.Target);
    }

    [Fact]
    public void ConditionalVisibilityPreserved()
    {
        var source = Parse("""<input name="x" disabled />""");

        var field = Assert.Single(source.StandaloneFields);
        Assert.Equal(VisibilityState.Hidden, field.Visibility.State);
    }

    [Fact]
    public void FetchMethodAndUrlExtraction()
    {
        var source = Parse("""const save = () => fetch("/api/customers", { method: "POST" });""");

        var fetch = Assert.Single(source.Fetches);
        Assert.Equal("POST", fetch.Method);
        Assert.Equal("/api/customers", fetch.Url);
    }

    [Fact]
    public void DynamicUrlRecordedAsUnresolved()
    {
        var source = Parse("const get = (id) => fetch(`/api/customers/${id}`);");

        var fetch = Assert.Single(source.Fetches);
        Assert.Null(fetch.Url);
        Assert.NotNull(fetch.UrlExpression);
    }

    [Fact]
    public void TitleFromH1Preserved()
    {
        var source = Parse("""<h1>مشتری جدید</h1>""");
        Assert.Equal("مشتری جدید", source.Title);
    }

    [Fact]
    public void ZodEmailAndMaxLengthRules()
    {
        var source = Parse("""
            const schema = z.object({
              email: z.string().email().max(100),
            });
            <form><input name="email" /></form>
            """);

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.Contains(field.ValidationRules, r => r.Kind == "email");
        Assert.Contains(field.ValidationRules, r => r.Kind == "maxLength" && (int)r.Value! == 100);
    }

    [Fact]
    public void ZodMinOneImpliesNonEmpty()
    {
        var source = Parse("""
            const schema = z.object({ name: z.string().min(1) });
            <form><input name="name" /></form>
            """);

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.True(field.Required);
    }

    [Fact]
    public void ZodStringAloneDoesNotImplyNonEmpty()
    {
        var source = Parse("""
            const schema = z.object({ name: z.string() });
            <form><input name="name" /></form>
            """);

        var field = Assert.Single(source.Forms[0].Fields);
        Assert.Null(field.Required);
        Assert.DoesNotContain(field.ValidationRules, r => r.Kind == "required" || r.Kind == "minLength");
    }

    [Fact]
    public void ReactHookFormRegisterRules()
    {
        var source = Parse("""
            <input {...register("lastName", { required: true, minLength: 2 })} />
            """);

        var field = Assert.Single(source.StandaloneFields);
        Assert.Contains(field.ValidationRules, r => r.Kind == "required");
        Assert.Contains(field.ValidationRules, r => r.Kind == "minLength" && (int)r.Value! == 2);
    }
}
