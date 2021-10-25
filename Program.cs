using System;
using System.ServiceProcess;
using DocGuard_Watcher.Class;

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
