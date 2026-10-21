using System.Text;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.Core
{
    public static class Ad
    {
        public const int Size = 12;

        public static async Task Shell(ApiResponse response, string px, string page, Ctx c, SiteData site)
        {
            response.SetElementContents(px + "-brand", Ui.Esc(site.Brand));
            response.SetElementContents(px + "-menu", await Auth.Menu(px, page, c));
            response.SetElementContents(px + "-who", Auth.Who(px, c));
        }

        public static string NoAccess(string px, string what)
        {
            return "<div class=\"" + px + "-panel\"><h1>No access</h1><p class=\"" + px + "-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        public static HashSet<string>? OwnIds(SiteData site, Staff u, string area)
        {
            if (!Access.IsOwn(u, area))
            {
                return null;
            }
            return site.Courses.Where(c => c.InstructorId == u.ProviderId).Select(c => c.Id).ToHashSet();
        }

        public static string Opts(IEnumerable<(string Key, string Name)> items, string current, string blank)
        {
            StringBuilder sb = new StringBuilder();
            if (blank != string.Empty)
            {
                sb.Append("<option value=\"\">" + Ui.Esc(blank) + "</option>");
            }
            foreach ((string key, string name) in items)
            {
                sb.Append("<option value=\"" + Ui.Esc(key) + "\"" + (key == current ? " selected" : string.Empty) + ">" + Ui.Esc(name) + "</option>");
            }
            return sb.ToString();
        }

        public static string MediaSelect(string px, string id, string ns, List<MediaItem> media, string kind, string current)
        {
            string preview = kind == "photo" ? "<img class=\"" + px + "-prev\" id=\"" + id + "-prev\" src=\"" + Ui.Esc(current) + "\" alt=\"\"" + (current == string.Empty ? " hidden" : string.Empty) + ">" : string.Empty;
            List<(string, string)> items = media.Where(m => m.Kind == kind).OrderBy(m => m.Name).Select(m => (m.Path, m.Name)).ToList();
            if (current != string.Empty && items.All(i => i.Item1 != current))
            {
                items.Insert(0, (current, current));
            }
            return "<select id=\"" + id + "\" onchange=\"" + ns + ".picked('" + id + "')\">" + Opts(items, current, kind == "photo" ? "No picture" : "No video") + "</select>" + preview;
        }

        public static int Pages(int total)
        {
            return Math.Max(1, (total + Size - 1) / Size);
        }

        public static string Pager(string px, string ns, int page, int total, string one, string many)
        {
            int pages = Pages(total);
            if (total == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("<div class=\"" + px + "-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(total, one, many) + "</span><div>");
            sb.Append("<button type=\"button\" class=\"" + px + "-btn " + px + "-btn-sm " + px + "-btn-ghost\" " + (page <= 1 ? "disabled" : "onclick=\"" + ns + ".page(" + (page - 1) + ")\"") + ">Previous</button>");
            sb.Append("<button type=\"button\" class=\"" + px + "-btn " + px + "-btn-sm " + px + "-btn-ghost\" " + (page >= pages ? "disabled" : "onclick=\"" + ns + ".page(" + (page + 1) + ")\"") + ">Next</button></div></div>");
            return sb.ToString();
        }

        public static string Pill(string px, string kind, string text)
        {
            return "<span class=\"" + px + "-pill " + px + "-pill-" + kind + "\">" + Ui.Esc(text) + "</span>";
        }

        public static string Reason(string px, string text)
        {
            return "<small class=\"" + px + "-sub\">" + Ui.Esc(text) + "</small>";
        }

        public static async Task<ApiResponse> Refuse(ApiResponse response, string ns, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript(ns + ".toast('" + Ui.Js(message) + "');");
            return response;
        }

        public static async Task<Ctx?> Guard(ApiResponse response, string ns, string? tok)
        {
            Ctx? c = await Auth.Check(tok);
            if (c == null)
            {
                response.ExecuteScript(ns + ".expired();");
            }
            return c;
        }
    }
}
