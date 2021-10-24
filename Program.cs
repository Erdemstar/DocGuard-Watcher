using System;
using DocGuard_Watcher.Class;
using System.ServiceProcess;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;

namespace DocGuard_Watcher
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceBase.Run(new DocGuardService());
        }
    }
}
