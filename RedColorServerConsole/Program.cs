using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RedColorServer;

namespace RedColorServerConsole
{
    
    class Program
    {
        static void Main(string[] args)
        {
            //log4net.GlobalContext.Properties["ProgramDataPath"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            log4net.Config.XmlConfigurator.Configure();

            Server.StartServer();
            Console.WriteLine("Press Enter to stop");
            Console.ReadLine();
        }
    }
}
