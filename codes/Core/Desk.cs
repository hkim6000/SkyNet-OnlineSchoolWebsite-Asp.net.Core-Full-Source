using OnlineSchool.Models;

namespace OnlineSchool.Core
{
    public static class Desk
    {
        public static async Task<(bool Ok, string Message)> Request(Student s, string orderId, string reason)
        {
            string text = Ui.Clip(reason, 500);
            if (text.Length < 5)
            {
                return (false, "Please tell us why in a sentence or two.");
            }
            RefundRequest req;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.Id == orderId && x.StudentId == s.Id);
                if (o == null)
                {
                    return (false, "That order was not found.");
                }
                List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
                if (requests.Any(r => r.OrderId == o.Id && (r.Status == "open" || r.Status == "processing")))
                {
                    return (false, "You already have an open request for this order.");
                }
                List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
                List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
                CourseInfo? c = site.Courses.FirstOrDefault(x => x.Id == o.CourseId);
                RefundState st = Learn.Check(site, o, Learn.EnrollmentOf(enrollments, s.Id, o.CourseId), c, certs, DateTime.UtcNow);
                if (!st.Can)
                {
                    return (false, st.Block);
                }
                req = new RefundRequest
                {
                    Id = "RR-" + Store.NewCode(6),
                    OrderId = o.Id,
                    StudentId = s.Id,
                    StudentName = s.Name,
                    CourseId = o.CourseId,
                    CourseTitle = o.CourseTitle,
                    Reason = text,
                    Created = DateTime.UtcNow,
                    Eligible = st.Eligible,
                    DaysUsed = st.DaysUsed,
                    Progress = st.Progress,
                    HadCertificate = st.HasCert,
                    Status = "open"
                };
                requests.Add(req);
                await Store.SaveRows("refunds", requests);
            }
            await Store.Log(s.Id, "refund requested", req.OrderId + " · " + (req.Eligible ? "eligible" : "outside policy"), true);
            return (true, req.Eligible ? "Your request was sent. It is inside the refund conditions; our team checks every request." : "Your request was sent. It is outside the refund conditions, so our team will look at it case by case.");
        }

        public static async Task<(bool Ok, string Message)> Withdraw(Student s, string requestId)
        {
            using (await Store.Hold())
            {
                List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
                RefundRequest? r = requests.FirstOrDefault(x => x.Id == requestId && x.StudentId == s.Id);
                if (r == null || r.Status != "open")
                {
                    return (false, "Only an open request can be withdrawn.");
                }
                r.Status = "withdrawn";
                r.Decided = DateTime.UtcNow;
                await Store.SaveRows("refunds", requests);
            }
            await Store.Log(s.Id, "refund withdrawn", requestId, true);
            return (true, "Your request was withdrawn.");
        }

        public static async Task<(bool Ok, string Message)> Decide(Ctx c, string requestId, string kind, decimal amount, string note)
        {
            string n = Ui.Clip(note, 300);
            Order snapshot;
            RefundRequest req;
            decimal give;
            using (await Store.Hold())
            {
                List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
                RefundRequest? r = requests.FirstOrDefault(x => x.Id == requestId);
                if (r == null)
                {
                    return (false, "That request was not found.");
                }
                bool stale = r.Status == "processing" && (r.Decided ?? DateTime.MinValue) < DateTime.UtcNow.AddMinutes(-5);
                if (r.Status != "open" && !stale)
                {
                    return (false, r.Status == "processing" ? "This request is being processed right now." : "This request was already decided.");
                }
                List<Order> orders = await Store.Rows<Order>("orders");
                Order? o = orders.FirstOrDefault(x => x.Id == r.OrderId);
                if (o == null)
                {
                    return (false, "The order of this request was not found.");
                }
                if (kind == "decline")
                {
                    if (n.Length < 3)
                    {
                        return (false, "Write the reason for declining. The student will see it.");
                    }
                    r.Status = "declined";
                    r.Kind = "decline";
                    r.Note = n;
                    r.By = c.U.Id;
                    r.Decided = DateTime.UtcNow;
                    await Store.SaveRows("refunds", requests);
                    await Store.Log(c.U.Id, "refund declined", r.OrderId + " · " + r.StudentName, true);
                    return (true, "Request declined. The student can see your reason.");
                }
                decimal left = Learn.Remaining(o);
                if (left <= 0)
                {
                    return (false, "Nothing is left to refund on this order.");
                }
                if (kind == "full")
                {
                    give = left;
                }
                else if (kind == "partial")
                {
                    give = Math.Round(amount, 2);
                    if (give <= 0)
                    {
                        return (false, "Enter the amount to refund.");
                    }
                    if (give > left)
                    {
                        return (false, "That is more than can be refunded. At most " + Ui.Money(left) + " is left (paid " + Ui.Money(o.Amount) + " minus earlier refunds " + Ui.Money(Learn.Refunded(o)) + ").");
                    }
                    if (give == left)
                    {
                        return (false, "That is everything left on the order. Choose Full refund to end access.");
                    }
                }
                else
                {
                    return (false, "Choose full refund, partial refund or decline.");
                }
                r.Status = "processing";
                r.Decided = DateTime.UtcNow;
                await Store.SaveRows("refunds", requests);
                snapshot = o;
                req = r;
            }
            string refundId;
            bool simulate = Store.Demo();
            if (simulate)
            {
                refundId = "re_demo_" + Store.NewCode(10);
            }
            else if (!Pay.Live(snapshot))
            {
                await Reopen(requestId);
                await Store.Log(c.U.Id, "refund failed", snapshot.Id + " · not a Stripe payment", false);
                return (false, "This order was not paid through Stripe, so it cannot be refunded from here.");
            }
            else
            {
                (string id, string err) = await Pay.Refund(snapshot, give, requestId);
                if (err != string.Empty)
                {
                    await Reopen(requestId);
                    await Store.Log(c.U.Id, "refund failed", snapshot.Id + " · " + err, false);
                    return (false, "Stripe did not make the refund: " + err);
                }
                refundId = id;
            }
            bool fullNow = false;
            bool revoked = false;
            using (await Store.Hold())
            {
                List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
                List<Order> orders = await Store.Rows<Order>("orders");
                List<Enrollment> enrollments = await Store.Rows<Enrollment>("enrollments");
                List<CertRecord> certs = await Store.Rows<CertRecord>("certificates");
                RefundRequest r = requests.First(x => x.Id == requestId);
                Order o = orders.First(x => x.Id == r.OrderId);
                int before = certs.Count(x => !x.Revoked);
                DateTime now = DateTime.UtcNow;
                if (o.Refunds.All(l => l.Id != refundId))
                {
                    fullNow = Learn.ApplyRefund(o, new RefundLine { Id = refundId, Amount = give, When = now, Source = simulate ? "demo" : "stripe", Kind = kind, RequestId = requestId, By = c.U.Id }, enrollments, certs, now);
                }
                else
                {
                    fullNow = Learn.Remaining(o) <= 0;
                }
                revoked = certs.Count(x => !x.Revoked) < before;
                r.Status = kind == "full" ? "approved" : "partial";
                r.Kind = kind;
                r.Amount = give;
                r.Note = n;
                r.By = c.U.Id;
                r.Decided = now;
                r.RefundId = refundId;
                await Store.SaveRows("refunds", requests);
                await Store.SaveRows("orders", orders);
                await Store.SaveRows("enrollments", enrollments);
                await Store.SaveRows("certificates", certs);
            }
            await Store.Log(c.U.Id, kind == "full" ? "refund approved (full)" : "refund approved (partial)", req.OrderId + " · " + Ui.Money(give) + " · " + refundId, true);
            if (fullNow)
            {
                await Store.Log(c.U.Id, "access ended", req.OrderId + " · " + req.CourseTitle, true);
            }
            if (revoked)
            {
                await Store.Log(c.U.Id, "certificate revoked", req.OrderId + " · " + req.CourseTitle, true);
            }
            string tail = fullNow ? " Access to the course has ended" + (revoked ? " and the certificate is revoked." : ".") : " Access to the course stays.";
            return (true, Ui.Money(give) + " refunded (" + refundId + ")." + tail);
        }

        public static async Task<bool> Reopen(string requestId)
        {
            using (await Store.Hold())
            {
                List<RefundRequest> requests = await Store.Rows<RefundRequest>("refunds");
                RefundRequest? r = requests.FirstOrDefault(x => x.Id == requestId);
                if (r == null || (r.Status != "declined" && r.Status != "processing"))
                {
                    return false;
                }
                if (requests.Any(x => x.Id != r.Id && x.OrderId == r.OrderId && (x.Status == "open" || x.Status == "processing")))
                {
                    return false;
                }
                r.Status = "open";
                r.Kind = string.Empty;
                r.Note = string.Empty;
                r.By = string.Empty;
                r.Decided = null;
                await Store.SaveRows("refunds", requests);
                return true;
            }
        }
    }
}
