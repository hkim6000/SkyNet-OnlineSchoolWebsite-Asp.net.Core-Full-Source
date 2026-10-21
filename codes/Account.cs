using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Account : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("My account | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<SCtx?> Guard(ApiResponse response)
        {
            SCtx? c = await Auth.StudentCheck(GetDataValue("stok"));
            if (c == null)
            {
                response.ExecuteScript("AccountJs.gone();");
            }
            return c;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.StudentSignOut(GetDataValue("stok"));
            return response;
        }

        private static string StatusPill(Order o)
        {
            return o.Status switch
            {
                "paid" => Learn.Refunded(o) >= o.Amount ? "<span class=\"ac-pill ac-pill-off\">Refunded</span>" : Learn.Refunded(o) > 0 ? "<span class=\"ac-pill ac-pill-wait\">Part refunded</span>" : "<span class=\"ac-pill ac-pill-go\">Paid</span>",
                "pending" => "<span class=\"ac-pill ac-pill-wait\">Waiting for payment</span>",
                _ => "<span class=\"ac-pill ac-pill-off\">Cancelled</span>"
            };
        }

        private static string RequestLine(RefundRequest r, bool again)
        {
            string eligible = r.Eligible ? "<span class=\"ac-pill ac-pill-go\">Eligible</span>" : "<span class=\"ac-pill ac-pill-wait\">Outside policy</span>";
            switch (r.Status)
            {
                case "open":
                case "processing":
                    return "<div class=\"ac-req\"><div><b>Refund request sent " + Ui.Day(r.Created) + "</b> " + eligible + "<p>Waiting for review. Reason: " + Ui.Esc(r.Reason) + "</p></div><button type=\"button\" class=\"ac-btn ac-btn-sm ac-btn-ghost\" onclick=\"AccountJs.withdraw('" + Ui.Esc(r.Id) + "')\">Withdraw request</button></div>";
                case "approved":
                    return "<div class=\"ac-req ac-req-ok\"><div><b>Full refund of " + Ui.Money(r.Amount) + " approved " + (r.Decided.HasValue ? Ui.Day(r.Decided.Value) : string.Empty) + "</b><p>" + (again ? "You enrolled again with a new order, so the course is open and your progress came back." : "Access to the course has ended. Your progress is kept but locked, and you can enrol again at any time.") + (r.Note != string.Empty ? " Note from the team: " + Ui.Esc(r.Note) : string.Empty) + "</p></div></div>";
                case "partial":
                    return "<div class=\"ac-req ac-req-ok\"><div><b>Partial refund of " + Ui.Money(r.Amount) + " approved " + (r.Decided.HasValue ? Ui.Day(r.Decided.Value) : string.Empty) + "</b><p>You keep access to the course." + (r.Note != string.Empty ? " Note from the team: " + Ui.Esc(r.Note) : string.Empty) + "</p></div></div>";
                case "declined":
                    return "<div class=\"ac-req ac-req-bad\"><div><b>Refund request declined " + (r.Decided.HasValue ? Ui.Day(r.Decided.Value) : string.Empty) + "</b><p>" + Ui.Esc(r.Note) + "</p></div></div>";
                default:
                    return "<div class=\"ac-req\"><div><b>Refund request withdrawn</b></div></div>";
            }
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            List<Order> orders = (await Store.Rows<Order>("orders")).Where(o => o.StudentId == c.S.Id).OrderByDescending(o => o.Created).ToList();
            List<RefundRequest> requests = (await Store.Rows<RefundRequest>("refunds")).Where(r => r.StudentId == c.S.Id).OrderByDescending(r => r.Created).ToList();
            List<Enrollment> enrollments = (await Store.Rows<Enrollment>("enrollments")).Where(e => e.StudentId == c.S.Id).ToList();
            SiteData site = await Store.LoadSite();
            StringBuilder sb = new StringBuilder("<div class=\"ac-page-h\"><h1>My account</h1><p>Your orders, receipts and refund requests.</p></div>");
            sb.Append("<section class=\"ac-panel\"><div class=\"ac-phead\"><h2>Profile</h2><div class=\"ac-row\"><button type=\"button\" class=\"ac-btn ac-btn-sm ac-btn-ghost\" onclick=\"AccountJs.profile()\">Edit profile</button><button type=\"button\" class=\"ac-btn ac-btn-sm ac-btn-ghost\" onclick=\"AccountJs.password()\">Change password</button></div></div><dl class=\"ac-kv\"><dt>Name</dt><dd>" + Ui.Esc(c.S.Name) + "</dd><dt>Email</dt><dd>" + Ui.Esc(c.S.Email) + "</dd><dt>Phone</dt><dd>" + (c.S.Phone == string.Empty ? "<span class=\"ac-hint\">Not given yet</span>" : Ui.Esc(c.S.Phone)) + "</dd><dt>Member since</dt><dd>" + Ui.Day(c.S.Created) + "</dd></dl></section>");
            sb.Append("<h2 class=\"ac-h2\">Orders</h2>");
            if (orders.Count == 0)
            {
                sb.Append("<div class=\"ac-panel\"><p>You have no orders yet.</p><p><a class=\"ac-btn\" href=\"Courses\">Browse the catalog</a></p></div>");
            }
            else
            {
                sb.Append("<div class=\"ac-stack\">");
                foreach (Order o in orders)
                {
                    List<RefundRequest> mine = requests.Where(r => r.OrderId == o.Id).ToList();
                    bool open = mine.Any(r => r.Status == "open" || r.Status == "processing");
                    sb.Append("<article class=\"ac-panel ac-order\"><div class=\"ac-ohead\"><div><b>" + Ui.Esc(o.CourseTitle) + "</b><small>Order " + Ui.Esc(o.Id) + " &middot; " + Ui.Day(o.Created) + "</small></div><div class=\"ac-oprice\"><b>" + Ui.Money(o.Amount) + "</b>" + StatusPill(o) + "</div></div>");
                    if (Learn.Refunded(o) > 0)
                    {
                        sb.Append("<p class=\"ac-refunded\">Refunded " + Ui.Money(Learn.Refunded(o)) + " &middot; net paid " + Ui.Money(o.Amount - Learn.Refunded(o)) + "</p>");
                    }
                    bool again = enrollments.Any(e => e.CourseId == o.CourseId && e.Status == "active" && e.OrderId != o.Id);
                    foreach (RefundRequest r in mine)
                    {
                        sb.Append(RequestLine(r, again));
                    }
                    sb.Append("<div class=\"ac-row\">");
                    if (o.Status == "paid")
                    {
                        sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-sm ac-btn-ghost\" onclick=\"AccountJs.receipt('" + Ui.Esc(o.Id) + "')\">Receipt</button>");
                        if (Learn.Remaining(o) > 0 && !open)
                        {
                            sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-sm ac-btn-ghost\" onclick=\"AccountJs.refund('" + Ui.Esc(o.Id) + "')\">Request a refund</button>");
                        }
                    }
                    else if (o.Status == "pending")
                    {
                        sb.Append("<a class=\"ac-btn ac-btn-sm\" href=\"Checkout?o=" + Ui.Esc(o.Id) + "\">Complete payment</a>");
                    }
                    sb.Append("</div></article>");
                }
                sb.Append("</div>");
            }
            sb.Append("<p class=\"ac-hint ac-conds\">" + Ui.Esc(Learn.ConditionsText(site)) + " <a href=\"Policy\">Read the full terms</a>.</p>");
            response.SetElementContents("ac-body", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Profile()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string html = "<form class=\"ac-form\" onsubmit=\"return AccountJs.saveProfile()\" novalidate>"
                + "<label class=\"ac-field\"><span>Your name</span><input type=\"text\" id=\"ac-pname\" value=\"" + Ui.Esc(c.S.Name) + "\" maxlength=\"60\" autocomplete=\"name\"><em class=\"ac-err\" id=\"ac-e-pname\"></em></label>"
                + "<label class=\"ac-field\"><span>Phone</span><input type=\"tel\" id=\"ac-pphone\" value=\"" + Ui.Esc(c.S.Phone) + "\" maxlength=\"24\" autocomplete=\"tel\"><em class=\"ac-err\" id=\"ac-e-pphone\"></em></label>"
                + "<p class=\"ac-hint\">Your email is your sign-in name. To change it, write to us from the Contact page.</p>"
                + "<div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AccountJs.closeModal()\">Cancel</button><button type=\"submit\" class=\"ac-btn\">Save</button></div></form>";
            response.SetElementContents("ac-mbody", html);
            response.ExecuteScript("AccountJs.opened('Edit profile');");
            return response;
        }

        public async Task<ApiResponse> SaveProfile()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string name = Ui.Clip(GetDataValue("pname"), 60);
            string phone = Ui.Clip(GetDataValue("pphone"), 24);
            bool badName = name.Length < 2;
            bool badPhone = !Ui.IsPhone(phone);
            response.SetElementContents("ac-e-pname", badName ? "Please enter your name." : string.Empty);
            response.SetElementContents("ac-e-pphone", badPhone ? "Please enter a phone number we can reach you on." : string.Empty);
            if (badName || badPhone)
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<Student> all = await Store.Rows<Student>("students");
                Student? s = all.FirstOrDefault(x => x.Id == c.S.Id);
                if (s != null)
                {
                    s.Name = name;
                    s.Phone = phone;
                    await Store.SaveRows("students", all);
                }
            }
            await Store.Log(c.S.Id, "edit own profile", name, true);
            response.ExecuteScript("AccountJs.profileSaved();");
            return response;
        }

        public async Task<ApiResponse> Password()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string html = "<form class=\"ac-form\" onsubmit=\"return AccountJs.savePassword()\" novalidate>"
                + "<label class=\"ac-field\"><span>Current password</span><input type=\"password\" id=\"ac-cur\" maxlength=\"80\" autocomplete=\"current-password\"></label>"
                + "<label class=\"ac-field\"><span>New password</span><input type=\"password\" id=\"ac-np1\" maxlength=\"80\" autocomplete=\"new-password\"></label>"
                + "<label class=\"ac-field\"><span>New password again</span><input type=\"password\" id=\"ac-np2\" maxlength=\"80\" autocomplete=\"new-password\"><em class=\"ac-err\" id=\"ac-e-pw\"></em></label>"
                + "<p class=\"ac-hint\">At least 8 characters. Your other sign-ins are closed when the password changes.</p>"
                + "<div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AccountJs.closeModal()\">Cancel</button><button type=\"submit\" class=\"ac-btn\">Change password</button></div></form>";
            response.SetElementContents("ac-mbody", html);
            response.ExecuteScript("AccountJs.opened('Change password');");
            return response;
        }

        public async Task<ApiResponse> SavePassword()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string err = await Auth.StudentChangePassword(c.S.Id, GetDataValue("cur") ?? string.Empty, GetDataValue("np1") ?? string.Empty, GetDataValue("np2") ?? string.Empty);
            if (err != string.Empty)
            {
                await Store.Log(c.S.Id, "change own password", c.S.Email, false);
                response.SetElementContents("ac-e-pw", Ui.Esc(err));
                return response;
            }
            await Auth.CloseStudentOthers(c.S.Id, c.Token);
            await Store.Log(c.S.Id, "change own password", c.S.Email, true);
            response.ExecuteScript("AccountJs.passwordSaved();");
            return response;
        }

        public async Task<ApiResponse> Receipt()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string oid = Ui.Clip(GetDataValue("oid"), 20);
            Order? o = (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == oid && x.StudentId == c.S.Id);
            if (o == null || o.Status != "paid")
            {
                response.ExecuteScript("AccountJs.toast('That receipt was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            StringBuilder sb = new StringBuilder("<div class=\"ac-receipt\"><div class=\"ac-rhead\"><b>" + Ui.Esc(site.Brand) + "</b><span>Receipt for order " + Ui.Esc(o.Id) + "</span></div><dl class=\"ac-kv\"><dt>Student</dt><dd>" + Ui.Esc(o.StudentName) + " (" + Ui.Esc(o.StudentEmail) + ")</dd><dt>Course</dt><dd>" + Ui.Esc(o.CourseTitle) + "</dd><dt>Paid on</dt><dd>" + (o.Paid.HasValue ? Ui.When(o.Paid.Value) : string.Empty) + "</dd><dt>Method</dt><dd>" + Ui.Esc(o.Method) + "</dd></dl>");
            sb.Append("<table class=\"ac-rt\"><thead><tr><th>Line</th><th>Date</th><th class=\"ac-num\">Amount</th></tr></thead><tbody><tr><td data-l=\"Line\">Payment: " + Ui.Esc(o.CourseTitle) + "</td><td data-l=\"Date\">" + (o.Paid.HasValue ? Ui.Day(o.Paid.Value) : string.Empty) + "</td><td class=\"ac-num\" data-l=\"Amount\">" + Ui.Money(o.Amount) + "</td></tr>");
            foreach (RefundLine l in o.Refunds.OrderBy(x => x.When))
            {
                sb.Append("<tr class=\"ac-rrefund\"><td data-l=\"Line\">Refund (" + (l.Kind == "full" ? "full" : l.Kind == "partial" ? "goodwill" : "from the payment provider") + ") " + Ui.Esc(l.Id) + "</td><td data-l=\"Date\">" + Ui.Day(l.When) + "</td><td class=\"ac-num\" data-l=\"Amount\">-" + Ui.Money(l.Amount) + "</td></tr>");
            }
            sb.Append("</tbody><tfoot><tr><td colspan=\"2\">Net paid</td><td class=\"ac-num\">" + Ui.Money(o.Amount - Learn.Refunded(o)) + "</td></tr></tfoot></table>");
            sb.Append("<div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AccountJs.closeModal()\">Close</button><button type=\"button\" class=\"ac-btn\" onclick=\"AccountJs.print()\">Print receipt</button></div></div>");
            response.SetElementContents("ac-mbody", sb.ToString());
            response.ExecuteScript("AccountJs.opened('Receipt " + Ui.Js(o.Id) + "');");
            return response;
        }

        public async Task<ApiResponse> RefundInfo()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string oid = Ui.Clip(GetDataValue("oid"), 20);
            SiteData site = await Store.LoadSite();
            Order? o = (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == oid && x.StudentId == c.S.Id);
            if (o == null)
            {
                response.ExecuteScript("AccountJs.toast('That order was not found.');");
                return response;
            }
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == o.CourseId);
            RefundState st = Learn.Check(site, o, Learn.EnrollmentOf(await Store.Rows<Enrollment>("enrollments"), c.S.Id, o.CourseId), course, await Store.Rows<CertRecord>("certificates"), DateTime.UtcNow);
            if (!st.Can)
            {
                response.SetElementContents("ac-mbody", "<p>" + Ui.Esc(st.Block) + "</p><div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AccountJs.closeModal()\">Close</button></div>");
                response.ExecuteScript("AccountJs.opened('Request a refund');");
                return response;
            }
            string days = st.WindowOk ? st.DaysLeft + (st.DaysLeft == 1 ? " day" : " days") + " left of " + site.RefundDays : "The " + site.RefundDays + "-day window ended " + Ui.Count(-st.DaysLeft, "day", "days") + " ago";
            StringBuilder sb = new StringBuilder("<div class=\"ac-elig " + (st.Eligible ? "ac-elig-ok" : "ac-elig-out") + "\"><b>" + (st.Eligible ? "Inside the refund conditions" : "Outside the refund conditions") + "</b><p>" + (st.Eligible ? "Your request will be marked eligible. Our team still checks every request." : "You can still send your request. It will be marked outside policy and our team looks at it case by case.") + "</p></div>");
            sb.Append("<ul class=\"ac-checks\"><li class=\"" + (st.WindowOk ? "ac-yes" : "ac-no") + "\"><b>Time</b><span>" + Ui.Esc(days) + "</span></li><li class=\"" + (st.ProgressOk ? "ac-yes" : "ac-no") + "\"><b>Progress</b><span>You have finished " + st.Progress + "% of the lessons (the limit is under " + site.ProgressLimit + "%)</span></li>");
            if (site.NoCertificate)
            {
                sb.Append("<li class=\"" + (st.CertOk ? "ac-yes" : "ac-no") + "\"><b>Certificate</b><span>" + (st.HasCert ? "A certificate has been issued" : "No certificate issued yet") + "</span></li>");
            }
            sb.Append("</ul><p class=\"ac-hint\">You can be refunded up to " + Ui.Money(st.Remaining) + " (what you paid minus earlier refunds). If we approve a full refund, your access ends, your progress is kept but locked, and a certificate is revoked. A partial refund keeps your access.</p>");
            sb.Append("<div class=\"ac-field\"><span>Why would you like a refund?</span><textarea id=\"ac-reason\" maxlength=\"500\" placeholder=\"A sentence or two is enough\"></textarea><em class=\"ac-err\" id=\"ac-e-reason\"></em></div>");
            sb.Append("<div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AccountJs.closeModal()\">Cancel</button><button type=\"button\" class=\"ac-btn\" onclick=\"AccountJs.sendRefund('" + Ui.Esc(o.Id) + "')\">Send request</button></div>");
            response.SetElementContents("ac-mbody", sb.ToString());
            response.ExecuteScript("AccountJs.opened('Request a refund');");
            return response;
        }

        public async Task<ApiResponse> RequestRefund()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (bool ok, string msg) = await Desk.Request(c.S, Ui.Clip(GetDataValue("oid"), 20), GetDataValue("reason") ?? string.Empty);
            if (!ok)
            {
                response.SetElementContents("ac-e-reason", Ui.Esc(msg));
                return response;
            }
            response.ExecuteScript("AccountJs.requested('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> Withdraw()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (bool ok, string msg) = await Desk.Withdraw(c.S, Ui.Clip(GetDataValue("rid"), 20));
            response.ExecuteScript("AccountJs.withdrawn(" + (ok ? "true" : "false") + ",'" + Ui.Js(msg) + "');");
            return response;
        }
    }
}
