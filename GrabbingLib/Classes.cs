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
    public class Grabber
    {
        public static readonly String ZPLatestURL =
            "http://www.escapistmagazine.com/videos/view/zero-punctuation/latest";

        public static readonly String EscapistDir = "Escapist";
        private static Downloader download;

        //There are no instances of Grabber
        private Grabber()
        {
        }

        public static void finishDL()
        {
            if (download != null)
            {
                download.finishdl();
                download = null;
            }
        }

        public static async Task waitForNewZPEpisode(CancellationToken ctoken, Func<String, Task<bool>> confirmold,
            Action<int> updateattempt, Action foundaction, Action htmlaction,
            Action jsonaction, Func<String, Task<String>> getFilePath, Downloader downloader, Func<String, Task> showmsg,
            Action canceltask, Func<Exception, Task> erroraction)
        {
            String url = ZPLatestURL;
            String oldname = (await getJSONURL(url, false)).title;
            if (!ctoken.IsCancellationRequested && await confirmold.Invoke(oldname))
            {
                int maximum = 20;
                int attempt;
                for (attempt = 1;
                    (await getJSONURL(url, false)).title.Equals(oldname) && !ctoken.IsCancellationRequested &&
                    attempt < maximum;
                    attempt++)
                {
                    updateattempt.Invoke(attempt);
                    await Task.Delay(1000, ctoken);
                }
                if (attempt == maximum)
                    erroraction.Invoke(new OverflowException()); //TODO overflowexception is for arithmetic, what do we use?

                if (!ctoken.IsCancellationRequested)
                {
                    foundaction.Invoke();
                    await
                        evaluateURL(ZPLatestURL, false, erroraction, htmlaction, jsonaction, getFilePath, downloader,
                            showmsg, canceltask, ctoken);
                }
                else
                    canceltask.Invoke();
            }
            else
                canceltask.Invoke();
        }

        public static async Task<String> getLatestZPTitle()
        {
            ParsingResult parsingresult = await getJSONURL(ZPLatestURL, false);
            return parsingresult.error == null ? parsingresult.title : parsingresult.error.ToString();
        }

        //showmsg is for debugging
        public static async Task evaluateURL(String videopage, bool hq, Func<Exception, Task> erroraction,
            Action htmlaction,
            Action jsonaction, Func<String, Task<String>> getFilePath, Downloader downloader, Func<String, Task> showmsg,
            Action canceltask, CancellationToken ctoken)
        {
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            ParsingResult htmlresult = await getJSONURL(videopage, hq);
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            if (htmlresult.error != null)
            {
                await erroraction.Invoke(htmlresult.error);
                return;
            }
            htmlaction.Invoke();
            ParsingResult jsonresult = await getVideoURL(htmlresult.URL);
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            if (jsonresult.error != null)
            {
                await erroraction.Invoke(jsonresult.error);
                return;
            }
            jsonaction.Invoke();
            //String title = htmlresult.title ?? jsonresult.title;
            String title = String.Join(" ", jsonresult.title.Split(Path.GetInvalidFileNameChars()));
            title = Regex.Replace(title, @"\s+", " ").Trim();
            //Title may contain : and " characters and needs to be beautified
            String path = await getFilePath.Invoke(title);
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            if (path != null)
            {
                download = downloader;
                download.startdownload(jsonresult.URL, path);
            }
            else
                canceltask.Invoke();
        }

        public static async Task<ParsingResult> getJSONURL(String videopage, bool hq)
        {
            var result = new ParsingResult();
            try
            {
                WebRequest request = WebRequest.CreateHttp(videopage);
                request.Headers[HttpRequestHeader.IfModifiedSince] = DateTime.UtcNow.ToString();
                request.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = await request.GetResponseAsync();

                var htmldoc = new HtmlDocument();
                htmldoc.Load(response.GetResponseStream());
                HtmlNode head = htmldoc.DocumentNode.ChildNodes.FindFirst("head");
                if (head != null)
                {
                    result.title =
                        head.ChildNodes.Where(node => node.Name.Equals("meta"))
                            .FirstOrDefault(
                                node => node.Attributes["name"] != null && node.Attributes["name"].Value.Equals("title"))
                            .Attributes["content"].Value;
                    String hrefval =
                        head.ChildNodes.Where(node => node.Name.Equals("link"))
                            .FirstOrDefault(node => node.Attributes["rel"].Value.Equals("video_src"))
                            .Attributes
                            ["href"].Value;
                    result.URL =
                        (WebUtility.UrlDecode(hrefval.Split('=')[1]).Split('?'))[0];
                    if (hq)
                        result.URL += "?hq=1";
                }
            }
            catch (Exception e)
            {
                result.error = e;
            }
            return result;
        }

        public static async Task<ParsingResult> getVideoURL(String jsonurl)
        {
            var result = new ParsingResult();
            try
            {
                if (jsonurl == null)
                    throw new Exception("Video URL not found");
                WebRequest request = WebRequest.CreateHttp(jsonurl);
                request.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = await request.GetResponseAsync();
                String jsontext = new StreamReader(response.GetResponseStream()).ReadToEnd();

                JObject obj = JObject.Parse(jsontext);
                //Uses Newtonsoft.Json.Linq from the Json.Net package because Windows.Data.Json is only available on Windows (Phone) 8 and above
                result.title = ScrubHtml(WebUtility.HtmlDecode((String) obj["plugins"]["viral"]["share"]["description"]));
                result.URL =
                    (String)
                        (obj["playlist"].Cast<JObject>().Where(elem => ((String) elem["eventCategory"]).Equals("Video")))
                            .FirstOrDefault()["url"];

                /*jsontext = jsontext.Replace('\'', '"'); //Windows.Data.Json is rather strict about this
                JsonObject obj = JsonObject.Parse(jsontext);
                String desc = obj["plugins"].GetObject()["viral"].GetObject()["share"].GetObject()["description"].GetString();
                result.title = ScrubHtml(WebUtility.HtmlDecode(desc));
                foreach (JsonValue elem in obj["playlist"].GetArray())
                    if (elem.GetObject()["eventCategory"].GetString().Equals("Video"))
                    {
                        result.URL = elem.GetObject()["url"].GetString();
                        return result;
                    }*/
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
    }

    public abstract class Downloader
    {
        protected Action<String, bool> finishhandler;
        protected Action<ulong, ulong> updatehandler;

        protected Downloader(Action<ulong, ulong> updatehandler, Action<String, bool> finishhandler)
        {
            this.updatehandler = updatehandler;
            this.finishhandler = finishhandler;
        }

        public abstract void finishdl();
        public abstract void startdownload(String sourceuri, String targeturi);
    }
}