using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminUsers : WebPage
    {
        private const string AreaKey = "users";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Users and rights | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"us-chip" + (key == cur ? " us-act" : string.Empty) + "\" onclick=\"AdminUsersJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("managers", "Managers", cur) + Chip("staff", "Staff", cur) + Chip("off", "Switched off", cur);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminUsersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "us", "AdminUsers", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("us-main", Ad.NoAccess("us", "users and rights"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"us-ph\"><div><h1>Users and rights</h1><p>Who can sign in, and what each person may do.</p></div><button type=\"button\" class=\"us-btn\" onclick=\"AdminUsersJs.open('new')\">New user</button></div>");
            sb.Append("<div class=\"us-note us-note-" + (Store.Demo() ? "wait" : "ok") + "\">" + (Store.Demo() ? "Demo mode: any user name and password signs in as a demo manager. An account listed here signs in as that person when the password is right, so you can try each role." : "Production: only the accounts listed here can sign in.") + "</div>");
            sb.Append("<div class=\"us-tools\"><input type=\"search\" id=\"us-q\" placeholder=\"Search name or user name\" aria-label=\"Search\" oninput=\"AdminUsersJs.search()\"><div class=\"us-chips\" id=\"us-chips\">" + Chips("all") + "</div></div><div id=\"us-list\"></div>");
            response.SetElementContents("us-main", sb.ToString());
            response.ExecuteScript("AdminUsersJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminUsersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminUsersJs", c, "view users", AreaKey, "Only managers can see users and rights.");
            }
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            IEnumerable<Staff> rows = (await Store.LoadStaff()).OrderByDescending(u => u.Admin).ThenBy(u => u.Name);
            switch (st)
            {
                case "managers": rows = rows.Where(u => u.Admin); break;
                case "staff": rows = rows.Where(u => !u.Admin); break;
                case "off": rows = rows.Where(u => !u.Active); break;
            }
            if (q.Length > 0)
            {
                rows = rows.Where(u => Ui.Has(u.Name, q) || Ui.Has(u.Login, q) || Ui.Has(u.Title, q));
            }
            List<Staff> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            response.SetElementContents("us-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"us-empty\">No users match.</div>");
            }
            else
            {
                sb.Append("<div class=\"us-wrapt\"><table class=\"us-table\"><thead><tr><th>Person</th><th>User name</th><th>Can do</th><th>Status</th></tr></thead><tbody>");
                foreach (Staff u in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    string status = !u.Active ? "Switched off" : u.MustChange ? "Must choose password" : u.Admin ? "Manager" : "Active";
                    sb.Append("<tr class=\"us-row\" tabindex=\"0\" onclick=\"AdminUsersJs.open('" + Ui.Js(u.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminUsersJs.open('" + Ui.Js(u.Id) + "')}\"><td data-l=\"Person\"><b>" + Ui.Esc(u.Name) + "</b><small>" + Ui.Esc(u.Title) + "</small></td><td data-l=\"User name\">" + Ui.Esc(u.Login) + "</td><td data-l=\"Can do\">" + Ui.Esc(Access.Words(u)) + "</td><td data-l=\"Status\">" + Ad.Pill("us", !u.Active ? "off" : u.MustChange ? "wait" : u.Admin ? "new" : "go", status) + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("us", "AdminUsersJs", pg, list.Count, "user", "users"));
            }
            response.SetElementContents("us-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminUsersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminUsersJs", c, "view user", AreaKey, "Only managers can see users and rights.");
            }
            SiteData site = await Store.LoadSite();
            string id = GetDataValue("uid") ?? string.Empty;
            bool isNew = id == "new";
            Staff? u = isNew ? new Staff { Active = true, Title = "Staff" } : (await Store.LoadStaff()).FirstOrDefault(x => x.Id == id);
            if (u == null)
            {
                response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                return response;
            }
            bool self = !isNew && u.Id == c.U.Id;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"us-stack\"><div class=\"us-grid2\"><label class=\"us-field\"><span>Name</span><input type=\"text\" id=\"us-name\" maxlength=\"60\" value=\"" + Ui.Esc(u.Name) + "\" oninput=\"AdminUsersJs.markDirty()\"><em class=\"us-err\" id=\"us-e-name\"></em></label>");
            sb.Append("<label class=\"us-field\"><span>User name</span><input type=\"text\" id=\"us-login\" maxlength=\"30\" autocapitalize=\"none\" value=\"" + Ui.Esc(u.Login) + "\" oninput=\"AdminUsersJs.markDirty()\"><em class=\"us-err\" id=\"us-e-login\"></em></label></div>");
            sb.Append("<label class=\"us-field\"><span>Job title</span><input type=\"text\" id=\"us-title\" maxlength=\"60\" value=\"" + Ui.Esc(u.Title) + "\" oninput=\"AdminUsersJs.markDirty()\"></label>");
            sb.Append("<label class=\"us-check\"><input type=\"checkbox\" id=\"us-admin\"" + (u.Admin ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + " onchange=\"AdminUsersJs.manager()\"><span>Manager: everything, including users and settings</span></label>");
            sb.Append("<label class=\"us-check\"><input type=\"checkbox\" id=\"us-active\"" + (u.Active ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + " onchange=\"AdminUsersJs.markDirty()\"><span>Can sign in</span></label>");
            sb.Append("<div class=\"us-box\" id=\"us-rights\"><h3>Rights</h3><p class=\"us-hint\">View shows a menu item and its records. Edit changes them. The Extra right unlocks the sensitive action named in the last column.</p><div class=\"us-wrapt\"><table class=\"us-table us-grid\"><thead><tr><th>Area</th><th class=\"us-c\">View</th><th class=\"us-c\">Edit</th><th>Extra</th></tr></thead><tbody>");
            foreach (Area a in Access.Areas.Where(x => !x.AdminOnly))
            {
                string have = u.Perms.TryGetValue(a.Key, out string? l) ? Access.Clean(a, l) : string.Empty;
                sb.Append("<tr><td data-l=\"Area\"><b>" + Ui.Esc(a.Name) + "</b>" + (a.Own ? "<small>Can be limited to own</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"View\" class=\"us-c\"><input type=\"checkbox\" data-a=\"" + a.Key + "\" data-r=\"V\" aria-label=\"View " + Ui.Esc(a.Name) + "\"" + (have.Contains('V') ? " checked" : string.Empty) + " onchange=\"AdminUsersJs.right(this)\"></td>");
                sb.Append("<td data-l=\"Edit\" class=\"us-c\">" + (a.NoEdit ? "<span class=\"us-none\">-</span>" : "<input type=\"checkbox\" data-a=\"" + a.Key + "\" data-r=\"E\" aria-label=\"Edit " + Ui.Esc(a.Name) + "\"" + (have.Contains('E') ? " checked" : string.Empty) + " onchange=\"AdminUsersJs.right(this)\">") + "</td>");
                sb.Append("<td data-l=\"Extra\">" + (a.Extra == string.Empty ? "<span class=\"us-none\">-</span>" : "<label class=\"us-check\"><input type=\"checkbox\" data-a=\"" + a.Key + "\" data-r=\"X\"" + (have.Contains('X') ? " checked" : string.Empty) + " onchange=\"AdminUsersJs.right(this)\"><span>" + Ui.Esc(a.Extra) + "</span></label>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append("<div class=\"us-grid2 us-own\"><label class=\"us-field\"><span>Linked instructor profile</span><select id=\"us-prov\" onchange=\"AdminUsersJs.markDirty()\">" + Ad.Opts(site.Instructors.Select(i => (i.Id, i.Name)), u.ProviderId, "Not an instructor") + "</select></label>");
            sb.Append("<label class=\"us-check\"><input type=\"checkbox\" id=\"us-own\"" + (u.Own ? " checked" : string.Empty) + " onchange=\"AdminUsersJs.markDirty()\"><span>See only own courses and students (Courses, Quizzes, Students, Enrollments)</span></label></div></div>");
            sb.Append("<div class=\"us-note us-note-ok\" id=\"us-pw\" hidden></div>");
            if (self)
            {
                sb.Append("<p class=\"us-hint\">This is your own account. You cannot remove your manager right or switch yourself off.</p>");
            }
            sb.Append("<div class=\"us-acts\"><button type=\"button\" class=\"us-btn\" onclick=\"AdminUsersJs.save('" + Ui.Js(isNew ? "new" : u.Id) + "')\">" + (isNew ? "Create user" : "Save") + "</button>");
            if (!isNew)
            {
                sb.Append("<button type=\"button\" class=\"us-btn us-btn-ghost\" onclick=\"AdminUsersJs.reset('" + Ui.Js(u.Id) + "')\">Reset password</button>");
            }
            sb.Append("<button type=\"button\" class=\"us-btn us-btn-ghost\" onclick=\"AdminUsersJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("us-mbody", sb.ToString());
            response.ExecuteScript("AdminUsersJs.opened('" + Ui.Js(isNew ? "New user" : u.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminUsersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("uid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminUsersJs", c, "save user", id, "Only managers can change users and rights.");
            }
            string name = Ui.Clip(GetDataValue("uname"), 60);
            string login = Ui.Clip(GetDataValue("ulogin"), 30).ToLowerInvariant();
            bool validLogin = login.Length >= 3 && login.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-');
            response.SetElementContents("us-e-name", name.Length < 2 ? "Please enter a name." : string.Empty);
            response.SetElementContents("us-e-login", validLogin ? string.Empty : "Use 3 to 30 letters, digits, dots, dashes or underscores.");
            if (name.Length < 2 || !validLogin)
            {
                return response;
            }
            bool admin = GetDataValue("uadmin") == "1";
            bool active = GetDataValue("uactive") == "1";
            string fresh = string.Empty;
            string taken = string.Empty;
            string problem = string.Empty;
            string savedId = id;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                SiteData site = await Store.LoadSite();
                if (staff.Any(x => x.Id != id && x.Login.Equals(login, StringComparison.OrdinalIgnoreCase)))
                {
                    taken = "That user name is already used.";
                }
                Staff? u = id == "new" ? null : staff.FirstOrDefault(x => x.Id == id);
                if (taken == string.Empty && id != "new" && u == null)
                {
                    problem = "That user was not found.";
                }
                if (taken == string.Empty && problem == string.Empty)
                {
                    bool self = u != null && u.Id == c.U.Id;
                    if (self)
                    {
                        admin = true;
                        active = true;
                    }
                    List<Staff> others = staff.Where(x => u == null || x.Id != u.Id).ToList();
                    if (!(admin && active) && !others.Any(x => x.Admin && x.Active) && c.U.Id != Auth.DemoUser.Id)
                    {
                        problem = "At least one active manager must remain.";
                    }
                    if (problem == string.Empty)
                    {
                        if (u == null)
                        {
                            u = new Staff { Id = "u-" + login };
                            while (staff.Any(x => x.Id == u.Id))
                            {
                                u.Id += "x";
                            }
                            fresh = Access.NewPassword();
                            u.Hash = Access.HashPassword(fresh);
                            u.MustChange = true;
                            staff.Add(u);
                        }
                        u.Name = name;
                        u.Login = login;
                        u.Title = Ui.Clip(GetDataValue("utitle"), 60);
                        u.Admin = admin;
                        u.Active = active;
                        string prov = GetDataValue("uprov") ?? string.Empty;
                        u.ProviderId = site.Instructors.Any(i => i.Id == prov) ? prov : string.Empty;
                        u.Own = GetDataValue("uown") == "1" && u.ProviderId != string.Empty;
                        u.Perms = new Dictionary<string, string>();
                        foreach (string part in (GetDataValue("uperms") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] kv = part.Split(':', 2);
                            Area? a = kv.Length == 2 ? Access.Find(kv[0]) : null;
                            if (a != null && !a.AdminOnly)
                            {
                                string clean = Access.Clean(a, kv[1]);
                                if (clean.Length > 0)
                                {
                                    u.Perms[a.Key] = clean;
                                }
                            }
                        }
                        savedId = u.Id;
                        await Store.SaveStaff(staff);
                    }
                }
            }
            if (taken != string.Empty)
            {
                response.SetElementContents("us-e-login", taken);
                return response;
            }
            if (problem != string.Empty)
            {
                response.ExecuteScript("AdminUsersJs.toast('" + Ui.Js(problem) + "');");
                return response;
            }
            if (id != "new" && !active)
            {
                await Auth.CloseAllFor(savedId);
            }
            await Store.Log(c.U.Id, id == "new" ? "create user" : "edit user rights", name + " (" + login + ")", true);
            response.ExecuteScript("AdminUsersJs.saved(" + (fresh == string.Empty ? "''" : "'" + Ui.Js(fresh) + "'") + ", '" + Ui.Js(login) + "');");
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminUsersJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("uid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminUsersJs", c, "reset user password", id, "Only managers can reset passwords.");
            }
            string pass = Access.NewPassword();
            string name = string.Empty;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == id);
                if (u != null)
                {
                    u.Hash = Access.HashPassword(pass);
                    u.MustChange = true;
                    name = u.Name;
                    await Store.SaveStaff(staff);
                }
            }
            if (name == string.Empty)
            {
                response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                return response;
            }
            await Auth.CloseAllFor(id);
            await Store.Log(c.U.Id, "reset user password", name, true);
            response.ExecuteScript("AdminUsersJs.shown('" + Ui.Js(pass) + "');");
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
