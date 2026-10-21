using System.Globalization;
using System.Text;
using System.Text.Json;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Courses : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Course catalog | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Filter practical skills courses by topic and level.");
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_blocks}", PageHtml(site, media, "courses"));
        }

        public async Task<ApiResponse> Filter()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            string topic = Pick(GetDataValue("ftopic"), site.Topics.Select(t => t.Key));
            string level = Pick(GetDataValue("flevel"), Learn.Levels.Select(l => l.Key));
            response.SetElementContents("cs-list", ListHtml(site, topic, level));
            response.SetElementContents("cs-count", CountLine(site, topic, level));
            response.SetElementContents("cs-chips", Chips(site, topic));
            response.ExecuteScript("CoursesJs.filtered('" + topic + "','" + level + "');");
            return response;
        }

        private static string Pick(string? value, IEnumerable<string> allowed)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return allowed.Contains(v) ? v : string.Empty;
        }

        private static IEnumerable<CourseInfo> Match(SiteData site, string topic, string level)
        {
            return site.Courses.Where(c => c.Published && (topic == string.Empty || c.Topic == topic) && (level == string.Empty || c.Level == level));
        }

        private static string CountLine(SiteData site, string topic, string level)
        {
            int n = Match(site, topic, level).Count();
            return n == 0 ? string.Empty : Ui.Count(n, "course", "courses") + (topic != string.Empty || level != string.Empty ? " match your filters" : " in the catalog");
        }

        private static string ListHtml(SiteData site, string topic, string level)
        {
            List<CourseInfo> list = Match(site, topic, level).OrderBy(c => site.Topics.FindIndex(t => t.Key == c.Topic)).ThenBy(c => Learn.Levels.Select(l => l.Key).ToList().IndexOf(c.Level)).ToList();
            if (list.Count == 0)
            {
                return "<div class=\"cs-none\"><b>No courses match.</b><p>Try another level, or <button type=\"button\" class=\"cs-link\" onclick=\"CoursesJs.clear()\">clear the filters</button>.</p></div>";
            }
            return string.Concat(list.Select(c => CourseCard(site, c)));
        }

        private static string Chips(SiteData site, string topic)
        {
            StringBuilder sb = new StringBuilder("<button type=\"button\" class=\"cs-chip" + (topic == string.Empty ? " cs-act" : string.Empty) + "\" aria-pressed=\"" + (topic == string.Empty ? "true" : "false") + "\" onclick=\"CoursesJs.topic('')\">All topics</button>");
            foreach (Topic t in site.Topics)
            {
                sb.Append("<button type=\"button\" class=\"cs-chip" + (t.Key == topic ? " cs-act" : string.Empty) + "\" aria-pressed=\"" + (t.Key == topic ? "true" : "false") + "\" onclick=\"CoursesJs.topic('" + t.Key + "')\">" + Ui.Esc(t.Label) + "</button>");
            }
            return sb.ToString();
        }

        private static string FilterBar(SiteData site, string topic, string level)
        {
            StringBuilder lv = new StringBuilder("<option value=\"\">All levels</option>");
            foreach ((string key, string name) in Learn.Levels)
            {
                lv.Append("<option value=\"" + key + "\"" + (key == level ? " selected" : string.Empty) + ">" + name + "</option>");
            }
            return "<div class=\"cs-bar2\"><div class=\"cs-chips\" id=\"cs-chips\">" + Chips(site, topic) + "</div><label class=\"cs-lvsel\"><span>Level</span><select id=\"cs-level\" onchange=\"CoursesJs.filter()\">" + lv + "</select></label></div><input type=\"hidden\" id=\"cs-topic\" value=\"" + topic + "\">";
        }

        private string DataPart(SiteData site, List<MediaItem> media, Block b, bool first)
        {
            switch (b.Source)
            {
                case "catalog":
                    {
                        string topic = Pick(QueryValue("topic"), site.Topics.Select(t => t.Key));
                        string level = Pick(QueryValue("level"), Learn.Levels.Select(l => l.Key));
                        return "<section class=\"cs-wrap cs-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + DHead(site, b, first) + FilterBar(site, topic, level) + "<p class=\"cs-count\" id=\"cs-count\" aria-live=\"polite\">" + CountLine(site, topic, level) + "</p><div class=\"cs-cgrid\" id=\"cs-list\">" + ListHtml(site, topic, level) + "</div></section>";
                    }
                default:
                    return string.Empty;
            }
        }

        private static string DHead(SiteData site, Block b, bool first)
        {
            string tag = first ? "h1" : "h2";
            return "<div class=\"cs-bhead\">" + Kick(site, b) + (b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"cs-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">") + Rich(site, b.Text, "cs-blead") + "</div>";
        }

        private static string CourseCard(SiteData site, CourseInfo c)
        {
            return "<a class=\"cs-ccard\" href=\"Course?id=" + Ui.Esc(c.Id) + "\"><img src=\"" + Ui.Esc(Media.Thumb(c.Cover)) + "\" alt=\"\" loading=\"lazy\"><span class=\"cs-ccard-b\"><small>" + Ui.Esc(Learn.TopicName(site, c.Topic)) + " &middot; " + Ui.Count(c.Lessons.Count, "lesson", "lessons") + "</small><b>" + Ui.Esc(c.Title) + "</b><p>" + Ui.Esc(c.Summary) + "</p><span class=\"cs-ccard-f\"><span class=\"cs-lvl\">" + Ui.Esc(Learn.LevelName(c.Level)) + "</span><span class=\"cs-price\">" + Ui.Price(c.Price) + "</span></span></span></a>";
        }


        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "camera" => a + "<path d=\"M4 8h3l1.5-2h7L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13\" r=\"3.4\"/></svg>",
                "grid" => a + "<rect x=\"3.5\" y=\"4.5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 9.5h17M3.5 14.5h17M9.5 4.5v15M15 4.5v15\"/></svg>",
                "pot" => a + "<path d=\"M5 10h14v6a4 4 0 0 1-4 4H9a4 4 0 0 1-4-4zM3 10h18M9 5c0-1.5 1.5-1.5 1.5-3M14 5c0-1.5 1.5-1.5 1.5-3\"/></svg>",
                "pencil" => a + "<path d=\"M4 20l1-4L16.5 4.5a2 2 0 0 1 3 3L8 19zM14 7l3 3\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "cal" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "heart" => a + "<path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg>",
                "star" => a + "<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "users" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "cert" => a + "<rect x=\"3.5\" y=\"4.5\" width=\"17\" height=\"12\" rx=\"2\"/><circle cx=\"12\" cy=\"10.5\" r=\"2.6\"/><path d=\"M9.5 16.5V21l2.5-1.6 2.5 1.6v-4.5\"/></svg>",
                "play" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M10 8.5l5.5 3.5-5.5 3.5z\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            if (Blocks.IsVideo(src))
            {
                return "<video src=\"" + Ui.Esc(src) + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Esc(src) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"cs-btn" + (main ? string.Empty : " cs-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"cs-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"cs-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"cs-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"cs-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "cs-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"cs-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"cs-wrap cs-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"cs-bhero" + (mv == string.Empty ? " cs-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"cs-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"cs-wrap cs-bhero-in\"><div class=\"cs-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"cs-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "cs-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"cs-bsplit" + (b.Flip ? " cs-bflip" : string.Empty) + (b.Media == string.Empty ? " cs-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"cs-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"cs-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"cs-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"cs-bcards cs-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"cs-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"cs-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"cs-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"cs-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"cs-bsteps cs-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"cs-bstats cs-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Initials(string name)
        {
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"cs-bpeople cs-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"cs-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"cs-bnoface\">" + Ui.Esc(Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"cs-bboxes cs-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"cs-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"cs-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Esc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string QaList(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"cs-bfaq\">" + Head(site, b, false) + "<div class=\"cs-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"cs-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"cs-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"cs-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"cs-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "cs-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(SiteData site, List<MediaItem> media, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, media, b, first),
                    "text" => Sec(b, "<div class=\"cs-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, media, b),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, media, b),
                    "boxes" => Boxes(site, b),
                    "faq" => QaList(site, b),
                    "quote" => Quote(site, media, b),
                    "media" => Wide(site, media, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, media, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }
    }
}
