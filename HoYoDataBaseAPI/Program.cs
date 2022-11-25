using System;
using System.IO;
using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace HoYoDataBaseAPI
{
    class Program
    {
        private static readonly Encoding Encoding = Encoding.UTF8;

        private static SubtitleManager SubtitleManager = new SubtitleManager();

        public static Config config;

        static async Task Main(string[] args)
        {
            config = await Config.LoadConfig();
            var address = $"http://+:{config.Port}/";
            using var server = new HttpListener();
            server.Prefixes.Add(address);
            server.Start();
            SubtitleManager.startCheckUpdate();
            Console.WriteLine($"数据库已启动，端口为{config.Port}。");
            while (true)
            {
                var ctx = await server.GetContextAsync();
                var path = ctx.Request.Url!.LocalPath[1..];
                var query = System.Web.HttpUtility.ParseQueryString(ctx.Request.Url.Query);

                switch (path)
                {
                    case "database/ss":
                        var words = query.Get("words");
                        var character = query.Get("character");
                        ResponseJson(ctx.Response, SubtitleManager.search(words, character));
                        break;
                    case "database/getsrt":
                        var srtName = query.Get("srt");
                        ResponseJson(ctx.Response, SubtitleManager.getSrtUrl(srtName));
                        break;
                    case "database/main":
                        ResponseJson(ctx.Response, SubtitleManager.getMainJson());
                        break;
                    case "database/search":
                        // 暂时没用上
                        ResponseJson(ctx.Response, SubtitleManager.getSearchJson());
                        break;
                    case "database/characters":
                        ResponseJson(ctx.Response, SubtitleManager.getCharactersJson());
                        break;
                    default:
                        break;
                }
                /*
                switch (path)
                {
                    case string s when path.StartsWith("ws/") && long.TryParse(path[3..], out _):
                        var ID = s[3..];
                        if (ctx.Request.IsWebSocketRequest)
                        {
                            var wsCtx = await ctx.AcceptWebSocketAsync(null);
                            // 已存在
                            switch (query.Get("source"))
                            {
                                case "acfun":
                                    manager.NewConection(
                                        DanmuManager.SourceType.ACFUN, ID, wsCtx.WebSocket);
                                    break;
                                case "bilibili":
                                    manager.NewConection(
                                        DanmuManager.SourceType.BILIBILI, ID, wsCtx.WebSocket);
                                    break;
                                case "qq":
                                    manager.NewConection(
                                        DanmuManager.SourceType.TENCENT_QQ, ID, wsCtx.WebSocket);
                                    break;
                            }
                        }
                        break;
                    case string s when path == "r/":
                        ctx.Response.StatusCode = 204;
                        ctx.Response.Close();
                        break;
                    case string s:
                        StaticFile(ctx.Response, s, "");
                        break;
                    default:
                        ctx.Response.StatusCode = 404;
                        ctx.Response.Close();
                        break;
                }*/
            }
        }

        private static async void StaticFile(HttpListenerResponse response, string file, string contentType)
        {
            try
            {
                if (File.Exists($@".\{file}"))
                {
                    using var stream = File.Open($@".\{file}", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    response.StatusCode = 200;
                    response.ContentType = contentType;
                    response.ContentEncoding = Encoding;
                    await stream.CopyToAsync(response.OutputStream);
                }
                else
                {
                    response.StatusCode = 404;
                }
                if (response != null)
                {
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void ResponseJson(HttpListenerResponse response, string jsonString)
        {
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = 200;
            byte[] buffer = new byte[] { };
            buffer = Encoding.UTF8.GetBytes(jsonString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            if (response != null)
            {
                response.Close();
            }
        }

        private static void ResponseJson(HttpListenerResponse response, JObject json)
        {
            ResponseJson(response, json.ToString());
        }
        private static void ResponseJson(HttpListenerResponse response, JArray json)
        {
            ResponseJson(response, json.ToString());
        }

        /*
        private static async Task<string> getByAPIAsync(string api)
        {
            var content = await client.GetStringAsync(api);
            return content;
        }*/

    }
}
