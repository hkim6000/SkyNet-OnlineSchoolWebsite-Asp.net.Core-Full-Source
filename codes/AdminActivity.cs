using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminActivity : WebPage
    {
        private const string AreaKey = "activity";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Activity | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"ay-chip" + (key == cur ? " ay-act" : string.Empty) + "\" onclick=\"AdminActivityJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("refused", "Refused or failed", cur);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminActivityJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "ay", "AdminActivity", c, site);
            bool all = Access.Can(c.U, AreaKey, 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ay-ph\"><div><h1>" + (all ? "Activity log" : "My activity") + "</h1><p>" + (all ? "Every change and every refused attempt, newest first." : "What you did on the site, newest first.") + "</p></div></div>");
            sb.Append("<div class=\"ay-tools\"><input type=\"search\" id=\"ay-q\" placeholder=\"Search action, person or item\" aria-label=\"Search\" oninput=\"AdminActivityJs.search()\"><div class=\"ay-chips\" id=\"ay-chips\">" + Chips("all") + "</div></div><div id=\"ay-list\"></div>");
            response.SetElementContents("ay-main", sb.ToString());
            response.ExecuteScript("AdminActivityJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminActivityJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            bool everyone = Access.Can(c.U, AreaKey, 'V');
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            List<LogEntry> log = await Store.Rows<LogEntry>("activity");
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (string id in log.Select(l => l.UserId).Distinct())
            {
                names[id] = await Store.Who(id);
            }
            IEnumerable<LogEntry> rows = log.Where(l => everyone || l.UserId == c.U.Id);
            if (st == "refused")
            {
                rows = rows.Where(l => !l.Ok);
            }
            if (q.Length > 0)
            {
                rows = rows.Where(l => Ui.Has(l.Action, q) || Ui.Has(l.Target, q) || Ui.Has(names[l.UserId], q));
            }
            List<LogEntry> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            response.SetElementContents("ay-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"ay-empty\">Nothing to show.</div>");
            }
            else
            {
                sb.Append("<div class=\"ay-wrapt\"><table class=\"ay-table\"><thead><tr><th>When</th><th>Who</th><th>Action</th><th>Item</th><th>Result</th></tr></thead><tbody>");
                foreach (LogEntry l in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    sb.Append("<tr><td data-l=\"When\">" + Ui.When(l.When) + "</td><td data-l=\"Who\">" + Ui.Esc(names[l.UserId]) + "</td><td data-l=\"Action\"><b>" + Ui.Esc(l.Action) + "</b></td><td data-l=\"Item\">" + Ui.Esc(l.Target) + "</td><td data-l=\"Result\">" + Ad.Pill("ay", l.Ok ? "go" : "bad", l.Ok ? "Done" : "Refused") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("ay", "AdminActivityJs", pg, list.Count, "entry", "entries"));
            }
            response.SetElementContents("ay-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.SignOut(GetDataValue("tok"));
            return response;
        }
    }
}
