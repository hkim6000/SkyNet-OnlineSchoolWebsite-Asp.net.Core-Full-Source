using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Settings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Field(string id, string label, string value, int max, bool edit, string hint = "")
        {
            return "<label class=\"st-field\"><span>" + Ui.Esc(label) + "</span><input type=\"text\" id=\"st-" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + (edit ? " oninput=\"AdminSettingsJs.touch()\"" : " disabled") + "><em class=\"st-err\" id=\"st-e-" + id + "\"></em>" + (hint == string.Empty ? string.Empty : "<small>" + Ui.Esc(hint) + "</small>") + "</label>";
        }

        private static string Num(string id, string label, int value, int min, int max, bool edit, string hint)
        {
            return "<label class=\"st-field\"><span>" + Ui.Esc(label) + "</span><input type=\"number\" id=\"st-" + id + "\" min=\"" + min + "\" max=\"" + max + "\" value=\"" + value + "\"" + (edit ? " oninput=\"AdminSettingsJs.touch()\"" : " disabled") + "><em class=\"st-err\" id=\"st-e-" + id + "\"></em><small>" + Ui.Esc(hint) + "</small></label>";
        }

        private static string Status(string kind, string text)
        {
            return Ad.Pill("st", kind, text);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminSettingsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "st", "AdminSettings", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("st-main", Ad.NoAccess("st", "settings"));
                return response;
            }
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"st-ph\"><div><h1>Settings</h1><p>School details, refund conditions and payments.</p></div></div>");
            sb.Append("<form class=\"st-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel st-stack\"><h2>School</h2><div class=\"st-grid2\">" + Field("school", "School name", site.Brand, 60, edit) + Field("tag", "Tagline", site.Tagline, 120, edit) + Field("email", "Contact email", site.Email, 80, edit) + Field("phone", "Phone", site.Phone, 40, edit) + Field("addr", "Address", site.Address, 120, edit) + Field("hours", "Opening hours", site.Hours, 120, edit) + "</div></section>");
            sb.Append("<section class=\"st-panel st-stack\"><h2>Refund conditions</h2><p class=\"st-hint\">A student sees whether a request is inside these conditions. Staff check every request, and can approve or decline any of them.</p><div class=\"st-grid2\">" + Num("days", "Refund window (days)", site.RefundDays, 0, 365, edit, "Counted from the payment date.") + Num("limit", "Progress limit (%)", site.ProgressLimit, 0, 100, edit, "Eligible while less than this share of the lessons is finished.") + "</div>");
            sb.Append("<label class=\"st-check\"><input type=\"checkbox\" id=\"st-nocert\"" + (site.NoCertificate ? " checked" : string.Empty) + (edit ? " onchange=\"AdminSettingsJs.touch()\"" : " disabled") + "><span>Not eligible once a certificate has been issued</span></label><p class=\"st-hint\">Now: " + Ui.Esc(Learn.ConditionsText(site)) + "</p></section>");
            sb.Append("<section class=\"st-panel st-stack\"><h2>Videos</h2>" + Num("vid", "Largest video upload (MB)", site.MaxVideoMb, 1, 100, edit, "Larger files are refused in the Media library.") + "</section>");
            if (edit)
            {
                sb.Append("<div class=\"st-acts\"><button type=\"submit\" class=\"st-btn\">Save settings</button></div>");
            }
            sb.Append("</form>");
            bool demo = Store.Demo();
            sb.Append("<section class=\"st-panel st-stack\"><h2>Payments</h2><dl class=\"st-kv\"><dt>Run mode</dt><dd>" + Status(demo ? "wait" : "go", demo ? "Demo" : "Production") + "</dd>");
            sb.Append("<dt>Card payments</dt><dd>" + (demo ? "Demo test payment, no card is charged." : Pay.KeyReady ? Status("go", "Stripe secret key set") : Status("bad", "Stripe secret key missing")) + "</dd>");
            sb.Append("<dt>Webhook secret</dt><dd>" + (Pay.HookSecret.StartsWith("whsec_", StringComparison.Ordinal) ? Status("go", "Set") : Status(demo ? "off" : "bad", demo ? "Not needed in Demo" : "Missing")) + "</dd>");
            sb.Append("<dt>Webhook address</dt><dd><code>" + Ui.Esc(Local.BaseUrl().TrimEnd('/') + "/stripe/webhook") + "</code></dd></dl>");
            sb.Append("<p class=\"st-hint\">Keys and the run mode live in <code>appConfig/application.cfg</code>, not on this page. In Stripe, send the events checkout.session.completed and charge.refunded to the webhook address.</p></section>");
            if (demo && c.U.Admin)
            {
                sb.Append("<section class=\"st-panel st-stack st-danger\"><h2>Initialize data</h2><p class=\"st-hint\">Removes every record made so far: students, enrollments, orders, refund requests, certificates, messages, the activity log and all staff accounts. Settings, courses, quizzes, pages and the media library stay. The next sign-in page shows a new first password.</p><button type=\"button\" class=\"st-btn st-btn-danger\" onclick=\"AdminSettingsJs.initialize()\">Initialize data</button></section>");
            }
            response.SetElementContents("st-main", sb.ToString());
            response.ExecuteScript("AdminSettingsJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminSettingsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminSettingsJs", c, "save settings", AreaKey, "Only managers can change settings.");
            }
            string brand = Ui.Clip(GetDataValue("sbrand"), 60);
            string email = Ui.Clip(GetDataValue("semail"), 80);
            int days = Ui.Whole(GetDataValue("sdays"), -1);
            int limit = Ui.Whole(GetDataValue("slimit"), -1);
            int vid = Ui.Whole(GetDataValue("svid"), -1);
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (brand.Length < 2)
            {
                errors["school"] = "Please enter the school name.";
            }
            if (email.Length > 0 && !Ui.IsEmail(email))
            {
                errors["email"] = "Please enter a valid email address.";
            }
            if (days < 0 || days > 365)
            {
                errors["days"] = "Enter a number from 0 to 365.";
            }
            if (limit < 0 || limit > 100)
            {
                errors["limit"] = "Enter a number from 0 to 100.";
            }
            if (vid < 1 || vid > 100)
            {
                errors["vid"] = "Enter a number from 1 to 100.";
            }
            foreach (string f in new[] { "school", "email", "days", "limit", "vid" })
            {
                response.SetElementContents("st-e-" + f, errors.TryGetValue(f, out string? m) ? Ui.Esc(m) : string.Empty);
            }
            if (errors.Count > 0)
            {
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                site.Brand = brand;
                site.Tagline = Ui.Clip(GetDataValue("stag"), 120);
                site.Email = email;
                site.Phone = Ui.Clip(GetDataValue("sphone"), 40);
                site.Address = Ui.Clip(GetDataValue("saddr"), 120);
                site.Hours = Ui.Clip(GetDataValue("shours"), 120);
                site.RefundDays = days;
                site.ProgressLimit = limit;
                site.NoCertificate = GetDataValue("snocert") == "1";
                site.MaxVideoMb = vid;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "save settings", "refund " + days + " days, under " + limit + "%", true);
            response.ExecuteScript("AdminSettingsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Initialize()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminSettingsJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!c.U.Admin)
            {
                return await Ad.Refuse(response, "AdminSettingsJs", c, "initialize data", "settings", "Only managers can initialize the data.");
            }
            if (!Store.Demo())
            {
                return await Ad.Refuse(response, "AdminSettingsJs", c, "initialize data", "settings", "In Production the data is initialized once, on the first start. Edit records in their own pages instead.");
            }
            if ((GetDataValue("word") ?? string.Empty).Trim() != "INITIALIZE")
            {
                response.ExecuteScript("AdminSettingsJs.toast('Type INITIALIZE to confirm.');");
                return response;
            }
            await Store.Log(c.U.Id, "initialize data", "requested from Settings", true);
            int removed = await Init.Run("Settings, by " + c.U.Name, false);
            response.ExecuteScript("AdminSettingsJs.initialized(" + removed + ");");
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
