using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Course : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            SiteData site = await Store.LoadSite();
            string id = Ui.Clip(QueryValue("id"), 60);
            CourseInfo? c = site.Courses.FirstOrDefault(x => x.Id == id && x.Published);
            if (c == null)
            {
                HtmlDoc.SetTitle("Course not found | Brightpath Academy");
                HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
                HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_id}", string.Empty).Replace("{plhd_course}", "<section class=\"cd-wrap cd-sec\"><div class=\"cd-panel cd-stack\"><h1>Course not found</h1><p>That course does not exist or is not on sale. Have a look at the catalog.</p><p><a class=\"cd-btn\" href=\"Courses\">Browse courses</a></p></div></section>");
                return;
            }
            HtmlDoc.SetTitle(c.Title + " | Brightpath Academy");
            HtmlDoc.AddMetaElement("description", c.Summary);
            InstructorInfo? ins = site.Instructors.FirstOrDefault(x => x.Id == c.InstructorId);
            int minutes = c.Lessons.Sum(l => l.Minutes);
            int quizzes = site.Quizzes.Count(q => q.CourseId == c.Id && q.Questions.Count > 0);
            LessonInfo? free = c.Lessons.FirstOrDefault(l => l.Free);
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"cd-top\"><div class=\"cd-wrap cd-topin\"><div class=\"cd-info\"><nav class=\"cd-crumb\" aria-label=\"Breadcrumb\"><a href=\"Courses\">Courses</a> / <a href=\"Courses?topic=" + Ui.Esc(c.Topic) + "\">" + Ui.Esc(Learn.TopicName(site, c.Topic)) + "</a></nav>");
            sb.Append("<div class=\"cd-tags\"><span class=\"cd-lvl\">" + Ui.Esc(Learn.LevelName(c.Level)) + "</span><span>" + Ui.Esc(Learn.TopicName(site, c.Topic)) + "</span></div><h1>" + Ui.Esc(c.Title) + "</h1><p class=\"cd-lead\">" + Ui.Esc(c.Summary) + "</p>");
            sb.Append("<ul class=\"cd-meta\"><li><b>" + c.Lessons.Count + "</b> lessons</li><li><b>" + minutes + "</b> minutes of video</li><li><b>" + quizzes + "</b> " + (quizzes == 1 ? "quiz" : "quizzes") + "</li><li><b>1</b> certificate</li></ul>");
            if (ins != null)
            {
                sb.Append("<a class=\"cd-ins\" href=\"Instructors#" + Ui.Esc(ins.Id) + "\"><img src=\"" + Ui.Esc(Media.Thumb(ins.Photo)) + "\" alt=\"\"><span><small>Taught by</small><b>" + Ui.Esc(ins.Name) + "</b></span></a>");
            }
            sb.Append("</div><aside class=\"cd-buy cd-panel\"><img src=\"" + Ui.Esc(Media.Thumb(c.Cover)) + "\" alt=\"Cover art for " + Ui.Esc(c.Title) + "\"><div class=\"cd-pricerow\"><b class=\"cd-price\">" + Ui.Price(c.Price) + "</b><small>one payment, yours to keep</small></div>");
            sb.Append("<button type=\"button\" class=\"cd-btn cd-btn-block\" id=\"cd-buy\" onclick=\"CourseJs.buy()\">Enrol for " + Ui.Price(c.Price) + "</button>");
            if (free != null)
            {
                sb.Append("<a class=\"cd-btn cd-btn-ghost cd-btn-block\" href=\"Lesson?c=" + Ui.Esc(c.Id) + "&amp;l=" + Ui.Esc(free.Id) + "\">Watch the free preview lesson</a>");
            }
            sb.Append("<div class=\"cd-note cd-note-bad\" id=\"cd-err\" role=\"alert\"></div><p class=\"cd-fine\">Pay by card with Stripe Checkout. " + Ui.Esc(Learn.ConditionsText(site)) + "</p></aside></div></section>");
            sb.Append("<section class=\"cd-wrap cd-sec cd-cols\"><div class=\"cd-main\"><h2>What you will learn</h2><ul class=\"cd-ticks\">" + string.Concat(c.Outcomes.Replace("\r", string.Empty).Split('\n').Where(l => l.Trim().Length > 0).Select(l => "<li>" + Ui.Esc(l.Trim()) + "</li>")) + "</ul><h2>About this course</h2><div class=\"cd-text\">" + Ui.Paras(c.Description) + "</div>");
            sb.Append("<h2 id=\"syllabus\">Syllabus</h2><ol class=\"cd-syl\">");
            int n = 0;
            foreach (Step s in Learn.Sequence(site, c))
            {
                if (s.Kind == "lesson")
                {
                    n++;
                    LessonInfo l = c.Lessons.First(x => x.Id == s.Id);
                    sb.Append("<li class=\"cd-lesson\"><span class=\"cd-num\">" + n + "</span><span class=\"cd-lt\"><b>" + Ui.Esc(l.Title) + "</b><small>" + Ui.Esc(l.Summary) + "</small></span><span class=\"cd-lm\">" + (l.Free ? "<a class=\"cd-free\" href=\"Lesson?c=" + Ui.Esc(c.Id) + "&amp;l=" + Ui.Esc(l.Id) + "\">Free preview</a>" : "<span class=\"cd-lock\" aria-label=\"Locked until you enrol\">&#128274;</span>") + "<em>" + l.Minutes + " min</em></span></li>");
                }
                else
                {
                    QuizInfo q = site.Quizzes.First(x => x.Id == s.Id);
                    sb.Append("<li class=\"cd-lesson cd-quiz\"><span class=\"cd-num\">?</span><span class=\"cd-lt\"><b>Quiz: " + Ui.Esc(q.Title) + "</b><small>" + Ui.Count(q.Questions.Count, "question", "questions") + ", pass mark " + q.PassMark + "%</small></span><span class=\"cd-lm\"><em>Quiz</em></span></li>");
                }
            }
            sb.Append("</ol></div><aside class=\"cd-side\">");
            if (ins != null)
            {
                sb.Append("<div class=\"cd-panel cd-teacher\"><img src=\"" + Ui.Esc(ins.Photo) + "\" alt=\"Portrait of " + Ui.Esc(ins.Name) + "\" loading=\"lazy\"><h3>" + Ui.Esc(ins.Name) + "</h3><small>" + Ui.Esc(ins.Role) + "</small><p>" + Ui.Esc(ins.Bio) + "</p></div>");
            }
            sb.Append("<div class=\"cd-panel cd-refund\" id=\"refund-policy\"><h3>Refund policy</h3><p>" + Ui.Esc(Learn.ConditionsText(site)) + "</p><ul><li><b>" + site.RefundDays + " days</b> from payment</li><li>Less than <b>" + site.ProgressLimit + "%</b> of lessons finished</li>" + (site.NoCertificate ? "<li><b>No certificate</b> issued yet</li>" : string.Empty) + "</ul><p class=\"cd-hint\">You ask from My account and a person checks every request, also those outside the conditions. <a href=\"Policy\">Read the full terms</a>.</p></div></aside></section>");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_course}", sb.ToString()).Replace("{plhd_id}", Ui.Esc(c.Id));
        }

        public async Task<ApiResponse> Buy()
        {
            ApiResponse response = new ApiResponse();
            SCtx? s = await Auth.StudentCheck(GetDataValue("stok"));
            if (s == null)
            {
                response.ExecuteScript("CourseJs.needSignIn();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            CourseInfo? c = site.Courses.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("bcourse"), 60) && x.Published);
            if (c == null)
            {
                response.SetElementContents("cd-err", "This course is not on sale.");
                return response;
            }
            string oid;
            bool fresh = false;
            using (await Store.Hold())
            {
                List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
                if (enrollments.Any(e => e.StudentId == s.S.Id && e.CourseId == c.Id && e.Status == "active"))
                {
                    response.ExecuteScript("CourseJs.owned();");
                    return response;
                }
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.StudentId == s.S.Id && x.CourseId == c.Id && x.Status == "pending");
                if (o == null)
                {
                    o = new Order { Id = Learn.NewOrderId(orders), StudentId = s.S.Id, StudentName = s.S.Name, StudentEmail = s.S.Email, CourseId = c.Id, CourseTitle = c.Title, Amount = c.Price, Status = "pending", Created = DateTime.UtcNow };
                    orders.Add(o);
                    await Store.SaveRows("orders", orders);
                    fresh = true;
                }
                else if (o.Amount != c.Price)
                {
                    o.Amount = c.Price;
                    await Store.SaveRows("orders", orders);
                }
                oid = o.Id;
            }
            if (fresh)
            {
                await Store.Log(s.S.Id, "order created", oid + " · " + c.Title, true);
            }
            response.ExecuteScript("CourseJs.checkout('" + Ui.Js(oid) + "');");
            return response;
        }
    }
}
