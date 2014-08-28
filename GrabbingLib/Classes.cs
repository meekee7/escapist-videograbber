using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

using HtmlAgilityPack;
//using Windows.Networking.BackgroundTransfer;
//using Windows.Data.Json;
using System.Text.RegularExpressions;
//using Windows.Storage;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace GrabbingLib
{
    public class Grabber
    {
        public static readonly String ZPLatestURL = "http://www.escapistmagazine.com/videos/view/zero-punctuation/latest";
        private static Downloader download = null;

        //There are no instances of Grabber
        private Grabber()
        { }

        public static void finishDL()
        {
            if (download != null)
            {
                download.finishdl();
                download = null;
            }
            /*{
                if (download.Progress.Status != BackgroundTransferStatus.Completed || download.Progress.Status != BackgroundTransferStatus.Error || download.Progress.Status != BackgroundTransferStatus.Canceled)
                    download.AttachAsync().Cancel();
                download = null;
            }*/
        }

        public static async Task evaluateURL(String videopage, Func<Exception, Task> erroraction, Action htmlaction,
            Action jsonaction, Func<String, Task<String>> getFilePath, Downloader downloader, Func<String, Task> showmsg, Action canceltask, CancellationToken ctoken)
        {
            if (ctoken.IsCancellationRequested)
            {
                canceltask.Invoke();
                return;
            }
            ParsingResult htmlresult = await getJSONURL(videopage);
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
            String title = htmlresult.title != null ? htmlresult.title : jsonresult.title;
            title = String.Join(" ", jsonresult.title.Split(Path.GetInvalidFileNameChars()));
            title = Regex.Replace(title, @"\s+", " ").Trim(); //Title may contain : and " characters and needs to be beautified
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
                /*{
                    StorageFile file = await StorageFile.GetFileFromPathAsync(path);
                    download = new BackgroundDownloader().CreateDownload(new Uri(jsonresult.URL), file);
                    await download.StartAsync().AsTask(new Progress<DownloadOperation>((DownloadOperation dlop) =>
                    {
                        updatehandler.Invoke(dlop.Progress.BytesReceived, dlop.Progress.TotalBytesToReceive, dlop.ResultFile.Path);
                    }));
                }*/
            }
            else
                canceltask.Invoke();
        }

        public static async Task<ParsingResult> getJSONURL(String videopage)
        {
            ParsingResult result = new ParsingResult();
            try
            {
                WebRequest request = HttpWebRequest.CreateHttp(videopage);
                request.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = await request.GetResponseAsync();

                //String webpagehtml = new StreamReader(response.GetResponseStream()).ReadToEnd();
                HtmlDocument htmldoc = new HtmlDocument();
                htmldoc.Load(response.GetResponseStream());
                HtmlNode head = htmldoc.DocumentNode.ChildNodes.FindFirst("head");
                if (head != null)
                    foreach (HtmlNode node in head.ChildNodes)
                        if (node.Name.Equals("link"))
                        {
                            foreach (HtmlAttribute relattr in node.Attributes)
                                if (relattr.Name.Equals("rel") && relattr.Value.Equals("video_src"))
                                    foreach (HtmlAttribute hrefattr in node.Attributes)
                                        if (hrefattr.Name.Equals("href"))
                                        {
                                            result.URL = (WebUtility.UrlDecode(hrefattr.Value.Split('=')[1]).Split('?'))[0];
                                            if (result.title != null)
                                                return result;
                                        }
                        }
                        else if (node.Name.Equals("meta"))
                        {
                            foreach (HtmlAttribute nameattr in node.Attributes)
                                if (nameattr.Name.Equals("name") && nameattr.Value.Equals("title"))
                                    foreach (HtmlAttribute conattr in node.Attributes)
                                        if (conattr.Name.Equals("content"))
                                        {
                                            result.title = conattr.Value;
                                            if (result.URL != null)
                                                return result;
                                        }
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
            ParsingResult result = new ParsingResult();
            try
            {
                if (jsonurl == null)
                    throw new Exception("Video URL not found");
                WebRequest request = HttpWebRequest.CreateHttp(jsonurl);
                request.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = await request.GetResponseAsync();
                String jsontext = new StreamReader(response.GetResponseStream()).ReadToEnd();

                JObject obj = JObject.Parse(jsontext); //Uses Newtonsoft.Json.Linq from the Json.Net package
                result.title = ScrubHtml(WebUtility.HtmlDecode((String) obj["plugins"]["viral"]["share"]["description"]));
                foreach (JObject elem in (JArray) obj["playlist"])
                    if (((String) elem["eventCategory"]).Equals("Video"))
                    {
                        result.URL = (String) elem["url"];
                        return result;
                    }

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

        private static string ScrubHtml(string value) //Borrowed from Stackoverflow http://stackoverflow.com/questions/19523913/remove-html-tags-from-string-including-nbsp-in-c-sharp
        {
            String step1 = Regex.Replace(value, @"<[^>]+>|&nbsp;", "").Trim();
            String step2 = Regex.Replace(step1, @"\s{2,}", " ");
            return step2;
        }

        public static string ByteSize(ulong size) //Borrowed from Stackoverflow http://stackoverflow.com/questions/281640/how-do-i-get-a-human-readable-file-size-in-bytes-abbreviation-using-net
        {
            string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            string formatTemplate = "{0}{1:0.#} {2}";

            if (size == 0)
                return string.Format(formatTemplate, null, 0, sizeSuffixes[0]);

            double absSize = Math.Abs((double) size);
            double fpPower = Math.Log(absSize, 1000);
            int intPower = (int) fpPower;
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
        protected Action<ulong, ulong> updatehandler;
        protected Action<String, bool> finishhandler;
        public Downloader(Action<ulong, ulong> updatehandler, Action<String, bool> finishhandler)
        {
            this.updatehandler = updatehandler;
            this.finishhandler = finishhandler;
        }
        public abstract void finishdl();
        public abstract void startdownload(String sourceuri, String targeturi);
    }
}
