using System;
using System.IO;

namespace Mal
{
    public class MalProgram
    {
        // pass args via the project's properties if necessary
        static public int Main(string[] args)
        {
            // If true Mal will process test files, if false, Mal handles interactive user input
            bool offline = false;

            StreamWriter writer = null;

            if (offline)
            {
                // Establish the test files.
                try
                {
                    // Create streams for the input and output.
                    writer = new StreamWriter("F:/Mal-development/mal-tests/mal-step1-test-01.LOG.txt");
                    Console.SetOut(writer);
                    Console.SetIn(new StreamReader("F:/Mal-development/mal-tests/mal-step1-test-01.txt"));
                }
                catch(IOException e)
                {
                    // Oops - missing input file or other stream badness.
                    TextWriter errorWriter = Console.Error;
                    errorWriter.WriteLine(e.Message);
                    return (-1);
                }
            }

            // Run some lisp!
            Console.WriteLine("*** Welcome to JK's Lisp ***");
            Console.WriteLine("Warning - Quasiquiotes are deferred");
            MalRepl mal = new MalRepl();

            // And this is where the story really starts.
            mal.RunREPL();

            // recover I/O
            if(writer != null)
            {
                writer.Close();
                StreamWriter standardOutput = new StreamWriter(Console.OpenStandardOutput())
                {
                    AutoFlush = true
                };
                Console.SetOut(standardOutput);
            }

            Console.WriteLine("Mal run successfully");
            return (0);
        }
    } 
}
