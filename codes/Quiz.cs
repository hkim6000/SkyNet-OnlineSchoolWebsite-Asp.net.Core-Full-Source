using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Quiz : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Quiz | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Panel(string title, string text, string links)
        {
            return "<div class=\"qz-narrow\"><div class=\"qz-panel qz-stack\"><h1>" + Ui.Esc(title) + "</h1><p>" + Ui.Esc(text) + "</p><div class=\"qz-row\">" + links + "</div></div></div>";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            string cid = Ui.Clip(GetDataValue("qc"), 60);
            string qid = Ui.Clip(GetDataValue("qq"), 80);
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == cid && x.Published);
            QuizInfo? quiz = site.Quizzes.FirstOrDefault(x => x.Id == qid && x.CourseId == cid);
            if (course == null || quiz == null || quiz.Questions.Count == 0)
            {
                response.SetElementContents("qz-body", Panel("Quiz not found", "That quiz does not exist.", "<a class=\"qz-btn\" href=\"Courses\">Browse courses</a>"));
                return response;
            }
            SCtx? s = await Auth.StudentCheck(GetDataValue("stok"));
            if (s == null)
            {
                response.SetElementContents("qz-body", Panel("Sign in to take the quiz", "Quizzes are part of the paid course.", "<a class=\"qz-btn\" href=\"Join?next=" + Uri.EscapeDataString("Quiz?c=" + cid + "&q=" + qid) + "\">Sign in</a><a class=\"qz-btn qz-btn-ghost\" href=\"Course?id=" + Ui.Esc(cid) + "\">See the course</a>"));
                return response;
            }
            Enrollment? e = (await Store.Rows<Enrollment>("enrollments")).FirstOrDefault(x => x.StudentId == s.S.Id && x.CourseId == cid);
            if (e == null || e.Status != "active")
            {
                response.SetElementContents("qz-body", Panel("This quiz is locked", e == null ? "You have not enrolled in this course yet." : "Your access to this course ended after a refund. Enrol again to continue.", "<a class=\"qz-btn\" href=\"Course?id=" + Ui.Esc(cid) + "\">" + (e == null ? "See the course" : "Enrol again") + "</a>"));
                return response;
            }
            QuizResult? best = e.Quizzes.FirstOrDefault(x => x.QuizId == quiz.Id);
            StringBuilder sb = new StringBuilder("<div class=\"qz-narrow\"><nav class=\"qz-crumb\"><a href=\"MyCourses\">My courses</a> / <a href=\"Course?id=" + Ui.Esc(cid) + "\">" + Ui.Esc(course.Title) + "</a></nav><div class=\"qz-qhead\"><h1>" + Ui.Esc(quiz.Title) + "</h1><p class=\"qz-hint\">" + Ui.Count(quiz.Questions.Count, "question", "questions") + ". Pass mark " + quiz.PassMark + "%. You can try again as often as you like.</p>");
            if (best != null)
            {
                sb.Append("<p class=\"qz-note " + (best.Passed ? "qz-note-ok" : string.Empty) + "\">" + (best.Passed ? "You passed this quiz. Best score " + best.Best + "%." : "Best score so far " + best.Best + "% (" + Ui.Count(best.Attempts, "try", "tries") + ").") + "</p>");
            }
            sb.Append("</div><form id=\"qz-form\" class=\"qz-stack\" onsubmit=\"return QuizJs.submit()\" novalidate>");
            int n = 0;
            foreach (QuizQuestion q in quiz.Questions)
            {
                sb.Append("<fieldset class=\"qz-q\" id=\"qz-fs" + n + "\"><legend>" + (n + 1) + ". " + Ui.Esc(q.Text) + "</legend>");
                for (int j = 0; j < q.Options.Count; j++)
                {
                    sb.Append("<label class=\"qz-opt\"><input type=\"radio\" name=\"qz-r" + n + "\" id=\"qz-r" + n + "-" + j + "\" value=\"" + j + "\"><span>" + Ui.Esc(q.Options[j]) + "</span></label>");
                }
                sb.Append("</fieldset>");
                n++;
            }
            sb.Append("<div class=\"qz-note qz-note-bad\" id=\"qz-err\" role=\"alert\"></div><div class=\"qz-row\"><button type=\"submit\" class=\"qz-btn\">Submit answers</button></div></form><div id=\"qz-result\" aria-live=\"polite\"></div></div>");
            response.SetElementContents("qz-body", sb.ToString());
            response.ExecuteScript("QuizJs.ready(" + quiz.Questions.Count + ");");
            return response;
        }

        public async Task<ApiResponse> Submit()
        {
            ApiResponse response = new ApiResponse();
            SCtx? s = await Auth.StudentCheck(GetDataValue("stok"));
            if (s == null)
            {
                response.ExecuteScript("QuizJs.gone();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            string cid = Ui.Clip(GetDataValue("qc"), 60);
            string qid = Ui.Clip(GetDataValue("qq"), 80);
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == cid);
            QuizInfo? quiz = site.Quizzes.FirstOrDefault(x => x.Id == qid && x.CourseId == cid);
            if (course == null || quiz == null)
            {
                return response;
            }
            string[] parts = (GetDataValue("ans") ?? string.Empty).Split(',');
            int[] given = parts.Select(p => int.TryParse(p, out int v) ? v : -1).ToArray();
            if (given.Length != quiz.Questions.Count || given.Any(g => g < 0))
            {
                List<int> missing = Enumerable.Range(0, quiz.Questions.Count).Where(i => i >= given.Length || given[i] < 0).Select(i => i + 1).ToList();
                response.SetElementContents("qz-err", "Please answer every question. Missing: " + string.Join(", ", missing) + ".");
                return response;
            }
            response.SetElementContents("qz-err", string.Empty);
            int right = 0;
            List<int> wrong = new List<int>();
            for (int i = 0; i < quiz.Questions.Count; i++)
            {
                if (given[i] == quiz.Questions[i].Answer)
                {
                    right++;
                }
                else
                {
                    wrong.Add(i);
                }
            }
            int score = right * 100 / quiz.Questions.Count;
            bool passed = score >= quiz.PassMark;
            CertRecord? cert = null;
            Enrollment? e;
            using (await Store.Hold())
            {
                List<Enrollment> all = await Store.Rows<Enrollment>("enrollments");
                e = all.FirstOrDefault(x => x.StudentId == s.S.Id && x.CourseId == cid);
                if (e == null || e.Status != "active")
                {
                    response.ExecuteScript("QuizJs.toast('Your access to this course has ended.');");
                    return response;
                }
                QuizResult? r = e.Quizzes.FirstOrDefault(x => x.QuizId == quiz.Id);
                if (r == null)
                {
                    r = new QuizResult { QuizId = quiz.Id };
                    e.Quizzes.Add(r);
                }
                r.Attempts++;
                r.Best = Math.Max(r.Best, score);
                r.When = DateTime.UtcNow;
                if (passed && !r.Passed)
                {
                    r.Passed = true;
                }
                List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
                cert = Learn.TryIssue(site, course, e, s.S, certs, DateTime.UtcNow);
                await Store.SaveRows("enrollments", all);
                if (cert != null)
                {
                    await Store.SaveRows("certificates", certs);
                }
            }
            await Store.Log(s.S.Id, passed ? "quiz passed" : "quiz failed", course.Title + " · " + quiz.Title + " · " + score + "%", true);
            if (cert != null)
            {
                await Store.Log(s.S.Id, "certificate issued", course.Title + " · " + cert.Code, true);
            }
            List<Step> seq = Learn.Sequence(site, course);
            int at = seq.FindIndex(x => x.Kind == "quiz" && x.Id == quiz.Id);
            Step? next = at >= 0 && at + 1 < seq.Count ? seq[at + 1] : null;
            StringBuilder sb = new StringBuilder("<div class=\"qz-panel qz-res " + (passed ? "qz-res-ok" : "qz-res-bad") + "\"><h2>" + (passed ? "You passed" : "Not quite yet") + "</h2><p class=\"qz-score\">" + score + "%</p><p>" + right + " of " + quiz.Questions.Count + " correct. The pass mark is " + quiz.PassMark + "%.</p>");
            if (passed)
            {
                sb.Append("<ol class=\"qz-why\">");
                for (int i = 0; i < quiz.Questions.Count; i++)
                {
                    QuizQuestion q = quiz.Questions[i];
                    sb.Append("<li><b>" + Ui.Esc(q.Text) + "</b><span>" + Ui.Esc(q.Options[q.Answer]) + "</span>" + (q.Why != string.Empty ? "<small>" + Ui.Esc(q.Why) + "</small>" : string.Empty) + "</li>");
                }
                sb.Append("</ol>");
            }
            else
            {
                sb.Append("<p>Look again at question" + (wrong.Count == 1 ? " " : "s ") + string.Join(", ", wrong.Select(i => (i + 1).ToString())) + ", then try again. Re-watch the lessons if you need to.</p>");
            }
            if (cert != null)
            {
                sb.Append("<p class=\"qz-cert\">Your certificate is ready. <a href=\"Certificate?code=" + Ui.Esc(cert.Code) + "\">View certificate</a></p>");
            }
            sb.Append("<div class=\"qz-row\">" + (passed ? (next != null ? "<a class=\"qz-btn\" href=\"" + (next.Kind == "lesson" ? "Lesson?c=" + Ui.Esc(cid) + "&amp;l=" + Ui.Esc(next.Id) : "Quiz?c=" + Ui.Esc(cid) + "&amp;q=" + Ui.Esc(next.Id)) + "\">Next: " + Ui.Esc(next.Title) + "</a>" : "<a class=\"qz-btn\" href=\"MyCourses\">Back to my courses</a>") : "<button type=\"button\" class=\"qz-btn\" onclick=\"QuizJs.again()\">Try again</button>") + "</div></div>");
            response.SetElementContents("qz-result", sb.ToString());
            response.ExecuteScript("QuizJs.graded(" + (passed ? "true" : "false") + ");");
            return response;
        }
    }
}
