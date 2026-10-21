using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminInstructors : WebPage
    {
        private const string AreaKey = "instructors";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Instructors | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminInstructorsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "ia", "AdminInstructors", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("ia-main", Ad.NoAccess("ia", "instructors"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ia-ph\"><div><h1>Instructors</h1><p>The people who teach. Their profiles show on the Instructors page.</p></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"ia-btn\" onclick=\"AdminInstructorsJs.open('new')\">New instructor</button>");
            }
            sb.Append("</div><div class=\"ia-tools\"><input type=\"search\" id=\"ia-q\" placeholder=\"Search instructors\" aria-label=\"Search\" oninput=\"AdminInstructorsJs.search()\"></div><div id=\"ia-list\"></div>");
            response.SetElementContents("ia-main", sb.ToString());
            response.ExecuteScript("AdminInstructorsJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminInstructorsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminInstructorsJs", c, "view instructors", AreaKey, "You do not have access to instructors.");
            }
            SiteData site = await Store.LoadSite();
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            IEnumerable<InstructorInfo> rows = site.Instructors.OrderBy(i => i.Name);
            if (q.Length > 0)
            {
                rows = rows.Where(i => Ui.Has(i.Name, q) || Ui.Has(i.Role, q));
            }
            List<InstructorInfo> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"ia-empty\">No instructors match.</div>");
            }
            else
            {
                sb.Append("<div class=\"ia-wrapt\"><table class=\"ia-table\"><thead><tr><th>Instructor</th><th>Topic</th><th class=\"ia-num\">Courses</th><th>Status</th></tr></thead><tbody>");
                foreach (InstructorInfo i in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    sb.Append("<tr class=\"ia-row\" tabindex=\"0\" onclick=\"AdminInstructorsJs.open('" + Ui.Js(i.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminInstructorsJs.open('" + Ui.Js(i.Id) + "')}\"><td data-l=\"Instructor\"><b>" + Ui.Esc(i.Name) + "</b><small>" + Ui.Esc(i.Role) + "</small></td><td data-l=\"Topic\">" + Ui.Esc(Learn.TopicName(site, i.Topic)) + "</td><td data-l=\"Courses\" class=\"ia-num\">" + site.Courses.Count(k => k.InstructorId == i.Id) + "</td><td data-l=\"Status\">" + Ad.Pill("ia", i.Active ? "go" : "off", i.Active ? "Shown" : "Hidden") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("ia", "AdminInstructorsJs", pg, list.Count, "instructor", "instructors"));
            }
            response.SetElementContents("ia-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminInstructorsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminInstructorsJs", c, "view instructor", AreaKey, "You do not have access to instructors.");
            }
            SiteData site = await Store.LoadSite();
            string id = GetDataValue("iid") ?? string.Empty;
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool isNew = id == "new";
            if (isNew && !edit)
            {
                return await Ad.Refuse(response, "AdminInstructorsJs", c, "new instructor", AreaKey, "You do not have the Edit right on Instructors.");
            }
            InstructorInfo? i = isNew ? new InstructorInfo { Topic = site.Topics.FirstOrDefault()?.Key ?? string.Empty } : site.Instructors.FirstOrDefault(x => x.Id == id);
            if (i == null)
            {
                response.ExecuteScript("AdminInstructorsJs.toast('That instructor was not found.');");
                return response;
            }
            string ro = edit ? string.Empty : " disabled";
            string dirty = edit ? " oninput=\"AdminInstructorsJs.markDirty()\" onchange=\"AdminInstructorsJs.markDirty()\"" : string.Empty;
            List<MediaItem> media = await Media.Load();
            int courses = site.Courses.Count(k => k.InstructorId == i.Id);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ia-stack\"><div class=\"ia-grid2\"><label class=\"ia-field\"><span>Name</span><input type=\"text\" id=\"ia-name\" maxlength=\"60\" value=\"" + Ui.Esc(i.Name) + "\"" + ro + dirty + "><em class=\"ia-err\" id=\"ia-e-name\"></em></label>");
            sb.Append("<label class=\"ia-field\"><span>Role</span><input type=\"text\" id=\"ia-role\" maxlength=\"80\" value=\"" + Ui.Esc(i.Role) + "\"" + ro + dirty + "><em class=\"ia-err\" id=\"ia-e-role\"></em></label></div>");
            sb.Append("<label class=\"ia-field\"><span>Teaches</span><select id=\"ia-topic\"" + ro + dirty + ">" + Ad.Opts(site.Topics.Select(t => (t.Key, t.Label)), i.Topic, string.Empty) + "</select></label>");
            sb.Append("<label class=\"ia-field\"><span>About</span><textarea id=\"ia-bio\" rows=\"5\" maxlength=\"900\"" + ro + dirty + ">" + Ui.Esc(i.Bio) + "</textarea></label>");
            sb.Append("<div class=\"ia-field\"><span>Photo</span>" + Ad.MediaSelect("ia", "ia-photo", "AdminInstructorsJs", media, "photo", i.Photo).Replace("<select ", "<select" + ro + " ") + "<small>Pictures come from the <a href=\"AdminMedia\">Media library</a>.</small></div>");
            sb.Append("<label class=\"ia-check\"><input type=\"checkbox\" id=\"ia-active\"" + (i.Active ? " checked" : string.Empty) + ro + dirty + "><span>Show on the Instructors page</span></label>");
            sb.Append("<div class=\"ia-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"ia-btn\" onclick=\"AdminInstructorsJs.save('" + Ui.Js(isNew ? "new" : i.Id) + "')\">" + (isNew ? "Add instructor" : "Save") + "</button>");
                if (!isNew)
                {
                    sb.Append("<button type=\"button\" class=\"ia-btn ia-btn-danger\" onclick=\"AdminInstructorsJs.remove('" + Ui.Js(i.Id) + "', " + courses + ")\">Delete</button>");
                }
            }
            sb.Append("<button type=\"button\" class=\"ia-btn ia-btn-ghost\" onclick=\"AdminInstructorsJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("ia-mbody", sb.ToString());
            response.ExecuteScript("AdminInstructorsJs.opened('" + Ui.Js(isNew ? "New instructor" : i.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminInstructorsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("iid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminInstructorsJs", c, "save instructor", id, "You do not have the Edit right on Instructors.");
            }
            string name = Ui.Clip(GetDataValue("iname"), 60);
            string role = Ui.Clip(GetDataValue("irole"), 80);
            response.SetElementContents("ia-e-name", name.Length < 2 ? "Please enter a name." : string.Empty);
            response.SetElementContents("ia-e-role", role.Length < 3 ? "Add a short role, such as Photographer and teacher." : string.Empty);
            if (name.Length < 2 || role.Length < 3)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string photo = GetDataValue("iphoto") ?? string.Empty;
            if (photo != string.Empty && Media.Find(media, photo, "photo") == null)
            {
                photo = string.Empty;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                InstructorInfo? i;
                if (id == "new")
                {
                    string slug = "ins-" + new string(name.ToLowerInvariant().Split(' ')[0].Where(char.IsAsciiLetterOrDigit).ToArray());
                    string key = slug;
                    int n = 2;
                    while (site.Instructors.Any(x => x.Id == key))
                    {
                        key = slug + n++;
                    }
                    i = new InstructorInfo { Id = key };
                    site.Instructors.Add(i);
                }
                else
                {
                    i = site.Instructors.FirstOrDefault(x => x.Id == id);
                }
                if (i == null)
                {
                    response.ExecuteScript("AdminInstructorsJs.toast('That instructor was not found.');");
                    return response;
                }
                string topic = GetDataValue("itopic") ?? string.Empty;
                i.Name = name;
                i.Role = role;
                i.Topic = site.Topics.Any(t => t.Key == topic) ? topic : site.Topics.FirstOrDefault()?.Key ?? string.Empty;
                i.Bio = Ui.Clip(GetDataValue("ibio"), 900);
                i.Photo = photo;
                i.Active = GetDataValue("iactive") == "1";
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, id == "new" ? "add instructor" : "edit instructor", name, true);
            response.ExecuteScript("AdminInstructorsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminInstructorsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("iid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminInstructorsJs", c, "delete instructor", id, "You do not have the Edit right on Instructors.");
            }
            string name = string.Empty;
            string problem = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                InstructorInfo? i = site.Instructors.FirstOrDefault(x => x.Id == id);
                if (i != null)
                {
                    if (site.Courses.Any(k => k.InstructorId == id))
                    {
                        problem = "This instructor still has courses. Move the courses to someone else first, or hide the profile instead.";
                    }
                    else if ((await Store.LoadStaff()).Any(u => u.ProviderId == id))
                    {
                        problem = "A staff account is linked to this instructor. Unlink it under Users and rights first.";
                    }
                    else
                    {
                        name = i.Name;
                        site.Instructors.Remove(i);
                        await Store.SaveSite(site);
                    }
                }
            }
            if (problem != string.Empty)
            {
                response.ExecuteScript("AdminInstructorsJs.toast('" + Ui.Js(problem) + "');");
                return response;
            }
            if (name == string.Empty)
            {
                response.ExecuteScript("AdminInstructorsJs.toast('That instructor was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, "delete instructor", name, true);
            response.ExecuteScript("AdminInstructorsJs.saved();");
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
