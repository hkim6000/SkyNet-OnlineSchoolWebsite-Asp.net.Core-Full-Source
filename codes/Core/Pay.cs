using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OnlineSchool.Models;

namespace OnlineSchool.Core
{
    public static class Pay
    {
        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("https://api.stripe.com/"), Timeout = TimeSpan.FromSeconds(30) };

        public static string Key => Store.ConfigValue("app.stripe.secretkey");

        public static string HookSecret => Store.ConfigValue("app.stripe.webhooksecret");

        public static bool KeyReady => Key.StartsWith("sk_", StringComparison.Ordinal) && Key.Length > 20;

        public static bool Live(Order o)
        {
            return o.Intent.StartsWith("pi_", StringComparison.Ordinal) && !o.Intent.StartsWith("pi_demo", StringComparison.Ordinal);
        }

        public static long Cents(decimal v)
        {
            return (long)Math.Round(v * 100m, 0, MidpointRounding.AwayFromZero);
        }

        private static async Task<(JsonDocument? Doc, string Error)> Call(HttpMethod method, string path, Dictionary<string, string>? form, string once = "")
        {
            if (!KeyReady)
            {
                return (null, "Stripe is not set up. Enter the secret key in appConfig/application.cfg.");
            }
            try
            {
                using HttpRequestMessage req = new HttpRequestMessage(method, path);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Key);
                if (once != string.Empty)
                {
                    req.Headers.Add("Idempotency-Key", once);
                }
                if (form != null)
                {
                    req.Content = new FormUrlEncodedContent(form);
                }
                using HttpResponseMessage res = await Http.SendAsync(req);
                string body = await res.Content.ReadAsStringAsync();
                JsonDocument doc = JsonDocument.Parse(body);
                if (!res.IsSuccessStatusCode)
                {
                    string msg = doc.RootElement.TryGetProperty("error", out JsonElement er) && er.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? "Stripe refused the request." : "Stripe refused the request.";
                    return (null, msg);
                }
                return (doc, string.Empty);
            }
            catch (HttpRequestException)
            {
                return (null, "Stripe could not be reached. Try again in a moment.");
            }
            catch (TaskCanceledException)
            {
                return (null, "Stripe took too long to answer. Try again in a moment.");
            }
            catch (JsonException)
            {
                return (null, "Stripe sent an answer that could not be read.");
            }
        }

        public static async Task<(string Url, string Session, string Error)> CreateSession(Order o, CourseInfo c)
        {
            string b = Local.BaseUrl();
            Dictionary<string, string> f = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["success_url"] = b + "/Checkout?o=" + o.Id + "&back=1",
                ["cancel_url"] = b + "/Course?id=" + c.Id,
                ["client_reference_id"] = o.Id,
                ["customer_email"] = o.StudentEmail,
                ["line_items[0][quantity]"] = "1",
                ["line_items[0][price_data][currency]"] = "usd",
                ["line_items[0][price_data][unit_amount]"] = Cents(o.Amount).ToString(CultureInfo.InvariantCulture),
                ["line_items[0][price_data][product_data][name]"] = c.Title,
                ["metadata[order]"] = o.Id,
                ["payment_intent_data[metadata][order]"] = o.Id
            };
            (JsonDocument? doc, string err) = await Call(HttpMethod.Post, "v1/checkout/sessions", f);
            if (doc == null)
            {
                return (string.Empty, string.Empty, err);
            }
            using (doc)
            {
                string url = doc.RootElement.TryGetProperty("url", out JsonElement u) ? u.GetString() ?? string.Empty : string.Empty;
                string id = doc.RootElement.TryGetProperty("id", out JsonElement i) ? i.GetString() ?? string.Empty : string.Empty;
                return url == string.Empty ? (string.Empty, string.Empty, "Stripe did not return a payment page.") : (url, id, string.Empty);
            }
        }

        public static async Task<(bool Paid, string Intent, string Error)> SessionStatus(string sessionId)
        {
            (JsonDocument? doc, string err) = await Call(HttpMethod.Get, "v1/checkout/sessions/" + Uri.EscapeDataString(sessionId), null);
            if (doc == null)
            {
                return (false, string.Empty, err);
            }
            using (doc)
            {
                bool paid = doc.RootElement.TryGetProperty("payment_status", out JsonElement p) && p.GetString() == "paid";
                string intent = doc.RootElement.TryGetProperty("payment_intent", out JsonElement i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? string.Empty : string.Empty;
                return (paid, intent, string.Empty);
            }
        }

        public static async Task<(string RefundId, string Error)> Refund(Order o, decimal amount, string requestId)
        {
            Dictionary<string, string> f = new Dictionary<string, string>
            {
                ["payment_intent"] = o.Intent,
                ["amount"] = Cents(amount).ToString(CultureInfo.InvariantCulture),
                ["reason"] = "requested_by_customer",
                ["metadata[order]"] = o.Id,
                ["metadata[request]"] = requestId
            };
            (JsonDocument? doc, string err) = await Call(HttpMethod.Post, "v1/refunds", f, "refund-" + o.Id + "-" + requestId + "-" + Cents(amount).ToString(CultureInfo.InvariantCulture));
            if (doc == null)
            {
                return (string.Empty, err);
            }
            using (doc)
            {
                string id = doc.RootElement.TryGetProperty("id", out JsonElement i) ? i.GetString() ?? string.Empty : string.Empty;
                return id == string.Empty ? (string.Empty, "Stripe did not return a refund id.") : (id, string.Empty);
            }
        }

        public static async Task<bool> Confirm(string orderId)
        {
            Order? o = (await Store.Rows<Order>("orders")).FirstOrDefault(x => x.Id == orderId);
            if (o == null || o.Status == "paid" || o.Session == string.Empty)
            {
                return o != null && o.Status == "paid";
            }
            (bool paid, string intent, string _) = await SessionStatus(o.Session);
            if (!paid)
            {
                return false;
            }
            await MarkPaid(orderId, "Card (Stripe)", o.Session, intent, "stripe");
            return true;
        }

        public static async Task<bool> MarkPaid(string orderId, string method, string session, string intent, string by)
        {
            bool fresh = false;
            string student = string.Empty;
            string title = string.Empty;
            using (await Store.Hold())
            {
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.Id == orderId);
                if (o == null || o.Status == "paid")
                {
                    return false;
                }
                List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
                Learn.Settle(o, method, session, intent, enrollments, DateTime.UtcNow);
                await Store.SaveRows("orders", orders);
                await Store.SaveRows("enrollments", enrollments);
                fresh = true;
                student = o.StudentName;
                title = o.CourseTitle;
            }
            if (fresh)
            {
                await Store.Log(by, "order paid", orderId + " · " + student + " · " + title, true);
            }
            return fresh;
        }

        private static bool Signed(string payload, string header, string secret)
        {
            string t = string.Empty;
            List<string> sigs = new List<string>();
            foreach (string part in header.Split(','))
            {
                string[] kv = part.Trim().Split('=', 2);
                if (kv.Length != 2)
                {
                    continue;
                }
                if (kv[0] == "t")
                {
                    t = kv[1];
                }
                else if (kv[0] == "v1")
                {
                    sigs.Add(kv[1]);
                }
            }
            if (t == string.Empty || sigs.Count == 0 || !long.TryParse(t, out long stamp))
            {
                return false;
            }
            if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - stamp) > 300)
            {
                return false;
            }
            using HMACSHA256 h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            string want = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(t + "." + payload))).ToLowerInvariant();
            byte[] w = Encoding.UTF8.GetBytes(want);
            return sigs.Any(s => CryptographicOperations.FixedTimeEquals(w, Encoding.UTF8.GetBytes(s.ToLowerInvariant())));
        }

        public static async Task Webhook(HttpContext ctx)
        {
            if (!HttpMethods.IsPost(ctx.Request.Method))
            {
                ctx.Response.StatusCode = 405;
                return;
            }
            using StreamReader rd = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            string payload = await rd.ReadToEndAsync();
            string secret = HookSecret;
            if (secret.Length < 8 || secret == "your-stripe-webhook-secret")
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Webhook secret is not set.");
                return;
            }
            if (!Signed(payload, ctx.Request.Headers["Stripe-Signature"].ToString(), secret))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Bad signature.");
                return;
            }
            string result = await Process(payload);
            ctx.Response.StatusCode = result.StartsWith("error", StringComparison.Ordinal) ? 400 : 200;
            await ctx.Response.WriteAsync(result);
        }

        public static async Task<string> Process(string payload)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                string type = doc.RootElement.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
                if (!doc.RootElement.TryGetProperty("data", out JsonElement data) || !data.TryGetProperty("object", out JsonElement obj))
                {
                    return "error: no object";
                }
                if (type == "checkout.session.completed")
                {
                    string orderId = Str(obj, "client_reference_id");
                    if (orderId == string.Empty || Str(obj, "payment_status") != "paid")
                    {
                        return "ok: ignored";
                    }
                    bool done = await MarkPaid(orderId, "Card (Stripe)", Str(obj, "id"), Str(obj, "payment_intent"), "stripe");
                    return done ? "ok: paid" : "ok: already paid";
                }
                if (type == "charge.refunded")
                {
                    return await ChargeRefunded(obj);
                }
                return "ok: ignored";
            }
            catch (JsonException)
            {
                return "error: bad json";
            }
        }

        private static string Str(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
        }

        private static long Num(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : 0;
        }

        private static async Task<string> ChargeRefunded(JsonElement charge)
        {
            string intent = Str(charge, "payment_intent");
            long refundedCents = Num(charge, "amount_refunded");
            if (intent == string.Empty)
            {
                return "ok: no payment";
            }
            string logLine = string.Empty;
            string orderName = string.Empty;
            bool full = false;
            using (await Store.Hold())
            {
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.Intent == intent);
                if (o == null)
                {
                    return "ok: unknown payment";
                }
                List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
                List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
                List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
                DateTime now = DateTime.UtcNow;
                decimal added = 0;
                List<(string Id, long Cents)> incoming = new List<(string, long)>();
                if (charge.TryGetProperty("refunds", out JsonElement rf) && rf.TryGetProperty("data", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement r in arr.EnumerateArray())
                    {
                        incoming.Add((Str(r, "id"), Num(r, "amount")));
                    }
                }
                long known = incoming.Where(x => o.Refunds.All(l => l.Id != x.Id)).Sum(x => x.Cents) + Cents(Learn.Refunded(o));
                if (refundedCents > known)
                {
                    incoming.Add(("re_sync_" + Store.NewCode(8), refundedCents - known));
                }
                orderName = o.Id;
                foreach ((string id, long cents) in incoming)
                {
                    if (id == string.Empty || cents <= 0 || o.Refunds.Any(l => l.Id == id))
                    {
                        continue;
                    }
                    decimal amount = Math.Min(cents / 100m, Learn.Remaining(o));
                    if (amount <= 0)
                    {
                        continue;
                    }
                    RefundLine line = new RefundLine { Id = id, Amount = amount, When = now, Source = "dashboard", Kind = "external", By = "stripe" };
                    full = Learn.ApplyRefund(o, line, enrollments, certs, now) || full;
                    added += amount;
                }
                if (added <= 0)
                {
                    return "ok: nothing new";
                }
                RefundRequest? open = requests.FirstOrDefault(q => q.OrderId == o.Id && q.Status == "open");
                if (open != null && full)
                {
                    open.Status = "approved";
                    open.Kind = "full";
                    open.Amount = added;
                    open.Note = "Refunded in the Stripe dashboard.";
                    open.By = "stripe";
                    open.Decided = now;
                }
                await Store.SaveRows("orders", orders);
                await Store.SaveRows("enrollments", enrollments);
                await Store.SaveRows("certificates", certs);
                await Store.SaveRows("refunds", requests);
                logLine = Ui.Money(added) + (full ? " (full refund, access ended, certificate revoked)" : " (access stays)");
            }
            await Store.Log("stripe", "refund synced", orderName + " · " + logLine, true);
            return "ok: refund synced";
        }

        public static string DashboardPayload(Order o, decimal addAmount)
        {
            long total = Cents(Learn.Refunded(o)) + Cents(addAmount);
            var evt = new
            {
                id = "evt_demo_" + Store.NewCode(8),
                type = "charge.refunded",
                data = new
                {
                    @object = new
                    {
                        id = "ch_demo_" + Store.NewCode(8),
                        payment_intent = o.Intent,
                        amount = Cents(o.Amount),
                        amount_refunded = total,
                        refunds = new { data = new[] { new { id = "re_dash_" + Store.NewCode(8), amount = Cents(addAmount) } } }
                    }
                }
            };
            return JsonSerializer.Serialize(evt);
        }
    }
}
