using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminAccount : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("My account | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminAccountJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "aa", "AdminAccount", c, site);
            bool demo = c.U.Id == Auth.DemoUser.Id;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"aa-ph\"><div><h1>My account</h1><p>Your details and password.</p></div></div>");
            sb.Append("<section class=\"aa-panel aa-stack\"><h2>" + Ui.Esc(c.U.Name) + "</h2><dl class=\"aa-kv\"><dt>User name</dt><dd>" + Ui.Esc(c.U.Login) + "</dd><dt>Job title</dt><dd>" + Ui.Esc(c.U.Title) + "</dd><dt>Can do</dt><dd>" + Ui.Esc(Access.Words(c.U)) + "</dd>");
            string linked = c.U.ProviderId == string.Empty ? string.Empty : site.Instructors.FirstOrDefault(i => i.Id == c.U.ProviderId)?.Name ?? string.Empty;
            if (linked != string.Empty)
            {
                sb.Append("<dt>Instructor</dt><dd>" + Ui.Esc(linked) + "</dd>");
            }
            sb.Append("</dl></section>");
            if (demo)
            {
                sb.Append("<section class=\"aa-panel aa-stack\"><h2>Password</h2><p class=\"aa-hint\">You are signed in as the demo manager, which has no password. Real staff accounts are made under Users and rights.</p></section>");
            }
            else
            {
                sb.Append("<section class=\"aa-panel\"><form class=\"aa-stack\" onsubmit=\"return AdminAccountJs.change()\" novalidate><h2>Change password</h2>");
                sb.Append("<label class=\"aa-field\"><span>Current password</span><input type=\"password\" id=\"aa-cur\" maxlength=\"80\" autocomplete=\"current-password\"><em class=\"aa-err\" id=\"aa-e-cur\"></em></label>");
                sb.Append("<label class=\"aa-field\"><span>New password</span><input type=\"password\" id=\"aa-new\" maxlength=\"80\" autocomplete=\"new-password\"><em class=\"aa-err\" id=\"aa-e-new\"></em><small>At least 8 characters, and different from the current one.</small></label>");
                sb.Append("<label class=\"aa-field\"><span>New password again</span><input type=\"password\" id=\"aa-new2\" maxlength=\"80\" autocomplete=\"new-password\"><em class=\"aa-err\" id=\"aa-e-new2\"></em></label>");
                sb.Append("<p class=\"aa-hint\">Your other sign-ins are closed when the password changes.</p><div class=\"aa-acts\"><button type=\"submit\" class=\"aa-btn\">Change password</button></div></form></section>");
            }
            response.SetElementContents("aa-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Change()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminAccountJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (c.U.Id == Auth.DemoUser.Id)
            {
                return await Ad.Refuse(response, "AdminAccountJs", c, "change password", "demo", "The demo manager has no password.");
            }
            string cur = GetDataValue("acur") ?? string.Empty;
            string fresh = GetDataValue("anew") ?? string.Empty;
            string again = GetDataValue("anew2") ?? string.Empty;
            string e1 = string.Empty;
            string e2 = string.Empty;
            string e3 = string.Empty;
            if (!Access.VerifyPassword(cur, c.U.Hash))
            {
                e1 = "The current password is not right.";
            }
            else if (fresh.Length < 8)
            {
                e2 = "Use at least 8 characters.";
            }
            else if (fresh == cur)
            {
                e2 = "Choose a password different from the current one.";
            }
            else if (fresh != again)
            {
                e3 = "The two new passwords are not the same.";
            }
            response.SetElementContents("aa-e-cur", e1);
            response.SetElementContents("aa-e-new", e2);
            response.SetElementContents("aa-e-new2", e3);
            if (e1 + e2 + e3 != string.Empty)
            {
                if (e1 != string.Empty)
                {
                    await Store.Log(c.U.Id, "change password", "wrong current password", false);
                }
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u == null)
                {
                    return response;
                }
                u.Hash = Access.HashPassword(fresh);
                await Store.SaveStaff(staff);
            }
            await Auth.CloseOthers(c.U.Id, c.Token);
            await Store.Log(c.U.Id, "change password", c.U.Name, true);
            response.ExecuteScript("AdminAccountJs.changed();");
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
