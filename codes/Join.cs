using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Join : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Sign in | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            bool demo = Store.Demo();
            string note = demo ? "<div class=\"jn-demo\"><b>Demo mode</b><span>The form is filled in with a sample student. Other sample students: ben@, chloe@, dev@, eli@, fay@ and gus@example.com, all with the password <em>demo1234</em>.</span></div>" : string.Empty;
            string staff = demo ? "<p class=\"jn-demo-s\">Demo mode: the staff form is filled in for you. Sample instructors <em>maya</em> and <em>luis</em>, and staff <em>rosa</em>, <em>jamal</em>, <em>nina</em> and <em>omar</em>, use the password <em>demo1234</em>.</p>" : string.Empty;
            string reset = Ui.Clip(QueryValue("reset"), 40).ToLowerInvariant();
            string mode = reset.Length > 0 ? "reset" : QueryValue("mode") == "new" ? "new" : QueryValue("mode") == "forgot" ? "forgot" : "in";
            string resetNote = string.Empty;
            if (reset.Length > 0)
            {
                Student? who = await Auth.StudentResetWho(reset);
                resetNote = who == null ? "<div class=\"jn-note jn-note-bad\">This link has expired or was already used. Ask for a new one below.</div>" : "<p class=\"jn-hint\">Choose a new password for <b>" + Ui.Esc(who.Email) + "</b>.</p>";
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_mode}", mode)
                .Replace("{plhd_reset}", Ui.Esc(reset))
                .Replace("{plhd_resetnote}", resetNote)
                .Replace("{plhd_demo}", note)
                .Replace("{plhd_staffdemo}", staff)
                .Replace("{plhd_semail}", demo ? "ava@example.com" : string.Empty)
                .Replace("{plhd_spass}", demo ? "demo1234" : string.Empty);
        }

        private static void Fields(ApiResponse response, Dictionary<string, string> errors, params string[] names)
        {
            foreach (string n in names)
            {
                response.SetElementContents("jn-e-" + n, errors.TryGetValue(n, out string? m) ? Ui.Esc(m) : string.Empty);
            }
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("jemail"), 80);
            string pass = GetDataValue("jpass") ?? string.Empty;
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (!Ui.IsEmail(email))
            {
                errors["email"] = "Please enter your email address.";
            }
            if (pass.Length == 0)
            {
                errors["pass"] = "Please enter your password.";
            }
            Fields(response, errors, "email", "pass");
            if (errors.Count > 0)
            {
                response.SetElementContents("jn-note", string.Empty);
                return response;
            }
            (string tok, string err) = await Auth.StudentSignIn(email, pass);
            if (tok == string.Empty)
            {
                response.SetElementContents("jn-note", Ui.Esc(err));
                return response;
            }
            SCtx? who = await Auth.StudentCheck(tok);
            await Store.Log(who?.S.Id ?? "student", "sign in", email, true);
            response.ExecuteScript("JoinJs.signed('" + tok + "');");
            return response;
        }

        public async Task<ApiResponse> Forgot()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("femail"), 80);
            if (!Ui.IsEmail(email))
            {
                response.SetElementContents("jn-e-femail", "Please enter your email address.");
                return response;
            }
            response.SetElementContents("jn-e-femail", string.Empty);
            SiteData site = await Store.LoadSite();
            if (!Mail.Ready)
            {
                response.SetElementContents("jn-fnote", "Password reset by email is not set up on this site yet. Please contact us at " + Ui.Esc(site.Email) + (site.Phone.Length > 0 ? " or " + Ui.Esc(site.Phone) : string.Empty) + " and we will reset it for you.");
                return response;
            }
            if (Auth.Paused("r:" + email.ToLowerInvariant()))
            {
                response.SetElementContents("jn-fnote", "Too many tries. Wait a few minutes and try again.");
                return response;
            }
            Auth.Failed("r:" + email.ToLowerInvariant());
            (string token, Student? who) = await Auth.StudentResetLink(email);
            if (who != null)
            {
                string link = Local.BaseUrl() + "/Join?reset=" + token;
                string text = "Hello " + who.Name + ",\n\nSomeone asked to reset the password of your " + site.Brand + " account. Open this link within one hour to choose a new password:\n\n" + link + "\n\nIf that was not you, ignore this email; your password stays as it is.\n\n" + site.Brand;
                string err = await Mail.Send(who.Email, site.Brand + ": reset your password", text);
                await Store.Log(who.Id, "password reset link", err == string.Empty ? who.Email : who.Email + " · " + err, err == string.Empty);
                if (err != string.Empty)
                {
                    response.SetElementContents("jn-fnote", "We could not send the email right now. Please try again later or contact us at " + Ui.Esc(site.Email) + ".");
                    return response;
                }
            }
            response.SetElementContents("jn-fnote", "If that email has an account, we have sent it a link to choose a new password. The link works for one hour.");
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            string token = Ui.Clip(GetDataValue("rtoken"), 40);
            (string tok, string err) = await Auth.StudentResetUse(token, GetDataValue("rpass1") ?? string.Empty, GetDataValue("rpass2") ?? string.Empty);
            if (tok == string.Empty)
            {
                response.SetElementContents("jn-e-rpass", Ui.Esc(err));
                return response;
            }
            SCtx? who = await Auth.StudentCheck(tok);
            await Store.Log(who?.S.Id ?? "student", "password reset", who?.S.Email ?? string.Empty, true);
            response.ExecuteScript("JoinJs.signed('" + tok + "');");
            return response;
        }

        public async Task<ApiResponse> SignUp()
        {
            ApiResponse response = new ApiResponse();
            string name = Ui.Clip(GetDataValue("jname"), 60);
            string email = Ui.Clip(GetDataValue("jemail2"), 80);
            string phone = Ui.Clip(GetDataValue("jphone"), 24);
            string pass = GetDataValue("jpass2") ?? string.Empty;
            string again = GetDataValue("jpass3") ?? string.Empty;
            if (pass != again)
            {
                Fields(response, new Dictionary<string, string> { ["pass2"] = "The two passwords are not the same." }, "name", "email2", "phone", "pass1", "pass2");
                return response;
            }
            (string tok, Dictionary<string, string> found) = await Auth.StudentSignUp(name, email, phone, pass);
            Dictionary<string, string> errors = new Dictionary<string, string>();
            foreach ((string from, string to) in new[] { ("name", "name"), ("email", "email2"), ("phone", "phone"), ("password", "pass1") })
            {
                if (found.TryGetValue(from, out string? m))
                {
                    errors[to] = m;
                }
            }
            Fields(response, errors, "name", "email2", "phone", "pass1", "pass2");
            if (tok == string.Empty)
            {
                return response;
            }
            response.ExecuteScript("JoinJs.signed('" + tok + "');");
            return response;
        }
    }
}
