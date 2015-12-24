using DiscordSharpRefactored;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoredTest
{
    class Program
    {
        static void Main(string[] args)
        {
            DiscordClient c = new DiscordClient();
            c.ClientPrivateInformation.email = "miketheripper1@msn.com";
            c.ClientPrivateInformation.password = "asdf12";
            c.Login();

            Console.WriteLine("\n\nPress enter to continue..");
            Console.ReadLine();
        }
    }
}
