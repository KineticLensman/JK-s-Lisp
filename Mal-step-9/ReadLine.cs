using System;
//using System.Collections.Generic;
//using System.Text;

namespace Mal
{
    // A seperate class for consistency with the Mal guide.
    static public class ReadLine
    {
        public static string Readline(string prompt)
        {
            Console.Write(prompt);
            Console.Out.Flush();
            return Console.ReadLine();
        }
    }
}
