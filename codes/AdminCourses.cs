using System.Globalization;
using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminCourses : WebPage
    {
        private const string AreaKey = "courses";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Courses | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"cu-chip" + (key == cur ? " cu-act" : string.Empty) + "\" onclick=\"AdminCoursesJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("live", "Published", cur) + Chip("draft", "Drafts", cur);
        }

        private static CourseInfo? Mine(SiteData site, Ctx c, string id)
        {
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            return site.Courses.FirstOrDefault(k => k.Id == id && (own == null || own.Contains(k.Id)));
        }

        private static string Slug(string title, SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in title.ToLowerInvariant())
            {
                if (ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9')
                {
                    sb.Append(ch);
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                {
                    sb.Append('-');
                }
            }
            string slug = sb.ToString().Trim('-');
            if (slug.Length == 0)
            {
                slug = "course";
            }
            if (slug.Length > 50)
            {
                slug = slug.Substring(0, 50).Trim('-');
            }
            string id = slug;
            int n = 2;
            while (site.Courses.Any(k => k.Id == id))
            {
                id = slug + "-" + n++;
            }
            return id;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "cu", "AdminCourses", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("cu-main", Ad.NoAccess("cu", "courses"));
                return response;
            }
            bool own = Access.IsOwn(c.U, AreaKey);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"cu-ph\"><div><h1>Courses and lessons</h1><p>" + (own ? "The courses you teach." : "Everything the school teaches.") + "</p></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"cu-btn\" onclick=\"AdminCoursesJs.open('new')\">New course</button>");
            }
            sb.Append("</div><div class=\"cu-tools\"><input type=\"search\" id=\"cu-q\" placeholder=\"Search courses\" aria-label=\"Search\" oninput=\"AdminCoursesJs.search()\"><div class=\"cu-chips\" id=\"cu-chips\">" + Chips("all") + "</div></div><div id=\"cu-list\"></div>");
            response.SetElementContents("cu-main", sb.ToString());
            response.ExecuteScript("AdminCoursesJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "view courses", AreaKey, "You do not have access to courses.");
            }
            SiteData site = await Store.LoadSite();
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
            IEnumerable<CourseInfo> rows = site.Courses.Where(k => own == null || own.Contains(k.Id)).OrderBy(k => k.Title);
            if (st == "live")
            {
                rows = rows.Where(k => k.Published);
            }
            else if (st == "draft")
            {
                rows = rows.Where(k => !k.Published);
            }
            if (q.Length > 0)
            {
                rows = rows.Where(k => Ui.Has(k.Title, q) || Ui.Has(Learn.TopicName(site, k.Topic), q));
            }
            List<CourseInfo> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            response.SetElementContents("cu-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"cu-empty\">No courses match.</div>");
            }
            else
            {
                sb.Append("<div class=\"cu-wrapt\"><table class=\"cu-table\"><thead><tr><th>Course</th><th>Topic</th><th>Level</th><th class=\"cu-num\">Price</th><th class=\"cu-num\">Lessons</th><th class=\"cu-num\">Students</th><th>Status</th></tr></thead><tbody>");
                foreach (CourseInfo k in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    string teacher = site.Instructors.FirstOrDefault(i => i.Id == k.InstructorId)?.Name ?? string.Empty;
                    sb.Append("<tr class=\"cu-row\" tabindex=\"0\" onclick=\"AdminCoursesJs.open('" + Ui.Js(k.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminCoursesJs.open('" + Ui.Js(k.Id) + "')}\"><td data-l=\"Course\"><b>" + Ui.Esc(k.Title) + "</b><small>" + Ui.Esc(teacher) + "</small></td><td data-l=\"Topic\">" + Ui.Esc(Learn.TopicName(site, k.Topic)) + "</td><td data-l=\"Level\">" + Ui.Esc(Learn.LevelName(k.Level)) + "</td><td data-l=\"Price\" class=\"cu-num\">" + Ui.Money(k.Price) + "</td><td data-l=\"Lessons\" class=\"cu-num\">" + k.Lessons.Count + "</td><td data-l=\"Students\" class=\"cu-num\">" + enrollments.Count(e => e.CourseId == k.Id) + "</td><td data-l=\"Status\">" + Ad.Pill("cu", k.Published ? "go" : "off", k.Published ? "Published" : "Draft") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("cu", "AdminCoursesJs", pg, list.Count, "course", "courses"));
            }
            response.SetElementContents("cu-list", sb.ToString());
            return response;
        }

        private static async Task<string> Form(Ctx c, SiteData site, CourseInfo k, bool isNew)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            string ro = edit ? string.Empty : " disabled";
            string dirty = edit ? " oninput=\"AdminCoursesJs.markDirty()\" onchange=\"AdminCoursesJs.markDirty()\"" : string.Empty;
            List<MediaItem> media = await Media.Load();
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"cu-stack\">");
            if (!isNew)
            {
                sb.Append("<div class=\"cu-line\">" + Ad.Pill("cu", k.Published ? "go" : "off", k.Published ? "Published" : "Draft") + "</div>");
            }
            sb.Append("<label class=\"cu-field\"><span>Title</span><input type=\"text\" id=\"cu-title\" maxlength=\"80\" value=\"" + Ui.Esc(k.Title) + "\"" + ro + dirty + "><em class=\"cu-err\" id=\"cu-e-title\"></em></label>");
            sb.Append("<div class=\"cu-grid3\"><label class=\"cu-field\"><span>Topic</span><select id=\"cu-topic\"" + ro + dirty + ">" + Ad.Opts(site.Topics.Select(t => (t.Key, t.Label)), k.Topic, string.Empty) + "</select></label>");
            sb.Append("<label class=\"cu-field\"><span>Level</span><select id=\"cu-level\"" + ro + dirty + ">" + Ad.Opts(Learn.Levels, k.Level, string.Empty) + "</select></label>");
            sb.Append("<label class=\"cu-field\"><span>Price (USD)</span><input type=\"number\" id=\"cu-price\" min=\"1\" max=\"999\" step=\"0.01\" value=\"" + k.Price.ToString("0.##", CultureInfo.InvariantCulture) + "\"" + ro + dirty + "><em class=\"cu-err\" id=\"cu-e-price\"></em></label></div>");
            if (own == null)
            {
                sb.Append("<label class=\"cu-field\"><span>Instructor</span><select id=\"cu-instr\"" + ro + dirty + ">" + Ad.Opts(site.Instructors.Select(i => (i.Id, i.Name)), k.InstructorId, "No instructor") + "</select></label>");
            }
            sb.Append("<label class=\"cu-field\"><span>Short summary</span><input type=\"text\" id=\"cu-summary\" maxlength=\"160\" value=\"" + Ui.Esc(k.Summary) + "\"" + ro + dirty + "><em class=\"cu-err\" id=\"cu-e-summary\"></em></label>");
            sb.Append("<label class=\"cu-field\"><span>Description</span><textarea id=\"cu-desc\" rows=\"5\" maxlength=\"1500\"" + ro + dirty + ">" + Ui.Esc(k.Description) + "</textarea></label>");
            sb.Append("<label class=\"cu-field\"><span>What you will learn (one line each)</span><textarea id=\"cu-outc\" rows=\"4\" maxlength=\"800\"" + ro + dirty + ">" + Ui.Esc(k.Outcomes) + "</textarea></label>");
            sb.Append("<div class=\"cu-field\"><span>Cover picture</span>" + Ad.MediaSelect("cu", "cu-cover", "AdminCoursesJs", media, "photo", k.Cover).Replace("<select ", "<select" + ro + " ") + "<small>Pictures come from the <a href=\"AdminMedia\">Media library</a>.</small></div>");
            sb.Append("<label class=\"cu-check\"><input type=\"checkbox\" id=\"cu-feat\"" + (k.Featured ? " checked" : string.Empty) + ro + dirty + "><span>Show on the home page</span></label>");
            if (!isNew)
            {
                sb.Append("<div><div class=\"cu-bar\"><h3>Lessons (" + k.Lessons.Count + ")</h3>" + (edit ? "<button type=\"button\" class=\"cu-btn cu-btn-sm cu-btn-ghost\" onclick=\"AdminCoursesJs.lesson('" + Ui.Js(k.Id) + "', 'new')\">Add a lesson</button>" : string.Empty) + "</div>");
                if (k.Lessons.Count == 0)
                {
                    sb.Append("<p class=\"cu-hint\">No lessons yet.</p>");
                }
                else
                {
                    sb.Append("<ol class=\"cu-lessons\">");
                    for (int i = 0; i < k.Lessons.Count; i++)
                    {
                        LessonInfo l = k.Lessons[i];
                        sb.Append("<li><div class=\"cu-ltext\"><b>" + Ui.Esc(l.Title) + "</b><small class=\"cu-sub\">" + l.Minutes + " min" + (l.Free ? " &middot; free preview" : string.Empty) + (l.Video == string.Empty ? " &middot; no video" : string.Empty) + "</small></div><div class=\"cu-lbtns\">");
                        if (edit)
                        {
                            sb.Append("<button type=\"button\" class=\"cu-ibtn\" aria-label=\"Move up\"" + (i == 0 ? " disabled" : string.Empty) + " onclick=\"AdminCoursesJs.move('" + Ui.Js(k.Id) + "', '" + Ui.Js(l.Id) + "', -1)\">&uarr;</button><button type=\"button\" class=\"cu-ibtn\" aria-label=\"Move down\"" + (i == k.Lessons.Count - 1 ? " disabled" : string.Empty) + " onclick=\"AdminCoursesJs.move('" + Ui.Js(k.Id) + "', '" + Ui.Js(l.Id) + "', 1)\">&darr;</button>");
                        }
                        sb.Append("<button type=\"button\" class=\"cu-btn cu-btn-sm cu-btn-ghost\" onclick=\"AdminCoursesJs.lesson('" + Ui.Js(k.Id) + "', '" + Ui.Js(l.Id) + "')\">" + (edit ? "Edit" : "View") + "</button></div></li>");
                    }
                    sb.Append("</ol>");
                }
                sb.Append("</div>");
            }
            sb.Append("<div class=\"cu-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"cu-btn\" onclick=\"AdminCoursesJs.save('" + Ui.Js(isNew ? "new" : k.Id) + "')\">" + (isNew ? "Create draft" : "Save") + "</button>");
            }
            if (!isNew && extra)
            {
                sb.Append("<button type=\"button\" class=\"cu-btn cu-btn-ghost\" onclick=\"AdminCoursesJs.publish('" + Ui.Js(k.Id) + "', " + (k.Published ? "true" : "false") + ")\">" + (k.Published ? "Unpublish" : "Publish") + "</button><button type=\"button\" class=\"cu-btn cu-btn-danger\" onclick=\"AdminCoursesJs.remove('" + Ui.Js(k.Id) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"cu-btn cu-btn-ghost\" onclick=\"AdminCoursesJs.closeModal()\">Close</button></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "view course", AreaKey, "You do not have access to courses.");
            }
            SiteData site = await Store.LoadSite();
            string id = GetDataValue("cid") ?? string.Empty;
            bool isNew = id == "new";
            CourseInfo? k;
            if (isNew)
            {
                if (!Access.Can(c.U, AreaKey, 'E'))
                {
                    return await Ad.Refuse(response, "AdminCoursesJs", c, "new course", AreaKey, "You do not have the Edit right on Courses.");
                }
                k = new CourseInfo { Topic = site.Topics.FirstOrDefault()?.Key ?? string.Empty, Price = 49, Published = false };
            }
            else
            {
                k = Mine(site, c, id);
            }
            if (k == null)
            {
                response.ExecuteScript("AdminCoursesJs.toast('That course was not found.');");
                return response;
            }
            response.SetElementContents("cu-mbody", await Form(c, site, k, isNew));
            response.ExecuteScript("AdminCoursesJs.opened('" + Ui.Js(isNew ? "New course" : k.Title) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("cid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "save course", id, "You do not have the Edit right on Courses.");
            }
            string title = Ui.Clip(GetDataValue("ctitle"), 80);
            string summary = Ui.Clip(GetDataValue("csummary"), 160);
            decimal price = Math.Round(Ui.Dec(GetDataValue("cprice"), 0), 2);
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (title.Length < 3)
            {
                errors["title"] = "Please enter a title of at least 3 letters.";
            }
            if (price < 1 || price > 999)
            {
                errors["price"] = "Enter a price from 1 to 999.";
            }
            if (summary.Length < 5)
            {
                errors["summary"] = "Add a short summary for the course catalog.";
            }
            foreach (string f in new[] { "title", "price", "summary" })
            {
                response.SetElementContents("cu-e-" + f, errors.TryGetValue(f, out string? m) ? Ui.Esc(m) : string.Empty);
            }
            if (errors.Count > 0)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string cover = GetDataValue("ccover") ?? string.Empty;
            if (cover != string.Empty && Media.Find(media, cover, "photo") == null)
            {
                cover = string.Empty;
            }
            string label;
            string savedId;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
                string topic = GetDataValue("ctopic") ?? string.Empty;
                if (site.Topics.All(t => t.Key != topic))
                {
                    topic = site.Topics.FirstOrDefault()?.Key ?? string.Empty;
                }
                string level = GetDataValue("clevel") ?? string.Empty;
                if (Learn.Levels.All(l => l.Key != level))
                {
                    level = "beginner";
                }
                CourseInfo? k;
                if (id == "new")
                {
                    k = new CourseInfo { Id = Slug(title, site), Published = false };
                    site.Courses.Add(k);
                    label = "create course";
                }
                else
                {
                    k = site.Courses.FirstOrDefault(x => x.Id == id && (own == null || own.Contains(x.Id)));
                    label = "edit course";
                }
                if (k == null)
                {
                    response.ExecuteScript("AdminCoursesJs.toast('That course was not found.');");
                    return response;
                }
                k.Title = title;
                k.Topic = topic;
                k.Level = level;
                k.Price = price;
                k.Summary = summary;
                k.Description = Ui.Clip(GetDataValue("cdesc"), 1500);
                k.Outcomes = Ui.Clip(GetDataValue("coutc"), 800);
                k.Cover = cover;
                k.Featured = GetDataValue("cfeat") == "1";
                if (Access.IsOwn(c.U, AreaKey))
                {
                    k.InstructorId = c.U.ProviderId;
                }
                else
                {
                    string instr = GetDataValue("cinstr") ?? string.Empty;
                    k.InstructorId = site.Instructors.Any(i => i.Id == instr) ? instr : string.Empty;
                }
                await Store.SaveSite(site);
                savedId = k.Id;
            }
            await Store.Log(c.U.Id, label, title, true);
            response.ExecuteScript("AdminCoursesJs.saved('" + Ui.Js(savedId) + "', " + (id == "new" ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> Publish()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("cid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "publish course", id, "Publishing needs the Extra right on Courses.");
            }
            bool live;
            string title;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                CourseInfo? k = Mine(site, c, id);
                if (k == null)
                {
                    response.ExecuteScript("AdminCoursesJs.toast('That course was not found.');");
                    return response;
                }
                if (!k.Published && k.Lessons.Count == 0)
                {
                    response.ExecuteScript("AdminCoursesJs.toast('Add at least one lesson before publishing.');");
                    return response;
                }
                k.Published = !k.Published;
                live = k.Published;
                title = k.Title;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, live ? "publish course" : "unpublish course", title, true);
            response.ExecuteScript("AdminCoursesJs.changed('" + (live ? "Course published." : "Course is a draft again. Students who already joined keep access.") + "', '" + Ui.Js(id) + "');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("cid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "delete course", id, "Deleting needs the Extra right on Courses.");
            }
            string title = string.Empty;
            string problem = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                CourseInfo? k = Mine(site, c, id);
                if (k == null)
                {
                    response.ExecuteScript("AdminCoursesJs.toast('That course was not found.');");
                    return response;
                }
                if ((await Store.Rows<Enrollment>("enrollments")).Any(e => e.CourseId == id) || (await Store.Rows<Order>("orders")).Any(o => o.CourseId == id && o.Status == "paid"))
                {
                    problem = "Students have joined this course, so it cannot be deleted. Unpublish it instead.";
                }
                else
                {
                    title = k.Title;
                    site.Courses.Remove(k);
                    site.Quizzes.RemoveAll(q => q.CourseId == id);
                    await Store.SaveSite(site);
                }
            }
            if (problem != string.Empty)
            {
                response.ExecuteScript("AdminCoursesJs.toast('" + Ui.Js(problem) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "delete course", title, true);
            response.ExecuteScript("AdminCoursesJs.changed('Course deleted.', '');");
            return response;
        }

        public async Task<ApiResponse> LessonForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "view lesson", AreaKey, "You do not have access to courses.");
            }
            SiteData site = await Store.LoadSite();
            CourseInfo? k = Mine(site, c, GetDataValue("cid") ?? string.Empty);
            string lid = GetDataValue("lid") ?? string.Empty;
            LessonInfo? l = lid == "new" ? new LessonInfo { Minutes = 8 } : k?.Lessons.FirstOrDefault(x => x.Id == lid);
            if (k == null || l == null)
            {
                response.ExecuteScript("AdminCoursesJs.toast('That lesson was not found.');");
                return response;
            }
            bool edit = Access.Can(c.U, AreaKey, 'E');
            string ro = edit ? string.Empty : " disabled";
            string dirty = edit ? " oninput=\"AdminCoursesJs.markDirty()\" onchange=\"AdminCoursesJs.markDirty()\"" : string.Empty;
            List<MediaItem> media = await Media.Load();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"cu-stack\"><button type=\"button\" class=\"cu-link\" onclick=\"AdminCoursesJs.back('" + Ui.Js(k.Id) + "')\">&larr; Back to " + Ui.Esc(k.Title) + "</button>");
            sb.Append("<label class=\"cu-field\"><span>Lesson title</span><input type=\"text\" id=\"cu-ltitle\" maxlength=\"80\" value=\"" + Ui.Esc(l.Title) + "\"" + ro + dirty + "><em class=\"cu-err\" id=\"cu-e-ltitle\"></em></label>");
            sb.Append("<div class=\"cu-grid2\"><label class=\"cu-field\"><span>Length (minutes)</span><input type=\"number\" id=\"cu-lmin\" min=\"1\" max=\"600\" value=\"" + l.Minutes + "\"" + ro + dirty + "><em class=\"cu-err\" id=\"cu-e-lmin\"></em></label>");
            sb.Append("<label class=\"cu-check cu-lfree\"><input type=\"checkbox\" id=\"cu-lfree\"" + (l.Free ? " checked" : string.Empty) + ro + dirty + "><span>Free preview for everyone</span></label></div>");
            sb.Append("<div class=\"cu-field\"><span>Video</span>" + Ad.MediaSelect("cu", "cu-lvideo", "AdminCoursesJs", media, "video", l.Video).Replace("<select ", "<select" + ro + " ") + "<small>Upload videos in the <a href=\"AdminMedia\">Media library</a>.</small></div>");
            sb.Append("<div class=\"cu-field\"><span>Video poster</span>" + Ad.MediaSelect("cu", "cu-lposter", "AdminCoursesJs", media, "photo", l.Poster).Replace("<select ", "<select" + ro + " ") + "</div>");
            sb.Append("<label class=\"cu-field\"><span>One-line summary</span><input type=\"text\" id=\"cu-lsum\" maxlength=\"160\" value=\"" + Ui.Esc(l.Summary) + "\"" + ro + dirty + "></label>");
            sb.Append("<label class=\"cu-field\"><span>Notes for students</span><textarea id=\"cu-lnotes\" rows=\"6\" maxlength=\"3000\"" + ro + dirty + ">" + Ui.Esc(l.Notes) + "</textarea></label>");
            sb.Append("<div class=\"cu-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"cu-btn\" onclick=\"AdminCoursesJs.saveLesson('" + Ui.Js(k.Id) + "', '" + Ui.Js(lid) + "')\">Save lesson</button>");
            }
            if (lid != "new" && Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"cu-btn cu-btn-danger\" onclick=\"AdminCoursesJs.removeLesson('" + Ui.Js(k.Id) + "', '" + Ui.Js(lid) + "')\">Delete lesson</button>");
            }
            sb.Append("<button type=\"button\" class=\"cu-btn cu-btn-ghost\" onclick=\"AdminCoursesJs.back('" + Ui.Js(k.Id) + "')\">Back</button></div></div>");
            response.SetElementContents("cu-mbody", sb.ToString());
            response.ExecuteScript("AdminCoursesJs.lessonOpened('" + Ui.Js(lid == "new" ? "New lesson" : l.Title) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveLesson()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string cid = GetDataValue("cid") ?? string.Empty;
            string lid = GetDataValue("lid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "save lesson", cid, "You do not have the Edit right on Courses.");
            }
            string title = Ui.Clip(GetDataValue("ltitle"), 80);
            int minutes = Ui.Whole(GetDataValue("lmin"), 0);
            response.SetElementContents("cu-e-ltitle", title.Length < 3 ? "Please enter a lesson title of at least 3 letters." : string.Empty);
            response.SetElementContents("cu-e-lmin", minutes < 1 || minutes > 600 ? "Enter the length in minutes, from 1 to 600." : string.Empty);
            if (title.Length < 3 || minutes < 1 || minutes > 600)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string video = GetDataValue("lvideo") ?? string.Empty;
            string poster = GetDataValue("lposter") ?? string.Empty;
            if (video != string.Empty && Media.Find(media, video, "video") == null)
            {
                video = string.Empty;
            }
            if (poster != string.Empty && Media.Find(media, poster, "photo") == null)
            {
                poster = string.Empty;
            }
            string course;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                CourseInfo? k = Mine(site, c, cid);
                if (k == null)
                {
                    response.ExecuteScript("AdminCoursesJs.toast('That course was not found.');");
                    return response;
                }
                LessonInfo? l;
                if (lid == "new")
                {
                    int next = k.Lessons.Select(x => int.TryParse(x.Id.TrimStart('l'), out int n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
                    l = new LessonInfo { Id = "l" + next };
                    k.Lessons.Add(l);
                }
                else
                {
                    l = k.Lessons.FirstOrDefault(x => x.Id == lid);
                }
                if (l == null)
                {
                    response.ExecuteScript("AdminCoursesJs.toast('That lesson was not found.');");
                    return response;
                }
                l.Title = title;
                l.Minutes = minutes;
                l.Video = video;
                l.Poster = poster;
                l.Summary = Ui.Clip(GetDataValue("lsum"), 160);
                l.Notes = Ui.Clip(GetDataValue("lnotes"), 3000);
                l.Free = GetDataValue("lfree") == "1";
                course = k.Title;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, lid == "new" ? "add lesson" : "edit lesson", course + " / " + title, true);
            response.ExecuteScript("AdminCoursesJs.lessonSaved('" + Ui.Js(cid) + "');");
            return response;
        }

        public async Task<ApiResponse> RemoveLesson()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string cid = GetDataValue("cid") ?? string.Empty;
            string lid = GetDataValue("lid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "delete lesson", cid, "Deleting needs the Extra right on Courses.");
            }
            string label = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                CourseInfo? k = Mine(site, c, cid);
                LessonInfo? l = k?.Lessons.FirstOrDefault(x => x.Id == lid);
                if (k != null && l != null)
                {
                    label = k.Title + " / " + l.Title;
                    k.Lessons.Remove(l);
                    if (k.Lessons.Count == 0)
                    {
                        k.Published = false;
                    }
                    await Store.SaveSite(site);
                }
            }
            if (label == string.Empty)
            {
                response.ExecuteScript("AdminCoursesJs.toast('That lesson was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, "delete lesson", label, true);
            response.ExecuteScript("AdminCoursesJs.lessonSaved('" + Ui.Js(cid) + "');");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminCoursesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string cid = GetDataValue("cid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminCoursesJs", c, "move lesson", cid, "You do not have the Edit right on Courses.");
            }
            int dir = GetDataValue("dir") == "-1" ? -1 : 1;
            string lid = GetDataValue("lid") ?? string.Empty;
            string label = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                CourseInfo? k = Mine(site, c, cid);
                int at = k == null ? -1 : k.Lessons.FindIndex(x => x.Id == lid);
                if (k != null && at >= 0 && at + dir >= 0 && at + dir < k.Lessons.Count)
                {
                    LessonInfo l = k.Lessons[at];
                    k.Lessons.RemoveAt(at);
                    k.Lessons.Insert(at + dir, l);
                    label = k.Title + " / " + l.Title;
                    await Store.SaveSite(site);
                }
            }
            if (label != string.Empty)
            {
                await Store.Log(c.U.Id, "move lesson", label, true);
            }
            response.ExecuteScript("AdminCoursesJs.lessonSaved('" + Ui.Js(cid) + "');");
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
