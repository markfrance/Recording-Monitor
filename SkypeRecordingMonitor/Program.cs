using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;


namespace SkypeRecordingMonitor
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var program = new Monitor();

            if(Environment.UserInteractive)
            {
                program.Start();
            }
            ServiceBase[] servicesToRun;
            servicesToRun = new ServiceBase[]
            {
                program
            };
            ServiceBase.Run(servicesToRun);

        }



    }
}
