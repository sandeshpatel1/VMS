using System.Security.Claims;
using DwaarixVMS.Data;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Helpers;

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public static int? ResidentId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("ResidentId"), out var id) ? id : null;
}

public static class Ui
{
    public static string StatusClass(string status) => status switch
    {
        VisitStatus.CheckedIn => "in",
        VisitStatus.CheckedOut => "out",
        VisitStatus.Pending => "pending",
        VisitStatus.Approved => "ok",
        VisitStatus.Rejected => "bad",
        _ => "muted"
    };

    public static string StatusText(string status) => status switch
    {
        VisitStatus.CheckedIn => "IN",
        VisitStatus.CheckedOut => "OUT",
        _ => status
    };

    public static string StatusLabel(string status) => status switch
    {
        VisitStatus.CheckedIn => "Inside",
        VisitStatus.CheckedOut => "Checked out",
        _ => status
    };

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        return parts.Length == 1 ? parts[0][..1].ToUpper() : (parts[0][..1] + parts[^1][..1]).ToUpper();
    }

    public static string Dt(DateTime? d) => d.HasValue ? d.Value.ToString("dd MMM yyyy, hh:mm tt") : "-";
    public static string Tm(DateTime? d) => d.HasValue ? d.Value.ToString("hh:mm tt") : "-";

    public static IHtmlContent Icon(this IHtmlHelper html, string name) =>
        new HtmlString($"<svg class=\"ic\" aria-hidden=\"true\"><use href=\"#i-{name}\"/></svg>");

    public static string CsvCell(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}

public static class CodeGen
{
    /// <summary>Visitor ID such as V261019001 (V + yyMMdd + running number for the day).</summary>
    public static async Task<string> NextVisitorCodeAsync(AppDbContext db)
    {
        var prefix = "V" + DateTime.Now.ToString("yyMMdd");
        var last = await db.Visitors.Where(v => v.VisitorCode.StartsWith(prefix))
            .OrderByDescending(v => v.VisitorCode).Select(v => v.VisitorCode).FirstOrDefaultAsync();
        var next = last == null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return prefix + next.ToString("000");
    }
}
