using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class MyCourses : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("My courses | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<SCtx?> Guard(ApiResponse response)
        {
            SCtx? c = await Auth.StudentCheck(GetDataValue("stok"));
            if (c == null)
            {
                response.ExecuteScript("MyCoursesJs.gone();");
            }
            return c;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.StudentSignOut(GetDataValue("stok"));
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<Enrollment> mine;
            List<CertRecord> certs;
            List<Order> orders = (await Store.Rows<Order>("orders")).Where(o => o.StudentId == c.S.Id).OrderByDescending(o => o.Created).ToList();
            List<string> issued = new List<string>();
            using (await Store.Hold())
            {
                List<Enrollment> all = await Store.Rows<Enrollment>("enrollments");
                certs = await Store.Rows<CertRecord>("certificates");
                mine = all.Where(e => e.StudentId == c.S.Id).ToList();
                foreach (Enrollment e in mine.Where(x => x.Status == "active"))
                {
                    CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == e.CourseId);
                    CertRecord? cert = course == null ? null : Learn.TryIssue(site, course, e, c.S, certs, DateTime.UtcNow);
                    if (cert != null)
                    {
                        issued.Add(cert.CourseTitle);
                    }
                }
                if (issued.Count > 0)
                {
                    await Store.SaveRows("certificates", certs);
                }
            }
            foreach (string t in issued)
            {
                await Store.Log(c.S.Id, "certificate issued", t, true);
            }
            StringBuilder sb = new StringBuilder("<div class=\"mc-page-h\"><h1>My courses</h1><p>Hello, " + Ui.Esc(c.S.Name) + ". Pick up where you left off.</p></div>");
            if (issued.Count > 0)
            {
                sb.Append("<div class=\"mc-note mc-note-ok\">Congratulations! Your certificate for " + Ui.Esc(string.Join(", ", issued)) + " is ready.</div>");
            }
            List<Enrollment> shown = mine.OrderBy(e => e.Status == "active" ? 0 : 1).ThenByDescending(e => e.Started).ToList();
            if (shown.Count == 0)
            {
                sb.Append("<div class=\"mc-panel mc-empty\"><h2>No courses yet</h2><p>Your courses appear here as soon as a payment is confirmed.</p><a class=\"mc-btn\" href=\"Courses\">Browse the catalog</a></div>");
            }
            else
            {
                sb.Append("<div class=\"mc-stack\">");
                foreach (Enrollment e in shown)
                {
                    CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == e.CourseId);
                    if (course != null)
                    {
                        sb.Append(Card(site, course, e, certs.Where(x => x.StudentId == c.S.Id && x.CourseId == e.CourseId).OrderBy(x => x.Revoked).ThenByDescending(x => x.Issued).FirstOrDefault()));
                    }
                }
                sb.Append("</div>");
            }
            List<Order> waiting = orders.Where(o => o.Status == "pending").ToList();
            if (waiting.Count > 0)
            {
                sb.Append("<h2 class=\"mc-h2\">Waiting for payment</h2><div class=\"mc-stack\">");
                foreach (Order o in waiting)
                {
                    sb.Append("<div class=\"mc-panel mc-wait\"><div><b>" + Ui.Esc(o.CourseTitle) + "</b><small>Order " + Ui.Esc(o.Id) + " &middot; " + Ui.Money(o.Amount) + "</small></div><a class=\"mc-btn mc-btn-sm\" href=\"Checkout?o=" + Ui.Esc(o.Id) + "\">Complete payment</a></div>");
                }
                sb.Append("</div>");
            }
            List<CourseInfo> more = site.Courses.Where(x => x.Published && mine.All(e => e.CourseId != x.Id)).Take(3).ToList();
            if (more.Count > 0)
            {
                sb.Append("<h2 class=\"mc-h2\">Keep learning</h2><div class=\"mc-cgrid\">" + string.Concat(more.Select(x => "<a class=\"mc-ccard\" href=\"Course?id=" + Ui.Esc(x.Id) + "\"><img src=\"" + Ui.Esc(Media.Thumb(x.Cover)) + "\" alt=\"\" loading=\"lazy\"><span class=\"mc-ccard-b\"><small>" + Ui.Esc(Learn.TopicName(site, x.Topic)) + "</small><b>" + Ui.Esc(x.Title) + "</b><span class=\"mc-ccard-f\"><span class=\"mc-lvl\">" + Ui.Esc(Learn.LevelName(x.Level)) + "</span><span class=\"mc-price\">" + Ui.Price(x.Price) + "</span></span></span></a>")) + "</div>");
            }
            response.SetElementContents("mc-body", sb.ToString());
            return response;
        }

        private static string Card(SiteData site, CourseInfo course, Enrollment e, CertRecord? cert)
        {
            int pct = Learn.Percent(course, e);
            int done = Learn.Done(course, e);
            bool locked = e.Status != "active";
            List<Step> seq = Learn.Sequence(site, course);
            Step? next = seq.FirstOrDefault(s => !Learn.StepDone(e, s));
            string url = next == null ? "Lesson?c=" + course.Id + "&l=" + course.Lessons.FirstOrDefault()?.Id : (next.Kind == "lesson" ? "Lesson?c=" + course.Id + "&l=" + next.Id : "Quiz?c=" + course.Id + "&q=" + next.Id);
            string label = done == 0 && next != null ? "Start course" : next == null ? "Review course" : "Continue";
            StringBuilder sb = new StringBuilder("<article class=\"mc-panel mc-ecard" + (locked ? " mc-locked" : string.Empty) + "\"><img src=\"" + Ui.Esc(Media.Thumb(course.Cover)) + "\" alt=\"\" loading=\"lazy\"><div class=\"mc-eb\"><small>" + Ui.Esc(Learn.TopicName(site, course.Topic)) + " &middot; " + Ui.Esc(Learn.LevelName(course.Level)) + "</small><h2>" + Ui.Esc(course.Title) + "</h2>");
            sb.Append("<div class=\"mc-bar\" role=\"progressbar\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-valuenow=\"" + pct + "\"><i style=\"width:" + pct + "%\"></i></div><p class=\"mc-prog\">" + done + " of " + course.Lessons.Count + " lessons &middot; " + pct + "%" + (locked ? " &middot; progress kept" : string.Empty) + "</p>");
            if (locked)
            {
                sb.Append("<p class=\"mc-lockmsg\"><span class=\"mc-pill mc-pill-off\">Access ended</span> Your access ended after a refund. Your progress is kept and comes back if you enrol again.</p><div class=\"mc-row\"><a class=\"mc-btn mc-btn-sm\" href=\"Course?id=" + Ui.Esc(course.Id) + "\">Enrol again</a></div>");
            }
            else
            {
                sb.Append("<div class=\"mc-row\"><a class=\"mc-btn mc-btn-sm\" href=\"" + Ui.Esc(url) + "\">" + label + "</a>");
                if (cert != null && !cert.Revoked)
                {
                    sb.Append("<a class=\"mc-btn mc-btn-sm mc-btn-ghost\" href=\"Certificate?code=" + Ui.Esc(cert.Code) + "\">View certificate</a>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div></article>");
            return sb.ToString();
        }
    }
}
