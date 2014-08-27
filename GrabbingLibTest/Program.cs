using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GrabbingLib;

namespace GrabbingLibTest
{
    class Program
    {
        static void Main(string[] args)
        {
            String currenturl = "http://www.escapistmagazine.com/videos/view/zero-punctuation/latest";
            //String jsonurl = "http://www.escapistmagazine.com/videos/config/4181-30ea42621faa094f03f52855b6284b40.js";
            //String jsonurl = "whereever";
            //String jsonurl = "http://www.escapistmagazine.com/videos/config/9603-3bbe3f90935893326341efef90188d42.js";
            //String pageurl = "http://www.escapistmagazine.com/videos/view/zero-punctuation/4181-Driver-San-Francisco";
            /*Task<String> task = Grabber.getJSONURL(pageurl);
            task.GetAwaiter().OnCompleted(() =>
            { 
                Console.WriteLine("done"); 
            });*/
            Task<ParsingResult> wstask = Grabber.getJSONURL(currenturl);
            Console.WriteLine("Webseite: Request gestartet");
            wstask.Wait();
            Console.WriteLine("Webseite geladen");
            if (wstask.Result.error == null)
            {
                Console.WriteLine(wstask.Result.title);
                Console.WriteLine(wstask.Result.URL);
                Task<ParsingResult> jstask = Grabber.getVideoURL(wstask.Result.URL);
                jstask.Wait();
                Console.WriteLine("JSON geladen");
                ParsingResult result = jstask.Result;
                if (result.error == null)
                {
                    Console.WriteLine(result.title);
                    Console.WriteLine(result.URL);
                }
                else
                    Console.WriteLine(result.error);
            }
            else
                Console.WriteLine(wstask.Result.error);
             
            /*Task task = Grabber.evaluateURL(Grabber.ZPLatestURL, printerror, ()=>{
                Console.WriteLine("Webseite ausgewertet");
            },()=>{
                Console.WriteLine("Videodaten ausgewertet");
            },getfilepath, (DownloadOperation dlop) =>{
            });*/
            Console.WriteLine("Ende. Taste drücken zum schließen");
            Console.ReadKey();
        }

        private static async Task printerror(Exception e)
        {
            Console.WriteLine(e);
        }

        private static void ClearLine()
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write(new String(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 1);
        }
    }
}
