using System.Security.Cryptography;
using System.Text.Json;
using OnlineSchool.Models;

namespace OnlineSchool.Core
{
    public static class Store
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim FileGate = new SemaphoreSlim(1, 1);
        private static DateTime configStamp = DateTime.MinValue;
        private static bool demoMode = true;

        private sealed class Release : IDisposable
        {
            public void Dispose()
            {
                Gate.Release();
            }
        }

        public static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string ConfigFile => Path.Combine(Root, "appConfig", "application.cfg");

        public static string InitialFile => Path.Combine(Root, "appConfig", "initial-admin.txt");

        public static string StaffSessions => Path.Combine(Root, "temp", "admin", "sessions.json");

        public static string StudentSessions => Path.Combine(Root, "temp", "students", "sessions.json");

        public static string SessionFile => StaffSessions;

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Release();
        }

        public static async Task<T> Read<T>(string file, Func<T> empty)
        {
            if (!File.Exists(file))
            {
                return empty();
            }
            try
            {
                using FileStream s = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                T? v = await JsonSerializer.DeserializeAsync<T>(s, Json);
                return v == null ? empty() : v;
            }
            catch (JsonException)
            {
                return empty();
            }
        }

        public static async Task Write<T>(string file, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, Json));
            File.Move(tmp, file, true);
        }

        public static string ConfigValue(string key)
        {
            if (!File.Exists(ConfigFile))
            {
                return string.Empty;
            }
            string line = File.ReadLines(ConfigFile).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(key + " ", StringComparison.Ordinal) || l.StartsWith(key + "|", StringComparison.Ordinal)) ?? string.Empty;
            string[] part = line.Split('|', 2);
            return part.Length == 2 ? part[1].Trim() : string.Empty;
        }

        public static bool Demo()
        {
            DateTime stamp = File.Exists(ConfigFile) ? File.GetLastWriteTimeUtc(ConfigFile) : DateTime.MinValue;
            if (stamp != configStamp)
            {
                demoMode = ConfigValue("app.app.runmode") != "1";
                configStamp = stamp;
            }
            return demoMode;
        }

        public static async Task<SiteData> LoadSite()
        {
            SiteData site = await Read(Path.Combine(DataDir, "site.json"), () => new SiteData());
            Blocks.Ensure(site);
            return site;
        }

        public static Task SaveSite(SiteData site)
        {
            return Write(Path.Combine(DataDir, "site.json"), site);
        }

        public static Task<List<T>> Rows<T>(string name)
        {
            return Read(Path.Combine(DataDir, name + ".json"), () => new List<T>());
        }

        public static Task SaveRows<T>(string name, List<T> rows)
        {
            return Write(Path.Combine(DataDir, name + ".json"), rows);
        }

        public static Task<List<Staff>> LoadStaff()
        {
            return Rows<Staff>("staff");
        }

        public static Task SaveStaff(List<Staff> list)
        {
            return SaveRows("staff", list);
        }

        public static async Task Log(string userId, string action, string target, bool ok)
        {
            await FileGate.WaitAsync();
            try
            {
                string file = Path.Combine(DataDir, "activity.json");
                List<LogEntry> list = await Read(file, () => new List<LogEntry>());
                list.Insert(0, new LogEntry { When = DateTime.UtcNow, UserId = userId, Action = action, Target = target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write(file, list);
            }
            finally
            {
                FileGate.Release();
            }
        }

        public static async Task EnsureFirstManager()
        {
            if (Demo())
            {
                return;
            }
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                if (staff.Any(u => u.Admin && u.Active && !u.MustChange && u.Hash != string.Empty))
                {
                    return;
                }
                if (File.Exists(InitialFile) && staff.Any(u => u.Admin && u.Active && u.MustChange))
                {
                    return;
                }
                string pass = Access.NewPassword();
                Staff? u = staff.FirstOrDefault(x => x.Login == "admin");
                if (u == null)
                {
                    u = new Staff { Id = "u-admin", Login = "admin", Name = "Administrator", Title = "Manager" };
                    staff.Insert(0, u);
                }
                u.Admin = true;
                u.Active = true;
                u.MustChange = true;
                u.Hash = Access.HashPassword(pass);
                await SaveStaff(staff);
                Directory.CreateDirectory(Path.GetDirectoryName(InitialFile) ?? Root);
                await File.WriteAllTextAsync(InitialFile, "user: admin" + Environment.NewLine + "password: " + pass + Environment.NewLine);
            }
        }

        public static string InitialPassword()
        {
            if (!File.Exists(InitialFile))
            {
                return string.Empty;
            }
            string line = File.ReadAllLines(InitialFile).FirstOrDefault(l => l.StartsWith("password: ", StringComparison.Ordinal)) ?? string.Empty;
            return line.Length > 10 ? line.Substring(10).Trim() : string.Empty;
        }

        public static string NewCode(int length)
        {
            const string letters = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            char[] c = new char[length];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
            }
            return new string(c);
        }

        public static async Task<string> Who(string id)
        {
            switch (id)
            {
                case "system": return "System";
                case "stripe": return "Stripe";
                case "u-demo": return "Demo manager";
            }
            Staff? s = (await LoadStaff()).FirstOrDefault(x => x.Id == id);
            if (s != null)
            {
                return s.Name;
            }
            Student? st = (await Rows<Student>("students")).FirstOrDefault(x => x.Id == id);
            return st != null ? st.Name + " (student)" : id;
        }
    }
}
