using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Collective_App_Downloader
{
    internal class Program
    {
        static Dictionary<string, int> progressDict = new Dictionary<string, int>();
        static bool[] Errored = new bool[2147483647];
        static object consoleLock = new object();
        static string tempPath = Path.GetTempPath();

        [STAThread]

        public static void Main(string[] args)
        {
            SelectMode();
        }

        public static async void SelectMode()
        {
            while (true)
            {
                Console.Clear();

                Console.Title = "Collective App Downloader";
                FormatList();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("*-------------------------*");
                Console.WriteLine("|Collective App Downloader|");
                Console.WriteLine("*-------------------------*");
                Console.ResetColor();

                Console.WriteLine("  ─  Copyright (c) 2024 Syobosyobonn/Zisty All rights reserved.");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Warning! This app may be unstable. Do not use it in important situations such as school or work.");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine();

                Console.WriteLine("Plese select mode.");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("1.Download from list");
                Console.WriteLine("2.Edit List");
                Console.ResetColor();

                Console.WriteLine();
                Console.Write(">");

                string mode = Console.ReadLine();

                if (mode == "1")
                {
                    Task.WaitAll(Download());
                }
                else if (mode == "2")
                {
                    EditList();
                }
            }
        }

        public static async Task Download()
        {
            Console.Title = "Collective App Downloader (Download)";

            Console.Clear();

            if (Properties.Settings.Default.URLs != "")
            {
                string urlsString = Properties.Settings.Default.URLs;
                List<string> urls = urlsString.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                List<Task> downloadTasks = new List<Task>();

                foreach (string url in urls)
                {
                    progressDict[url] = 0;
                    downloadTasks.Add(DownloadFileAsync(url));
                }

                Task displayTask = DisplayProgressAsync(urls);

                await Task.WhenAll(downloadTasks);
                await displayTask;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done");
                Console.ResetColor();
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Nothing on the list.");
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Plese enter any key.");
            Console.ResetColor();
            Console.ReadKey();
        }

        static async Task DownloadFileAsync(string url)
        {
            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            string fullPath = Path.Combine(tempPath, fileName);

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch { }
            }

            using (var client = new HttpClient())
            {
                try
                {
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;

                        string contentType = response.Content.Headers.ContentType.MediaType;
                        string newFileName = fileName;

                        if (contentType == "application/x-msdownload" || contentType == "application/octet-stream")
                        {
                            if (fileName.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                            {
                                newFileName = Path.ChangeExtension(fileName, ".exe");
                            }
                        }
                        else if (contentType == "application/x-msi")
                        {
                            if (fileName.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                            {
                                newFileName = Path.ChangeExtension(fileName, ".msi");
                            }
                        }

                        string newFullPath = Path.Combine(tempPath, newFileName);

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(newFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var totalRead = 0L;
                            var buffer = new byte[8192];
                            var isMoreToRead = true;

                            do
                            {
                                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                                if (read == 0)
                                {
                                    isMoreToRead = false;
                                }
                                else
                                {
                                    await fileStream.WriteAsync(buffer, 0, read);

                                    totalRead += read;
                                    if (totalBytes.HasValue)
                                    {
                                        var percentage = (int)((float)totalRead / (float)totalBytes * 100);
                                        progressDict[url] = percentage;
                                    }
                                }
                            }
                            while (isMoreToRead);
                        }

                        progressDict[url] = 100;

                        if (File.Exists(newFullPath))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(newFullPath) { UseShellExecute = true });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    progressDict[url] = -1;
                }
            }
        }

        static async Task DisplayProgressAsync(List<string> urls)
        {
            while (progressDict.Values.Any(v => v < 100 && v != -1))
            {
                lock (consoleLock)
                {
                    Console.Clear();
                    for (int i = 0; i < urls.Count; i++)
                    {
                        var url = urls[i];
                        int progress = progressDict[url];

                        string progressBar = progress >= 0 ? new string('-', progress / 2) : "Error";
                        string percentageDisplay = progress >= 0 ? $"{progress,3}%" : "ERR%";

                        if (progressBar == "Error" || percentageDisplay == "ERR%")
                        {
                            Errored[i] = true;

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"{url}");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\n{percentageDisplay}[{progressBar.PadRight(50)}]");
                            Console.WriteLine();
                            Console.ResetColor();
                        }
                        else
                        {
                            Errored[i] = false;

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"{url}");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\n{percentageDisplay}[{progressBar.PadRight(50)}]");
                            Console.WriteLine();
                            Console.ResetColor();
                        }
                    }
                }
                await Task.Delay(250);
            }

            Console.Clear();

            for (int i = 0; i < urls.Count; i++)
            {
                if (Errored[i])
                {
                    var url = urls[i];
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{url}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nERR%[Error                                             ]");
                    Console.WriteLine();
                    Console.ResetColor();
                }
                else
                {
                    var url = urls[i];
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{url}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n100%[--------------------------------------------------]");
                    Console.WriteLine();
                    Console.ResetColor();
                }
            }
        }

        public static void EditList()
        {
            while (true)
            {
                Console.Clear();
                Console.Title = "Collective App Downloader (EditList - ModeSelect)";

                Console.WriteLine("Plese select mode.");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("1. > | List");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("2. + | Add");

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("3. - | Delete");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("4. ! | Reset");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("5. e | List export");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("6. i | List inport");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("q. x | Quit");

                Console.ResetColor();
                Console.WriteLine();
                Console.Write(">");
                string EditMode = Console.ReadLine();

                Console.Clear();

                if (EditMode == "1" || EditMode == ">")
                {
                    Console.Title = "Collective App Downloader (EditList - List)";

                    FormatList();

                    Console.WriteLine("List of URLs");
                    Console.WriteLine("----------------------------------------------------------");
                    Console.WriteLine(Properties.Settings.Default.URLs);
                    Console.WriteLine("----------------------------------------------------------");

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Plese enter any key.");
                    Console.ReadKey();
                }
                else if (EditMode == "2" || EditMode == "+")
                {
                    Console.Title = "Collective App Downloader (EditList - Add)";

                    FormatList();

                    Console.WriteLine("List of URLs");
                    Console.WriteLine("----------------------------------------------------------");
                    Console.WriteLine(Properties.Settings.Default.URLs);
                    Console.WriteLine("----------------------------------------------------------");
                    Console.WriteLine();

                    Console.WriteLine("Plese input download url");
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("(ex : https://example.com/download/installer-x64.exe )");
                    Console.ResetColor();
                    Console.WriteLine();

                    Console.Write(">");
                    string Input = Console.ReadLine();

                    Properties.Settings.Default.URLs = Input + "\n" + Properties.Settings.Default.URLs;

                    FormatList();

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done");
                    Console.ResetColor();
                    Console.WriteLine();

                    Console.WriteLine("List of URLs");
                    Console.WriteLine("----------------------------------------------------------");
                    Console.WriteLine(Properties.Settings.Default.URLs);
                    Console.WriteLine("----------------------------------------------------------");

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Plese enter any key.");
                    Console.ReadKey();
                }
                else if (EditMode == "3" || EditMode == "-")
                {
                    Console.Title = "Collective App Downloader (EditList - Delete)";

                    FormatList();

                    string urlsString = Properties.Settings.Default.URLs;
                    var urls = urlsString.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    Console.WriteLine("List of URLs");
                    Console.WriteLine("----------------------------------------------------------");
                    for (int i = 0; i < urls.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}: {urls[i]}");
                    }
                    Console.WriteLine("----------------------------------------------------------");
                    Console.WriteLine();

                    Console.WriteLine("Plese input delete line's number");
                    Console.WriteLine();
                    Console.Write(">");

                    if (int.TryParse(Console.ReadLine(), out int lineNumber) && lineNumber > 0 && lineNumber <= urls.Count)
                    {
                        urls.RemoveAt(lineNumber - 1);

                        Properties.Settings.Default.URLs = string.Join("\n", urls);
                        Properties.Settings.Default.Save();

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Done");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid line number.");
                        Console.ResetColor();
                        Console.WriteLine();
                    }

                    FormatList();

                    Properties.Settings.Default.Save();

                    Console.WriteLine("List of URLs");
                    Console.WriteLine("----------------------------------------------------------");
                    Console.WriteLine(Properties.Settings.Default.URLs);
                    Console.WriteLine("----------------------------------------------------------");

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Plese enter any key.");
                    Console.ReadKey();
                }
                else if (EditMode == "4" || EditMode == "!")
                {
                    Console.Title = "Collective App Downloader (EditList - reset)";

                    FormatList();

                    Console.WriteLine("OK?");
                    Console.WriteLine();

                    Console.Write("(y/n)>");
                    string YorN = Console.ReadLine();

                    if (YorN == "y")
                    {
                        Properties.Settings.Default.Reset();

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Done");
                        Console.ResetColor();
                        Console.WriteLine();

                        Console.WriteLine("List of URLs");
                        Console.WriteLine("----------------------------------------------------------");
                        Console.WriteLine(Properties.Settings.Default.URLs);
                        Console.WriteLine("----------------------------------------------------------");

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Plese enter any key.");
                        Console.ReadKey();
                    }
                }
                else if (EditMode == "5" || EditMode == "e")
                {
                    Console.Title = "Collective App Downloader (EditList - List export)";

                    FormatList();

                    string URLs = Properties.Settings.Default.URLs;

                    SaveFileDialog saveFileDialog = new SaveFileDialog()
                    {
                        FileName = "CollectiveAppDownloader_URLs.txt",
                        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                        RestoreDirectory = true
                    };

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(saveFileDialog.FileName, URLs, Encoding.UTF8);
                    }

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done");
                    Console.ResetColor();
                    Console.WriteLine();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Plese enter any key.");
                    Console.ReadKey();
                }
                else if (EditMode == "6" || EditMode == "i")
                {
                    Console.Title = "Collective App Downloader (EditList - List inport)";

                    FormatList();

                    OpenFileDialog openFileDialog = new OpenFileDialog()
                    {
                        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                        RestoreDirectory = true
                    };

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string URLs = File.ReadAllText(openFileDialog.FileName);
                        Properties.Settings.Default.URLs = URLs;

                        FormatList();

                        Properties.Settings.Default.Save();
                        Properties.Settings.Default.Reload();

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Done");
                        Console.ResetColor();
                        Console.WriteLine();

                        Console.WriteLine("List of URLs");
                        Console.WriteLine("----------------------------------------------------------");
                        Console.WriteLine(Properties.Settings.Default.URLs);
                        Console.WriteLine("----------------------------------------------------------");

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Plese enter any key.");
                        Console.ReadKey();
                    }
                }
                else if (EditMode == "q" || EditMode == "x")
                {
                    FormatList();

                    Console.ResetColor();

                    return;
                }

                Console.ResetColor();
            }
        }

        public static void FormatList()
        {
            string[] lines = Properties.Settings.Default.URLs.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                result += lines[i].Trim() + "\n\n";
            }

            Properties.Settings.Default.URLs = result.TrimEnd();

            Properties.Settings.Default.Save();
        }
    }
}