using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace DocGuard_Watcher.Class
{
    class DocGuardService : ServiceBase
    {

        #region variable

        public string Email { get; set; }
        public string Password { get; set; }
        public string Token { get; set; }
        public string Url { get; set; }
        public bool warningShow = false;

        private FileSystemWatcher Watcher = null;

        #endregion

        #region Constructor

        public DocGuardService()
        {
            Email = "";
            Password = "";
            Token = null;
            Url = "https://api.docguard.net:8443/";
        }

        #endregion

        #region ServiceFunctions

        protected override void OnStart(string[] args)
        {
            Log("DocGuard-Watcher Service is starting", "Info");
            readConfig();
            variableStatus();

            if (args.Length == 1) { if (args[0] == "--warning") { warningShow = true; } }


            foreach (var drive in DriveInfo.GetDrives())
            {
                //File watcher create
                Watcher = new FileSystemWatcher()
                {
                    Path = drive.Name,
                    Filters = { ".hta", "*.pdf", "*.slk", "*.csv", "*.doc", "*.dot", "*.docx", "*.docm", "*.dotx", "*.dotm",
                    "*.wll", "*.xls", "*.xll", "*.xlw", "*.xlt", "*.xlsx", "*.xlsm", "*.xlsb", "*.xlam", "*.xltx", "*.xltm", "*.ppt", "*.pps",
                    "*.pptx", "*.pptm", "*.ppsx", "*.ppam", "*.ppa", "*.rtf", "*.bin", "*.pub" },
                    NotifyFilter = NotifyFilters.LastWrite |NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.DirectoryName,
                    InternalBufferSize = 8192 * 8, // 64k
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                //Event
                Watcher.Changed += new FileSystemEventHandler(OnChanged);
                Watcher.Renamed += new RenamedEventHandler(OnRenamed);

            }
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            Log("DocGuard-Watcher Service is Stopping", "Info");
            Watcher.EnableRaisingEvents = false;
            Watcher.Dispose();

            base.OnStop();
        }

        protected override void OnPause()
        {
            Log("DocGuard-Watcher Service is Pausing", "Info");
            Watcher.EnableRaisingEvents = false;
            Watcher.Dispose();

            base.OnPause();
        }

        #endregion

        #region EventFunctions

        //
        public void OnChanged(object sender, FileSystemEventArgs e)
        {
            var control = eventControl(e.FullPath);

            if (control)
            {
                getFile(e.FullPath, Path.GetFileName(e.FullPath));
            }
        }

        //
        public void OnRenamed(object sender, RenamedEventArgs e)
        {
            string FileName = Path.GetFileName(e.FullPath);

            var control = eventControl(FileName);

            if (control == true)
            {
                getFile(e.FullPath, Path.GetFileName(e.FullPath));
            }

        }

        #endregion

        #region OthersFunctions

        //Control file path
        public bool eventControl(string filePath)
        {

            if (Regex.IsMatch(filePath, @"\$|\~\$"))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        //read config file which is comming from user
        public void readConfig()
        {
            //opening the subkey  
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\DocGuard-Watcher"))
            {
                //if it does exist, retrieve the stored values  
                if (!(key is null))
                {
                    Email = key.GetValue("email").ToString();
                    Password = key.GetValue("password").ToString();
                    if (!(key.GetValue("url").ToString() is null))
                    {
                        Url = key.GetValue("Url").ToString();
                    }

                    getToken();

                }
                else
                {
                    Log("Register path is null so DocGuard-Watcher will not work anymore", "Error");
                    Stop();
                }

            }

        }

        //send login request and take user's jwt
        public void getToken()
        {
            // if Email and Password is provided user wants to store anaylze privately otherwise anaylze will be public
            if (Email != "" && Password != "")
            {
                string resp = null;

                //Send request for loging and take response
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        using (var content = new MultipartFormDataContent())
                        {

                            content.Add(new StringContent(Email), "Username");
                            content.Add(new StringContent(Email), "Email");
                            content.Add(new StringContent(Password), "Password");
                            content.Add(new StringContent("false"), "RememberMe");

                            var response = client.PostAsync(Url + "Account/Login", content).GetAwaiter().GetResult();
                            if (response.IsSuccessStatusCode)
                            {
                                resp = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //username password gelip login esnasında hata alınmışsa username password hatalı veya bir yerden bir problem var
                    // burada service'i durdurmak lazım diğer türlü yüklenecekd dosyalar public olacaktır.
                    Log("There is error while sending login request. Please control your connection", "Error");
                    return;
                }

                //Control resp for token
                if (!(resp is null))
                {
                    if (resp.Contains("Token"))
                    {
                        try
                        {
                            Token = JObject.Parse(resp)["Token"].ToString();
                        }
                        catch (Exception ex)
                        {
                            //token parse edereken esnasında hata alınmışsa bir yerden bir problem var
                            //burada service'i durdurmak lazım diğer türlü yüklenecekd dosyalar public olacaktır.
                            Log("There is error while parsing Token response. Please try to login DocGuard on Web UI and control your credentials", "Error");
                            Stop();
                        }
                    }
                    else
                    {
                        //eger token yoksa email password hatalı ne yapmak lazım ?
                        Log("There is error while taking a token. The credential you provided may be wrong please control it", "Error");
                        Stop();
                    }
                }

            }

        }

        //Take a filePath then send it fileUpload then parse output
        public void getFile(string file, string fileName)
        {
            JObject response = null;

            try
            {
                response = JObject.Parse(fileUpload(file, fileName));
            }
            catch (Exception ex)
            {
                if (warningShow)
                {
                    string message = String.Format("File name : {0}\nError message : {1}", fileName, ex.ToString());
                    Log(message, "Warning");
                }
                
            }

            if (!(response is null))
            {
                if (response.ContainsKey("Error"))
                {
                    var message = String.Format("File Name : {0}\nError : {1}\nToken : {2}", fileName, response["Error"].ToString(), Token);
                    Log(message, "Info");
                }
                else if (response.ContainsKey("Verdict"))
                {
                    var message = String.Format("File Name : {0}\nFileType : {1}\nVerdict : {2}\nFileMD5Hash : {3}\nToken : {4}"
                        , fileName, response["FileType"].ToString(), response["Verdict"].ToString(), response["FileMD5Hash"].ToString(), Token);
                    Log(message,"Info");

                }
            }

        }

        //FileUpload
        public string fileUpload(string file, string FileName)
        {
            getToken();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            if (!(Token is null))
                            {
                                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Token);
                            }

                            content.Add(new StringContent("false"), "sanitization");
                            content.Add(new StringContent("false"), "isPublic");

                            var fileContent = new StreamContent(fileStream);
                            content.Add(fileContent, "file", FileName);

                            var response = client.PostAsync(Url + "api/FileAnalyzing/AnalyzeFile/", content).GetAwaiter().GetResult();
                            if (response.IsSuccessStatusCode)
                            {
                                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                if (warningShow)
                {
                    string message = String.Format("File name : {0}\nError message : {1}", FileName, ex.ToString());
                    Log(message, "Warning");
                }

                return null;
            }

            return null;
        }

        //
        public void variableStatus()
        {
            string message = String.Format("Email : {0}\nPassword : {1}\nUrl : {2}\nToken : {3}", Email, Password, Url, Token);
            Log(message, "Info");

        }

        //write Output
        public void Log(string logMessage, string type)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "DocGuard-Watcher";

                if (type == "Info")
                {
                    eventLog.WriteEntry(logMessage, EventLogEntryType.Information, 10000);
                }
                else if (type == "Warning")
                {
                    eventLog.WriteEntry(logMessage, EventLogEntryType.Warning, 10001);
                }
                else if (type == "Error")
                {
                    eventLog.WriteEntry(logMessage, EventLogEntryType.Error, 10002);
                }

            }
        }
        #endregion


    }
}
