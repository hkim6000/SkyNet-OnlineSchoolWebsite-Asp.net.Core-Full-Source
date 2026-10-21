using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Certificate : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Certificate | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            SCtx? s = await Auth.StudentCheck(GetDataValue("stok"));
            if (s == null)
            {
                response.ExecuteScript("CertificateJs.gone();");
                return response;
            }
            string code = Ui.Clip(GetDataValue("ccode"), 20).ToUpperInvariant();
            List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
            CertRecord? c = certs.FirstOrDefault(x => x.Code == code && x.StudentId == s.S.Id);
            if (c == null)
            {
                response.SetElementContents("ce-body", "<div class=\"ce-narrow\"><div class=\"ce-panel ce-stack\"><h1>Certificate not found</h1><p>There is no certificate with this code on your account. Certificates appear when you finish every lesson and pass every quiz.</p><div class=\"ce-row\"><a class=\"ce-btn\" href=\"MyCourses\">My courses</a></div></div></div>");
                return response;
            }
            if (c.Revoked)
            {
                response.SetElementContents("ce-body", "<div class=\"ce-narrow\"><div class=\"ce-panel ce-stack\"><h1>This certificate was revoked</h1><p>The certificate " + Ui.Esc(c.Code) + " for " + Ui.Esc(c.CourseTitle) + " is no longer valid" + (c.RevokedOn.HasValue ? " (revoked " + Ui.Day(c.RevokedOn.Value) + ")" : string.Empty) + ". This happens when a course is refunded in full.</p><div class=\"ce-row\"><a class=\"ce-btn\" href=\"MyCourses\">My courses</a></div></div></div>");
                return response;
            }
            SiteData site = await Store.LoadSite();
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == c.CourseId);
            InstructorInfo? ins = course == null ? null : site.Instructors.FirstOrDefault(x => x.Id == course.InstructorId);
            string html = "<div class=\"ce-narrow ce-noprint\"><div class=\"ce-tools\"><a href=\"MyCourses\">&larr; My courses</a><button type=\"button\" class=\"ce-btn ce-btn-sm\" onclick=\"CertificateJs.print()\">Print certificate</button></div></div>"
                + "<div class=\"ce-sheetwrap\"><article class=\"ce-sheet\" aria-label=\"Certificate of completion\"><div class=\"ce-frame\"><svg class=\"ce-mark\" viewBox=\"0 0 40 40\" width=\"64\" height=\"64\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"10\" fill=\"#1F2A37\"/><path d=\"M8 28a12 12 0 0 1 24 0\" fill=\"#F6D77A\"/><path d=\"M13 28a7 7 0 0 1 14 0\" fill=\"#C2540A\"/><path d=\"M6 30h28\" stroke=\"#FBF7EF\" stroke-width=\"2.4\" stroke-linecap=\"round\"/></svg>"
                + "<p class=\"ce-school\">" + Ui.Esc(site.Brand) + "</p><h1>Certificate of completion</h1><p class=\"ce-line\">This certifies that</p><p class=\"ce-name\">" + Ui.Esc(c.StudentName) + "</p><p class=\"ce-line\">has completed every lesson and passed every quiz in</p><p class=\"ce-course\">" + Ui.Esc(c.CourseTitle) + "</p>"
                + "<div class=\"ce-sig\"><div><b>" + Ui.Day(c.Issued) + "</b><span>Date issued</span></div><div><b>" + Ui.Esc(ins?.Name ?? site.Brand) + "</b><span>Instructor</span></div><div><b class=\"ce-code\">" + Ui.Esc(c.Code) + "</b><span>Verify code</span></div></div>"
                + "<p class=\"ce-verify\">Anyone can check this certificate on the Certificate page of our website with the code above.</p></div></article></div>";
            response.SetElementContents("ce-body", html);
            return response;
        }
    }
}
