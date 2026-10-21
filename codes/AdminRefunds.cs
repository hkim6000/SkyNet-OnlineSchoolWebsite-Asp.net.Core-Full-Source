using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminRefunds : WebPage
    {
        private const string AreaKey = "refunds";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Refunds | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static (string Kind, string Text) StatusPill(RefundRequest r)
        {
            switch (r.Status)
            {
                case "open": return ("new", "Waiting");
                case "processing": return ("wait", "Processing");
                case "approved": return ("go", "Refunded in full");
                case "partial": return ("wait", "Part refund");
                case "declined": return ("bad", "Declined");
                default: return ("off", "Withdrawn");
            }
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"rf-chip" + (key == cur ? " rf-act" : string.Empty) + "\" onclick=\"AdminRefundsJs.chip('" + key + "')\">" + label + "</button>";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminRefundsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "rf", "AdminRefunds", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("rf-main", Ad.NoAccess("rf", "refunds"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rf-ph\"><div><h1>Refunds</h1><p>Every request is checked by staff. Conditions: " + Ui.Esc(Learn.ConditionsText(site)) + "</p></div></div>");
            sb.Append("<div class=\"rf-note rf-note-" + (Store.Demo() ? "wait" : "ok") + "\">" + (Store.Demo() ? "Demo mode: approving simulates the refund, no money moves." : "Production: approving sends the refund to Stripe and saves the refund id.") + "</div>");
            sb.Append("<div class=\"rf-tools\"><input type=\"search\" id=\"rf-q\" placeholder=\"Search student, course or order\" aria-label=\"Search\" oninput=\"AdminRefundsJs.search()\"><div class=\"rf-chips\" id=\"rf-chips\">" + Chip("open", "Waiting", "open") + Chip("done", "Decided", "open") + Chip("all", "All", "open") + "</div></div>");
            sb.Append("<div id=\"rf-list\"></div>");
            response.SetElementContents("rf-main", sb.ToString());
            response.ExecuteScript("AdminRefundsJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminRefundsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminRefundsJs", c, "view refunds", AreaKey, "You do not have access to refunds.");
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "rf", "AdminRefunds", c, site);
            string st = GetDataValue("st") ?? "open";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            response.SetElementContents("rf-chips", Chip("open", "Waiting", st) + Chip("done", "Decided", st) + Chip("all", "All", st));
            List<RefundRequest> all = await Store.Rows<RefundRequest>("refunds");
            List<Order> orders = await Store.Rows<Order>("orders");
            IEnumerable<RefundRequest> rows = all.OrderBy(r => r.Status == "open" ? 0 : 1).ThenByDescending(r => r.Created);
            if (st == "open")
            {
                rows = rows.Where(r => r.Status == "open" || r.Status == "processing");
            }
            else if (st == "done")
            {
                rows = rows.Where(r => r.Status != "open" && r.Status != "processing");
            }
            if (q.Length > 0)
            {
                rows = rows.Where(r => Ui.Has(r.StudentName, q) || Ui.Has(r.CourseTitle, q) || Ui.Has(r.OrderId, q));
            }
            List<RefundRequest> list = rows.ToList();
            int pages = Ad.Pages(list.Count);
            pg = Math.Min(pg, pages);
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"rf-empty\">" + (st == "open" ? "No refund requests are waiting." : "No requests match.") + "</div>");
            }
            else
            {
                sb.Append("<div class=\"rf-wrapt\"><table class=\"rf-table\"><thead><tr><th>Requested</th><th>Student</th><th>Course</th><th>Order</th><th class=\"rf-num\">Paid</th><th>Policy</th><th>Status</th></tr></thead><tbody>");
                foreach (RefundRequest r in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    Order? o = orders.FirstOrDefault(x => x.Id == r.OrderId);
                    (string kind, string text) = StatusPill(r);
                    sb.Append("<tr class=\"rf-row\" tabindex=\"0\" onclick=\"AdminRefundsJs.open('" + Ui.Js(r.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminRefundsJs.open('" + Ui.Js(r.Id) + "')}\">");
                    sb.Append("<td data-l=\"Requested\">" + Ui.Day(r.Created) + "</td><td data-l=\"Student\"><b>" + Ui.Esc(r.StudentName) + "</b></td><td data-l=\"Course\">" + Ui.Esc(r.CourseTitle) + "</td><td data-l=\"Order\">" + Ui.Esc(r.OrderId) + "</td>");
                    sb.Append("<td data-l=\"Paid\" class=\"rf-num\">" + (o == null ? "-" : Ui.Money(o.Amount)) + "</td><td data-l=\"Policy\">" + Ad.Pill("rf", r.Eligible ? "go" : "wait", r.Eligible ? "Eligible" : "Outside policy") + "</td><td data-l=\"Status\">" + Ad.Pill("rf", kind, text) + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("rf", "AdminRefundsJs", pg, list.Count, "request", "requests"));
            }
            response.SetElementContents("rf-list", sb.ToString());
            return response;
        }

        private static string Check(bool ok, string label, string detail)
        {
            return "<li class=\"" + (ok ? "rf-ok" : "rf-bad") + "\"><b>" + (ok ? "Met" : "Not met") + "</b> <span>" + Ui.Esc(label) + "</span><small class=\"rf-sub\">" + Ui.Esc(detail) + "</small></li>";
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminRefundsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminRefundsJs", c, "view refund", AreaKey, "You do not have access to refunds.");
            }
            SiteData site = await Store.LoadSite();
            RefundRequest? r = (await Store.Rows<RefundRequest>("refunds")).FirstOrDefault(x => x.Id == GetDataValue("rid"));
            Order? o = r == null ? null : (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == r.OrderId);
            if (r == null || o == null)
            {
                response.ExecuteScript("AdminRefundsJs.toast('That request was not found.');");
                return response;
            }
            decimal left = Learn.Remaining(o);
            (string kind, string text) = StatusPill(r);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rf-stack\"><div class=\"rf-line\">" + Ad.Pill("rf", kind, text) + Ad.Pill("rf", r.Eligible ? "go" : "wait", r.Eligible ? "Eligible" : "Outside policy") + "</div>");
            sb.Append("<dl class=\"rf-kv\"><dt>Student</dt><dd>" + Ui.Esc(r.StudentName) + "</dd><dt>Course</dt><dd>" + Ui.Esc(r.CourseTitle) + "</dd><dt>Order</dt><dd>" + Ui.Esc(o.Id) + " &middot; paid " + Ui.Money(o.Amount) + " on " + Ui.Day(o.Paid ?? o.Created) + "</dd><dt>Refunded so far</dt><dd>" + Ui.Money(Learn.Refunded(o)) + "</dd><dt>Left to refund</dt><dd>" + Ui.Money(left) + "</dd><dt>Requested</dt><dd>" + Ui.When(r.Created) + "</dd><dt>Reason</dt><dd>" + Ui.Esc(r.Reason) + "</dd></dl>");
            sb.Append("<div><h3>Conditions when the request was made</h3><ul class=\"rf-checks\">");
            sb.Append(Check(r.DaysUsed <= site.RefundDays, "Within " + site.RefundDays + " days", r.DaysUsed + " days after payment"));
            sb.Append(Check(r.Progress < site.ProgressLimit, "Progress under " + site.ProgressLimit + "%", "The student had finished " + r.Progress + "%"));
            sb.Append(Check(!(site.NoCertificate && r.HadCertificate), "No certificate issued", r.HadCertificate ? "A certificate had been issued" : "No certificate"));
            sb.Append("</ul><p class=\"rf-hint\">These checks help you decide. You can approve or decline any request.</p></div>");
            if (o.Refunds.Count > 0)
            {
                sb.Append("<div><h3>Refunds on this order</h3><ul class=\"rf-rowlist\">");
                foreach (RefundLine l in o.Refunds)
                {
                    sb.Append("<li><div><b>" + Ui.Money(l.Amount) + "</b><small class=\"rf-sub\">" + Ui.When(l.When) + " &middot; " + Ui.Esc(l.Source) + " &middot; " + Ui.Esc(l.Id) + "</small></div></li>");
                }
                sb.Append("</ul></div>");
            }
            if (r.Status == "open" || r.Status == "processing")
            {
                if (!Access.Can(c.U, AreaKey, 'E'))
                {
                    sb.Append("<p class=\"rf-note\">You can view this request. Deciding needs the Edit right on Refunds.</p>");
                }
                else if (left <= 0)
                {
                    sb.Append("<p class=\"rf-note rf-note-bad\">Nothing is left to refund on this order.</p>");
                }
                else
                {
                    string def = r.Eligible ? "full" : "decline";
                    sb.Append("<div class=\"rf-stack\"><h3>Decision</h3><div class=\"rf-radio\">");
                    sb.Append("<label><input type=\"radio\" name=\"rf-kind\" value=\"full\"" + (def == "full" ? " checked" : string.Empty) + " onchange=\"AdminRefundsJs.kind()\"><span><b>Full refund, " + Ui.Money(left) + "</b><small>Access ends and progress is kept but locked. A certificate is revoked.</small></span></label>");
                    sb.Append("<label><input type=\"radio\" name=\"rf-kind\" value=\"partial\" onchange=\"AdminRefundsJs.kind()\"><span><b>Partial refund (goodwill)</b><small>The student keeps access. At most " + Ui.Money(left - 0.01m) + ".</small></span></label>");
                    sb.Append("<label><input type=\"radio\" name=\"rf-kind\" value=\"decline\"" + (def == "decline" ? " checked" : string.Empty) + " onchange=\"AdminRefundsJs.kind()\"><span><b>Decline</b><small>Write the reason. The student sees it.</small></span></label></div>");
                    sb.Append("<label class=\"rf-field\" id=\"rf-amtbox\" hidden><span>Amount to refund (USD)</span><input type=\"number\" id=\"rf-amt\" min=\"0.01\" step=\"0.01\" max=\"" + (left - 0.01m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "\" oninput=\"AdminRefundsJs.markDirty()\"></label>");
                    sb.Append("<label class=\"rf-field\"><span id=\"rf-notelabel\">Note to the student</span><textarea id=\"rf-note\" rows=\"3\" maxlength=\"300\" oninput=\"AdminRefundsJs.markDirty()\"></textarea></label>");
                    sb.Append("<p class=\"rf-hint\">" + (Store.Demo() ? "Demo mode: the refund is simulated and no money moves." : "Production: the refund is made through Stripe and its refund id is saved here.") + "</p>");
                    sb.Append("<div class=\"rf-acts\"><button type=\"button\" class=\"rf-btn\" onclick=\"AdminRefundsJs.decide('" + Ui.Js(r.Id) + "', " + (left - 0.01m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ", " + left.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")\">Save decision</button><button type=\"button\" class=\"rf-btn rf-btn-ghost\" onclick=\"AdminRefundsJs.closeModal()\">Close</button></div></div>");
                }
            }
            else
            {
                string by = r.By == string.Empty ? string.Empty : await Store.Who(r.By);
                sb.Append("<div><h3>Outcome</h3><dl class=\"rf-kv\"><dt>Result</dt><dd>" + Ui.Esc(text) + "</dd>");
                if (r.Amount > 0)
                {
                    sb.Append("<dt>Amount</dt><dd>" + Ui.Money(r.Amount) + "</dd><dt>Refund id</dt><dd>" + Ui.Esc(r.RefundId) + "</dd>");
                }
                if (r.Decided.HasValue)
                {
                    sb.Append("<dt>When</dt><dd>" + Ui.When(r.Decided.Value) + "</dd>");
                }
                if (by != string.Empty)
                {
                    sb.Append("<dt>By</dt><dd>" + Ui.Esc(by) + "</dd>");
                }
                if (r.Note != string.Empty)
                {
                    sb.Append("<dt>Note</dt><dd>" + Ui.Esc(r.Note) + "</dd>");
                }
                sb.Append("</dl></div><div class=\"rf-acts\">");
                if (r.Status == "declined" && Access.Can(c.U, AreaKey, 'X'))
                {
                    sb.Append("<button type=\"button\" class=\"rf-btn rf-btn-ghost\" onclick=\"AdminRefundsJs.reopen('" + Ui.Js(r.Id) + "')\">Reopen this request</button>");
                }
                sb.Append("<button type=\"button\" class=\"rf-btn rf-btn-ghost\" onclick=\"AdminRefundsJs.closeModal()\">Close</button></div>");
            }
            sb.Append("</div>");
            response.SetElementContents("rf-mbody", sb.ToString());
            response.ExecuteScript("AdminRefundsJs.opened('" + Ui.Js("Request " + r.OrderId) + "');");
            return response;
        }

        public async Task<ApiResponse> Decide()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminRefundsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("rid") ?? string.Empty;
            string kind = GetDataValue("kind") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminRefundsJs", c, "decide refund", id, "You do not have the Edit right on Refunds.");
            }
            decimal amount = Ui.Dec(GetDataValue("amount"), 0);
            (bool ok, string message) = await Desk.Decide(c, id, kind, amount, GetDataValue("note") ?? string.Empty);
            response.ExecuteScript("AdminRefundsJs.decided(" + (ok ? "true" : "false") + ", '" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> Reopen()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminRefundsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("rid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminRefundsJs", c, "reopen refund", id, "Reopening needs the Extra right on Refunds.");
            }
            RefundRequest? before = (await Store.Rows<RefundRequest>("refunds")).FirstOrDefault(x => x.Id == id);
            if (before == null || before.Status != "declined")
            {
                response.ExecuteScript("AdminRefundsJs.toast('Only a declined request can be reopened.');");
                return response;
            }
            bool done = await Desk.Reopen(id);
            if (done)
            {
                await Store.Log(c.U.Id, "refund reopened", before.OrderId + " · " + before.StudentName, true);
            }
            response.ExecuteScript("AdminRefundsJs.reopened(" + (done ? "true" : "false") + ");");
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
