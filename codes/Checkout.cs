using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Checkout : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Checkout | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<SCtx?> Guard(ApiResponse response)
        {
            SCtx? c = await Auth.StudentCheck(GetDataValue("stok"));
            if (c == null)
            {
                response.ExecuteScript("CheckoutJs.gone();");
            }
            return c;
        }

        private static async Task<Order?> Mine(SCtx c, string oid)
        {
            return (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == oid && x.StudentId == c.S.Id);
        }

        private static string Panel(string title, string text, string links)
        {
            return "<div class=\"ck-narrow\"><div class=\"ck-panel ck-stack\"><h1>" + Ui.Esc(title) + "</h1><p>" + Ui.Esc(text) + "</p><div class=\"ck-row\">" + links + "</div></div></div>";
        }

        private static async Task<string> View(SCtx c, Order o)
        {
            SiteData site = await Store.LoadSite();
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == o.CourseId);
            string first = course?.Lessons.FirstOrDefault()?.Id ?? "l1";
            if (o.Status == "paid")
            {
                return "<div class=\"ck-narrow\"><div class=\"ck-panel ck-stack ck-ok\"><span class=\"ck-pill ck-pill-go\">Payment received</span><h1>You are enrolled</h1><p><b>" + Ui.Esc(o.CourseTitle) + "</b> is open. We have recorded order " + Ui.Esc(o.Id) + " for " + Ui.Money(o.Amount) + ". You can see the receipt in My account.</p><div class=\"ck-row\"><a class=\"ck-btn\" href=\"Lesson?c=" + Ui.Esc(o.CourseId) + "&amp;l=" + Ui.Esc(first) + "\">Start learning</a><a class=\"ck-btn ck-btn-ghost\" href=\"MyCourses\">My courses</a><a class=\"ck-btn ck-btn-ghost\" href=\"Account\">Receipt</a></div></div></div>";
            }
            if (o.Status != "pending")
            {
                return Panel("This order was cancelled", "No payment was taken. You can start a new order from the course page.", "<a class=\"ck-btn\" href=\"Course?id=" + Ui.Esc(o.CourseId) + "\">Back to the course</a>");
            }
            bool demo = Store.Demo();
            StringBuilder sb = new StringBuilder("<div class=\"ck-narrow\"><div class=\"ck-grid\"><section class=\"ck-panel ck-stack\"><h1>Checkout</h1>");
            sb.Append("<div class=\"ck-sum\">" + (course != null ? "<img src=\"" + Ui.Esc(Media.Thumb(course.Cover)) + "\" alt=\"\">" : string.Empty) + "<div><b>" + Ui.Esc(o.CourseTitle) + "</b><small>Order " + Ui.Esc(o.Id) + "</small></div><b class=\"ck-total\">" + Ui.Money(o.Amount) + "</b></div>");
            if (demo)
            {
                sb.Append("<div class=\"ck-note\"><b>Demo mode: test payment</b><p>No card is charged. The button below makes a test payment, and your course opens right away. In Production this step opens Stripe Checkout for a real card payment.</p></div><div class=\"ck-row\"><button type=\"button\" class=\"ck-btn\" onclick=\"CheckoutJs.demo()\">Pay " + Ui.Money(o.Amount) + " (test payment)</button></div>");
            }
            else
            {
                sb.Append("<p class=\"ck-hint\">You pay by card on Stripe's secure payment page. Your course opens as soon as the payment is confirmed.</p><div class=\"ck-row\"><button type=\"button\" class=\"ck-btn\" onclick=\"CheckoutJs.start()\">Pay " + Ui.Money(o.Amount) + " by card</button></div>");
            }
            sb.Append("<div class=\"ck-note ck-note-bad\" id=\"ck-err\" role=\"alert\"></div><p class=\"ck-hint\">" + Ui.Esc(Learn.ConditionsText(site)) + " <a href=\"Policy\">Terms and refunds</a></p></section><aside class=\"ck-panel ck-aside\"><h2>What you get</h2><ul class=\"ck-ticks\"><li>" + Ui.Count(course?.Lessons.Count ?? 0, "lesson", "lessons") + " with notes</li><li>Quizzes with a pass mark</li><li>A certificate with a check code</li></ul><p class=\"ck-row\"><button type=\"button\" class=\"ck-btn ck-btn-ghost ck-btn-sm\" onclick=\"CheckoutJs.cancel()\">Cancel this order</button></p></aside></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string oid = Ui.Clip(GetDataValue("oid"), 20);
            Order? o = await Mine(c, oid);
            if (o == null)
            {
                response.SetElementContents("ck-body", Panel("Order not found", "We could not find that order on your account.", "<a class=\"ck-btn\" href=\"Courses\">Browse courses</a>"));
                return response;
            }
            if (GetDataValue("cback") == "1" && o.Status == "pending" && o.Session != string.Empty && !Store.Demo())
            {
                await Pay.Confirm(o.Id);
                o = await Mine(c, oid) ?? o;
            }
            response.SetElementContents("ck-body", await View(c, o));
            return response;
        }

        public async Task<ApiResponse> Start()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Order? o = await Mine(c, Ui.Clip(GetDataValue("oid"), 20));
            if (o == null || o.Status != "pending")
            {
                response.SetElementContents("ck-err", "This order cannot be paid any more.");
                return response;
            }
            if (Store.Demo())
            {
                response.SetElementContents("ck-err", "Demo mode uses the test payment button.");
                return response;
            }
            SiteData site = await Store.LoadSite();
            CourseInfo? course = site.Courses.FirstOrDefault(x => x.Id == o.CourseId);
            if (course == null)
            {
                response.SetElementContents("ck-err", "This course is no longer on sale.");
                return response;
            }
            (string url, string session, string err) = await Pay.CreateSession(o, course);
            if (err != string.Empty || !url.StartsWith("https://", StringComparison.Ordinal))
            {
                response.SetElementContents("ck-err", Ui.Esc(err == string.Empty ? "Stripe did not return a payment page." : err));
                return response;
            }
            using (await Store.Hold())
            {
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? fresh = orders.FirstOrDefault(x => x.Id == o.Id);
                if (fresh != null)
                {
                    fresh.Session = session;
                    await Store.SaveRows("orders", orders);
                }
            }
            await Store.Log(c.S.Id, "checkout started", o.Id, true);
            response.ExecuteScript("CheckoutJs.go('" + Ui.Js(url) + "');");
            return response;
        }

        public async Task<ApiResponse> DemoPay()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Order? o = await Mine(c, Ui.Clip(GetDataValue("oid"), 20));
            if (o == null || o.Status != "pending")
            {
                response.SetElementContents("ck-err", "This order cannot be paid any more.");
                return response;
            }
            if (!Store.Demo())
            {
                response.SetElementContents("ck-err", "Test payments are only available in Demo mode.");
                return response;
            }
            await Pay.MarkPaid(o.Id, "Demo test payment", "cs_demo_" + Store.NewCode(10), "pi_demo_" + Store.NewCode(12), c.S.Id);
            Order? paid = await Mine(c, o.Id);
            response.SetElementContents("ck-body", await View(c, paid ?? o));
            response.ExecuteScript("CheckoutJs.paid();");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            SCtx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string oid = Ui.Clip(GetDataValue("oid"), 20);
            Order? check = await Mine(c, oid);
            if (check != null && check.Status == "pending" && check.Session != string.Empty && !Store.Demo() && await Pay.Confirm(oid))
            {
                response.SetElementContents("ck-body", await View(c, await Mine(c, oid) ?? check));
                response.ExecuteScript("CheckoutJs.toast('That order was already paid, so it was kept.');");
                return response;
            }
            string title = string.Empty;
            using (await Store.Hold())
            {
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.Id == oid && x.StudentId == c.S.Id && x.Status == "pending");
                if (o != null)
                {
                    o.Status = "cancelled";
                    title = o.CourseTitle;
                    await Store.SaveRows("orders", orders);
                }
            }
            if (title != string.Empty)
            {
                await Store.Log(c.S.Id, "order cancelled", oid + " · " + title, true);
            }
            response.ExecuteScript("CheckoutJs.cancelled();");
            return response;
        }
    }
}
