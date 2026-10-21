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
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_mode}", QueryValue("mode") == "new" ? "new" : "in")
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
