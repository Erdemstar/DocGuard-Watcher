using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

        public string _logFileLocation = @"C:\Users\Erdemstar\Desktop\servicelog.txt";

        private FileSystemWatcher Watcher = null;

        #endregion

        #region Constructor

        public DocGuardService()
        {
            Email = null;
            Password = null;
            Token = null;
            Url = "https://api.docguard.net:8443";
        }

        #endregion

        #region ServiceFunctions

        protected override void OnStart(string[] args)
        {
            Log("Starting");

            if (args.Length == 1)
            {
                readConfig(args[0]);
            }

            foreach (var drive in DriveInfo.GetDrives())
            {
                //File watcher create
                Watcher = new FileSystemWatcher()
                {
                    Path = drive.Name,
                    Filters = { ".hta", "*.pdf", "*.slk", "*.csv", "*.doc", "*.dot", "*.docx", "*.docm", "*.dotx", "*.dotm",
                    "*.wll", "*.xls", "*.xll", "*.xlw", "*.xlt", "*.xlsx", "*.xlsm", "*.xlsb", "*.xlam", "*.xltx", "*.xltm", "*.ppt", "*.pps",
                    "*.pptx", "*.pptm", "*.ppsx", "*.ppam", "*.ppa", "*.rtf", "*.bin", "*.pub" },
                    NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.CreationTime,
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
            Log("Stopping");
            Watcher.EnableRaisingEvents = false;
            Watcher.Dispose();

            base.OnStop();
        }

        protected override void OnPause()
        {
            Log("Pausing");
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
                Log("\nOnChanged : " + e.FullPath);
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
                Log("\nOnRenamed : " + e.FullPath);
                getFile(e.FullPath, Path.GetFileName(e.FullPath));
            }

        }

        #endregion

        #region OthersFunctions

        //Control file path
        public bool eventControl(string filePath)
        {

            if (Regex.IsMatch(filePath,@"\$|\~\$"))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        //
        public void readConfig(string configFilePath)
        {
            Log("Config path : " + configFilePath);

            JObject data;
            try
            {
                data = JObject.Parse(File.ReadAllText(configFilePath));
            }
            catch
            {
                throw;
            }
            if (!(data is null))
            {
                try
                {
                    Email = data["email"].ToString();
                    Password = data["password"].ToString();

                    Log("Email : " + Email);
                    Log("Password : " + Password);

                }
                catch (Exception ex)
                {
                    //Username password'de bir hata varsa service'in durması lazım çünkü yüklenece dosyalar diğer türlü public olacaktır.
                    throw;
                }
                try
                {
                    Url = data["url"].ToString();
                    Log("Url : " + Url);
                }
                catch (Exception ex)
                {
                    Url = "https://api.docguard.net:8443/";
                }
            }
            else
            {
                //eğer verilen configFile null ise de bir hata vermek gerekir diper tülü yüklenece dosyalar public olacaktır.
            }

        }
        
        //
        public void getToken()
        {
            if (!(Email is null && Password is null))
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
                    throw;
                }

                //Control resp for token
                if (!(resp is null))
                {
                    if (resp.Contains("Token"))
                    {
                        try
                        {
                            Token = JObject.Parse(resp)["Token"].ToString();
                            Log("Token : " + Token);
                        }
                        catch (Exception ex)
                        {
                            //token parse edereken esnasında hata alınmışsa bir yerden bir problem var
                            //burada service'i durdurmak lazım diğer türlü yüklenecekd dosyalar public olacaktır.
                            throw;
                        }
                    }
                    else
                    {
                        //eger token yoksa email password hatalı ne yapmak lazım ?
                    }
                }
                else
                {
                    //eger gelen cevap nullsa login isteğinin cevabı bile yok ne yapmak lazım
                }

            }
            else
            {
                //Verilen config dosyadın email ve password null ise ne olacak ? 
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
                Log("\n[!] File name : " + fileName + " - Error message : " + ex.ToString());
            }

            if (!(response is null))
            {
                if (response.ContainsKey("Error"))
                {
                    var message = String.Format("\nFile Name : {0}\nError : {1}", fileName, response["Error"].ToString());
                    Log(message);
                }
                else if (response.ContainsKey("Verdict"))
                {
                    var message = String.Format("\nFile Name : {0}\nFileType : {1}\nVerdict : {2}\nFileMD5Hash : {3}\n"
                        , fileName, response["FileType"].ToString(), response["Verdict"].ToString(), response["FileMD5Hash"].ToString());
                    Log(message);

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
                Log("\n[!] File name : " + FileName + " - Error message : " + ex.ToString());
                return null;
            }

            return null;
        }

        //write Output
        public void Log(string logMessage)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFileLocation));
                File.AppendAllText(_logFileLocation, DateTime.UtcNow.ToString() + " : " + logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {

                throw;
            }
        }
        #endregion


    }
}
