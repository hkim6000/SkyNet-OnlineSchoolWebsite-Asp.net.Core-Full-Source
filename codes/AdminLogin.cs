using OnlineSchool.Core;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Staff sign in | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            await Init.OnStart();
            await Store.EnsureFirstManager();
            string demo = string.Empty;
            string user = string.Empty;
            string pass = string.Empty;
            string first = string.Empty;
            if (Store.Demo())
            {
                demo = "<div class=\"lg-box lg-demo\"><b>Demo mode</b><span>Any user name and password signs in as a demo manager with every right. The form is filled in for you. A sample account signs in as that person with the password <em>demo1234</em>: instructors <em>maya</em> and <em>luis</em>, staff <em>rosa</em> (registrar), <em>jamal</em> (finance), <em>nina</em> (content) and <em>omar</em> (support).</span></div>";
                user = "demo";
                pass = "demo1234";
            }
            else if (Local.Request() && Store.InitialPassword() != string.Empty)
            {
                first = "<div class=\"lg-box lg-first\"><b>First sign-in</b><dl><dt>User name</dt><dd>admin</dd><dt>Password</dt><dd>" + Ui.Esc(Store.InitialPassword()) + "</dd></dl><span>Shown only on this computer, until you choose your own password. Also in appConfig/initial-admin.txt.</span></div>";
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_demo}", demo).Replace("{plhd_first}", first).Replace("{plhd_user}", Ui.Esc(user)).Replace("{plhd_pass}", Ui.Esc(pass));
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("luser"), 60);
            string pass = GetDataValue("lpass") ?? string.Empty;
            if (login.Length == 0 && !Store.Demo())
            {
                response.SetElementContents("lg-note", "Please enter your user name.");
                return response;
            }
            (string tok, string err, bool change) = await Auth.SignIn(login, pass);
            if (change)
            {
                response.SetElementContents("lg-note", string.Empty);
                response.ExecuteScript("AdminLoginJs.mustChange();");
                return response;
            }
            if (tok == string.Empty)
            {
                await Store.Log("anonymous", "sign in refused", login, false);
                response.SetElementContents("lg-note", Ui.Esc(err));
                return response;
            }
            Ctx? c = await Auth.Check(tok);
            await Store.Log(c?.U.Id ?? "u-demo", "sign in", login, true);
            response.ExecuteScript("AdminLoginJs.signed('" + tok + "');");
            return response;
        }

        public async Task<ApiResponse> Choose()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("luser"), 60);
            string current = GetDataValue("lpass") ?? string.Empty;
            string fresh = GetDataValue("lnew") ?? string.Empty;
            string again = GetDataValue("lnew2") ?? string.Empty;
            (string tok, string err) = await Auth.SetPassword(login, current, fresh, again);
            if (tok == string.Empty)
            {
                response.SetElementContents("lg-note", Ui.Esc(err));
                return response;
            }
            response.ExecuteScript("AdminLoginJs.signed('" + tok + "');");
            return response;
        }
    }
}
