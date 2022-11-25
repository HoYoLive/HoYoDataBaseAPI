using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Timers;
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

namespace HoYoDataBaseAPI
{
    internal class SubtitleManager
    {
        private static string[] Data_Sources = new string[] {
            "https://cdn.jsdelivr.net/gh/HoYoLive/HoYoLiveData@latest/database",
            "https://raw.githubusercontent.com/HoYoLive/HoYoLiveData/main/database"
            };

        private static Uri REPOS_INFO = new Uri("https://api.github.com/repos/HoYoLive/HoYoLiveData");
        private static int UPDATE_TIME = 60 * 60 * 1000;

        private readonly HttpClient client = new HttpClient();
        JObject? mainJson, searchJson, charactersJson;
        string lastPush = "";

        System.Timers.Timer timer = new System.Timers.Timer(UPDATE_TIME);

        public SubtitleManager()
        {
            client.DefaultRequestHeaders.Add("user-agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.150 Safari/537.36 Edg/88.0.705.63");
            client.Timeout = new TimeSpan(0, 5, 0);
            checkUpdate();
        }

        public JObject getMainJson()
        {
            if (mainJson == null) return default(JObject);
            return mainJson;
        }

        public JObject getSearchJson()
        {
            if (searchJson == null) return default(JObject);
            return searchJson;
        }

        public JObject getCharactersJson()
        {
            if (charactersJson == null) return default(JObject);
            return charactersJson;
        }

        public JArray search(string word, string? character = null)
        {
            JArray result = new JArray();
            if (character != null && character != "")
            {
                if(!mainJson!.ContainsKey(character)) return result;
                result = searchByCharacter(word, character);
            }
            else
            {
                foreach(var x in mainJson!)
                {
                    string name = x.Key;
                    //JToken value = x.Value;
                    JArray temp = searchByCharacter(word, name);
                    result.Merge(temp);
                }
            }
            return result;
        }

        private JArray searchByCharacter(string word, string character)
        {
            JArray result = new JArray();
            var list = mainJson![character]!;
            foreach (var live in list)
            {
                if (live["srt"] != null && live["srt"]!.ToString() != "")
                {
                    var srtString = searchJson![character]![live["srt"]!.ToString()]!.ToString();
                    if (word != null && word != "" && srtString.Contains(word!))
                    {
                        var temp = (JObject)live.DeepClone();
                        temp.Add("match", Regex.Matches(srtString, word).Count);
                        result.Add(temp);
                    }else if(word == null || word == "")
                    {
                        result.Add(live);
                    }
                }
            }
            return result;
        }

        public JArray getSrtUrl(string srtName)
        {
            JArray result = new JArray();
            foreach (var x in mainJson!)
            {
                string name = x.Key;
                JArray value = (JArray)x.Value;
                foreach (var y in value)
                {
                    if (y["srt"].ToString() == srtName)
                    {
                        string date = y["date"].ToString();
                        string year = "20" + date.Split('-')[0];
                        string month = date.Split('-')[1];
                        foreach(string s in Data_Sources)
                        {
                            result.Add(s + $"/{name}/{year}/{month}/srt/{srtName}.srt");
                        }
                    }
                }
            }
            return result;
        }

        private async Task<string> getLastPush()
        {
            JObject? json = (JObject?)await getJson(REPOS_INFO);
            if (json == null || json["pushed_at"] == null) return "";
            return json["pushed_at"]!.ToString();
        }

        public void startCheckUpdate()
        {
            timer.Elapsed += checkUpdate;
            timer.AutoReset = true;
            timer.Start();
        }

        private void checkUpdate(object? sender, ElapsedEventArgs elapsedEventArg)
        {
            checkUpdate();
        }

        public async void checkUpdate()
        {
            string push = await getLastPush();
            if (lastPush == push) return;
            lastPush = push;
            if (readCache()) return;
            updateJson();
        }

        private async void updateJson()
        {
            Console.WriteLine($"更新Json中：{lastPush}");
            for(int i = 0; i < Data_Sources.Length; i++)
            {
                try
                {
                    JObject? databaseMainJson = (JObject?)await getJson(Data_Sources[i] + "/main.json");
                    List<string> characters = databaseMainJson["character"].ToObject<List<string>>();
                    charactersJson = databaseMainJson;
                    JObject Main = new JObject();
                    JObject Search = new JObject();
                    foreach (var character in characters)
                    {
                        JObject? characterMainJson = (JObject?)await getJson(Data_Sources[i] + $"/{character}/main.json");
                        List<int> years = characterMainJson["years"].ToObject<List<int>>();
                        JArray yearsMainJson = new JArray();
                        JObject yearsSearchJson = new JObject();
                        foreach (var year in years)
                        {
                            JArray? yearMainJson = (JArray?)await getJson(Data_Sources[i] + $"/{character}/{year}/main.json");
                            JObject? yearSearchJson = (JObject?)await getJson(Data_Sources[i] + $"/{character}/{year}/search.json");
                            yearsMainJson.Merge(yearMainJson);
                            yearsSearchJson.Merge(yearSearchJson);
                        }
                        Main.Add(character, yearsMainJson);
                        Search.Add(character, yearsSearchJson);
                    }
                    mainJson = Main;
                    searchJson = Search;
                    saveCache();
                    Console.WriteLine($"更新Json完成：{lastPush}");
                    break;
                }
                catch
                {
                    Console.WriteLine($"【错误】：源{Data_Sources[i]}连接失败。");
                    continue;
                }
            }
            
        }

        private void saveCache()
        {
            if (!System.IO.Directory.Exists(@".\cache\"))
            {
                System.IO.Directory.CreateDirectory(@".\cache\");//不存在就创建目录 
            }
            File.WriteAllText(@".\cache\main.json", mainJson.ToString());
            File.WriteAllText(@".\cache\search.json", searchJson.ToString());
            File.WriteAllText(@".\cache\characters.json", charactersJson.ToString());
            File.WriteAllText(@".\cache\last_push.txt", lastPush);
        }

        private bool readCache()
        {
            try
            {
                string lastPushCache = File.ReadAllText(@".\cache\last_push.txt");
                if(lastPushCache != lastPush)return false;
                string mainJsonCache = File.ReadAllText(@".\cache\main.json");
                string searchJsonCache = File.ReadAllText(@".\cache\search.json");
                string charactersJsonCache = File.ReadAllText(@".\cache\characters.json");
                mainJson = JObject.Parse(mainJsonCache);
                searchJson = JObject.Parse(searchJsonCache);
                charactersJson = JObject.Parse(charactersJsonCache);
                Console.WriteLine($"读取Json缓存完毕：{lastPush}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<object?> getJson(string url)
        {
            Uri uri = new Uri(url);
            return await getJson(uri);
        }
        private async Task<object?> getJson(Uri uri)
        {
            try
            {
                string responseBody = await client.GetStringAsync(uri);
                if (responseBody.StartsWith("{"))
                {
                    var json = JObject.Parse(responseBody);
                    return json;
                }
                else if (responseBody.StartsWith("["))
                {
                    var json = JArray.Parse(responseBody);
                    return json;
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
            return null;
        }

    }
}
