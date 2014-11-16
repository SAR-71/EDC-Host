using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EDC_Host
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("EDC-Client v0.2");
            Console.WriteLine("");

            Console.WriteLine("Enter the target port:");
            int port = Convert.ToInt32(Console.ReadLine());

            Console.WriteLine();
            Console.WriteLine("enter the AES-key:");
            string aesKey = Console.ReadLine();




            crypto cryptClass = new crypto("aselrias38490a32", "8947az34awl34kjq", aesKey, 25);
            Connections connection = new Connections(cryptClass, port);

            connection._getMessage += new Connections._D_getMessage(getMessage);
            connection._newConnection += new Connections._D_newConnection(memberConnected);

            connection.setup();


            Console.Clear();


            string tipped;
            while (true)
            {
                tipped = Console.ReadLine();
                connection.sendMessage(tipped);
                Console.CursorTop -= 1;

                Console.WriteLine("You:\t{0}", tipped);
                Console.WriteLine();
            }
        }


        static void memberConnected(string alias)
        {
            Console.WriteLine(alias + " connected.");
            Console.WriteLine();

        }
        static void getMessage(string message, string alias)
        {
            Console.WriteLine(alias + ":\t" + message);
            Console.WriteLine();
        }
    }
}
