using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminStudents : WebPage
    {
        private const string AreaKey = "students";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Students | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static async Task<HashSet<string>?> Scope(SiteData site, Ctx c)
        {
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            if (own == null)
            {
                return null;
            }
            return (await Store.Rows<Enrollment>("enrollments")).Where(e => own.Contains(e.CourseId)).Select(e => e.StudentId).ToHashSet();
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"su-chip" + (key == cur ? " su-act" : string.Empty) + "\" onclick=\"AdminStudentsJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("active", "Active", cur) + Chip("off", "Switched off", cur);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminStudentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "su", "AdminStudents", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("su-main", Ad.NoAccess("su", "students"));
                return response;
            }
            bool own = Access.IsOwn(c.U, AreaKey);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"su-ph\"><div><h1>Students</h1><p>" + (own ? "Students enrolled in your courses." : "Everyone with an account.") + "</p></div></div>");
            sb.Append("<div class=\"su-tools\"><input type=\"search\" id=\"su-q\" placeholder=\"Search name, email or phone\" aria-label=\"Search\" oninput=\"AdminStudentsJs.search()\"><div class=\"su-chips\" id=\"su-chips\">" + Chips("all") + "</div></div><div id=\"su-list\"></div>");
            response.SetElementContents("su-main", sb.ToString());
            response.ExecuteScript("AdminStudentsJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminStudentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminStudentsJs", c, "view students", AreaKey, "You do not have access to students.");
            }
            SiteData site = await Store.LoadSite();
            HashSet<string>? scope = await Scope(site, c);
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
            IEnumerable<Student> rows = (await Store.Rows<Student>("students")).Where(s => scope == null || scope.Contains(s.Id)).OrderBy(s => s.Name);
            if (st == "active")
            {
                rows = rows.Where(s => s.Active);
            }
            else if (st == "off")
            {
                rows = rows.Where(s => !s.Active);
            }
            if (q.Length > 0)
            {
                rows = rows.Where(s => Ui.Has(s.Name, q) || Ui.Has(s.Email, q) || Ui.Has(s.Phone, q));
            }
            List<Student> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            response.SetElementContents("su-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"su-empty\">No students match.</div>");
            }
            else
            {
                HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
                sb.Append("<div class=\"su-wrapt\"><table class=\"su-table\"><thead><tr><th>Student</th><th>Phone</th><th>Joined</th><th class=\"su-num\">Courses</th><th>Status</th></tr></thead><tbody>");
                foreach (Student s in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    int n = enrollments.Count(e => e.StudentId == s.Id && (own == null || own.Contains(e.CourseId)));
                    sb.Append("<tr class=\"su-row\" tabindex=\"0\" onclick=\"AdminStudentsJs.open('" + Ui.Js(s.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminStudentsJs.open('" + Ui.Js(s.Id) + "')}\"><td data-l=\"Student\"><b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Email) + "</small></td><td data-l=\"Phone\">" + (s.Phone == string.Empty ? "&ndash;" : Ui.Esc(s.Phone)) + "</td><td data-l=\"Joined\">" + Ui.Day(s.Created) + "</td><td data-l=\"Courses\" class=\"su-num\">" + n + "</td><td data-l=\"Status\">" + Ad.Pill("su", s.Active ? "go" : "off", s.Active ? "Active" : "Switched off") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("su", "AdminStudentsJs", pg, list.Count, "student", "students"));
            }
            response.SetElementContents("su-list", sb.ToString());
            return response;
        }

        private async Task<(Student? S, SiteData Site)> Target(Ctx c)
        {
            SiteData site = await Store.LoadSite();
            HashSet<string>? scope = await Scope(site, c);
            string id = GetDataValue("sid") ?? string.Empty;
            Student? s = (await Store.Rows<Student>("students")).FirstOrDefault(x => x.Id == id && (scope == null || scope.Contains(x.Id)));
            return (s, site);
        }

        private static async Task<string> Detail(Ctx c, Student s, SiteData site)
        {
            HashSet<string>? own = Ad.OwnIds(site, c.U, AreaKey);
            List<Enrollment> enrollments = (await Store.Rows<Enrollment>("enrollments")).Where(e => e.StudentId == s.Id && (own == null || own.Contains(e.CourseId))).ToList();
            List<Order> orders = (await Store.Rows<Order>("orders")).Where(o => o.StudentId == s.Id && (own == null || own.Contains(o.CourseId))).OrderByDescending(o => o.Created).ToList();
            List<CertRecord> certs = (await Store.Rows<CertRecord>("certificates")).Where(x => x.StudentId == s.Id && (own == null || own.Contains(x.CourseId))).ToList();
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"su-stack\"><div class=\"su-line\">" + Ad.Pill("su", s.Active ? "go" : "off", s.Active ? "Active" : "Switched off") + "</div>");
            sb.Append("<div class=\"su-grid2\"><label class=\"su-field\"><span>Name</span><input type=\"text\" id=\"su-name\" value=\"" + Ui.Esc(s.Name) + "\" maxlength=\"60\"" + (edit ? " oninput=\"AdminStudentsJs.markDirty()\"" : " disabled") + "><em class=\"su-err\" id=\"su-e-name\"></em></label>");
            sb.Append("<label class=\"su-field\"><span>Email</span><input type=\"text\" id=\"su-email\" value=\"" + Ui.Esc(s.Email) + "\" maxlength=\"80\"" + (edit ? " oninput=\"AdminStudentsJs.markDirty()\"" : " disabled") + "><em class=\"su-err\" id=\"su-e-email\"></em></label>");
            sb.Append("<label class=\"su-field\"><span>Phone</span><input type=\"tel\" id=\"su-phone\" value=\"" + Ui.Esc(s.Phone) + "\" maxlength=\"24\"" + (edit ? " oninput=\"AdminStudentsJs.markDirty()\"" : " disabled") + "><em class=\"su-err\" id=\"su-e-phone\"></em></label></div>");
            sb.Append("<p class=\"su-hint\">Joined " + Ui.Day(s.Created) + ". The site keeps only the name, email and phone a student gave.</p>");
            sb.Append("<div><h3>Courses</h3>");
            if (enrollments.Count == 0)
            {
                sb.Append("<p class=\"su-hint\">Not enrolled in any course.</p>");
            }
            else
            {
                sb.Append("<ul class=\"su-rowlist\">");
                foreach (Enrollment e in enrollments)
                {
                    CourseInfo? k = site.Courses.FirstOrDefault(x => x.Id == e.CourseId);
                    sb.Append("<li><div><b>" + Ui.Esc(k?.Title ?? e.CourseId) + "</b><small class=\"su-sub\">Started " + Ui.Day(e.Started) + "</small></div><div class=\"su-num\">" + (k == null ? 0 : Learn.Percent(k, e)) + "%<br>" + Ad.Pill("su", e.Status == "active" ? "go" : "off", e.Status == "active" ? "Active" : "Locked") + "</div></li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</div>");
            if (orders.Count > 0)
            {
                sb.Append("<div><h3>Orders</h3><ul class=\"su-rowlist\">");
                foreach (Order o in orders)
                {
                    sb.Append("<li><div><b>" + Ui.Esc(o.Id) + "</b><small class=\"su-sub\">" + Ui.Esc(o.CourseTitle) + " &middot; " + Ui.Day(o.Created) + "</small></div><div class=\"su-num\">" + Ui.Money(o.Amount) + "<br><small class=\"su-sub\">" + Ui.Esc(o.Status + (Learn.Refunded(o) > 0 ? ", refunded " + Ui.Money(Learn.Refunded(o)) : string.Empty)) + "</small></div></li>");
                }
                sb.Append("</ul></div>");
            }
            if (certs.Count > 0)
            {
                sb.Append("<div><h3>Certificates</h3><ul class=\"su-rowlist\">");
                foreach (CertRecord x in certs)
                {
                    sb.Append("<li><div><b>" + Ui.Esc(x.Code) + "</b><small class=\"su-sub\">" + Ui.Esc(x.CourseTitle) + "</small></div><div>" + Ad.Pill("su", x.Revoked ? "bad" : "go", x.Revoked ? "Revoked" : "Valid") + "</div></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("<div class=\"su-note su-note-ok\" id=\"su-pw\" hidden></div><div class=\"su-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"su-btn\" onclick=\"AdminStudentsJs.save('" + Ui.Js(s.Id) + "')\">Save</button>");
            }
            if (extra)
            {
                sb.Append("<button type=\"button\" class=\"su-btn su-btn-ghost\" onclick=\"AdminStudentsJs.reset('" + Ui.Js(s.Id) + "')\">Reset password</button><button type=\"button\" class=\"su-btn su-btn-danger\" onclick=\"AdminStudentsJs.toggle('" + Ui.Js(s.Id) + "', " + (s.Active ? "true" : "false") + ")\">" + (s.Active ? "Switch off" : "Switch on") + "</button>");
            }
            sb.Append("<button type=\"button\" class=\"su-btn su-btn-ghost\" onclick=\"AdminStudentsJs.closeModal()\">Close</button></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminStudentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminStudentsJs", c, "view student", AreaKey, "You do not have access to students.");
            }
            (Student? s, SiteData site) = await Target(c);
            if (s == null)
            {
                response.ExecuteScript("AdminStudentsJs.toast('That student was not found.');");
                return response;
            }
            response.SetElementContents("su-mbody", await Detail(c, s, site));
            response.ExecuteScript("AdminStudentsJs.opened('" + Ui.Js(s.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminStudentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("sid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminStudentsJs", c, "edit student", id, "You do not have the Edit right on Students.");
            }
            (Student? s, SiteData _) = await Target(c);
            if (s == null)
            {
                response.ExecuteScript("AdminStudentsJs.toast('That student was not found.');");
                return response;
            }
            string name = Ui.Clip(GetDataValue("name"), 60);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 24);
            bool badPhone = phone.Length > 0 && !Ui.IsPhone(phone);
            response.SetElementContents("su-e-name", name.Length < 2 ? "Please enter a name." : string.Empty);
            response.SetElementContents("su-e-email", !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty);
            response.SetElementContents("su-e-phone", badPhone ? "Please enter a valid phone number." : string.Empty);
            if (name.Length < 2 || !Ui.IsEmail(email) || badPhone)
            {
                return response;
            }
            bool taken = false;
            using (await Store.Hold())
            {
                List<Student> all = await Store.Rows<Student>("students");
                Student? t = all.FirstOrDefault(x => x.Id == id);
                if (t != null)
                {
                    if (all.Any(x => x.Id != id && x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                    {
                        taken = true;
                    }
                    else
                    {
                        t.Name = name;
                        t.Email = email;
                        t.Phone = phone;
                        await Store.SaveRows("students", all);
                    }
                }
            }
            if (taken)
            {
                response.SetElementContents("su-e-email", "Another account already uses this email.");
                return response;
            }
            await Store.Log(c.U.Id, "edit student", name, true);
            response.ExecuteScript("AdminStudentsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminStudentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("sid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminStudentsJs", c, "reset student password", id, "Resetting needs the Extra right on Students.");
            }
            (Student? s, SiteData _) = await Target(c);
            if (s == null)
            {
                response.ExecuteScript("AdminStudentsJs.toast('That student was not found.');");
                return response;
            }
            string pass = Access.NewPassword();
            using (await Store.Hold())
            {
                List<Student> all = await Store.Rows<Student>("students");
                Student? t = all.FirstOrDefault(x => x.Id == id);
                if (t != null)
                {
                    t.Hash = Access.HashPassword(pass);
                    await Store.SaveRows("students", all);
                }
            }
            await Auth.CloseStudent(id);
            await Store.Log(c.U.Id, "reset student password", s.Name, true);
            response.ExecuteScript("AdminStudentsJs.shown('" + Ui.Js(pass) + "');");
            return response;
        }

        public async Task<ApiResponse> Toggle()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminStudentsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("sid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminStudentsJs", c, "switch student", id, "Switching an account needs the Extra right on Students.");
            }
            (Student? s, SiteData _) = await Target(c);
            if (s == null)
            {
                response.ExecuteScript("AdminStudentsJs.toast('That student was not found.');");
                return response;
            }
            bool now;
            using (await Store.Hold())
            {
                List<Student> all = await Store.Rows<Student>("students");
                Student t = all.First(x => x.Id == id);
                t.Active = !t.Active;
                now = t.Active;
                await Store.SaveRows("students", all);
            }
            if (!now)
            {
                await Auth.CloseStudent(id);
            }
            await Store.Log(c.U.Id, now ? "switch student on" : "switch student off", s.Name, true);
            response.ExecuteScript("AdminStudentsJs.switched('" + (now ? "Account switched on." : "Account switched off. The student is signed out.") + "');");
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
