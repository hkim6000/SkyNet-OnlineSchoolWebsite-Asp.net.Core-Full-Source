using System.Security.Cryptography;
using OnlineSchool.Models;

namespace OnlineSchool.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
        public bool Own { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "courses", Name = "Courses and lessons", Page = "AdminCourses", Extra = "Publish and delete", Own = true },
            new Area { Key = "quizzes", Name = "Quizzes", Page = "AdminQuizzes", Own = true },
            new Area { Key = "students", Name = "Students", Page = "AdminStudents", Extra = "Reset password and switch off", Own = true },
            new Area { Key = "enrollments", Name = "Enrollments", Page = "AdminEnrollments", Extra = "Grant or end access", Own = true },
            new Area { Key = "orders", Name = "Orders", Page = "AdminOrders", Extra = "Cancel unpaid orders" },
            new Area { Key = "refunds", Name = "Refunds", Page = "AdminRefunds", Extra = "Reopen declined requests" },
            new Area { Key = "instructors", Name = "Instructors", Page = "AdminInstructors" },
            new Area { Key = "messages", Name = "Messages", Page = "AdminMessages", Extra = "Delete messages" },
            new Area { Key = "pages", Name = "Pages", Page = "AdminPages", Extra = "Restore versions" },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia", Extra = "Delete files" },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true },
            new Area { Key = "users", Name = "Users and rights", Page = "AdminUsers", AdminOnly = true }
        };

        public static Area? Find(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Clean(Area a, string? letters)
        {
            string l = letters ?? string.Empty;
            bool v = l.Contains('V');
            bool e = v && l.Contains('E') && !a.NoEdit;
            bool x = e && l.Contains('X') && a.Extra != string.Empty;
            return (v ? "V" : string.Empty) + (e ? "E" : string.Empty) + (x ? "X" : string.Empty);
        }

        public static string Level(Staff u, string area)
        {
            Area? a = Find(area);
            if (a == null || !u.Active)
            {
                return string.Empty;
            }
            if (u.Admin)
            {
                return a.NoEdit ? "V" : a.Extra != string.Empty ? "VEX" : "VE";
            }
            if (a.AdminOnly)
            {
                return string.Empty;
            }
            return u.Perms.TryGetValue(area, out string? l) ? Clean(a, l) : string.Empty;
        }

        public static bool Can(Staff u, string area, char right)
        {
            return Level(u, area).Contains(right);
        }

        public static bool IsOwn(Staff u, string area)
        {
            Area? a = Find(area);
            return a != null && a.Own && !u.Admin && u.Own && u.ProviderId != string.Empty;
        }

        public static string Words(Staff u)
        {
            if (u.Admin)
            {
                return "Manager: everything";
            }
            List<string> parts = new List<string>();
            foreach (Area a in Areas.Where(x => !x.AdminOnly))
            {
                string l = Level(u, a.Key);
                if (l.Length == 0)
                {
                    continue;
                }
                string what = l.Contains('X') ? a.Extra.ToLowerInvariant() + ", edit" : l.Contains('E') ? "edit" : "view";
                parts.Add(a.Name + ": " + what + (IsOwn(u, a.Key) ? " (own)" : string.Empty));
            }
            return parts.Count == 0 ? "No access yet" : string.Join(", ", parts);
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToHexString(salt).ToLowerInvariant() + "$" + Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static bool VerifyPassword(string password, string stored)
        {
            string[] p = (stored ?? string.Empty).Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2" || !int.TryParse(p[1], out int rounds))
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromHexString(p[2]);
                byte[] want = Convert.FromHexString(p[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static string NewPassword()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            char[] c = new char[12];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
            }
            string s = new string(c);
            return s.Substring(0, 4) + "-" + s.Substring(4, 4) + "-" + s.Substring(8, 4);
        }

        public static string NewToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }
    }
}
