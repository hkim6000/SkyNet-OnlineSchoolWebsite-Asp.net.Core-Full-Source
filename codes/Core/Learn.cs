using OnlineSchool.Models;

namespace OnlineSchool.Core
{
    public sealed class Step
    {
        public string Kind { get; init; } = "lesson";
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
    }

    public sealed class RefundState
    {
        public bool Can { get; set; }
        public string Block { get; set; } = string.Empty;
        public bool Eligible { get; set; }
        public int DaysUsed { get; set; }
        public int DaysLeft { get; set; }
        public int Progress { get; set; }
        public bool HasCert { get; set; }
        public bool WindowOk { get; set; }
        public bool ProgressOk { get; set; }
        public bool CertOk { get; set; }
        public decimal Remaining { get; set; }
    }

    public static class Learn
    {
        public static readonly (string Key, string Name)[] Levels = { ("beginner", "Beginner"), ("intermediate", "Intermediate"), ("advanced", "Advanced") };

        public static string LevelName(string key)
        {
            return Levels.FirstOrDefault(l => l.Key == key).Name ?? key;
        }

        public static string TopicName(SiteData site, string key)
        {
            return site.Topics.FirstOrDefault(t => t.Key == key)?.Label ?? key;
        }

        public static List<Step> Sequence(SiteData site, CourseInfo c)
        {
            List<Step> list = new List<Step>();
            List<QuizInfo> quizzes = site.Quizzes.Where(q => q.CourseId == c.Id && q.Questions.Count > 0).ToList();
            foreach (LessonInfo l in c.Lessons)
            {
                list.Add(new Step { Kind = "lesson", Id = l.Id, Title = l.Title });
                foreach (QuizInfo q in quizzes.Where(x => x.AfterLesson == l.Id))
                {
                    list.Add(new Step { Kind = "quiz", Id = q.Id, Title = q.Title });
                }
            }
            foreach (QuizInfo q in quizzes.Where(x => c.Lessons.All(l => l.Id != x.AfterLesson)))
            {
                list.Add(new Step { Kind = "quiz", Id = q.Id, Title = q.Title });
            }
            return list;
        }

        public static int Done(CourseInfo c, Enrollment? e)
        {
            return e == null ? 0 : c.Lessons.Count(l => e.Completed.Contains(l.Id));
        }

        public static int Percent(CourseInfo c, Enrollment? e)
        {
            return c.Lessons.Count == 0 ? 0 : Done(c, e) * 100 / c.Lessons.Count;
        }

        public static bool QuizPassed(Enrollment e, string quizId)
        {
            return e.Quizzes.Any(q => q.QuizId == quizId && q.Passed);
        }

        public static bool Finished(SiteData site, CourseInfo c, Enrollment e)
        {
            return c.Lessons.Count > 0 && Done(c, e) == c.Lessons.Count && Sequence(site, c).Where(s => s.Kind == "quiz").All(s => QuizPassed(e, s.Id));
        }

        public static bool StepDone(Enrollment e, Step s)
        {
            return s.Kind == "lesson" ? e.Completed.Contains(s.Id) : QuizPassed(e, s.Id);
        }

        public static decimal Refunded(Order o)
        {
            return o.Refunds.Sum(r => r.Amount);
        }

        public static decimal Remaining(Order o)
        {
            return o.Status == "paid" ? Math.Max(0, o.Amount - Refunded(o)) : 0;
        }

        public static Enrollment? EnrollmentOf(List<Enrollment> list, string studentId, string courseId)
        {
            return list.FirstOrDefault(e => e.StudentId == studentId && e.CourseId == courseId);
        }

        public static bool HasLiveCert(List<CertRecord> certs, string studentId, string courseId)
        {
            return certs.Any(c => c.StudentId == studentId && c.CourseId == courseId && !c.Revoked);
        }

        public static RefundState Check(SiteData site, Order o, Enrollment? e, CourseInfo? c, List<CertRecord> certs, DateTime now)
        {
            RefundState st = new RefundState { Remaining = Remaining(o) };
            DateTime paid = o.Paid ?? o.Created;
            st.DaysUsed = Math.Max(0, (now.Date - paid.Date).Days);
            st.DaysLeft = site.RefundDays - st.DaysUsed;
            st.WindowOk = st.DaysLeft >= 0;
            int total = c == null ? 0 : c.Lessons.Count;
            int done = c == null ? 0 : Done(c, e);
            st.Progress = total == 0 ? 0 : done * 100 / total;
            st.ProgressOk = total == 0 || done * 100 < site.ProgressLimit * total;
            st.HasCert = HasLiveCert(certs, o.StudentId, o.CourseId);
            st.CertOk = !site.NoCertificate || !st.HasCert;
            st.Eligible = st.WindowOk && st.ProgressOk && st.CertOk;
            if (o.Status != "paid")
            {
                st.Block = "Only paid orders can be refunded.";
            }
            else if (st.Remaining <= 0)
            {
                st.Block = "This order has already been refunded in full.";
            }
            else
            {
                st.Can = true;
            }
            return st;
        }

        public static string ConditionsText(SiteData site)
        {
            return "Refunds are open for " + Ui.Count(site.RefundDays, "day", "days") + " after payment, while you have finished less than " + site.ProgressLimit + "% of the lessons" + (site.NoCertificate ? " and have not been given a certificate." : ".");
        }

        public static Enrollment Settle(Order o, string method, string session, string intent, List<Enrollment> enrollments, DateTime now)
        {
            o.Status = "paid";
            o.Paid = now;
            o.Method = method;
            if (session != string.Empty)
            {
                o.Session = session;
            }
            if (intent != string.Empty)
            {
                o.Intent = intent;
            }
            Enrollment? e = EnrollmentOf(enrollments, o.StudentId, o.CourseId);
            if (e == null)
            {
                e = new Enrollment { Id = "E-" + Store.NewCode(6), StudentId = o.StudentId, CourseId = o.CourseId, Started = now };
                enrollments.Add(e);
            }
            e.Status = "active";
            e.LockedOn = null;
            e.OrderId = o.Id;
            o.Refunds ??= new List<RefundLine>();
            return e;
        }

        public static bool ApplyRefund(Order o, RefundLine line, List<Enrollment> enrollments, List<CertRecord> certs, DateTime now)
        {
            o.Refunds.Add(line);
            if (Remaining(o) > 0)
            {
                return false;
            }
            Enrollment? e = EnrollmentOf(enrollments, o.StudentId, o.CourseId);
            if (e != null && e.OrderId == o.Id)
            {
                e.Status = "locked";
                e.LockedOn = now;
            }
            foreach (CertRecord c in certs.Where(x => x.StudentId == o.StudentId && x.CourseId == o.CourseId && !x.Revoked))
            {
                c.Revoked = true;
                c.RevokedOn = now;
            }
            return true;
        }

        public static CertRecord? TryIssue(SiteData site, CourseInfo c, Enrollment e, Student s, List<CertRecord> certs, DateTime now)
        {
            if (e.Status != "active" || !Finished(site, c, e) || HasLiveCert(certs, s.Id, c.Id))
            {
                return null;
            }
            string code;
            do
            {
                code = "BPA-" + Store.NewCode(4) + "-" + Store.NewCode(4);
            }
            while (certs.Any(x => x.Code == code));
            CertRecord cert = new CertRecord { Code = code, StudentId = s.Id, StudentName = s.Name, CourseId = c.Id, CourseTitle = c.Title, EnrollmentId = e.Id, Issued = now };
            certs.Add(cert);
            return cert;
        }

        public static string NewOrderId(List<Order> orders)
        {
            string id;
            do
            {
                id = "BP-" + Store.NewCode(6);
            }
            while (orders.Any(o => o.Id == id));
            return id;
        }

        public static string RefundSummary(Order o)
        {
            decimal r = Refunded(o);
            return r <= 0 ? string.Empty : r >= o.Amount ? "Refunded in full" : "Refunded " + Ui.Money(r);
        }
    }
}
