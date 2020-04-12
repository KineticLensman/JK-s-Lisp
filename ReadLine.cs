using System;

namespace JKLApp
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
