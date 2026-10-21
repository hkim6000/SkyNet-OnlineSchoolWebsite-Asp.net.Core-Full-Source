using System.Text;
using OnlineSchool.Models;

namespace OnlineSchool.Core
{
    public sealed class Ctx
    {
        public Staff U { get; init; } = new Staff();
        public string Token { get; init; } = string.Empty;
    }

    public sealed class SCtx
    {
        public Student S { get; init; } = new Student();
        public string Token { get; init; } = string.Empty;
    }

    public static class Auth
    {
        private static readonly SemaphoreSlim SessionGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();
        private static readonly object FailLock = new object();

        public static Staff DemoUser => new Staff { Id = "u-demo", Login = "demo", Name = "Demo manager", Title = "Manager", Admin = true, Active = true };

        private static bool Paused(string key)
        {
            lock (FailLock)
            {
                if (!Fails.TryGetValue(key, out List<DateTime>? l))
                {
                    return false;
                }
                l.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));
                return l.Count >= 6;
            }
        }

        private static void Failed(string key)
        {
            lock (FailLock)
            {
                if (!Fails.TryGetValue(key, out List<DateTime>? l))
                {
                    l = new List<DateTime>();
                    Fails[key] = l;
                }
                l.Add(DateTime.UtcNow);
            }
        }

        private static void Cleared(string key)
        {
            lock (FailLock)
            {
                Fails.Remove(key);
            }
        }

        private static async Task<string> Open(string file, string userId, int hours)
        {
            await SessionGate.WaitAsync();
            try
            {
                List<SessionEntry> list = await Store.Read(file, () => new List<SessionEntry>());
                list.RemoveAll(s => s.Expires < DateTime.UtcNow);
                string tok = Access.NewToken();
                list.Add(new SessionEntry { Token = tok, UserId = userId, Expires = DateTime.UtcNow.AddHours(hours) });
                await Store.Write(file, list);
                return tok;
            }
            finally
            {
                SessionGate.Release();
            }
        }

        private static async Task<string> Lookup(string file, string? tok, int hours)
        {
            string t = (tok ?? string.Empty).Trim();
            if (t.Length != 32)
            {
                return string.Empty;
            }
            await SessionGate.WaitAsync();
            try
            {
                List<SessionEntry> list = await Store.Read(file, () => new List<SessionEntry>());
                SessionEntry? s = list.FirstOrDefault(x => x.Token == t && x.Expires > DateTime.UtcNow);
                if (s == null)
                {
                    return string.Empty;
                }
                if (s.Expires < DateTime.UtcNow.AddHours(hours - 1))
                {
                    s.Expires = DateTime.UtcNow.AddHours(hours);
                    await Store.Write(file, list);
                }
                return s.UserId;
            }
            finally
            {
                SessionGate.Release();
            }
        }

        private static async Task Close(string file, Func<SessionEntry, bool> drop)
        {
            await SessionGate.WaitAsync();
            try
            {
                List<SessionEntry> list = await Store.Read(file, () => new List<SessionEntry>());
                if (list.RemoveAll(s => drop(s)) > 0)
                {
                    await Store.Write(file, list);
                }
            }
            finally
            {
                SessionGate.Release();
            }
        }

        public static async Task<Ctx?> Check(string? tok)
        {
            string id = await Lookup(Store.StaffSessions, tok, 8);
            if (id == string.Empty)
            {
                return null;
            }
            if (id == "u-demo")
            {
                return Store.Demo() ? new Ctx { U = DemoUser, Token = tok ?? string.Empty } : null;
            }
            Staff? u = (await Store.LoadStaff()).FirstOrDefault(x => x.Id == id && x.Active && !x.MustChange);
            return u == null ? null : new Ctx { U = u, Token = tok ?? string.Empty };
        }

        public static async Task SignOut(string? tok)
        {
            string t = (tok ?? string.Empty).Trim();
            await Close(Store.StaffSessions, s => s.Token == t);
        }

        public static async Task CloseAllFor(string userId)
        {
            await Close(Store.StaffSessions, s => s.UserId == userId);
        }

        public static async Task CloseOthers(string userId, string keep)
        {
            await Close(Store.StaffSessions, s => s.UserId == userId && s.Token != keep);
        }

        public static async Task CloseEveryone()
        {
            await Close(Store.StaffSessions, s => true);
        }

        public static async Task<(string Token, string Error, bool Change)> SignIn(string login, string password)
        {
            string key = "s:" + login.ToLowerInvariant();
            if (Paused(key))
            {
                return (string.Empty, "Too many tries. Wait a few minutes and try again.", false);
            }
            List<Staff> staff = await Store.LoadStaff();
            Staff? u = staff.FirstOrDefault(x => x.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && x.Active);
            if (u != null && u.Hash != string.Empty && Access.VerifyPassword(password, u.Hash))
            {
                Cleared(key);
                if (u.MustChange)
                {
                    return (string.Empty, string.Empty, true);
                }
                return (await Open(Store.StaffSessions, u.Id, 8), string.Empty, false);
            }
            if (Store.Demo())
            {
                return (await Open(Store.StaffSessions, "u-demo", 8), string.Empty, false);
            }
            Failed(key);
            return (string.Empty, "The user name or password is not right.", false);
        }

        public static async Task<(string Token, string Error)> SetPassword(string login, string current, string fresh, string again)
        {
            List<Staff> staff;
            string tok = string.Empty;
            string err = string.Empty;
            using (await Store.Hold())
            {
                staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && x.Active);
                if (u == null || u.Hash == string.Empty || !Access.VerifyPassword(current, u.Hash))
                {
                    err = "The user name or password is not right.";
                }
                else if (fresh.Length < 8)
                {
                    err = "The new password needs at least 8 characters.";
                }
                else if (fresh == current)
                {
                    err = "Choose a password different from the first one.";
                }
                else if (fresh != again)
                {
                    err = "The two new passwords are not the same.";
                }
                else
                {
                    u.Hash = Access.HashPassword(fresh);
                    u.MustChange = false;
                    await Store.SaveStaff(staff);
                    if (File.Exists(Store.InitialFile))
                    {
                        File.Delete(Store.InitialFile);
                    }
                    await Close(Store.StaffSessions, s => s.UserId == u.Id);
                    tok = await Open(Store.StaffSessions, u.Id, 8);
                    await Store.Log(u.Id, "choose password", u.Name, true);
                }
            }
            return (tok, err);
        }

        public static async Task<string> Menu(string px, string current, Ctx c)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Item(px, "Admin", "Dashboard", current, 0));
            int unread = 0;
            int open = 0;
            if (Access.Can(c.U, "messages", 'V'))
            {
                unread = (await Store.Rows<ContactMessage>("messages")).Count(m => !m.Read);
            }
            if (Access.Can(c.U, "refunds", 'V'))
            {
                open = (await Store.Rows<RefundRequest>("refunds")).Count(r => r.Status == "open");
            }
            foreach (Area a in Access.Areas)
            {
                if (a.Key == "activity")
                {
                    sb.Append(Item(px, a.Page, Access.Can(c.U, a.Key, 'V') ? a.Name : "My activity", current, 0));
                    continue;
                }
                if (!Access.Can(c.U, a.Key, 'V'))
                {
                    continue;
                }
                sb.Append(Item(px, a.Page, a.Name, current, a.Key == "messages" ? unread : a.Key == "refunds" ? open : 0));
            }
            sb.Append(Item(px, "AdminAccount", "My account", current, 0));
            return sb.ToString();
        }

        private static string Item(string px, string page, string label, string current, int badge)
        {
            return "<a class=\"" + px + "-mi" + (page == current ? " " + px + "-on" : string.Empty) + "\" href=\"" + page + "\"><span>" + Ui.Esc(label) + "</span>" + (badge > 0 ? "<i class=\"" + px + "-badge\">" + badge + "</i>" : string.Empty) + "</a>";
        }

        public static string Who(string px, Ctx c)
        {
            return "<b>" + Ui.Esc(c.U.Name) + "</b><small>" + Ui.Esc(c.U.Title) + "</small>";
        }

        public static async Task<SCtx?> StudentCheck(string? tok)
        {
            string id = await Lookup(Store.StudentSessions, tok, 24 * 14);
            if (id == string.Empty)
            {
                return null;
            }
            Student? s = (await Store.Rows<Student>("students")).FirstOrDefault(x => x.Id == id && x.Active);
            return s == null ? null : new SCtx { S = s, Token = tok ?? string.Empty };
        }

        public static async Task<(string Token, string Error)> StudentSignIn(string email, string password)
        {
            string key = "u:" + email.ToLowerInvariant();
            if (Paused(key))
            {
                return (string.Empty, "Too many tries. Wait a few minutes and try again.");
            }
            Student? s = (await Store.Rows<Student>("students")).FirstOrDefault(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (s == null || !s.Active || !Access.VerifyPassword(password, s.Hash))
            {
                Failed(key);
                return (string.Empty, "The email or password is not right.");
            }
            Cleared(key);
            return (await Open(Store.StudentSessions, s.Id, 24 * 14), string.Empty);
        }

        public static async Task<(string Token, Dictionary<string, string> Errors)> StudentSignUp(string name, string email, string phone, string password)
        {
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (name.Length < 2)
            {
                errors["name"] = "Please enter your name.";
            }
            if (!Ui.IsEmail(email))
            {
                errors["email"] = "Please enter a valid email address.";
            }
            if (!Ui.IsPhone(phone))
            {
                errors["phone"] = "Please enter a phone number we can reach you on.";
            }
            if (password.Length < 8)
            {
                errors["password"] = "Use at least 8 characters.";
            }
            if (errors.Count > 0)
            {
                return (string.Empty, errors);
            }
            string id;
            using (await Store.Hold())
            {
                List<Student> list = await Store.Rows<Student>("students");
                if (list.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                {
                    errors["email"] = "That email already has an account. Sign in instead.";
                    return (string.Empty, errors);
                }
                id = "s-" + Store.NewCode(6).ToLowerInvariant();
                list.Add(new Student { Id = id, Name = name, Email = email, Phone = phone, Hash = Access.HashPassword(password), Created = DateTime.UtcNow, Active = true });
                await Store.SaveRows("students", list);
            }
            await Store.Log(id, "sign up", name, true);
            return (await Open(Store.StudentSessions, id, 24 * 14), errors);
        }

        public static async Task StudentSignOut(string? tok)
        {
            string t = (tok ?? string.Empty).Trim();
            await Close(Store.StudentSessions, s => s.Token == t);
        }

        public static async Task CloseStudent(string studentId)
        {
            await Close(Store.StudentSessions, s => s.UserId == studentId);
        }
    }
}
