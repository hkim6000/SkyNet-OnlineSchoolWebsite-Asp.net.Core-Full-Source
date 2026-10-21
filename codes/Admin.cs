using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Dashboard | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Stat(string value, string label, string link, string text)
        {
            return "<div class=\"db-stat\"><b>" + value + "</b><span>" + Ui.Esc(label) + "</span>" + (link == string.Empty ? string.Empty : "<a href=\"" + link + "\">" + Ui.Esc(text) + "</a>") + "</div>";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "db", "Admin", c, site);
            HashSet<string>? own = Ad.OwnIds(site, c.U, "courses");
            List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
            List<Order> orders = await Store.Rows<Order>("orders");
            List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
            List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
            List<Student> students = await Store.Rows<Student>("students");
            List<ContactMessage> messages = await Store.Rows<ContactMessage>("messages");
            List<CourseInfo> courses = site.Courses.Where(x => own == null || own.Contains(x.Id)).ToList();
            HashSet<string> courseIds = courses.Select(x => x.Id).ToHashSet();
            List<Enrollment> mine = enrollments.Where(e => own == null || own.Contains(e.CourseId)).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"db-ph\"><div><h1>" + (own != null ? "Your courses" : "Dashboard") + "</h1><p>" + (own != null ? "Students and progress in the courses you teach." : "How the school is doing today.") + "</p></div></div>");
            sb.Append("<div class=\"db-stats\">");
            bool seeOrders = Access.Can(c.U, "orders", 'V');
            if (seeOrders)
            {
                decimal net = orders.Where(o => o.Status == "paid").Sum(o => o.Amount - Learn.Refunded(o));
                int month = orders.Count(o => o.Status == "paid" && o.Paid.HasValue && o.Paid.Value > DateTime.UtcNow.AddDays(-30));
                sb.Append(Stat(Ui.Money(net), "Net revenue", "AdminOrders", Ui.Count(month, "sale", "sales") + " in 30 days"));
            }
            if (Access.Can(c.U, "enrollments", 'V'))
            {
                int active = mine.Count(e => e.Status == "active");
                sb.Append(Stat(active.ToString(), "Active enrollments", "AdminEnrollments", "See enrollments"));
            }
            if (Access.Can(c.U, "students", 'V'))
            {
                int count = own == null ? students.Count : mine.Select(e => e.StudentId).Distinct().Count();
                sb.Append(Stat(count.ToString(), own == null ? "Students" : "Your students", "AdminStudents", "See students"));
            }
            if (Access.Can(c.U, "refunds", 'V'))
            {
                int open = requests.Count(r => r.Status == "open");
                sb.Append(Stat(open.ToString(), "Open refund requests", "AdminRefunds", open > 0 ? "Review now" : "Nothing waiting"));
            }
            if (Access.Can(c.U, "messages", 'V'))
            {
                int unread = messages.Count(m => !m.Read);
                sb.Append(Stat(unread.ToString(), "Unread messages", "AdminMessages", "Open inbox"));
            }
            int issued = certs.Count(x => !x.Revoked && (own == null || own.Contains(x.CourseId)));
            sb.Append(Stat(issued.ToString(), "Certificates issued", string.Empty, string.Empty));
            int avg = mine.Count == 0 ? 0 : (int)Math.Round(mine.Average(e => Learn.Percent(site.Courses.FirstOrDefault(x => x.Id == e.CourseId) ?? new CourseInfo(), e)));
            sb.Append(Stat(avg + "%", "Average progress", string.Empty, string.Empty));
            sb.Append("</div><div class=\"db-cols\"><div>");
            if (seeOrders && own == null)
            {
                sb.Append("<section class=\"db-panel\"><h2>Latest orders</h2>");
                List<Order> latest = orders.OrderByDescending(o => o.Created).Take(6).ToList();
                if (latest.Count == 0)
                {
                    sb.Append("<p class=\"db-empty\">No orders yet.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"db-rowlist\">");
                    foreach (Order o in latest)
                    {
                        string kind = o.Status == "paid" ? (Learn.Refunded(o) > 0 ? "wait" : "go") : o.Status == "pending" ? "new" : "off";
                        string text = o.Status == "paid" && Learn.Refunded(o) > 0 ? (Learn.Remaining(o) <= 0 ? "refunded" : "part refunded") : o.Status;
                        sb.Append("<li><div><b>" + Ui.Esc(o.StudentName) + "</b><small class=\"db-sub\">" + Ui.Esc(o.CourseTitle) + " &middot; " + Ui.Day(o.Created) + "</small></div><div class=\"db-num\">" + Ui.Money(o.Amount) + "<br>" + Ad.Pill("db", kind, text) + "</div></li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append("</section>");
            }
            sb.Append("<section class=\"db-panel db-sect\"><h2>Enrollments by course</h2>");
            if (courses.Count == 0)
            {
                sb.Append("<p class=\"db-empty\">No courses yet.</p>");
            }
            else
            {
                int top = Math.Max(1, courses.Max(x => mine.Count(e => e.CourseId == x.Id)));
                sb.Append("<div class=\"db-bars\">");
                foreach (CourseInfo k in courses.OrderByDescending(x => mine.Count(e => e.CourseId == x.Id)))
                {
                    int n = mine.Count(e => e.CourseId == k.Id);
                    sb.Append("<div><span class=\"db-barname\">" + Ui.Esc(k.Title) + "</span><i style=\"width:" + (n * 100 / top) + "%\"></i><span>" + n + "</span></div>");
                }
                sb.Append("</div>");
            }
            sb.Append("</section></div><div>");
            if (Access.Can(c.U, "refunds", 'V'))
            {
                sb.Append("<section class=\"db-panel\"><h2>Refund queue</h2>");
                List<RefundRequest> queue = requests.Where(r => r.Status == "open").OrderBy(r => r.Created).Take(5).ToList();
                if (queue.Count == 0)
                {
                    sb.Append("<p class=\"db-empty\">No requests are waiting.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"db-rowlist\">");
                    foreach (RefundRequest r in queue)
                    {
                        sb.Append("<li><div><b>" + Ui.Esc(r.StudentName) + "</b><small class=\"db-sub\">" + Ui.Esc(r.CourseTitle) + "</small></div><div>" + Ad.Pill("db", r.Eligible ? "go" : "wait", r.Eligible ? "eligible" : "outside policy") + "</div></li>");
                    }
                    sb.Append("</ul><div class=\"db-acts\"><a class=\"db-btn db-btn-sm\" href=\"AdminRefunds\">Open the refund desk</a></div>");
                }
                sb.Append("</section>");
            }
            sb.Append("<section class=\"db-panel db-sect\"><h2>Your rights</h2><p class=\"db-hint\">" + Ui.Esc(Access.Words(c.U)) + "</p></section>");
            sb.Append("</div></div>");
            response.SetElementContents("db-main", sb.ToString());
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
