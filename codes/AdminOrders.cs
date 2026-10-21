using System.Globalization;
using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminOrders : WebPage
    {
        private const string AreaKey = "orders";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Orders | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static (string Kind, string Text) StatusPill(Order o)
        {
            if (o.Status == "paid")
            {
                decimal back = Learn.Refunded(o);
                if (back <= 0)
                {
                    return ("go", "Paid");
                }
                return Learn.Remaining(o) <= 0 ? ("bad", "Refunded") : ("wait", "Part refunded");
            }
            return o.Status == "pending" ? ("new", "Unpaid") : ("off", "Cancelled");
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"od-chip" + (key == cur ? " od-act" : string.Empty) + "\" onclick=\"AdminOrdersJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("paid", "Paid", cur) + Chip("refunded", "Refunded", cur) + Chip("pending", "Unpaid", cur) + Chip("cancelled", "Cancelled", cur);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminOrdersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "od", "AdminOrders", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("od-main", Ad.NoAccess("od", "orders"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"od-ph\"><div><h1>Orders</h1><p>Payments, receipts and refunds on every order.</p></div></div>");
            sb.Append("<div class=\"od-tools\"><input type=\"search\" id=\"od-q\" placeholder=\"Search student, course or order\" aria-label=\"Search\" oninput=\"AdminOrdersJs.search()\"><div class=\"od-chips\" id=\"od-chips\">" + Chips("all") + "</div></div>");
            sb.Append("<div id=\"od-list\"></div>");
            response.SetElementContents("od-main", sb.ToString());
            response.ExecuteScript("AdminOrdersJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminOrdersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminOrdersJs", c, "view orders", AreaKey, "You do not have access to orders.");
            }
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            IEnumerable<Order> rows = (await Store.Rows<Order>("orders")).OrderByDescending(o => o.Created);
            switch (st)
            {
                case "paid": rows = rows.Where(o => o.Status == "paid" && Learn.Refunded(o) <= 0); break;
                case "refunded": rows = rows.Where(o => o.Status == "paid" && Learn.Refunded(o) > 0); break;
                case "pending": rows = rows.Where(o => o.Status == "pending"); break;
                case "cancelled": rows = rows.Where(o => o.Status == "cancelled"); break;
            }
            if (q.Length > 0)
            {
                rows = rows.Where(o => Ui.Has(o.StudentName, q) || Ui.Has(o.CourseTitle, q) || Ui.Has(o.Id, q) || Ui.Has(o.StudentEmail, q));
            }
            List<Order> list = rows.ToList();
            int pages = Ad.Pages(list.Count);
            pg = Math.Min(pg, pages);
            response.SetElementContents("od-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"od-empty\">No orders match.</div>");
            }
            else
            {
                sb.Append("<div class=\"od-wrapt\"><table class=\"od-table\"><thead><tr><th>Order</th><th>Date</th><th>Student</th><th>Course</th><th class=\"od-num\">Amount</th><th class=\"od-num\">Refunded</th><th>Status</th></tr></thead><tbody>");
                foreach (Order o in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    (string kind, string text) = StatusPill(o);
                    string back = Learn.Refunded(o) > 0 ? Ui.Money(Learn.Refunded(o)) : "-";
                    sb.Append("<tr class=\"od-row\" tabindex=\"0\" onclick=\"AdminOrdersJs.open('" + Ui.Js(o.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminOrdersJs.open('" + Ui.Js(o.Id) + "')}\"><td data-l=\"Order\"><b>" + Ui.Esc(o.Id) + "</b></td><td data-l=\"Date\">" + Ui.Day(o.Created) + "</td><td data-l=\"Student\">" + Ui.Esc(o.StudentName) + "<small>" + Ui.Esc(o.StudentEmail) + "</small></td><td data-l=\"Course\">" + Ui.Esc(o.CourseTitle) + "</td><td data-l=\"Amount\" class=\"od-num\">" + Ui.Money(o.Amount) + "</td><td data-l=\"Refunded\" class=\"od-num\">" + back + "</td><td data-l=\"Status\">" + Ad.Pill("od", kind, text) + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("od", "AdminOrdersJs", pg, list.Count, "order", "orders"));
            }
            response.SetElementContents("od-list", sb.ToString());
            return response;
        }

        private static string Detail(Ctx c, Order o)
        {
            (string kind, string text) = StatusPill(o);
            decimal back = Learn.Refunded(o);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"od-stack\"><div class=\"od-line\">" + Ad.Pill("od", kind, text) + "</div>");
            sb.Append("<table class=\"od-receipt\"><tbody><tr><td>" + Ui.Esc(o.CourseTitle) + "</td><td class=\"od-num\">" + Ui.Money(o.Amount) + "</td></tr>");
            foreach (RefundLine l in o.Refunds.OrderBy(x => x.When))
            {
                sb.Append("<tr><td>Refund " + Ui.Day(l.When) + " <small class=\"od-sub\">" + Ui.Esc(l.Source) + " &middot; " + Ui.Esc(l.Id) + "</small></td><td class=\"od-num od-money-neg\">-" + Ui.Money(l.Amount) + "</td></tr>");
            }
            sb.Append("<tr class=\"od-total\"><td>Net received</td><td class=\"od-num\">" + Ui.Money(o.Status == "paid" ? o.Amount - back : 0) + "</td></tr></tbody></table>");
            sb.Append("<dl class=\"od-kv\"><dt>Order</dt><dd>" + Ui.Esc(o.Id) + "</dd><dt>Student</dt><dd>" + Ui.Esc(o.StudentName) + " &middot; " + Ui.Esc(o.StudentEmail) + "</dd><dt>Created</dt><dd>" + Ui.When(o.Created) + "</dd>");
            if (o.Paid.HasValue)
            {
                sb.Append("<dt>Paid</dt><dd>" + Ui.When(o.Paid.Value) + " &middot; " + Ui.Esc(o.Method) + "</dd>");
            }
            if (o.Intent != string.Empty)
            {
                sb.Append("<dt>Payment id</dt><dd>" + Ui.Esc(o.Intent) + "</dd>");
            }
            sb.Append("</dl><div class=\"od-acts\">");
            if (o.Status == "pending" && Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"od-btn od-btn-danger\" onclick=\"AdminOrdersJs.cancel('" + Ui.Js(o.Id) + "')\">Cancel unpaid order</button>");
            }
            sb.Append("<button type=\"button\" class=\"od-btn od-btn-ghost\" onclick=\"AdminOrdersJs.closeModal()\">Close</button></div>");
            if (Store.Demo() && o.Status == "paid" && Learn.Remaining(o) > 0 && Access.Can(c.U, AreaKey, 'E'))
            {
                string max = Learn.Remaining(o).ToString("0.00", CultureInfo.InvariantCulture);
                sb.Append("<div class=\"od-demo\"><h3>Demo: refund made in the Stripe dashboard</h3><p class=\"od-hint\">Sends the webhook event Stripe would send when someone refunds this payment in the Stripe dashboard. The order, the enrollment and the certificate follow, as they do in Production.</p><div class=\"od-inline\"><label class=\"od-field\"><span>Amount (USD)</span><input type=\"number\" id=\"od-amt\" min=\"0.01\" step=\"0.01\" max=\"" + max + "\" value=\"" + max + "\"></label><button type=\"button\" class=\"od-btn od-btn-ghost\" onclick=\"AdminOrdersJs.simulate('" + Ui.Js(o.Id) + "')\">Simulate dashboard refund</button></div></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminOrdersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminOrdersJs", c, "view order", AreaKey, "You do not have access to orders.");
            }
            Order? o = (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == GetDataValue("oid"));
            if (o == null)
            {
                response.ExecuteScript("AdminOrdersJs.toast('That order was not found.');");
                return response;
            }
            response.SetElementContents("od-mbody", Detail(c, o));
            response.ExecuteScript("AdminOrdersJs.opened('" + Ui.Js("Order " + o.Id) + "');");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminOrdersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("oid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminOrdersJs", c, "cancel order", id, "Cancelling needs the Extra right on Orders.");
            }
            Order? check = (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == id);
            if (check != null && check.Status == "pending" && check.Session != string.Empty && !Store.Demo() && await Pay.Confirm(id))
            {
                response.ExecuteScript("AdminOrdersJs.done('That order was already paid, so it was kept.');");
                return response;
            }
            string label = string.Empty;
            using (await Store.Hold())
            {
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.Id == id && x.Status == "pending");
                if (o != null)
                {
                    o.Status = "cancelled";
                    label = o.Id + " · " + o.StudentName;
                    await Store.SaveRows("orders", orders);
                }
            }
            if (label == string.Empty)
            {
                response.ExecuteScript("AdminOrdersJs.done('Only an unpaid order can be cancelled.');");
                return response;
            }
            await Store.Log(c.U.Id, "cancel order", label, true);
            response.ExecuteScript("AdminOrdersJs.done('Order cancelled.');");
            return response;
        }

        public async Task<ApiResponse> Simulate()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminOrdersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("oid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminOrdersJs", c, "simulate dashboard refund", id, "You do not have the Edit right on Orders.");
            }
            if (!Store.Demo())
            {
                return await Ad.Refuse(response, "AdminOrdersJs", c, "simulate dashboard refund", id, "The simulator works in Demo mode only. In Production Stripe sends the real event.");
            }
            Order? o = (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == id);
            decimal amount = Math.Round(Ui.Dec(GetDataValue("amount"), 0), 2);
            if (o == null || o.Status != "paid" || o.Intent == string.Empty)
            {
                response.ExecuteScript("AdminOrdersJs.toast('Only a paid order can be refunded.');");
                return response;
            }
            if (amount <= 0 || amount > Learn.Remaining(o))
            {
                response.ExecuteScript("AdminOrdersJs.toast('Enter an amount between 0.01 and " + Learn.Remaining(o).ToString("0.00", CultureInfo.InvariantCulture) + ".');");
                return response;
            }
            string result = await Pay.Process(Pay.DashboardPayload(o, amount));
            await Store.Log(c.U.Id, "simulate dashboard refund", o.Id + " · " + Ui.Money(amount), true);
            response.ExecuteScript("AdminOrdersJs.done('" + Ui.Js(result.StartsWith("ok", StringComparison.Ordinal) ? Ui.Money(amount) + " refund received from the Stripe dashboard." : result) + "');");
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
