﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using KryBot.lang;
using Newtonsoft.Json;
using RestSharp;
using static KryBot.Messages;
using static KryBot.Tools;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace KryBot
{
    public class Parse
    {
        // Steam //
        public static Classes.Log SteamGetProfile(Classes.Bot bot, bool echo)
        {
            try
            {
                var response = Web.Get("http://steamcommunity.com/", "", new List<Parameter>(),
                    Generate.Cookies_Steam(bot.SteamSessid, bot.SteamLogin), new List<HttpHeader>(),
                    bot.UserAgent, bot.Proxy ?? "");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                string login;
                try
                {
                    login =
                        htmlDoc.DocumentNode.SelectSingleNode("//a[@class='user_avatar playerAvatar in-game']")
                            .Attributes["href"].Value.Split('/')[4];
                }
                catch (NullReferenceException)
                {
                    try
                    {
                        login =
                            htmlDoc.DocumentNode.SelectSingleNode("//a[@class='user_avatar playerAvatar online']")
                                .Attributes["href"].Value.Split('/')[4];
                    }
                    catch (NullReferenceException)
                    {
                        login =
                            htmlDoc.DocumentNode.SelectSingleNode("//a[@class='user_avatar playerAvatar offline']")
                                .Attributes["href"].Value.Split('/')[4];
                    }
                }
                return
                    ConstructLog(
                        GetDateTime() + @"{Steam} Login success (" + login + ')', Color.White, true, echo);
            }
            catch (NullReferenceException)
            {
                ProfileLoaded();
                return ConstructLog(GetDateTime() + @"{Steam} " + strings.ParseProfile_LoginOrServerError, Color.Red,
                    false, echo);
            }
        }

        public static async Task<Classes.Log> SteamGetProfileAsync(Classes.Bot bot, bool echo)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamGetProfile(bot, echo);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        // Steam //

        // GameMiner //
        public static Classes.Log GameMinerGetProfile(Classes.Bot bot, bool echo)   
        {
            try
            {
                var response = Web.Get("http://gameminer.net/", "", new List<Parameter>(),
                    Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"), new List<HttpHeader>(),
                    bot.UserAgent, bot.Proxy ?? "");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                bot.GameMinerCoal =
                    int.Parse(htmlDoc.DocumentNode.SelectSingleNode("//span[@class='user__coal']").InnerText);
                bot.GameMinerLevel =
                    int.Parse(htmlDoc.DocumentNode.SelectSingleNode("//span[@class='g-level-icon']").InnerText);

                //var error = htmlDoc.DocumentNode.SelectSingleNode("//a[@class='notice-top label label-error label-big']");
                //if (error != null)
                //{
                //    bot.GameMinerEnabled = false;
                //    bot.GameMinerxsrf = "";
                //    bot.GameMinerToken = "";
                //    SaveSettings(bot, "");
                //    return
                //    ConstructLog(
                //        GetDateTime() + @"{GameMiner} " + strings.AccountNotActive, Color.Red, false, true);
                //}

                ProfileLoaded();
                return
                    ConstructLog(
                        GetDateTime() + @"{GameMiner} " + strings.ParseProfile_Coal + @": " + bot.GameMinerCoal + ' '
                        + strings.ParseProfile_Level + @": " + bot.GameMinerLevel, Color.White, true, echo);
            }
            catch (NullReferenceException)
            {
                ProfileLoaded();
                return ConstructLog(GetDateTime() + @"{GameMiner} " + strings.ParseProfile_LoginOrServerError, Color.Red,
                    false, echo);
            }
        }

        public static async Task<Classes.Log> GameMinerGetProfileAsync(Classes.Bot bot, bool echo)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = GameMinerGetProfile(bot, echo);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log GameMinerWonParse(Classes.Bot bot)
        {
            var response = Web.Get("http://gameminer.net/", "giveaways/won", new List<Parameter>(),
                Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"), new List<HttpHeader>(),
                bot.UserAgent, bot.Proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);

            var nodes = htmlDoc.DocumentNode.SelectNodes("//tbody[@class='giveaways__giveaways']/tr");

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    var test = node.SelectNodes(".//td[@class='valign-middle m-table-state-finished']");

                    if (test != null)
                    {
                        for (var i = 0; i < test.Count; i++)
                        {
                            if (test[i].InnerText == "Закончена\n\n" || test[i].InnerText == "Приз выслан победителю")
                            {
                                test.Remove(test[i]);
                                i--;
                            }
                        }

                        if (test.Count > 0)
                        {
                            return GiveawayHaveWon("GameMiner", test.Count);
                        }
                        return null;
                    }
                }
            }
            return null;
        }

        public static async Task<Classes.Log> GameMinerWonParseAsync(Classes.Bot bot)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = GameMinerWonParse(bot);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log GameMinerLoadGiveaways(Classes.Bot bot, List<GameMiner.GmGiveaway> giveaways)
        {
            try
            {
                var content = "";
                giveaways?.Clear();
                int pages = 0;
                if (bot.GameMinerFreeGolden)
                {
                    var goldenFreesResponse = Web.Get("http://gameminer.net",
                        "/api/giveaways/golden?page=1&count=10&q=&type=regular&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                        new List<Parameter>(),
                        Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                        new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");

                    try
                    {
                        var goldenFreeJsonResponse =
                            JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                goldenFreesResponse.RestResponse.Content);
                        GameMinerAddGiveaways(goldenFreeJsonResponse, bot, giveaways);

                        content += GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_Found + ' ' +
                                   goldenFreeJsonResponse.Total +
                                   ' ' + strings.ParseLoadGiveaways_FreeGoldenGiveawaysIn + ' ' +
                                   goldenFreeJsonResponse.Last_Page + ' ' + strings.ParseLoadGiveaways_Pages + "\n";

                        pages = goldenFreeJsonResponse.Last_Page;

                        if (pages > 1)
                        {
                            for (var i = 1; i < pages + 1; i++)
                            {
                                goldenFreesResponse = Web.Get("http://gameminer.net/",
                                    "/api/giveaways/golden?page=" + (i + 1) +
                                    "&count=10&q=&type=regular&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                                    new List<Parameter>(),
                                    Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                                    new List<HttpHeader>(),
                                    bot.UserAgent, bot.Proxy ?? "");
                                goldenFreeJsonResponse =
                                    JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                        goldenFreesResponse.RestResponse.Content);

                                GameMinerAddGiveaways(goldenFreeJsonResponse, bot, giveaways);
                            }
                        }
                    }
                    catch (JsonReaderException)
                    {
                        
                    }
                }
                if (bot.GameMinerRegular)
                {
                    if (bot.GameMinerOnlyGifts)
                    {
                        var regularGiftsResponse = Web.Get("http://gameminer.net",
                            "/api/giveaways/coal?page=1&count=10&q=&type=regular&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                            new List<Parameter>(),
                            Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                            new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                        try
                        {
                            var regularGiftJsonResponse =
                                JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                    regularGiftsResponse.RestResponse.Content);
                            GameMinerAddGiveaways(regularGiftJsonResponse, bot, giveaways);
                            content += GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_Found + ' ' +
                                       regularGiftJsonResponse.Total +
                                       ' ' + strings.ParseLoadGiveaways_RegularGiveawaysIn + ' ' +
                                       regularGiftJsonResponse.Last_Page + ' ' + strings.ParseLoadGiveaways_Pages + "\n";

                            pages = regularGiftJsonResponse.Last_Page;
                        }
                        catch (JsonReaderException)
                        {
                            
                        }
                    }
                    else
                    {
                        var regularAnyResponse = Web.Get("http://gameminer.net",
                            "/api/giveaways/coal?page=1&count=10&q=&type=any&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                            new List<Parameter>(),
                            Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                            new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                        try
                        {
                            var regularAnyJsonResponse =
                                JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                    regularAnyResponse.RestResponse.Content);
                            GameMinerAddGiveaways(regularAnyJsonResponse, bot, giveaways);
                            content += GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_Found + ' ' +
                                       regularAnyJsonResponse.Total +
                                       ' ' + strings.ParseLoadGiveaways_RegularGiveawaysIn + ' ' +
                                       regularAnyJsonResponse.Last_Page + ' ' + strings.ParseLoadGiveaways_Pages + "\n";
                            pages = regularAnyJsonResponse.Last_Page;
                        }
                        catch (JsonReaderException)
                        {
                            
                        }
                    }

                    if (pages > 1)
                    {
                        for (var i = 1; i < pages + 1; i++)
                        {
                            if (bot.GameMinerOnlyGifts)
                            {
                                var regularGiftsResponse = Web.Get("http://gameminer.net/",
                                    "/api/giveaways/coal?page=" + (i + 1) +
                                    "&count=10&q=&type=regular&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                                    new List<Parameter>(),
                                    Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                                try
                                {
                                    var regularGiftJsonResponse =
                                        JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                            regularGiftsResponse.RestResponse.Content);

                                    GameMinerAddGiveaways(regularGiftJsonResponse, bot, giveaways);
                                }
                                catch (JsonReaderException)
                                {

                                }
                            }
                            else
                            {
                                var regularAnyResponse = Web.Get("http://gameminer.net/",
                                    "/api/giveaways/coal?page=" + (i + 1) +
                                    "&count=10&q=&type=any&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                                    new List<Parameter>(),
                                    Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                                try
                                {
                                    var regularAnyJsonResponse =
                                        JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                            regularAnyResponse.RestResponse.Content);

                                    GameMinerAddGiveaways(regularAnyJsonResponse, bot, giveaways);
                                }
                                catch (JsonReaderException)
                                {
                                    
                                }
                            }
                        }
                    }
                }

                if (bot.GameMinerSandbox)
                {
                    if (bot.GameMinerOnlyGifts)
                    {
                        var sandboxGiftsResponse = Web.Get("http://gameminer.net",
                            "/api/giveaways/sandbox?page=1&count=10&q=&type=regular&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                            new List<Parameter>(),
                            Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                            new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                        try
                        {
                            var sandboxGiftJsonResponse =
                                JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                    sandboxGiftsResponse.RestResponse.Content);
                            GameMinerAddGiveaways(sandboxGiftJsonResponse, bot, giveaways);

                            content += GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_Found + ' ' +
                                       sandboxGiftJsonResponse.Total +
                                       ' ' + strings.ParseLoadGiveaways_RegularGiveawaysIn + ' ' +
                                       sandboxGiftJsonResponse.Last_Page + ' ' + strings.ParseLoadGiveaways_Pages + "\n";

                            pages = sandboxGiftJsonResponse.Last_Page;
                        }
                        catch (JsonReaderException)
                        {
                            
                        }
                    }
                    else
                    {
                        var regularAnyResponse = Web.Get("http://gameminer.net",
                            "/api/giveaways/sandbox?page=1&count=10&q=&type=any&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                            new List<Parameter>(),
                            Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                            new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                        try
                        {
                            var regularAnyJsonResponse =
                                JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                    regularAnyResponse.RestResponse.Content);
                            GameMinerAddGiveaways(regularAnyJsonResponse, bot, giveaways);

                            content += GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_Found + ' ' +
                                       regularAnyJsonResponse.Total +
                                       ' ' + strings.ParseLoadGiveaways_SandboxGiveawaysIn + ' ' +
                                       regularAnyJsonResponse.Last_Page + ' ' + strings.ParseLoadGiveaways_Pages + "\n";

                            pages = regularAnyJsonResponse.Last_Page;
                        }
                        catch (JsonReaderException)
                        {
                            
                        }
                    }

                    if (pages > 1)
                    {
                        for (var i = 1; i < pages + 1; i++)
                        {
                            if (bot.GameMinerOnlyGifts)
                            {
                                var regularGiftsResponse = Web.Get("http://gameminer.net/",
                                    "/api/giveaways/sandbox?page=" + (i + 1) +
                                    "&count=10&q=&type=regular&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                                    new List<Parameter>(),
                                    Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                                try
                                {
                                    var regularGiftJsonResponse =
                                        JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                            regularGiftsResponse.RestResponse.Content);

                                    GameMinerAddGiveaways(regularGiftJsonResponse, bot, giveaways);
                                }
                                catch (JsonReaderException)
                                {
                                    
                                }
                            }
                            else
                            {
                                var regularAnyResponse = Web.Get("http://gameminer.net/",
                                    "/api/giveaways/sandbox?page=" + (i + 1) +
                                    "&count=10&q=&type=any&enter_price=on&sortby=finish&order=asc&filter_entered=on",
                                    new List<Parameter>(),
                                    Generate.Cookies_GameMiner(bot.GameMinerToken, bot.GameMinerxsrf, "ru_RU"),
                                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                                try
                                {
                                    var regularAnyJsonResponse =
                                        JsonConvert.DeserializeObject<GameMiner.JsonRootObject>(
                                            regularAnyResponse.RestResponse.Content);

                                    GameMinerAddGiveaways(regularAnyJsonResponse, bot, giveaways);
                                }
                                catch (JsonReaderException)
                                {
        
                                }
                            }
                        }
                    }
                }
                
                if (giveaways == null)
                {
                    return
                        ConstructLog(
                            content + GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                            @": " + 0, Color.White, true, true);
                }
                return
                    ConstructLog(
                        content + GetDateTime() + @"{GameMiner} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                        @": " + giveaways.Count, Color.White, true, true);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static async Task<Classes.Log> GameMinerLoadGiveawaysAsync(Classes.Bot bot,
            List<GameMiner.GmGiveaway> giveaways)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = GameMinerLoadGiveaways(bot, giveaways);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        private static void GameMinerAddGiveaways(GameMiner.JsonRootObject json, Classes.Bot bot,
            List<GameMiner.GmGiveaway> giveaways)
        {
            if (json != null)
                foreach (var giveaway in json.Giveaways)
                {
                    var lot = new GameMiner.GmGiveaway();
                    try
                    {
                        if (giveaway.Golden && giveaway.Price != 0)
                        {
                            break;
                        }
                        if (lot.Price > bot.GameMinerCoal || lot.Price > bot.GameMinerJoinCoalLimit)
                        {
                            break;
                        }
                        lot.Name = giveaway.Game.Name;
                        lot.Id = giveaway.Code;
                        lot.IsRegular = giveaway.Sandbox == null;
                        lot.IsSandbox = giveaway.Sandbox != null;
                        lot.IsGolden = giveaway.Golden;
                        lot.Page = json.Page;
                        lot.Price = giveaway.Price;
                        giveaways?.Add(lot);
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
        }

        public static void GameMinerCompareGiveaways(List<GameMiner.GmGiveaway> giveaways)
        {
            for (var i = 0; i < giveaways.Count; i++)
            {
                for (var j = 0; j < giveaways.Count; j++)
                {
                    if (j != i && giveaways[j].Id == giveaways[i].Id)
                    {
                        giveaways.Remove(giveaways[j]);
                    }
                }
            }
        }

        //GameMiner //

        // SteamGifts//
        public static Classes.Log SteamGiftsGetProfile(Classes.Bot bot, bool echo)
        {
            var response = Web.Get("http://www.steamgifts.com/", "", new List<Parameter>(),
                    Generate.Cookies_SteamGifts(bot.SteamGiftsPhpSessId), new List<HttpHeader>(), bot.UserAgent,
                    bot.Proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);

            try
            {
                bot.SteamGiftsPoint =
                    int.Parse(htmlDoc.DocumentNode.SelectSingleNode("//a[@href='/account']/span[1]").InnerText);
                bot.SteamGiftsLevel =
                    int.Parse(
                        htmlDoc.DocumentNode.SelectSingleNode("//a[@href='/account']/span[2]").InnerText.Split(' ')[1]);
                return
                    ConstructLog(
                        GetDateTime() + @"{SteamGifts} " + strings.ParseProfile_Points + @": " + bot.SteamGiftsPoint +
                        ' '
                        + strings.ParseProfile_Level + @": " + bot.SteamGiftsLevel, Color.White, true, echo);
            }
            catch (Exception)
            {
                var error = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='notification notification--warning']");

                if (error != null)
                {
                    bot.SteamGiftsEnabled = false;
                    bot.SteamGiftsPhpSessId = "";
                    SaveProfile(bot, "");
                    return ConstructLog(GetDateTime() + @"{SteamGifts} " + strings.AccountNotActive,
                    Color.Red, false, echo);
                }

                return ConstructLog(GetDateTime() + @"{SteamGifts} " + strings.ParseProfile_LoginOrServerError,
                    Color.Red, false, echo);
            }
        }

        public static async Task<Classes.Log> SteamGiftsGetProfileAsync(Classes.Bot bot, bool echo)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamGiftsGetProfile(bot, echo);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamGiftsWonParse(Classes.Bot bot)
        {
            var response = Web.Get("http://www.steamgifts.com/", "", new List<Parameter>(),
                Generate.Cookies_SteamGifts(bot.SteamGiftsPhpSessId), new List<HttpHeader>(), bot.UserAgent,
                bot.Proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);

            var nodes =
                htmlDoc.DocumentNode.SelectSingleNode("//a[@title='Giveaways Won']//div[@class='nav__notification']");

            if (nodes != null)
            {
                return GiveawayHaveWon("SteamGifts", int.Parse(nodes.InnerText));
            }
            return null;
        }

        public static async Task<Classes.Log> SteamGiftsWonParseAsync(Classes.Bot bot)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamGiftsWonParse(bot);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamGiftsLoadGiveaways(Classes.Bot bot, List<SteamGifts.SgGiveaway> giveaways)
        {
            try
            {
                var content = "";
                giveaways?.Clear();

                if (bot.SteamGiftsWishList)
                {
                    content += SteamGiftsLoadWishListGiveaways(bot, giveaways);
                }

                if (bot.SteamGiftsGroup)
                {
                    content += SteamGiftsLoadGroupGiveaways(bot, giveaways);
                }

                if (bot.SteamGiftsRegular)
                {
                    var response = Web.Get("http://www.steamgifts.com", "/giveaways/search?page=1",
                        new List<Parameter>(), Generate.Cookies_SteamGifts(bot.SteamGiftsPhpSessId),
                        new List<HttpHeader>(),
                        bot.UserAgent, bot.Proxy ?? "");
                    if (response != null)
                    {
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(response.RestResponse.Content);
                        var pages =
                            int.Parse(
                                htmlDoc.DocumentNode.SelectSingleNode("//div[@class='pagination__navigation']/a[" +
                                                                      (htmlDoc.DocumentNode.SelectNodes(
                                                                          "//div[@class='pagination__navigation']/a")
                                                                          .Count - 1) + "]").Attributes[
                                                                              "data-page-number"]
                                    .Value);

                        if (pages != 1)
                        {
                            for (var i = 1; i < pages + 1; i++)
                            {
                                response = Web.Get("http://www.steamgifts.com", "/giveaways/search?page=" + (i + 1),
                                    new List<Parameter>(), Generate.Cookies_SteamGifts(bot.SteamGiftsPhpSessId),
                                    new List<HttpHeader>(),
                                    bot.UserAgent, bot.Proxy ?? "");
                                htmlDoc = new HtmlDocument();
                                htmlDoc.LoadHtml(response.RestResponse.Content);

                                var nodes =
                                    htmlDoc.DocumentNode.SelectNodes(
                                        "//div[@class='widget-container']//div[2]//div[3]//div[@class='giveaway__row-outer-wrap']//div[@class='giveaway__row-inner-wrap']");
                                pages =
                                    int.Parse(
                                        htmlDoc.DocumentNode.SelectSingleNode(
                                            "//div[@class='pagination__navigation']/a[" +
                                            (htmlDoc.DocumentNode.SelectNodes(
                                                "//div[@class='pagination__navigation']/a")
                                                .Count - 1) + "]").Attributes[
                                                    "data-page-number"]
                                            .Value);
                                SteamGiftsAddGiveaways(nodes, bot, giveaways);
                            }
                        }
                    }
                }

                if (giveaways == null)
                {
                    return
                        ConstructLog(
                            content + GetDateTime() + @"{SteamGifts} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                            @": " + 0, Color.White, true, true);
                }
                return
                    ConstructLog(
                        content + GetDateTime() + @"{SteamGifts} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                        @": " + giveaways.Count, Color.White, true, true);
            }
            catch (NullReferenceException)
            {
                return
                    ConstructLog(
                        "" + GetDateTime() + @"{SteamGifts} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                        @": " + giveaways?.Count, Color.White, true, true);
            }
        }

        public static async Task<Classes.Log> SteamGiftsLoadGiveawaysAsync(Classes.Bot bot,
            List<SteamGifts.SgGiveaway> giveaways)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamGiftsLoadGiveaways(bot, giveaways);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static string SteamGiftsLoadWishListGiveaways(Classes.Bot bot, List<SteamGifts.SgGiveaway> giveaways)
        {
            var nodesCount = 0;
            int pages;

            var response = Web.Get("http://www.steamgifts.com", "/giveaways/search?type=wishlist",
                new List<Parameter>(), Generate.Cookies_SteamGifts(bot.SteamGiftsPhpSessId), new List<HttpHeader>(),
                bot.UserAgent, bot.Proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);

            try
            {
                pages =
                    int.Parse(
                        htmlDoc.DocumentNode.SelectSingleNode("//div[@class='pagination__navigation']/a[" +
                                                              (htmlDoc.DocumentNode.SelectNodes(
                                                                  "//div[@class='pagination__navigation']/a")
                                                                  .Count - 1) + "]").Attributes["data-page-number"]
                            .Value);
            }
            catch (NullReferenceException)
            {
                pages = 1;
            }

            if (pages != 1)
            {
                for (var i = 1; i < pages + 1; i++)
                {
                    var nodes =
                htmlDoc.DocumentNode.SelectNodes(
                    "//div[@class='widget-container']//div[2]//div[3]//div[@class='giveaway__row-outer-wrap']//div[@class='giveaway__row-inner-wrap']");
                    pages =
                            int.Parse(
                                htmlDoc.DocumentNode.SelectSingleNode("//div[@class='pagination__navigation']/a[" +
                                                                      (htmlDoc.DocumentNode.SelectNodes(
                                                                          "//div[@class='pagination__navigation']/a")
                                                                          .Count - 1) + "]").Attributes[
                                                                              "data-page-number"]
                                    .Value);
                    nodesCount += nodes?.Count ?? 0;
                    SteamGiftsAddGiveaways(nodes, bot, giveaways);
                }
            }
            else
            {
                var nodes =
                htmlDoc.DocumentNode.SelectNodes(
                    "//div[@class='widget-container']//div[2]//div[3]//div[@class='giveaway__row-outer-wrap']//div[@class='giveaway__row-inner-wrap']");
                nodesCount += nodes?.Count ?? 0;
                SteamGiftsAddGiveaways(nodes, bot, giveaways);
            }

            return GetDateTime() + @"{SteamGifts} " + strings.ParseLoadGiveaways_FoundGiveAwaysInWishList + @": " +
                   nodesCount + "\n";
        }

        public static string SteamGiftsLoadGroupGiveaways(Classes.Bot bot, List<SteamGifts.SgGiveaway> giveaways)
        {
            var nodesCount = 0;
            int pages;

            var response = Web.Get("http://www.steamgifts.com", "/giveaways/search?type=group",
                new List<Parameter>(), Generate.Cookies_SteamGifts(bot.SteamGiftsPhpSessId), new List<HttpHeader>(),
                bot.UserAgent, bot.Proxy ?? "");
            if (response != null)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                try
                {
                    pages =
                        int.Parse(
                            htmlDoc.DocumentNode.SelectSingleNode("//div[@class='pagination__navigation']/a[" +
                                                                  (htmlDoc.DocumentNode.SelectNodes(
                                                                      "//div[@class='pagination__navigation']/a")
                                                                      .Count - 1) + "]").Attributes["data-page-number"]
                                .Value);
                }
                catch (NullReferenceException)
                {
                    pages = 1;
                }

                if (pages != 1)
                {
                    for (var i = 1; i < pages + 1; i++)
                    {
                        var nodes =
                            htmlDoc.DocumentNode.SelectNodes(
                                "//div[@class='widget-container']//div[2]//div[3]//div[@class='giveaway__row-outer-wrap']//div[@class='giveaway__row-inner-wrap']");
                        pages =
                            int.Parse(
                                htmlDoc.DocumentNode.SelectSingleNode("//div[@class='pagination__navigation']/a[" +
                                                                      (htmlDoc.DocumentNode.SelectNodes(
                                                                          "//div[@class='pagination__navigation']/a")
                                                                          .Count - 1) + "]").Attributes[
                                                                              "data-page-number"]
                                    .Value);
                        nodesCount += nodes?.Count ?? 0;
                        SteamGiftsAddGiveaways(nodes, bot, giveaways);
                    }
                }
                else
                {
                    var nodes =
                        htmlDoc.DocumentNode.SelectNodes(
                            "//div[@class='widget-container']//div[2]//div[3]//div[@class='giveaway__row-outer-wrap']//div[@class='giveaway__row-inner-wrap']");
                    SteamGiftsAddGiveaways(nodes, bot, giveaways);
                }
            }

            return GetDateTime() + @"{SteamGifts} " + strings.ParseLoadGiveaways_FoundGiveAwaysInGroup + @": " +
                   nodesCount + "\n";
        }

        private static void SteamGiftsAddGiveaways(HtmlNodeCollection nodes, Classes.Bot bot,
            List<SteamGifts.SgGiveaway> giveaways)
        {
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    try
                    {
                        var sgGiveaway = new SteamGifts.SgGiveaway
                        {
                            Name = node.SelectSingleNode(".//a[@class='giveaway__heading__name']").InnerText,
                            Link = node.SelectSingleNode(".//a[@class='giveaway__heading__name']").Attributes["href"].Value
                        };
                        sgGiveaway.Code = sgGiveaway.Link.Split('/')[2];

                        try
                        {
                            sgGiveaway.Price =
                                int.Parse(
                                    node.SelectSingleNode(".//span[@class='giveaway__heading__thin'][1]")
                                        .InnerText.Split('(')[1].Split('P')[0]);
                        }
                        catch (FormatException)
                        {
                            sgGiveaway.Price =
                                int.Parse(
                                    node.SelectSingleNode(".//span[@class='giveaway__heading__thin'][2]")
                                        .InnerText.Split('(')[1].Split('P')[0]);
                        }

                        try
                        {
                            sgGiveaway.Level = int.Parse(node.SelectSingleNode(".//div[@title='Contributor Level']")
                                .InnerText.Split(' ')[1].Replace("+", ""));
                        }
                        catch (NullReferenceException)
                        {}

                        if (sgGiveaway.Price <= bot.SteamGiftsPoint && sgGiveaway.Price <= bot.SteamGiftsJoinPointLimit && sgGiveaway.Level >= bot.SteamGiftsMinLevel)
                        {
                            giveaways?.Add(sgGiveaway);
                        }
                    }
                    catch (NullReferenceException)
                    {}
                }
            }
        }

        public static SteamGifts.SgGiveaway SteamGiftsGetJoinData(SteamGifts.SgGiveaway giveaway, string phpsessid,
            string useragent, string proxy)
        {
            var response = Web.Get("http://www.steamgifts.com", giveaway.Link,
                new List<Parameter>(), Generate.Cookies_SteamGifts(phpsessid), new List<HttpHeader>(), useragent,
                proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);

            try
            {
                giveaway.Code = htmlDoc.DocumentNode.SelectSingleNode("//input[@name='code']").Attributes["value"].Value;
                giveaway.Token =
                    htmlDoc.DocumentNode.SelectSingleNode("//input[@name='xsrf_token']").Attributes["value"].Value;
            }
            catch (NullReferenceException)
            {
            }
            return giveaway;
        }

        public static void SteamGiftsCompareGiveaways(List<SteamGifts.SgGiveaway> giveaways)
        {
            for (var i = 0; i < giveaways.Count; i++)
            {
                for (var j = 0; j < giveaways.Count; j++)
                {
                    if (giveaways[j].Link == giveaways[i].Link)
                    {
                        giveaways.Remove(giveaways[j]);
                    }
                }
            }
        }

        // SteamGifts //

        // SteamCompanion //
        public static Classes.Log SteamCompanionGetProfile(Classes.Bot bot, bool echo)
        {
            try
            {
                var response = Web.Get("https://steamcompanion.com", "/", new List<Parameter>(),
                    Generate.Cookies_SteamCompanion(bot.SteamCompanionPhpSessId, bot.SteamCompanionUserC,
                        bot.SteamCompanionUserId, bot.SteamCompanionUserT),
                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                if (response != null)
                {

                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(response.RestResponse.Content);

                    bot.SteamCompanionPoint =
                        int.Parse(htmlDoc.DocumentNode.SelectSingleNode("//span[@class='points']").InnerText);
                    bot.SteamCompanionProfileLink =
                        htmlDoc.DocumentNode.SelectSingleNode("//ul[@class='right']/li[1]/a[1]").Attributes["href"]
                            .Value;
                    return
                        ConstructLog(
                            GetDateTime() + @"{SteamCompanion} " + strings.ParseProfile_Points + @": " +
                            bot.SteamCompanionPoint, Color.White, true, echo);
                }
                return ConstructLog(GetDateTime() + @"{SteamCompanion} " + strings.ParseProfile_LoginOrServerError,
                    Color.Red, false, echo);
            }
            catch (Exception)
            {
                return ConstructLog(GetDateTime() + @"{SteamCompanion} " + strings.ParseProfile_LoginOrServerError,
                    Color.Red, false, echo);
            }
        }

        public static async Task<Classes.Log> SteamCompanionGetProfileAsync(Classes.Bot bot, bool echo)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamCompanionGetProfile(bot, echo);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamCompanionWonParse(Classes.Bot bot)
        {
            var response = Web.Get("https://steamcompanion.com/", "gifts/won", new List<Parameter>(),
                Generate.Cookies_SteamCompanion(bot.SteamCompanionPhpSessId, bot.SteamCompanionUserC,
                    bot.SteamCompanionUserId, bot.SteamCompanionUserT), new List<HttpHeader>(),
                bot.UserAgent, bot.Proxy ?? "");
            if (response != null)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//table[@id='created_giveaway']/tbody/tr");

                if (nodes != null)
                {
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var test = nodes[i].SelectSingleNode(".//input[@checked]");
                        if (test != null)
                        {
                            nodes.Remove(nodes[i]);
                            i--;
                        }
                    }

                    if (nodes.Count > 0)
                    {
                        return GiveawayHaveWon("SteamCompanion", nodes.Count);
                    }
                }
            }
            return null;
        }

        public static async Task<Classes.Log> SteamCompanionWonParseAsync(Classes.Bot bot)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamCompanionWonParse(bot);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamCompanionLoadGiveaways(Classes.Bot bot, List<SteamCompanion.ScGiveaway> giveaways)
        {
            var content = "";
            giveaways?.Clear();

            if (bot.SteamCompanionWishList)
            {
                content += SteamCompanionLoadWishListGiveaways(bot, giveaways);
            }

            if (bot.SteamCompanionGroup)
            {
                content += SteamCompanionLoadGroupGiveaways(bot, giveaways);
            }

            if (bot.SteamCompanionRegular)
            {
                var pages = 1;
                for (var i = 0; i < pages; i++)
                {
                    var response = Web.Get("https://steamcompanion.com",
                        i == 0 ? "/gifts/search/?type=public" : "/gifts/search.php?page=" + (i + 1) + "&type=public",
                        new List<Parameter>(),
                        Generate.Cookies_SteamCompanion(bot.SteamCompanionPhpSessId, bot.SteamCompanionUserC,
                            bot.SteamCompanionUserId, bot.SteamCompanionUserT), new List<HttpHeader>(), bot.UserAgent,
                        bot.Proxy ?? "");
                    if (response != null)
                    {

                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(response.RestResponse.Content);

                        try
                        {
                            try
                            {
                                pages =
                                    int.Parse(
                                        htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes[
                                            "href"
                                            ]
                                            .Value
                                            .Split('=')[1].Split('&')[0]);
                            }
                            catch (Exception)
                            {
                                pages =
                                    int.Parse(
                                        htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes[
                                            "href"
                                            ]
                                            .Value
                                            .Split('=')[2]);
                            }
                        }
                        catch (NullReferenceException)
                        {
                        }

                        var nodes = htmlDoc.DocumentNode.SelectNodes("//section[@class='col-2-3']/a");
                        if (nodes != null)
                        {
                            for (var j = 0; j < nodes.Count; j++)
                            {
                                try
                                {
                                    if (nodes[j].Attributes["style"].Value == "opacity: 0.5;")
                                    {
                                        nodes.Remove(nodes[j]);
                                        j--;
                                    }
                                }
                                catch (NullReferenceException)
                                {
                                }
                            }

                            if (nodes.Count > 0)
                            {
                                SteamCompanionAddGiveaways(nodes, bot, giveaways);
                            }
                        }
                    }
                }
            }

            if (giveaways == null)
            {
                return
                    ConstructLog(
                        content + GetDateTime() + @"{SteamCompanion} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                        @": " + 0, Color.White, true, true);
            }
            return
                ConstructLog(
                    content + GetDateTime() + @"{SteamCompanion} " + strings.ParseLoadGiveaways_FoundMatchGiveaways +
                    @": " + giveaways.Count, Color.White, true, true);
        }

        public static async Task<Classes.Log> SteamCompanionLoadGiveawaysAsync(Classes.Bot bot,
            List<SteamCompanion.ScGiveaway> giveaways)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamCompanionLoadGiveaways(bot, giveaways);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static string SteamCompanionLoadWishListGiveaways(Classes.Bot bot,
            List<SteamCompanion.ScGiveaway> giveaways)
        {
            var count = 0;
            var pages = 1;
            for (var i = 0; i < pages; i++)
            {
                var response = Web.Get("https://steamcompanion.com",
                    i == 0 ? "/gifts/search/?wishlist=true" : "/gifts/search/?wishlist=true&page=" + (i + 1),
                    new List<Parameter>(),
                    Generate.Cookies_SteamCompanion(bot.SteamCompanionPhpSessId, bot.SteamCompanionUserC,
                        bot.SteamCompanionUserId, bot.SteamCompanionUserT), new List<HttpHeader>(), bot.UserAgent,
                    bot.Proxy ?? "");
                if (response != null)
                {
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(response.RestResponse.Content);

                    try
                    {
                        pages =
                            int.Parse(
                                htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes["href"]
                                    .Value
                                    .Split('=')[1]);
                    }
                    catch (NullReferenceException)
                    {}
                    catch (FormatException)
                    {}

                    var nodes = htmlDoc.DocumentNode.SelectNodes("//section[@class='col-2-3']/a");
                    if (nodes != null)
                    {
                        for (var j = 0; j < nodes.Count; j++)
                        {
                            try
                            {
                                if (nodes[j].Attributes["style"].Value == "opacity: 0.5;")
                                {
                                    nodes.Remove(nodes[j]);
                                    j--;
                                }
                            }
                            catch (NullReferenceException)
                            {}
                        }
                        count += nodes.Count;
                        SteamCompanionAddGiveaways(nodes, bot, giveaways);
                    }
                }
            }
            return GetDateTime() + @"{SteamCompanion} " + strings.ParseLoadGiveaways_Found + ' ' + (giveaways.Count == 0 ? 0 : count) + ' ' +
                   strings.ParseLoadGiveaways_WishListGiveAwaysIn +                                                    
                   ' ' + pages + ' ' + strings.ParseLoadGiveaways_Pages + "\n";
        }

        public static string SteamCompanionLoadGroupGiveaways(Classes.Bot bot, List<SteamCompanion.ScGiveaway> giveaways)
        {
            var count = 0;
            var pages = 1;
            for (var i = 0; i < pages; i++)
            {
                var response = Web.Get("https://steamcompanion.com",
                    i == 0 ? "/gifts/search/?type=group" : "/gifts/search/?type=group&page=" + (i + 1),
                    new List<Parameter>(),
                    Generate.Cookies_SteamCompanion(bot.SteamCompanionPhpSessId, bot.SteamCompanionUserC,
                        bot.SteamCompanionUserId, bot.SteamCompanionUserT), new List<HttpHeader>(), bot.UserAgent,
                    bot.Proxy ?? "");
                if (response != null)
                {

                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(response.RestResponse.Content);

                    try
                    {
                        pages =
                            int.Parse(
                                htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes["href"]
                                    .Value
                                    .Split('=')[1].Split('&')[0]);
                    }
                    catch (NullReferenceException)
                    {}
                    catch (FormatException)
                    {
                        Debug.WriteLine(
                            htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes["href"].Value
                                .Split('=')[1].Split('&')[0]);
                        pages =
                            int.Parse(
                                htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes["href"]
                                    .Value
                                    .Split('=')[2]);
                        Debug.WriteLine(
                            htmlDoc.DocumentNode.SelectSingleNode("//li[@class='arrow']/a[1]").Attributes["href"].Value
                                .Split('=')[2]);
                    }

                    var nodes = htmlDoc.DocumentNode.SelectNodes("//section[@class='col-2-3']/a");
                    for (var j = 0; j < nodes.Count; j++)
                    {
                        try
                        {
                            if (nodes[j].Attributes["style"].Value == "opacity: 0.5;")
                            {
                                nodes.Remove(nodes[j]);
                                j--;
                            }
                        }
                        catch (NullReferenceException)
                        {}
                    }
                    count += nodes.Count;
                    SteamCompanionAddGiveaways(nodes, bot, giveaways);
                }
            }

            return GetDateTime() + @"{SteamCompanion} " + strings.ParseLoadGiveaways_Found + ' ' + count + ' ' +
                   strings.ParseLoadGiveaways_GroupGiveAwaysIn +
                   ' ' + pages + ' ' + strings.ParseLoadGiveaways_Pages + "\n";
        }

        private static void SteamCompanionAddGiveaways(HtmlNodeCollection nodes, Classes.Bot bot,
            List<SteamCompanion.ScGiveaway> giveaways)
        {
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    try
                    {
                        var scGiveaway = new SteamCompanion.ScGiveaway
                        {
                            Name = node.SelectNodes(".//p[@class='game-name']/span").Count > 1
                                ? node.SelectSingleNode(".//p[@class='game-name']/span[2]").InnerText
                                : node.SelectSingleNode(".//p[@class='game-name']/span[1]").InnerText,
                            Price =
                                int.Parse(node.SelectSingleNode(".//p[@class='game-name']").InnerText.Replace("p)", "")
                                    .Split('(')[
                                        node.SelectSingleNode(".//p[@class='game-name']")
                                            .InnerText.Replace("p)", "")
                                            .Split('(')
                                            .Length - 1]),
                            Link = node.Attributes["href"].Value
                        };

                        if (scGiveaway.Price <= bot.SteamCompanionPoint && scGiveaway.Price <= bot.SteamCompanionJoinPointLimit)
                        {
                            giveaways?.Add(scGiveaway);
                        }
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
            }
        }

        public static Classes.Log SteamCompanionGetJoinData(SteamCompanion.ScGiveaway giveaway, string phpsessid,
            string userid, string userc, string usert, string useragent, string botName, string proxy,
            string steamSessId, bool autoJoin, string steamLogin)
        {
            var response = Web.Get(giveaway.Link, "",
                new List<Parameter>(), Generate.Cookies_SteamCompanion(phpsessid, userc, userid, usert),
                new List<HttpHeader>(), useragent, proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);
            if (response.RestResponse.Content != "")
            {
                try
                {
                    giveaway.Code =
                        htmlDoc.DocumentNode.SelectSingleNode("//input[@name='giftID']").Attributes["value"].Value;
                    return ConstructLog("", Color.AliceBlue, true, false);
                }
                catch (NullReferenceException)
                {
                    try
                    {
                        var group =
                            htmlDoc.DocumentNode.SelectSingleNode(
                                "//a[@class='notification group-join regular-button qa']")
                                .Attributes["href"].Value;
                        if (autoJoin)
                        {
                            var trueGroupUrl = Web.Get(group, "", new List<Parameter>(),
                                Generate.Cookies_Steam(steamSessId, steamLogin),
                                new List<HttpHeader>(), useragent, proxy);
                            var result = Web.SteamJoinGroup(trueGroupUrl.RestResponse.ResponseUri.AbsoluteUri, "",
                                Generate.PostData_SteamGroupJoin(steamSessId),
                                Generate.Cookies_Steam(steamSessId, steamLogin), new List<HttpHeader>(), useragent,
                                proxy);
                            if (result)
                            {
                                return GroupJoined(trueGroupUrl.RestResponse.ResponseUri.AbsoluteUri);
                            }
                            return GroupNotJoinde(trueGroupUrl.RestResponse.ResponseUri.AbsoluteUri);
                        }
                        return
                            ConstructLog(
                                GetDateTime() + "{SteamCompanion} " + strings.GiveawayJoined_Join + ' ' + '"' +
                                giveaway.Name + '"' + ' ' + strings.GiveawayNotJoined_NotConfirmed +
                                @" {" + strings.GiveawayNotJoined_YouMustEnteredToGroup + ' ' +
                                htmlDoc.DocumentNode.SelectSingleNode(
                                    "//a[@class='notification group-join regular-button qa']")
                                    .Attributes["href"].Value + '}', Color.Yellow, false, true);
                    }
                    catch (NullReferenceException)
                    {
                        var exception =
                            htmlDoc.DocumentNode.SelectSingleNode("//a[@class='notification regular-button']").InnerText;
                        return GiveawayNotJoined("SteamCompanion", giveaway.Name, exception);
                    }
                }
            }
            return GiveawayNotJoined("SteamCompanion", giveaway.Name, "Content is empty");
        }

        public static void SteamCompanionCompareGiveaways(List<SteamCompanion.ScGiveaway> giveaways)
        {
            for (var i = 0; i < giveaways.Count; i++)
            {
                for (var j = 0; j < giveaways.Count; j++)
                {
                    if (giveaways[j].Link == giveaways[i].Link)
                    {
                        giveaways.Remove(giveaways[j]);
                    }
                }
            }
        }

        // SteamCompanion //

        // SteamPortal //
        public static Classes.Log SteamPortalGetProfile(Classes.Bot bot, bool echo)
        {
            try
            {
                var response = Web.Get("http://steamportal.net/", "", new List<Parameter>(),
                    Generate.Cookies_SteamPortal(bot.SteamPortalPhpSessId),
                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                bot.SteamPortalPoints =
                    int.Parse(htmlDoc.DocumentNode.SelectSingleNode("//span[@class='coin-icon my_coins']").InnerText);
                bot.SteamPortalProfileLink = "http://steamportal.net" +
                                             htmlDoc.DocumentNode.SelectSingleNode("//div[@class='my_profile']/a[1]")
                                                 .Attributes["href"].Value;

                return
                    ConstructLog(
                        GetDateTime() + @"{SteamPortal} " + strings.ParseProfile_Points + @": " + bot.SteamPortalPoints,
                        Color.White, true, echo);
            }
            catch (Exception)
            {
                return ConstructLog(GetDateTime() + @"{SteamPortal} " + strings.ParseProfile_LoginOrServerError,
                    Color.Red, false, echo);
            }
        }

        public static async Task<Classes.Log> SteamPortalGetProfileAsync(Classes.Bot bot, bool echo)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamPortalGetProfile(bot, echo);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamPortalWonParse(Classes.Bot bot)
        {
            var response = Web.Get("http://steamportal.net/", "profile/logs", new List<Parameter>(),
                Generate.Cookies_SteamPortal(bot.SteamPortalPhpSessId), new List<HttpHeader>(), bot.UserAgent,
                bot.Proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);

            var nodes = htmlDoc.DocumentNode.SelectNodes("//tr[@class='gray']");
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var content = nodes[i].SelectSingleNode("//tr/td[2]").InnerText;
                    if (!content.Contains("you've won the Giveaway"))
                    {
                        nodes.Remove(nodes[i]);
                        i--;
                    }
                }
                return GiveawayHaveWon("SteamPortal", nodes.Count);
            }
            return null;
        }

        public static async Task<Classes.Log> SteamPortalWonParsAsync(Classes.Bot bot)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamPortalWonParse(bot);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamPortalLoadGiveaways(Classes.Bot bot, List<SteamPortal.SpGiveaway> giveaways)
        {
            try
            {
                giveaways?.Clear();

                var pages = 1;

                for (var i = 0; i < pages; i++)
                {
                    if (pages != 1)
                    {
                        var headerList = new List<HttpHeader>();
                        var header = new HttpHeader
                        {
                            Name = "X-Requested-With",
                            Value = "XMLHttpRequest"
                        };
                        headerList.Add(header);

                        var jsonresponse = Web.Post("http://steamportal.net/", "page/ga_page",
                            Generate.PageData_SteamPortal(i + 1), headerList,
                            Generate.Cookies_SteamPortal(bot.SteamPortalPhpSessId), bot.UserAgent, "");
                        var data = jsonresponse.RestResponse.Content.Replace("\\", "");
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(data);
                        var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='giveaway_container']");
                        SteamPortalAddGiveaways(nodes, bot, giveaways);
                    }
                    else
                    {
                        var response = Web.Get("http://steamportal.net/", "",
                            new List<Parameter>(), Generate.Cookies_SteamPortal(bot.SteamPortalPhpSessId),
                            new List<HttpHeader>(),
                            bot.UserAgent, bot.Proxy ?? "");
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(response.RestResponse.Content);

                        var nodes =
                            htmlDoc.DocumentNode.SelectNodes("//div[@id='normal']/div[@class='giveaway_container']");
                        var count =
                            htmlDoc.DocumentNode.SelectNodes("//div[@class='nPagin']//div[@class='pagin']/span").Count;
                        pages = int.Parse(htmlDoc.DocumentNode.
                            SelectSingleNode("//div[@class='nPagin']//div[@class='pagin']/span[" + (count - 1) + "]")
                            .InnerText);
                        SteamPortalAddGiveaways(nodes, bot, giveaways);
                    }
                }

                if (giveaways == null)
                {
                    return
                        ConstructLog(
                            GetDateTime() + @"{SteamPortal} " + strings.ParseLoadGiveaways_FoundMatchGiveaways + @": " +
                            0,
                            Color.White, true, true);
                }
                return
                    ConstructLog(
                        GetDateTime() + @"{SteamPortal} " + strings.ParseLoadGiveaways_FoundMatchGiveaways + @": " +
                        giveaways.Count, Color.White, true, true);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static async Task<Classes.Log> SteamPortalLoadGiveawaysAsync(Classes.Bot bot,
            List<SteamPortal.SpGiveaway> giveaways)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamPortalLoadGiveaways(bot, giveaways);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        private static void SteamPortalAddGiveaways(HtmlNodeCollection nodes, Classes.Bot bot,
            List<SteamPortal.SpGiveaway> giveaways)
        {
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    try
                    {
                        var spGiveaway = new SteamPortal.SpGiveaway
                        {
                            Name = node.SelectSingleNode(".//div[@class='giveaway_name']").InnerText
                        };

                        try
                        {
                            spGiveaway.Price =
                                int.Parse(node.SelectSingleNode(".//span[@class='coin-white-icon']").InnerText);
                            spGiveaway.Code = node.SelectSingleNode(".//div[@class='ga_coin_join']").Attributes[
                                "onclick"].Value.Split(':')[1]
                                .Split(',')[0].Remove(
                                    node.SelectSingleNode(".//div[@class='ga_coin_join']").Attributes["onclick"].Value
                                        .Split(':')[1].Split(',')[0].Length - 1);
                            if (spGiveaway.Price <= bot.SteamPortalPoints && spGiveaway.Price <= bot.SteamPortalMaxJoinValue)
                            {
                                giveaways?.Add(spGiveaway);
                            }
                        }
                        catch (NullReferenceException)
                        {
                        }
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
            }
        }

        // SteamPortal //

        // GameAways //
        public static bool GameAwaysProfile(Classes.Bot bot, bool echo)
        {
            try
            {
                var response = Web.Get("http://www.gameaways.com/", "", new List<Parameter>(),
                    Generate.Cookies_GameAways(bot.GameAwaysPhpSessId),
                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                bot.GameAwaysPoints =
                    int.Parse(htmlDoc.DocumentNode.SelectSingleNode("//span[@class='ga-points']").InnerText);
                bot.GameAwaysProfileLink = "http://www.gameaways.com" +
                                           htmlDoc.DocumentNode.SelectSingleNode("//li[@class='user-menu-item'][2]/a[1]")
                                               .Attributes["href"].Value;
                if (echo)
                {
                    //Console.WriteLine(GetDateTime() + GetBotName(bot.Name) + @"{GameAways} " +
                    //                  strings.ParseProfile_Points + @": " + bot.GameAwaysPoints);
                }
                return true;
            }
            catch (Exception)
            {
                //Console.WriteLine(GetDateTime() + GetBotName(bot.Name) + @"{GameAways} " +
                //                    strings.ParseProfile_LoginOrServerError);
                return false;
            }
        }

        // GameAways //

        // SteamTrade //
        public static Classes.Log SteamTradeGetProfile(Classes.Bot bot, bool echo)
        {
            try
            {
                var response = Web.Get("http://steamtrade.info/", "", new List<Parameter>(),
                    Generate.Cookies_SteamTrade(bot.SteamTradePhpSessId, bot.SteamTradeDleUserId,
                        bot.SteamTradeDlePassword, bot.SteamTradePassHash),
                    new List<HttpHeader>(), bot.UserAgent, bot.Proxy ?? "");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                // ReSharper disable once UnusedVariable
                var test = htmlDoc.DocumentNode.SelectSingleNode("//a[@class='topm1']").Attributes["href"].Value;
                return ConstructLog(GetDateTime() + @"{SteamTrade} " + @"Login success", Color.White, true, echo);
            }
            catch (NullReferenceException)
            {
                return ConstructLog(GetDateTime() + @"{SteamTrade} " + strings.ParseProfile_LoginOrServerError,
                    Color.Red, false, echo);
            }
        }

        public static async Task<Classes.Log> SteamTradeGetProfileAsync(Classes.Bot bot, bool echo)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamTradeGetProfile(bot, echo);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        public static Classes.Log SteamTradeLoadGiveaways(Classes.Bot bot, List<SteamTrade.StGiveaway> giveaways)
        {
            try
            {
                giveaways?.Clear();

                var response = Web.Get("http://steamtrade.info/", "awards/", new List<Parameter>(),
                    Generate.Cookies_SteamTrade(bot.SteamTradePhpSessId, bot.SteamTradeDleUserId,
                        bot.SteamTradeDlePassword, bot.SteamTradePassHash),
                    new List<HttpHeader>(), bot.UserAgent, "");
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.RestResponse.Content);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//tbody[@bgcolor='#F3F5F7']/tr");
                SteamTradeAddGiveaways(nodes, giveaways);

                if (giveaways == null)
                {
                    return
                        ConstructLog(
                            GetDateTime() + @"{SteamTrade} " + strings.ParseLoadGiveaways_FoundMatchGiveaways + @": " +
                            0,
                            Color.White, true, true);
                }
                return
                    ConstructLog(
                        GetDateTime() + @"{SteamTrade} " + strings.ParseLoadGiveaways_FoundMatchGiveaways + @": " +
                        giveaways.Count, Color.White, true, true);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public static async Task<Classes.Log> SteamTradeLoadGiveawaysAsync(Classes.Bot bot,
            List<SteamTrade.StGiveaway> giveaways)
        {
            var task = new TaskCompletionSource<Classes.Log>();
            await Task.Run(() =>
            {
                try
                {
                    var result = SteamTradeLoadGiveaways(bot, giveaways);
                    task.SetResult(result);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            });

            return task.Task.Result;
        }

        private static void SteamTradeAddGiveaways(HtmlNodeCollection nodes, List<SteamTrade.StGiveaway> giveaways)
        {
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    try
                    {
                        if (node.SelectSingleNode(".//span[@class='status1']") == null)
                        {
                            var spGiveaway = new SteamTrade.StGiveaway
                            {
                                Name = node.SelectSingleNode(".//td[1]/a[2]").InnerText,
                                Link = node.SelectSingleNode(".//td[1]/a[2]").Attributes["href"].Value
                            };
                            giveaways?.Add(spGiveaway);
                        }
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
            }
        }

        public static SteamTrade.StGiveaway SteamTradeGetJoinData(SteamTrade.StGiveaway giveaway, string phpsessid,
            string useragent, string botName, string proxy, string dleUserId, string dlePassword, string passHash)
        {
            var response = Web.Get("http://steamtrade.info", giveaway.Link,
                new List<Parameter>(), Generate.Cookies_SteamTrade(phpsessid, dleUserId, dlePassword, passHash),
                new List<HttpHeader>(), useragent,
                proxy ?? "");
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.RestResponse.Content);
            try
            {
                giveaway.LinkJoin =
                    htmlDoc.DocumentNode.SelectSingleNode("//a[@class='inv_join']").Attributes["href"].Value.Trim();
            }
            catch (NullReferenceException)
            {
            }

            return giveaway;
        }

        // SteamTrade //
    }
}