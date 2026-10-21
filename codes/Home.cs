using System.Globalization;
using System.Text;
using System.Text.Json;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class Home : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Learn practical skills | Brightpath Academy");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Short video courses in photography, spreadsheets, home cooking and drawing, with quizzes and certificates.");
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_blocks}", PageHtml(site, media, "home"));
        }

        private string DataPart(SiteData site, List<MediaItem> media, Block b, bool first)
        {
            switch (b.Source)
            {
                case "featured":
                    {
                        List<CourseInfo> list = site.Courses.Where(c => c.Published && c.Featured).Take(4).ToList();
                        StringBuilder sb = new StringBuilder(DHead(site, b, first) + "<div class=\"hm-cgrid hm-c4\">");
                        foreach (CourseInfo c in list)
                        {
                            sb.Append(CourseCard(site, c));
                        }
                        sb.Append("</div><p class=\"hm-more\"><a class=\"hm-btn hm-btn-ghost\" href=\"Courses\">See all courses</a></p>");
                        return list.Count == 0 ? string.Empty : "<section class=\"hm-wrap hm-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + sb + "</section>";
                    }
                case "topics":
                    {
                        StringBuilder sb = new StringBuilder(DHead(site, b, first) + "<div class=\"hm-topics\">");
                        foreach (Topic t in site.Topics)
                        {
                            int n = site.Courses.Count(c => c.Published && c.Topic == t.Key);
                            sb.Append("<a class=\"hm-topic\" href=\"Courses?topic=" + Ui.Esc(t.Key) + "\"><img src=\"" + Ui.Esc(Media.Thumb(t.Image)) + "\" alt=\"\" loading=\"lazy\"><span><b>" + Ui.Esc(t.Label) + "</b><small>" + Ui.Count(n, "course", "courses") + "</small><em>" + Ui.Esc(t.Blurb) + "</em></span></a>");
                        }
                        sb.Append("</div>");
                        return "<section class=\"hm-wrap hm-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + sb + "</section>";
                    }
                default:
                    return string.Empty;
            }
        }

        private static string DHead(SiteData site, Block b, bool first)
        {
            string tag = first ? "h1" : "h2";
            return "<div class=\"hm-bhead\">" + Kick(site, b) + (b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"hm-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">") + Rich(site, b.Text, "hm-blead") + "</div>";
        }

        private static string CourseCard(SiteData site, CourseInfo c)
        {
            return "<a class=\"hm-ccard\" href=\"Course?id=" + Ui.Esc(c.Id) + "\"><img src=\"" + Ui.Esc(Media.Thumb(c.Cover)) + "\" alt=\"\" loading=\"lazy\"><span class=\"hm-ccard-b\"><small>" + Ui.Esc(Learn.TopicName(site, c.Topic)) + " &middot; " + Ui.Count(c.Lessons.Count, "lesson", "lessons") + "</small><b>" + Ui.Esc(c.Title) + "</b><p>" + Ui.Esc(c.Summary) + "</p><span class=\"hm-ccard-f\"><span class=\"hm-lvl\">" + Ui.Esc(Learn.LevelName(c.Level)) + "</span><span class=\"hm-price\">" + Ui.Price(c.Price) + "</span></span></span></a>";
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"hm-btn" + (main ? string.Empty : " hm-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"hm-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"hm-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"hm-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"hm-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "hm-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"hm-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"hm-wrap hm-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                StringBuilder sl = new StringBuilder("<div class=\"hm-bhero-p hm-slides\" id=\"hm-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures of the school\">");
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    string alt = m != null && m.Alt != string.Empty ? m.Alt : it.Title;
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(alt) + "\" decoding=\"async\">", it.Title));
                }
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"hm-slide" + (k == 0 ? " hm-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"hm-sctl\"><button type=\"button\" class=\"hm-sarr\" aria-label=\"Previous picture\" onclick=\"HomeJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"hm-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"hm-sdot" + (k == 0 ? " hm-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"HomeJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"hm-sarr\" aria-label=\"Next picture\" onclick=\"HomeJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"hm-bhero hm-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + "<div class=\"hm-wrap hm-bhero-in\"><div class=\"hm-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"hm-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "hm-blead") + Btns(site, b) + "</div></div></section>";
            }
            return "<section class=\"hm-bhero" + (mv == string.Empty ? " hm-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"hm-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"hm-wrap hm-bhero-in\"><div class=\"hm-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"hm-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "hm-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"hm-bsplit" + (b.Flip ? " hm-bflip" : string.Empty) + (b.Media == string.Empty ? " hm-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"hm-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"hm-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"hm-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"hm-bcards hm-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"hm-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"hm-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"hm-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"hm-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"hm-bsteps hm-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"hm-bstats hm-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"hm-bpeople hm-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"hm-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"hm-bnoface\">" + Ui.Esc(Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"hm-bboxes hm-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"hm-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"hm-bticks\">");
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
            StringBuilder sb = new StringBuilder("<div class=\"hm-bfaq\">" + Head(site, b, false) + "<div class=\"hm-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"hm-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"hm-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"hm-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"hm-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "hm-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"hm-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
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
