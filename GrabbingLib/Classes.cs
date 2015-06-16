using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace GrabbingLib
{
    public static class Grabber
    {
        public static readonly String ZPLatestURL =
            "http://www.escapistmagazine.com/videos/view/zero-punctuation/latest";

        public static readonly String EscapistDir = "Escapist";
        private static Downloader download;

        //There are no instances of Grabber

        public static void finishDL()
        {
            if (download != null)
            {
                download.finishdl();
                download = null;
            }
        }

        public static async Task waitForNewZPEpisode(CancellationToken ctoken, Func<String, Task<bool>> confirmold,
            Func<Task> timeoutaction, Action<int> updateattempt, Action foundaction, Action htmlaction,
            Action jsonaction, Func<String, ParsingRequest.CONTAINER, Task<String>> getFilePath, Downloader downloader, Func<String, Task> showmsg,
            Action canceltask, Func<Exception, Task> erroraction)
        {
            String url = ZPLatestURL;
            String oldname = (await getJSONURL(url)).title;
            if (!ctoken.IsCancellationRequested && await confirmold.Invoke(oldname))
            {
                int maximum = 500;
                int attempt;
                ParsingResult latestresult;
                for (attempt = 1;
                    (latestresult = await getJSONURL(url)).title.Equals(oldname) &&
                    !ctoken.IsCancellationRequested &&
                    attempt < maximum;
                    attempt++)
                {
                    updateattempt.Invoke(attempt);
                    try
                    {
                        await Task.Delay(5000, ctoken);
                    }
                    catch (TaskCanceledException)
                    {
                        //Already handled a few lines later
                    }
                }
                if (attempt == maximum)
                    await timeoutaction.Invoke();
                else if (!ctoken.IsCancellationRequested)
                {
                    foundaction.Invoke();
                    await
                        downloadHelper(erroraction, htmlaction, jsonaction, getFilePath, downloader, showmsg, canceltask,
                            ctoken, latestresult, null);
                }
                else
                    canceltask.Invoke();
            }
            else
                canceltask.Invoke();
        }

        public static async Task<String> getLatestZPTitle()
        {
            ParsingResult parsingresult = await getJSONURL(ZPLatestURL);
            return parsingresult.error == null ? parsingresult.title : parsingresult.error.ToString();
        }

        //showmsg is for debugging
        public static async Task evaluateURL(ParsingRequest request, Func<Exception, Task> erroraction,
            Action htmlaction, Action jsonaction, Func<String, ParsingRequest.CONTAINER, Task<String>> getFilePath, Downloader downloader,
            Func<String, Task> showmsg, Action canceltask, CancellationToken ctoken)
        {
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            ParsingResult htmlresult = await getJSONURL(request.URL);
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            await
                downloadHelper(erroraction, htmlaction, jsonaction, getFilePath, downloader, showmsg, canceltask, ctoken,
                    htmlresult, request);
        }

        private static async Task downloadHelper(Func<Exception, Task> erroraction, Action htmlaction, Action jsonaction,
            Func<string, ParsingRequest.CONTAINER, Task<string>> getFilePath,
            Downloader downloader, Func<String, Task> showmsg, Action canceltask, CancellationToken ctoken,
            ParsingResult htmlresult, ParsingRequest request)
        {
            if (htmlresult.error != null)
            {
                await erroraction.Invoke(htmlresult.error);
                return;
            }
            htmlaction.Invoke();
            ParsingResult jsonResult = await getVideoURL(request, htmlresult);
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            String title = String.Join(" ", htmlresult.title.Split(Path.GetInvalidFileNameChars()));
            title = Regex.Replace(title, @"\s+", " ").Trim();
            //Title may contain : and " characters and needs to be beautified
            String path = await getFilePath.Invoke(title, request.Container);
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            if (path != null)
            {
                download = downloader;
                download.startdownload(jsonResult.URL, path);
            }
            else
                canceltask.Invoke();
        }

        private static async Task<ParsingResult> getJSONURL(String videopage)
        {
            var result = new ParsingResult();
            try
            {
                WebRequest request = WebRequest.CreateHttp(videopage);
                if (videopage.Equals(ZPLatestURL)) //If we are waiting for a specific episode, then we can allow caching
                    request.Headers[HttpRequestHeader.CacheControl] = "no-cache"; //or use the default mode
                request.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = await request.GetResponseAsync();

                var htmldoc = new HtmlDocument();
                htmldoc.Load(response.GetResponseStream());
                HtmlNode head = htmldoc.DocumentNode.ChildNodes.FindFirst("head");
                HtmlNode body = htmldoc.DocumentNode.ChildNodes.FindFirst("body");
                if (head != null)
                {
                    result.title =
                        head.ChildNodes.Where(node => node.Name.Equals("meta"))
                            .FirstOrDefault(
                                node => node.Attributes["name"] != null && node.Attributes["name"].Value.Equals("title"))
                            .Attributes["content"].Value;
                }
                if (body != null)
                {
                    var playernode = body.Descendants()
                      .Where(node => node.Name.Equals("div"))
                      .Where(node => node.Attributes.Contains("id"))
                      .FirstOrDefault(node => node.Attributes["id"].Value.Equals("video_player_object"));
                    if (playernode != null)
                    {
                        var codenode =
                            playernode.Descendants().FirstOrDefault(node => node.InnerText.Contains("imsVideo.play"));
                        if (codenode != null)
                        {
                            String playerconfig =
                                codenode.InnerText.Split(new[] { "imsVideo.play(" }, StringSplitOptions.None)[1];
                            playerconfig = playerconfig.Substring(0, playerconfig.Length - 2);
                            JObject jobj = JObject.Parse(playerconfig);
                            String videoID = (String) jobj["videoID"];
                            result.hash = (String) jobj["hash"];
                            result.URL =
                                "http://www.escapistmagazine.com/videos/vidconfig.php" + "?" + "videoID=" + videoID +
                                "&" + "hash=" + result.hash;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result.error = e;
            }
            return result;
        }

        private static String decodeJSONConfig(String hash, String jsontext)
        {
            var t = "";
            var a = "";
            var r = "";
            while (t.Length < jsontext.Length / 2)
                t += hash;
            t = t.Substring(0, jsontext.Length / 2);
            for (var o = 0; o < jsontext.Length; o += 2)
                a += (char) Convert.ToInt32("" + jsontext[o] + jsontext[o + 1], 16);
            for (var o = 0; o < t.Length; o++)
                r += (char) (t[o] ^ a[o]);
            return r;
        }

        private static async Task<ParsingResult> getVideoURL(ParsingRequest parsingRequest, ParsingResult htmlResult)
        {
            var result = new ParsingResult();
            try
            {
                if (htmlResult.URL == null)
                    throw new Exception("Video URL not found");
                WebRequest request = WebRequest.CreateHttp(htmlResult.URL);
                request.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = await request.GetResponseAsync();
                String jsontext = new StreamReader(response.GetResponseStream()).ReadToEnd();

                jsontext = decodeJSONConfig(htmlResult.hash, jsontext);
                JObject obj = JObject.Parse(jsontext);
                result.title = ScrubHtml(WebUtility.HtmlDecode((String) obj["videoData"]["title"]));
                String cont = parsingRequest.Container == ParsingRequest.CONTAINER.C_MP4 ? "video/mp4" : "video/webm";
                String res = parsingRequest.Resolution == ParsingRequest.RESOLUTION.R_360P ? "360" : "480";
                result.URL =
                    (String)
                        obj["files"]["videos"]
                            .Cast<JObject>()
                            .FirstOrDefault(elem => elem["type"] != null && (String) elem["type"] == cont && elem["res"] != null && (String) elem["res"] == res)["src"];
            }
            catch (Exception e)
            {
                result.error = e;
            }
            return result;
        }

        private static string ScrubHtml(string value)
        //Borrowed from Stackoverflow http://stackoverflow.com/questions/19523913/remove-html-tags-from-string-including-nbsp-in-c-sharp
        {
            String step1 = Regex.Replace(value, @"<[^>]+>|&nbsp;", "").Trim();
            String step2 = Regex.Replace(step1, @"\s{2,}", " ");
            return step2;
        }

        public static string ByteSize(ulong size)
        //Borrowed from Stackoverflow http://stackoverflow.com/questions/281640/how-do-i-get-a-human-readable-file-size-in-bytes-abbreviation-using-net
        {
            string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            string formatTemplate = "{0}{1:0.#} {2}";

            if (size == 0)
                return string.Format(formatTemplate, null, 0, sizeSuffixes[0]);

            double absSize = Math.Abs((double) size);
            double fpPower = Math.Log(absSize, 1000);
            var intPower = (int) fpPower;
            int iUnit = intPower >= sizeSuffixes.Length ? sizeSuffixes.Length - 1 : intPower;
            double normSize = absSize / Math.Pow(1000, iUnit);

            return string.Format(formatTemplate, size < 0 ? "-" : null, normSize, sizeSuffixes[iUnit]);
        }
    }

    public class ParsingResult
    {
        public Exception error { get; set; }
        public String URL { get; set; }
        public String title { get; set; }
        public String hash { get; set; }
    }

    public class ParsingRequest
    {
        public enum RESOLUTION
        {
            R_360P,
            R_480P
        }

        public enum CONTAINER
        {
            C_WEBM,
            C_MP4
        }

        public readonly String URL;
        public readonly RESOLUTION Resolution;
        public readonly CONTAINER Container;

        public ParsingRequest(string url, RESOLUTION resolution, CONTAINER container)
        {
            URL = url;
            Resolution = resolution;
            Container = container;
        }
    }

    public abstract class Downloader
    {
        protected readonly Action<String, bool> finishhandler;
        protected readonly Action<ulong, ulong> updatehandler;

        protected Downloader(Action<ulong, ulong> updatehandler, Action<String, bool> finishhandler)
        {
            this.updatehandler = updatehandler;
            this.finishhandler = finishhandler;
        }

        public abstract void finishdl();
        public abstract void startdownload(String sourceuri, String targeturi);
    }
}