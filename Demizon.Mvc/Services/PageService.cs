namespace Demizon.Mvc.Services;

public class PageService
{
    private const string BasePageTitle = "FS Demižón";

    private string? PageTitle { get; set; }

    public string GetTitle()
    {
        return PageTitle is null ? BasePageTitle : $"{PageTitle} | {BasePageTitle}";
    }

    public void SetTitle(string? pageTitle)
    {
        PageTitle = pageTitle;
    }
}
