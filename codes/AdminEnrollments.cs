using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminEnrollments : WebPage
    {
        private const string AreaKey = "enrollments";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Enrollments | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"er-chip" + (key == cur ? " er-act" : string.Empty) + "\" onclick=\"AdminEnrollmentsJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("active", "Active", cur) + Chip("locked", "Locked", cur) + Chip("done", "Finished", cur);
        }

        private static List<CourseInfo> Visible(SiteData site, Ctx c)
        {
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            return site.Courses.Where(k => own == null || own.Contains(k.Id)).OrderBy(k => k.Title).ToList();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminEnrollmentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "er", "AdminEnrollments", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("er-main", Ad.NoAccess("er", "enrollments"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"er-ph\"><div><h1>Enrollments</h1><p>Who can open which course, and how far they are.</p></div>");
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"er-btn\" onclick=\"AdminEnrollmentsJs.grantForm()\">Grant access</button>");
            }
            sb.Append("</div><div class=\"er-tools\"><input type=\"search\" id=\"er-q\" placeholder=\"Search student or course\" aria-label=\"Search\" oninput=\"AdminEnrollmentsJs.search()\"><select id=\"er-course\" aria-label=\"Course\" onchange=\"AdminEnrollmentsJs.course()\"><option value=\"\">All courses</option>");
            foreach (CourseInfo k in Visible(site, c))
            {
                sb.Append("<option value=\"" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Title) + "</option>");
            }
            sb.Append("</select><div class=\"er-chips\" id=\"er-chips\">" + Chips("all") + "</div></div><div id=\"er-list\"></div>");
            response.SetElementContents("er-main", sb.ToString());
            response.ExecuteScript("AdminEnrollmentsJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminEnrollmentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminEnrollmentsJs", c, "view enrollments", AreaKey, "You do not have access to enrollments.");
            }
            SiteData site = await Store.LoadSite();
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            string cf = Ui.Clip(GetDataValue("cf"), 20);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            List<Student> students = await Store.Rows<Student>("students");
            List<Enrollment> all = (await Store.Rows<Enrollment>("enrollments")).Where(e => own == null || own.Contains(e.CourseId)).ToList();
            var rows = all.Select(e => new
            {
                E = e,
                S = students.FirstOrDefault(s => s.Id == e.StudentId),
                K = site.Courses.FirstOrDefault(k => k.Id == e.CourseId)
            }).Where(x => x.S != null && x.K != null).OrderByDescending(x => x.E.Started).ToList();
            var filtered = rows.Where(x => (cf.Length == 0 || x.E.CourseId == cf) && (q.Length == 0 || Ui.Has(x.S!.Name, q) || Ui.Has(x.K!.Title, q)) && (st == "all" || (st == "active" && x.E.Status == "active") || (st == "locked" && x.E.Status != "active") || (st == "done" && Learn.Finished(site, x.K!, x.E)))).ToList();
            pg = Math.Min(pg, Ad.Pages(filtered.Count));
            response.SetElementContents("er-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (filtered.Count == 0)
            {
                sb.Append("<div class=\"er-empty\">No enrollments match.</div>");
            }
            else
            {
                sb.Append("<div class=\"er-wrapt\"><table class=\"er-table\"><thead><tr><th>Student</th><th>Course</th><th>Started</th><th class=\"er-num\">Progress</th><th>Status</th></tr></thead><tbody>");
                foreach (var x in filtered.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    bool fin = Learn.Finished(site, x.K!, x.E);
                    sb.Append("<tr class=\"er-row\" tabindex=\"0\" onclick=\"AdminEnrollmentsJs.open('" + Ui.Js(x.E.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminEnrollmentsJs.open('" + Ui.Js(x.E.Id) + "')}\"><td data-l=\"Student\"><b>" + Ui.Esc(x.S!.Name) + "</b><small>" + Ui.Esc(x.S.Email) + "</small></td><td data-l=\"Course\">" + Ui.Esc(x.K!.Title) + "</td><td data-l=\"Started\">" + Ui.Day(x.E.Started) + "</td><td data-l=\"Progress\" class=\"er-num\">" + Learn.Percent(x.K, x.E) + "%</td><td data-l=\"Status\">" + Ad.Pill("er", x.E.Status != "active" ? "off" : fin ? "new" : "go", x.E.Status != "active" ? "Locked" : fin ? "Finished" : "Active") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("er", "AdminEnrollmentsJs", pg, filtered.Count, "enrollment", "enrollments"));
            }
            response.SetElementContents("er-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminEnrollmentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminEnrollmentsJs", c, "view enrollment", AreaKey, "You do not have access to enrollments.");
            }
            SiteData site = await Store.LoadSite();
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            Enrollment? e = (await Store.Rows<Enrollment>("enrollments")).FirstOrDefault(x => x.Id == GetDataValue("eid") && (own == null || own.Contains(x.CourseId)));
            Student? s = e == null ? null : (await Store.Rows<Student>("students")).FirstOrDefault(x => x.Id == e.StudentId);
            CourseInfo? k = e == null ? null : site.Courses.FirstOrDefault(x => x.Id == e.CourseId);
            if (e == null || s == null || k == null)
            {
                response.ExecuteScript("AdminEnrollmentsJs.toast('That enrollment was not found.');");
                return response;
            }
            CertRecord? cert = (await Store.Rows<CertRecord>("certificates")).Where(x => x.StudentId == s.Id && x.CourseId == k.Id).OrderByDescending(x => x.Issued).FirstOrDefault();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"er-stack\"><div class=\"er-line\">" + Ad.Pill("er", e.Status == "active" ? "go" : "off", e.Status == "active" ? "Active" : "Locked") + (cert == null ? string.Empty : Ad.Pill("er", cert.Revoked ? "bad" : "new", cert.Revoked ? "Certificate revoked" : "Certificate " + cert.Code)) + "</div>");
            sb.Append("<dl class=\"er-kv\"><dt>Student</dt><dd>" + Ui.Esc(s.Name) + " &middot; " + Ui.Esc(s.Email) + "</dd><dt>Course</dt><dd>" + Ui.Esc(k.Title) + "</dd><dt>Started</dt><dd>" + Ui.Day(e.Started) + "</dd><dt>Order</dt><dd>" + (e.OrderId == string.Empty ? "Granted by staff" : Ui.Esc(e.OrderId)) + "</dd><dt>Progress</dt><dd>" + Learn.Done(k, e) + " of " + k.Lessons.Count + " lessons (" + Learn.Percent(k, e) + "%)</dd>");
            if (e.LockedOn.HasValue)
            {
                sb.Append("<dt>Locked</dt><dd>" + Ui.Day(e.LockedOn.Value) + "</dd>");
            }
            sb.Append("</dl><div><h3>Lessons and quizzes</h3><ul class=\"er-rowlist\">");
            foreach (Step step in Learn.Sequence(site, k))
            {
                bool d = Learn.StepDone(e, step);
                QuizResult? r = step.Kind == "quiz" ? e.Quizzes.FirstOrDefault(z => z.QuizId == step.Id) : null;
                string tail = step.Kind == "quiz" ? (r == null ? "Not tried" : r.Best + "% best, " + Ui.Count(r.Attempts, "try", "tries")) : (d ? "Done" : "Not yet");
                sb.Append("<li><div><b>" + Ui.Esc(step.Title) + "</b><small class=\"er-sub\">" + (step.Kind == "quiz" ? "Quiz" : "Lesson") + "</small></div><div>" + Ad.Pill("er", d ? "go" : "off", tail) + "</div></li>");
            }
            sb.Append("</ul></div><div class=\"er-acts\">");
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                if (e.Status == "active")
                {
                    sb.Append("<button type=\"button\" class=\"er-btn er-btn-danger\" onclick=\"AdminEnrollmentsJs.end('" + Ui.Js(e.Id) + "')\">End access</button>");
                }
                else
                {
                    sb.Append("<button type=\"button\" class=\"er-btn\" onclick=\"AdminEnrollmentsJs.restore('" + Ui.Js(e.Id) + "')\">Restore access</button>");
                }
            }
            sb.Append("<button type=\"button\" class=\"er-btn er-btn-ghost\" onclick=\"AdminEnrollmentsJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("er-mbody", sb.ToString());
            response.ExecuteScript("AdminEnrollmentsJs.opened('" + Ui.Js(s.Name + " · " + k.Title) + "');");
            return response;
        }

        public async Task<ApiResponse> GrantForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminEnrollmentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminEnrollmentsJs", c, "open grant access", AreaKey, "Granting access needs the Extra right on Enrollments.");
            }
            SiteData site = await Store.LoadSite();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"er-stack\"><p class=\"er-hint\">Gives a student access to a course without a payment, for example a scholarship or a make-good. No order is made.</p><label class=\"er-field\"><span>Student</span><select id=\"er-gs\" onchange=\"AdminEnrollmentsJs.markDirty()\"><option value=\"\">Choose a student</option>");
            foreach (Student s in (await Store.Rows<Student>("students")).Where(x => x.Active).OrderBy(x => x.Name))
            {
                sb.Append("<option value=\"" + Ui.Esc(s.Id) + "\">" + Ui.Esc(s.Name) + " (" + Ui.Esc(s.Email) + ")</option>");
            }
            sb.Append("</select><em class=\"er-err\" id=\"er-e-gs\"></em></label><label class=\"er-field\"><span>Course</span><select id=\"er-gc\" onchange=\"AdminEnrollmentsJs.markDirty()\"><option value=\"\">Choose a course</option>");
            foreach (CourseInfo k in Visible(site, c))
            {
                sb.Append("<option value=\"" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Title) + "</option>");
            }
            sb.Append("</select><em class=\"er-err\" id=\"er-e-gc\"></em></label><div class=\"er-acts\"><button type=\"button\" class=\"er-btn\" onclick=\"AdminEnrollmentsJs.grant()\">Grant access</button><button type=\"button\" class=\"er-btn er-btn-ghost\" onclick=\"AdminEnrollmentsJs.closeModal()\">Cancel</button></div></div>");
            response.SetElementContents("er-mbody", sb.ToString());
            response.ExecuteScript("AdminEnrollmentsJs.opened('Grant access');");
            return response;
        }

        public async Task<ApiResponse> Grant()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminEnrollmentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string sid = GetDataValue("gsid") ?? string.Empty;
            string cid = GetDataValue("gcid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminEnrollmentsJs", c, "grant access", sid + " · " + cid, "Granting access needs the Extra right on Enrollments.");
            }
            SiteData site = await Store.LoadSite();
            CourseInfo? k = Visible(site, c).FirstOrDefault(x => x.Id == cid);
            Student? s = (await Store.Rows<Student>("students")).FirstOrDefault(x => x.Id == sid && x.Active);
            response.SetElementContents("er-e-gs", s == null ? "Choose a student." : string.Empty);
            response.SetElementContents("er-e-gc", k == null ? "Choose a course." : string.Empty);
            if (s == null || k == null)
            {
                return response;
            }
            string message;
            using (await Store.Hold())
            {
                List<Enrollment> all = await Store.Rows<Enrollment>("enrollments");
                Enrollment? e = Learn.EnrollmentOf(all, s.Id, k.Id);
                if (e != null && e.Status == "active")
                {
                    message = s.Name + " already has access to this course.";
                }
                else
                {
                    if (e == null)
                    {
                        all.Add(new Enrollment { Id = "E-" + Store.NewCode(6), StudentId = s.Id, CourseId = k.Id, Started = DateTime.UtcNow });
                    }
                    else
                    {
                        e.Status = "active";
                        e.LockedOn = null;
                    }
                    await Store.SaveRows("enrollments", all);
                    message = "Access granted to " + s.Name + ".";
                    await Store.Log(c.U.Id, "grant access", s.Name + " · " + k.Title, true);
                }
            }
            response.ExecuteScript("AdminEnrollmentsJs.changed('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task<ApiResponse> Switch(bool active, string action, string done)
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminEnrollmentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("eid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminEnrollmentsJs", c, action, id, "This needs the Extra right on Enrollments.");
            }
            SiteData site = await Store.LoadSite();
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            string label = string.Empty;
            using (await Store.Hold())
            {
                List<Enrollment> all = await Store.Rows<Enrollment>("enrollments");
                Enrollment? e = all.FirstOrDefault(x => x.Id == id && (own == null || own.Contains(x.CourseId)));
                if (e != null)
                {
                    e.Status = active ? "active" : "locked";
                    e.LockedOn = active ? null : DateTime.UtcNow;
                    await Store.SaveRows("enrollments", all);
                    string who = (await Store.Rows<Student>("students")).FirstOrDefault(s => s.Id == e.StudentId)?.Name ?? e.StudentId;
                    label = who + " · " + (site.Courses.FirstOrDefault(k => k.Id == e.CourseId)?.Title ?? e.CourseId);
                }
            }
            if (label == string.Empty)
            {
                response.ExecuteScript("AdminEnrollmentsJs.toast('That enrollment was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, action, label, true);
            response.ExecuteScript("AdminEnrollmentsJs.changed('" + done + "');");
            return response;
        }

        public Task<ApiResponse> End()
        {
            return Switch(false, "end access", "Access ended. Progress is kept.");
        }

        public Task<ApiResponse> Restore()
        {
            return Switch(true, "restore access", "Access restored.");
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.SignOut(GetDataValue("tok"));
            return response;
        }
    }
}
