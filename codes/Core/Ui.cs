using System.Globalization;
using System.Net;
using System.Text;

namespace OnlineSchool.Core
{
    public static class Ui
    {
        public static string Esc(string? v)
        {
            return WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in v ?? string.Empty)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\\""); break;
                    case '<': sb.Append("\\u003c"); break;
                    case '>': sb.Append("\\u003e"); break;
                    case '&': sb.Append("\\u0026"); break;
                    case '\r': break;
                    case '\n': sb.Append("\\n"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        public static string Clip(string? v, int max)
        {
            string s = (v ?? string.Empty).Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        public static bool Has(string? text, string term)
        {
            return term.Length > 0 && (text ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        public static string Count(int n, string one, string many)
        {
            return n + " " + (n == 1 ? one : many);
        }

        public static string Money(decimal v)
        {
            return "$" + v.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        public static string Price(decimal v)
        {
            return "$" + v.ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", CultureInfo.InvariantCulture);
        }

        public static string Day(DateTime d)
        {
            return d.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        public static string When(DateTime d)
        {
            return d.ToLocalTime().ToString("MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture);
        }

        public static string Pill(string px, string cls, string text)
        {
            return "<span class=\"" + px + "-pill " + px + "-pill-" + cls + "\">" + Esc(text) + "</span>";
        }

        public static int Int(string? v, int min, int max, int fallback)
        {
            return int.TryParse((v ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? Math.Clamp(n, min, max) : fallback;
        }

        public static int Whole(string? v, int fallback)
        {
            return int.TryParse((v ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
        }

        public static decimal Dec(string? v, decimal fallback)
        {
            string s = (v ?? string.Empty).Trim().Replace("$", string.Empty).Replace(",", string.Empty);
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : fallback;
        }

        public static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        public static bool IsPhone(string v)
        {
            if (v.Length > 24 || v.Any(ch => !char.IsDigit(ch) && " ()+-.".IndexOf(ch) < 0))
            {
                return false;
            }
            int digits = v.Count(char.IsDigit);
            return digits >= 7 && digits <= 15;
        }

        public static string Paras(string? text)
        {
            return string.Concat((text ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => "<p>" + Esc(l) + "</p>"));
        }
    }
}
