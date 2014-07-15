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
            Server.StartServer();
            Console.WriteLine("Press Enter to stop");
            Console.ReadLine();
        }
    }
}
