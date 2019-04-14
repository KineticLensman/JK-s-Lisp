using System;
using System.Collections.Generic;
using System.Text;
using static Mal.Types;

namespace Mal
{
    class Printer
    {
        // The converse of read_str - return a string rep of a MAL object.
        public static string pr_str(MalVal ast, bool printReadably)
        {
            // Mal Guide says switch on the ast type, but we use virtuals instead.
            if (ast != null)
            {
                return ast.ToString(printReadably);
            }
            else
            {
                // The MAL guide doesn't do this check but it stops comment lines from crashing.
                return "";
            }
        }
    }
}
