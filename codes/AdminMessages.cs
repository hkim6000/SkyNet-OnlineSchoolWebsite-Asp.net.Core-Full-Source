using System.Text;
using OnlineSchool.Core;
using OnlineSchool.Models;
using SkyNet;

namespace OnlineSchool.codes
{
    public class AdminMessages : WebPage
    {
        private const string AreaKey = "messages";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Messages | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Chip(string key, string label, string cur)
        {
            return "<button type=\"button\" class=\"ms-chip" + (key == cur ? " ms-act" : string.Empty) + "\" onclick=\"AdminMessagesJs.chip('" + key + "')\">" + label + "</button>";
        }

        private static string Chips(string cur)
        {
            return Chip("all", "All", cur) + Chip("unread", "Unread", cur) + Chip("read", "Read", cur);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminMessagesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "ms", "AdminMessages", c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("ms-main", Ad.NoAccess("ms", "messages"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ms-ph\"><div><h1>Messages</h1><p>Sent from the Contact page.</p></div></div>");
            sb.Append("<div class=\"ms-tools\"><input type=\"search\" id=\"ms-q\" placeholder=\"Search name, email or text\" aria-label=\"Search\" oninput=\"AdminMessagesJs.search()\"><div class=\"ms-chips\" id=\"ms-chips\">" + Chips("all") + "</div></div><div id=\"ms-list\"></div>");
            response.SetElementContents("ms-main", sb.ToString());
            response.ExecuteScript("AdminMessagesJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminMessagesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminMessagesJs", c, "view messages", AreaKey, "You do not have access to messages.");
            }
            SiteData site = await Store.LoadSite();
            await Ad.Shell(response, "ms", "AdminMessages", c, site);
            string st = GetDataValue("st") ?? "all";
            string q = Ui.Clip(GetDataValue("q"), 60);
            int pg = Ui.Int(GetDataValue("pg"), 1, 1000, 1);
            IEnumerable<ContactMessage> rows = (await Store.Rows<ContactMessage>("messages")).OrderByDescending(m => m.When);
            if (st == "unread")
            {
                rows = rows.Where(m => !m.Read);
            }
            else if (st == "read")
            {
                rows = rows.Where(m => m.Read);
            }
            if (q.Length > 0)
            {
                rows = rows.Where(m => Ui.Has(m.Name, q) || Ui.Has(m.Email, q) || Ui.Has(m.Text, q) || Ui.Has(m.Topic, q));
            }
            List<ContactMessage> list = rows.ToList();
            pg = Math.Min(pg, Ad.Pages(list.Count));
            response.SetElementContents("ms-chips", Chips(st));
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"ms-empty\">No messages here.</div>");
            }
            else
            {
                sb.Append("<div class=\"ms-wrapt\"><table class=\"ms-table\"><thead><tr><th>From</th><th>Topic</th><th>Message</th><th>Sent</th><th>Status</th></tr></thead><tbody>");
                foreach (ContactMessage m in list.Skip((pg - 1) * Ad.Size).Take(Ad.Size))
                {
                    sb.Append("<tr class=\"ms-row\" tabindex=\"0\" onclick=\"AdminMessagesJs.open('" + Ui.Js(m.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminMessagesJs.open('" + Ui.Js(m.Id) + "')}\"><td data-l=\"From\"><b>" + Ui.Esc(m.Name) + "</b><small>" + Ui.Esc(m.Email) + "</small></td><td data-l=\"Topic\">" + Ui.Esc(m.Topic) + "</td><td data-l=\"Message\">" + Ui.Esc(Ui.Clip(m.Text, 70)) + "</td><td data-l=\"Sent\">" + Ui.Day(m.When) + "</td><td data-l=\"Status\">" + Ad.Pill("ms", m.Read ? "off" : "new", m.Read ? "Read" : "New") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Ad.Pager("ms", "AdminMessagesJs", pg, list.Count, "message", "messages"));
            }
            response.SetElementContents("ms-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminMessagesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Ad.Refuse(response, "AdminMessagesJs", c, "view message", AreaKey, "You do not have access to messages.");
            }
            ContactMessage? m;
            bool changed = false;
            using (await Store.Hold())
            {
                List<ContactMessage> all = await Store.Rows<ContactMessage>("messages");
                m = all.FirstOrDefault(x => x.Id == GetDataValue("mid"));
                if (m != null && !m.Read && Access.Can(c.U, AreaKey, 'E'))
                {
                    m.Read = true;
                    changed = true;
                    await Store.SaveRows("messages", all);
                }
            }
            if (m == null)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message was not found.');");
                return response;
            }
            if (changed)
            {
                response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ms-stack\"><dl class=\"ms-kv\"><dt>From</dt><dd>" + Ui.Esc(m.Name) + "</dd><dt>Email</dt><dd><a class=\"ms-lnk\" href=\"mailto:" + Ui.Esc(m.Email) + "?subject=" + Uri.EscapeDataString("Re: " + m.Topic) + "\">" + Ui.Esc(m.Email) + "</a></dd><dt>Topic</dt><dd>" + Ui.Esc(m.Topic) + "</dd><dt>Sent</dt><dd>" + Ui.When(m.When) + "</dd></dl><div class=\"ms-text\">" + Ui.Paras(m.Text) + "</div><div class=\"ms-acts\">");
            sb.Append("<a class=\"ms-btn\" href=\"mailto:" + Ui.Esc(m.Email) + "?subject=" + Uri.EscapeDataString("Re: " + m.Topic) + "\">Reply by email</a>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-ghost\" onclick=\"AdminMessagesJs.mark('" + Ui.Js(m.Id) + "', " + (m.Read ? "false" : "true") + ")\">Mark as " + (m.Read ? "unread" : "read") + "</button>");
            }
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-danger\" onclick=\"AdminMessagesJs.remove('" + Ui.Js(m.Id) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-ghost\" onclick=\"AdminMessagesJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("ms-mbody", sb.ToString());
            response.ExecuteScript("AdminMessagesJs.opened('" + Ui.Js(m.Topic == string.Empty ? "Message" : m.Topic) + "');");
            return response;
        }

        public async Task<ApiResponse> Mark()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminMessagesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("mid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Ad.Refuse(response, "AdminMessagesJs", c, "mark message", id, "You do not have the Edit right on Messages.");
            }
            bool read = GetDataValue("read") == "1";
            string from = string.Empty;
            using (await Store.Hold())
            {
                List<ContactMessage> all = await Store.Rows<ContactMessage>("messages");
                ContactMessage? m = all.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Read = read;
                    from = m.Name;
                    await Store.SaveRows("messages", all);
                }
            }
            if (from != string.Empty)
            {
                await Store.Log(c.U.Id, read ? "mark message read" : "mark message unread", from, true);
            }
            response.ExecuteScript("AdminMessagesJs.changed('" + (read ? "Marked as read." : "Marked as unread.") + "');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Ad.Guard(response, "AdminMessagesJs", GetDataValue("tok"));
            if (c == null)
            {
                return response;
            }
            string id = GetDataValue("mid") ?? string.Empty;
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Ad.Refuse(response, "AdminMessagesJs", c, "delete message", id, "Deleting needs the Extra right on Messages.");
            }
            string from = string.Empty;
            using (await Store.Hold())
            {
                List<ContactMessage> all = await Store.Rows<ContactMessage>("messages");
                ContactMessage? m = all.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    from = m.Name;
                    all.Remove(m);
                    await Store.SaveRows("messages", all);
                }
            }
            if (from != string.Empty)
            {
                await Store.Log(c.U.Id, "delete message", from, true);
            }
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.changed('Message deleted.');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.SignOut(GetDataValue("tok"));
            return response;
        }
    }
}
