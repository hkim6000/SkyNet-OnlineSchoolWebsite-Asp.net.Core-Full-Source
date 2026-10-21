using System.Text;
using System.Text.Json;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminQuizzes : WebPage
    {
        private const string AreaKey = "quizzes";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Quizzes | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static List<CourseInfo> Visible(SiteData site, Ctx c)
        {
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            return site.Courses.Where(k => own == null || own.Contains(k.Id)).OrderBy(k => k.Title).ToList();
        }

        private static QuizInfo? Mine(SiteData site, Ctx c, string id)
        {
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            return site.Quizzes.FirstOrDefault(q => q.Id == id && (own == null || own.Contains(q.CourseId)));
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminQuizzesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "qa", "AdminQuizzes", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("qa-main", Ad.NoAccess("qa", "quizzes"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"qa-ph\"><div><h1>Quizzes</h1><p>Short checks inside a course. Students must reach the pass mark to move on.</p></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"qa-btn\" onclick=\"AdminQuizzesJs.open('new')\">New quiz</button>");
            }
            sb.Append("</div><div class=\"qa-tools\"><input type=\"search\" id=\"qa-q\" placeholder=\"Search quizzes\" aria-label=\"Search\" oninput=\"AdminQuizzesJs.search()\"></div><div id=\"qa-list\"></div>");
            response.SetElementContents("qa-main", sb.ToString());
            response.ExecuteScript("AdminQuizzesJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminQuizzesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminQuizzesJs", c, "view quizzes", AreaKey, "You do not have access to quizzes.");
            }
            SiteData site = await Store.LoadSite();
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            IEnumerable<QuizInfo> rows = site.Quizzes.Where(x => own == null || own.Contains(x.CourseId)).OrderBy(x => site.Courses.FirstOrDefault(k => k.Id == x.CourseId)?.Title ?? string.Empty).ThenBy(x => x.Title);
            if (q.Length > 0)
            {
                rows = rows.Where(x => Ui.Has(x.Title, q) || Ui.Has(site.Courses.FirstOrDefault(k => k.Id == x.CourseId)?.Title, q));
            }
            List<QuizInfo> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"qa-empty\">No quizzes yet.</div>");
            }
            else
            {
                sb.Append("<div class=\"qa-wrapt\"><table class=\"qa-table\"><thead><tr><th>Quiz</th><th>Course</th><th>After lesson</th><th class=\"qa-num\">Questions</th><th class=\"qa-num\">Pass mark</th></tr></thead><tbody>");
                foreach (QuizInfo z in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    CourseInfo? k = site.Courses.FirstOrDefault(x => x.Id == z.CourseId);
                    string after = k?.Lessons.FirstOrDefault(l => l.Id == z.AfterLesson)?.Title ?? "End of course";
                    sb.Append("<tr class=\"qa-row\" tabindex=\"0\" onclick=\"AdminQuizzesJs.open('" + Ui.Js(z.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminQuizzesJs.open('" + Ui.Js(z.Id) + "')}\"><td data-l=\"Quiz\"><b>" + Ui.Esc(z.Title) + "</b></td><td data-l=\"Course\">" + Ui.Esc(k?.Title ?? z.CourseId) + "</td><td data-l=\"After lesson\">" + Ui.Esc(after) + "</td><td data-l=\"Questions\" class=\"qa-num\">" + z.Questions.Count + "</td><td data-l=\"Pass mark\" class=\"qa-num\">" + z.PassMark + "%</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("qa", "AdminQuizzesJs", pg, list.Count, "quiz", "quizzes"));
            }
            response.SetElementContents("qa-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminQuizzesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminQuizzesJs", c, "view quiz", AreaKey, "You do not have access to quizzes.");
            }
            SiteData site = await Store.LoadSite();
            string id = GetDataValue("qid") ?? string.Empty;
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool isNew = id == "new";
            if (isNew && !edit)
            {
                return await Ad.Refuse(response, "AdminQuizzesJs", c, "new quiz", AreaKey, "You do not have the Edit right on Quizzes.");
            }
            List<CourseInfo> courses = Visible(site, c);
            QuizInfo? z = isNew ? new QuizInfo { CourseId = courses.FirstOrDefault()?.Id ?? string.Empty, PassMark = 70, Questions = new List<QuizQuestion> { new QuizQuestion { Options = new List<string> { string.Empty, string.Empty } } } } : Mine(site, c, id);
            if (z == null)
            {
                response.ExecuteScript("AdminQuizzesJs.toast('That quiz was not found.');");
                return response;
            }
            string ro = edit ? string.Empty : " disabled";
            string map = JsonSerializer.Serialize(courses.ToDictionary(k => k.Id, k => k.Lessons.Select(l => new[] { l.Id, l.Title }).ToList()));
            string data = JsonSerializer.Serialize(z.Questions.Select(x => new { text = x.Text, options = x.Options, answer = x.Answer, why = x.Why }));
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"qa-stack\" id=\"qa-form\" data-lessons=\"" + Ui.Esc(map) + "\" data-after=\"" + Ui.Esc(z.AfterLesson) + "\" data-q=\"" + Ui.Esc(data) + "\" data-edit=\"" + (edit ? "1" : "0") + "\">");
            sb.Append("<div class=\"qa-grid2\"><label class=\"qa-field\"><span>Course</span><select id=\"qa-course\"" + ro + " onchange=\"AdminQuizzesJs.course()\">" + Ad.Opts(courses.Select(k => (k.Id, k.Title)), z.CourseId, string.Empty) + "</select><em class=\"qa-err\" id=\"qa-e-course\"></em></label>");
            sb.Append("<label class=\"qa-field\"><span>Comes after</span><select id=\"qa-after\"" + ro + " onchange=\"AdminQuizzesJs.markDirty()\"></select></label></div>");
            sb.Append("<div class=\"qa-grid2\"><label class=\"qa-field\"><span>Quiz title</span><input type=\"text\" id=\"qa-title\" maxlength=\"80\" value=\"" + Ui.Esc(z.Title) + "\"" + ro + " oninput=\"AdminQuizzesJs.markDirty()\"><em class=\"qa-err\" id=\"qa-e-title\"></em></label>");
            sb.Append("<label class=\"qa-field\"><span>Pass mark (%)</span><input type=\"number\" id=\"qa-pass\" min=\"1\" max=\"100\" value=\"" + z.PassMark + "\"" + ro + " oninput=\"AdminQuizzesJs.markDirty()\"><em class=\"qa-err\" id=\"qa-e-pass\"></em></label></div>");
            sb.Append("<div><div class=\"qa-bar\"><h3>Questions</h3>" + (edit ? "<button type=\"button\" class=\"qa-btn qa-btn-sm qa-btn-ghost\" onclick=\"AdminQuizzesJs.addQuestion()\">Add a question</button>" : string.Empty) + "</div><em class=\"qa-err\" id=\"qa-e-qs\"></em><div id=\"qa-qs\"></div></div>");
            sb.Append("<div class=\"qa-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"qa-btn\" onclick=\"AdminQuizzesJs.save('" + Ui.Js(isNew ? "new" : z.Id) + "')\">" + (isNew ? "Create quiz" : "Save") + "</button>");
                if (!isNew)
                {
                    sb.Append("<button type=\"button\" class=\"qa-btn qa-btn-danger\" onclick=\"AdminQuizzesJs.remove('" + Ui.Js(z.Id) + "')\">Delete</button>");
                }
            }
            sb.Append("<button type=\"button\" class=\"qa-btn qa-btn-ghost\" onclick=\"AdminQuizzesJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("qa-mbody", sb.ToString());
            response.ExecuteScript("AdminQuizzesJs.opened('" + Ui.Js(isNew ? "New quiz" : z.Title) + "');");
            return response;
        }

        private sealed class QuestionIn
        {
            public string Text { get; set; } = string.Empty;
            public List<string> Options { get; set; } = new List<string>();
            public int Answer { get; set; }
            public string Why { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminQuizzesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("qid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminQuizzesJs", c, "save quiz", id, "You do not have the Edit right on Quizzes.");
            }
            string title = Ui.Clip(GetDataValue("qtitle"), 80);
            int pass = Ui.Whole(GetDataValue("qpass"), 0);
            string courseId = GetDataValue("qcourse") ?? string.Empty;
            List<QuestionIn> raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<QuestionIn>>(GetDataValue("qdata") ?? "[]", Store.Json) ?? new List<QuestionIn>();
            }
            catch (JsonException)
            {
                raw = new List<QuestionIn>();
            }
            string problem = string.Empty;
            List<QuizQuestion> questions = new List<QuizQuestion>();
            for (int i = 0; i < raw.Count && i < 30; i++)
            {
                QuestionIn r = raw[i];
                List<string> opts = r.Options.Select(o => Ui.Clip(o, 120)).ToList();
                if (Ui.Clip(r.Text, 200).Length < 3)
                {
                    problem = "Question " + (i + 1) + " needs some text.";
                    break;
                }
                if (opts.Count < 2 || opts.Count > 5 || opts.Any(o => o.Length == 0))
                {
                    problem = "Question " + (i + 1) + " needs 2 to 5 answers, all filled in.";
                    break;
                }
                if (r.Answer < 0 || r.Answer >= opts.Count)
                {
                    problem = "Mark the right answer of question " + (i + 1) + ".";
                    break;
                }
                questions.Add(new QuizQuestion { Text = Ui.Clip(r.Text, 200), Options = opts, Answer = r.Answer, Why = Ui.Clip(r.Why, 240) });
            }
            if (problem == string.Empty && questions.Count == 0)
            {
                problem = "Add at least one question.";
            }
            response.SetElementContents("qa-e-title", title.Length < 3 ? "Please enter a title of at least 3 letters." : string.Empty);
            response.SetElementContents("qa-e-pass", pass < 1 || pass > 100 ? "Enter a pass mark from 1 to 100." : string.Empty);
            response.SetElementContents("qa-e-qs", Ui.Esc(problem));
            if (title.Length < 3 || pass < 1 || pass > 100 || problem != string.Empty)
            {
                return response;
            }
            string name;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                CourseInfo? k = Visible(site, c).FirstOrDefault(x => x.Id == courseId);
                if (k == null)
                {
                    response.SetElementContents("qa-e-course", "Choose a course.");
                    return response;
                }
                QuizInfo? z;
                if (id == "new")
                {
                    int n = 1;
                    while (site.Quizzes.Any(x => x.Id == "q-" + k.Id + "-" + n))
                    {
                        n++;
                    }
                    z = new QuizInfo { Id = "q-" + k.Id + "-" + n };
                    site.Quizzes.Add(z);
                }
                else
                {
                    z = Mine(site, c, id);
                }
                if (z == null)
                {
                    response.ExecuteScript("AdminQuizzesJs.toast('That quiz was not found.');");
                    return response;
                }
                string after = GetDataValue("qafter") ?? string.Empty;
                z.CourseId = k.Id;
                z.Title = title;
                z.PassMark = pass;
                z.AfterLesson = k.Lessons.Any(l => l.Id == after) ? after : string.Empty;
                z.Questions = questions;
                name = k.Title + " / " + title;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, id == "new" ? "create quiz" : "edit quiz", name, true);
            response.ExecuteScript("AdminQuizzesJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminQuizzesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("qid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminQuizzesJs", c, "delete quiz", id, "You do not have the Edit right on Quizzes.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                QuizInfo? z = Mine(site, c, id);
                if (z != null)
                {
                    name = z.Title;
                    site.Quizzes.Remove(z);
                    await Store.SaveSite(site);
                }
            }
            if (name == string.Empty)
            {
                response.ExecuteScript("AdminQuizzesJs.toast('That quiz was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, "delete quiz", name, true);
            response.ExecuteScript("AdminQuizzesJs.saved();");
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
