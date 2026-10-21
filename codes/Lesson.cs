using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Lesson : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Lesson | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Tick(bool done)
        {
            return done ? "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.6\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>" : "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"7.5\"/></svg>";
        }

        private static string Lock()
        {
            return "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"5\" y=\"11\" width=\"14\" height=\"9\" rx=\"2\"/><path d=\"M8 11V8a4 4 0 0 1 8 0v3\"/></svg>";
        }

        private static string StepUrl(CourseInfo c, Step s)
        {
            return s.Kind == "lesson" ? "Lesson?c=" + Ui.Esc(c.Id) + "&amp;l=" + Ui.Esc(s.Id) : "Quiz?c=" + Ui.Esc(c.Id) + "&amp;q=" + Ui.Esc(s.Id);
        }

        private static string Outline(SiteData site, CourseInfo course, LessonInfo cur, Enrollment? e, bool active)
        {
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Esc(course.Title) + "</h2>");
            if (active && e != null)
            {
                int pct = Learn.Percent(course, e);
                sb.Append("<div class=\"ls-bar\"><i style=\"width:" + pct + "%\"></i></div><p class=\"ls-prog\">" + Learn.Done(course, e) + " of " + course.Lessons.Count + " lessons &middot; " + pct + "%</p>");
            }
            sb.Append("<ol class=\"ls-steps\">");
            int n = 0;
            foreach (Step s in Learn.Sequence(site, course))
            {
                LessonInfo? l = s.Kind == "lesson" ? course.Lessons.FirstOrDefault(x => x.Id == s.Id) : null;
                bool open = active || (l != null && l.Free);
                bool done = active && e != null && Learn.StepDone(e, s);
                bool here = s.Kind == "lesson" && s.Id == cur.Id;
                string href = open ? StepUrl(course, s) : "Course?id=" + Ui.Esc(course.Id);
                if (s.Kind == "lesson")
                {
                    n++;
                }
                sb.Append("<li><a class=\"ls-step" + (here ? " ls-here" : string.Empty) + (done ? " ls-done" : string.Empty) + (open ? string.Empty : " ls-shut") + "\" href=\"" + href + "\"" + (here ? " aria-current=\"step\"" : string.Empty) + "><span class=\"ls-mark\">" + (open ? Tick(done) : Lock()) + "</span><span class=\"ls-st\"><b>" + (s.Kind == "lesson" ? n + ". " : "Quiz: ") + Ui.Esc(s.Title) + "</b><small>" + (s.Kind == "lesson" ? (l != null ? l.Minutes + " min" + (l.Free ? " &middot; free preview" : string.Empty) : string.Empty) : "Pass mark " + (site.Quizzes.FirstOrDefault(q => q.Id == s.Id)?.PassMark ?? 0) + "%") + "</small></span></a></li>");
            }
            sb.Append("</ol>");
            return sb.ToString();
        }

        private static string Actions(SiteData site, CourseInfo course, LessonInfo cur, Enrollment? e, bool active)
        {
            List<Step> seq = Learn.Sequence(site, course);
            int at = seq.FindIndex(s => s.Kind == "lesson" && s.Id == cur.Id);
            Step? next = at >= 0 && at + 1 < seq.Count ? seq[at + 1] : null;
            string nextLink = next != null ? "<a class=\"ls-btn ls-btn-ghost\" href=\"" + StepUrl(course, next) + "\">Next: " + Ui.Esc(next.Title) + " &rarr;</a>" : (active ? "<a class=\"ls-btn ls-btn-ghost\" href=\"MyCourses\">Back to my courses</a>" : "<a class=\"ls-btn ls-btn-ghost\" href=\"Course?id=" + Ui.Esc(course.Id) + "\">Back to the course</a>");
            if (!active || e == null)
            {
                return nextLink.Contains("Next:") && cur.Free && course.Lessons.Count > 1 ? "<a class=\"ls-btn\" href=\"Course?id=" + Ui.Esc(course.Id) + "\">Unlock all " + course.Lessons.Count + " lessons</a>" : "<a class=\"ls-btn\" href=\"Course?id=" + Ui.Esc(course.Id) + "\">See the course</a>";
            }
            bool done = e.Completed.Contains(cur.Id);
            return (done ? "<span class=\"ls-pill ls-pill-go\">Completed</span>" : "<button type=\"button\" class=\"ls-btn\" onclick=\"LessonJs.complete()\">Mark complete</button>") + nextLink;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            string cid = Ui.Clip(GetDataValue("lc"), 60);
            string lid = Ui.Clip(GetDataValue("ll"), 20);
            SiteData site = await Store.LoadSite();
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == cid && x.Published);
            LessonInfo? lesson = course?.Lessons.FirstOrDefault(x => x.Id == lid);
            if (course == null || lesson == null)
            {
                response.SetElementContents("ls-body", "<div class=\"ls-narrow\"><div class=\"ls-panel\"><h1>Lesson not found</h1><p class=\"ls-hint\">That lesson does not exist. Choose a course from the catalog.</p><p><a class=\"ls-btn\" href=\"Courses\">Browse courses</a></p></div></div>");
                return response;
            }
            SCtx? s = await Auth.StudentCheck(GetDataValue("stok"));
            Enrollment? e = s == null ? null : (await Store.Rows<Enrollment>("enrollments")).FirstOrDefault(x => x.StudentId == s.S.Id && x.CourseId == course.Id);
            bool active = e != null && e.Status == "active";
            if (!active && !lesson.Free)
            {
                string why = e != null ? "Your access to this course ended after a refund. Your progress is kept and comes back if you enrol again." : s == null ? "This lesson is part of the paid course. Sign in if you already enrolled, or look at the course." : "You have not enrolled in this course yet.";
                response.SetElementContents("ls-body", "<div class=\"ls-narrow\"><div class=\"ls-panel ls-stack\"><h1>This lesson is locked</h1><p>" + Ui.Esc(why) + "</p><div class=\"ls-row\"><a class=\"ls-btn\" href=\"Course?id=" + Ui.Esc(course.Id) + "\">" + (e != null ? "Enrol again" : "See the course") + "</a>" + (s == null ? "<a class=\"ls-btn ls-btn-ghost\" href=\"Join?next=" + Uri.EscapeDataString("Lesson?c=" + course.Id + "&l=" + lesson.Id) + "\">Sign in</a>" : string.Empty) + "</div></div></div>");
                return response;
            }
            if (active && e != null)
            {
                using (await Store.Hold())
                {
                    List<Enrollment> all = await Store.Rows<Enrollment>("enrollments");
                    Enrollment? mine = all.FirstOrDefault(x => x.Id == e.Id);
                    if (mine != null && mine.Last != lesson.Id)
                    {
                        mine.Last = lesson.Id;
                        await Store.SaveRows("enrollments", all);
                    }
                }
            }
            StringBuilder sb = new StringBuilder("<div class=\"ls-wrap\"><nav class=\"ls-crumb\" aria-label=\"Breadcrumb\"><a href=\"Courses\">Courses</a> / <a href=\"Course?id=" + Ui.Esc(course.Id) + "\">" + Ui.Esc(course.Title) + "</a></nav><div class=\"ls-grid\"><div class=\"ls-main\">");
            string cover = Media.Thumb(course.Cover);
            sb.Append(lesson.Video != string.Empty ? "<div class=\"ls-video\"><video controls playsinline preload=\"metadata\" poster=\"" + Ui.Esc(cover) + "\" src=\"" + Ui.Esc(lesson.Video) + "\" aria-label=\"" + Ui.Esc(lesson.Title) + "\"></video></div>" : "<div class=\"ls-video ls-novideo\"><img src=\"" + Ui.Esc(cover) + "\" alt=\"\"><span>No video for this lesson yet. Read the notes below.</span></div>");
            sb.Append("<div class=\"ls-lhead\"><small>" + (lesson.Free && !active ? "Free preview &middot; " : string.Empty) + lesson.Minutes + " min</small><h1>" + Ui.Esc(lesson.Title) + "</h1><p class=\"ls-hint\">" + Ui.Esc(lesson.Summary) + "</p></div>");
            sb.Append("<div class=\"ls-celebrate\" id=\"ls-celebrate\" role=\"status\"></div><div class=\"ls-actions\" id=\"ls-actions\">" + Actions(site, course, lesson, e, active) + "</div>");
            sb.Append("<section class=\"ls-notes\"><h2>Lesson notes</h2>" + Ui.Paras(lesson.Notes) + "</section>");
            if (!active)
            {
                sb.Append("<div class=\"ls-panel ls-buy\"><div><b>Like what you see?</b><p class=\"ls-hint\">Get all " + course.Lessons.Count + " lessons, the quizzes and a certificate for " + Ui.Price(course.Price) + ".</p></div><a class=\"ls-btn\" href=\"Course?id=" + Ui.Esc(course.Id) + "\">See the course</a></div>");
            }
            sb.Append("</div><aside class=\"ls-side ls-panel\" id=\"ls-outline\">" + Outline(site, course, lesson, e, active) + "</aside></div></div>");
            response.SetElementContents("ls-body", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Complete()
        {
            ApiResponse response = new ApiResponse();
            SCtx? s = await Auth.StudentCheck(GetDataValue("stok"));
            if (s == null)
            {
                response.ExecuteScript("LessonJs.gone();");
                return response;
            }
            string cid = Ui.Clip(GetDataValue("lc"), 60);
            string lid = Ui.Clip(GetDataValue("ll"), 20);
            SiteData site = await Store.LoadSite();
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == cid);
            LessonInfo? lesson = course?.Lessons.FirstOrDefault(x => x.Id == lid);
            if (course == null || lesson == null)
            {
                return response;
            }
            Enrollment? e;
            CertRecord? cert = null;
            bool fresh = false;
            using (await Store.Hold())
            {
                List<Enrollment> all = await Store.Rows<Enrollment>("enrollments");
                e = all.FirstOrDefault(x => x.StudentId == s.S.Id && x.CourseId == course.Id);
                if (e == null || e.Status != "active")
                {
                    response.ExecuteScript("LessonJs.toast('Your access to this course has ended.');");
                    return response;
                }
                if (!e.Completed.Contains(lesson.Id))
                {
                    e.Completed.Add(lesson.Id);
                    fresh = true;
                }
                e.Last = lesson.Id;
                List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
                cert = Learn.TryIssue(site, course, e, s.S, certs, DateTime.UtcNow);
                await Store.SaveRows("enrollments", all);
                if (cert != null)
                {
                    await Store.SaveRows("certificates", certs);
                }
            }
            if (fresh)
            {
                await Store.Log(s.S.Id, "lesson complete", course.Title + " · " + lesson.Title, true);
            }
            if (cert != null)
            {
                await Store.Log(s.S.Id, "certificate issued", course.Title + " · " + cert.Code, true);
            }
            response.SetElementContents("ls-actions", Actions(site, course, lesson, e, true));
            response.SetElementContents("ls-outline", Outline(site, course, lesson, e, true));
            response.ExecuteScript("LessonJs.completed('" + (cert == null ? string.Empty : Ui.Js(cert.Code)) + "');");
            return response;
        }
    }
}
