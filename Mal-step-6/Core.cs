using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using static Mal.Types;
using static Mal.MalReader;

// Useful notes https://docs.microsoft.com/en-us/dotnet/csharp/lambda-expressions

// The builtin functions available from within Mal. They are written as lambda
// expressions that take a list of args. These builtins are added to the Mal Env at 
// start-up and may be extended by programmes that call def.

namespace Mal
{
    class Core
    {
        // Some constants.
        public static MalTrue malTrue = new MalTrue();
        public static MalFalse malFalse = new MalFalse();
        public static MalNil malNil = new MalNil();

        //------------------ General utilities -------------------------------------------
        private static void CheckArgCount(MalList args, int expectedCount, string callerName)
        {
            if (args.Count() != expectedCount)
            {
                throw new MalEvalError("'" + callerName + "' expected " + expectedCount + " arg(s) but got " + args.Count());
            }
        }


        //------------------ Utilities for Numbers and numeric ops -------------------------------------------
        // C#-Mal only allows integers and thus avoids a lot of complexity. JKL handles doubles but
        // to keep things simple, doesn't have a separate int type.
        // Also unlike C#-Mal, numeric args can have arg lists of variable  length, e.g. (+ 1 2 3).

        // Numeric functions that can be applied to lists of numbers, used to handle special cases.
        protected enum NumericOp { Plus, Multiply, Subtract, Divide };

        // Make sure that the arguments are all numeric and then do the desired calculations handling
        // special cases such as div by zero.
        static protected MalNum ProcessNumbers(MalList args, NumericOp op)
        {
            double result = 0;

            // Initiate the calculation with the first arg.
            switch (args[0])
            {
                case MalNum num:
                    result = (double)num;
                    // Handle the special case of (- 1).
                    if (args.Count() == 1 && op == NumericOp.Subtract)
                    {
                        double negative = (double)num;
                        return new MalNum(-negative);
                    }
                    break;

                default:
                    // Should have already detected this but just in case.
                    throw new MalInternalError("Non-number while calculating numbers: '" + args[0].ToString(true));
            }
            double divTest = 0;

            // Now apply the op to the remaining args.
            for (var i = 1; i < args.Count(); i++)
            {
                switch (args[i])
                {

                    case MalNum num:
                        switch (op)
                        {
                            case NumericOp.Plus:
                                result += (double)num;
                                break;
                            case NumericOp.Multiply:
                                result *= (double)num;
                                break;
                            case NumericOp.Subtract:
                                result -= (double)num;
                                break;
                            case NumericOp.Divide:
                                divTest = (double)num;
                                if (i > 0 && divTest == 0)
                                {
                                    throw new MalEvalError("Can't divide by zero");
                                }
                                result /= divTest;
                                break;
                        }
                        break;
                    default:
                        throw new MalInternalError("Non-number while calculating numbers: '" + args[i].ToString(true));
                }
            }
            return new MalNum(result);
        }

        //------------------ Builtins for Numbers and numeric ops -------------------------------------------

        public static MalFunc BuiltInAdd = new MalFunc(args =>
        {
            return ProcessNumbers(args, NumericOp.Plus);
        });

        public static MalFunc BuiltInMultiply = new MalFunc(args =>
        {
            return ProcessNumbers(args, NumericOp.Multiply);
        });

        public static MalFunc BuiltInSubtract = new MalFunc(args =>
        {
            return ProcessNumbers(args, NumericOp.Subtract);
        });

        public static MalFunc BuiltInDivide = new MalFunc(args =>
        {

            return ProcessNumbers(args, NumericOp.Divide);
        });

        //------------------ Builtins for List manipulation -------------------------------------------

        // Make a list containing the supplied args (already eval'd). Returns an empty list, not nil, if 
        // there are no args.
        public static MalFunc BuiltinList = new MalFunc(args =>
        {
            MalList newList = new MalList();

            for (var i = 0; i < args.Count(); i++)
            {
                newList.Add(args[i]);
            }
            return newList;
        });

        // Return true if first param is a List, otherwise false.
        public static MalFunc BuiltinIsList = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "list?");
            if (args[0] is MalList)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinEmpty = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "empty?");
            if (args[0] is MalList testList)
            {
                if (testList.Count() == 0)
                {
                    return malTrue;
                }
                else
                {
                    return malFalse;
                }
            }
            else
            {
                throw new MalEvalError("empty? expects a single list");
            }
        });

        public static MalFunc BuiltinCount = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "count");
            if (args[0] is MalList testList)
            {
                return new MalNum(testList.Count());
            }
            else if (args[0] is MalNil)
            {
                return new MalNum(0);
            }
            else
            {
                throw new MalEvalError("count expects a list or nil");

            }
        });

        //------------------ Builtins for boolean comparisons -------------------------------------------

        public static MalFunc BuiltinEQ = new MalFunc(args =>
        {
            CheckArgCount(args, 2, "=");
            return MalVal.EQ(args[0], args[1]);
        });

        // Helper for functions that expect two numeric args.
        // TODO - handle monotonic argument lists
        private static void CheckComparisonParams(MalList args, string op)
        {
            CheckArgCount(args, 2, op);

            if (!((object)args[0] is MalNum))
            {
                throw new MalEvalError("Number expected but got '" + args[0].ToString(true) + "'");
            }
            if (!((object)args[1] is MalNum))
            {
                throw new MalEvalError("Number expected but got '" + args[1].ToString(true) + "'");
            }
        }

        public static MalFunc BuiltinLT = new MalFunc(args =>
        {
            CheckComparisonParams(args, "<");
            if ((MalNum)args[0] < (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        public static MalFunc BuiltinLToE = new MalFunc(args =>
        {
            CheckComparisonParams(args, "<=");
            if ((MalNum)args[0] <= (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        public static MalFunc BuiltinGT = new MalFunc(args =>
        {
            CheckComparisonParams(args, ">");
            if ((MalNum)args[0] > (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        public static MalFunc BuiltinGToE = new MalFunc(args =>
        {
            CheckComparisonParams(args, ">=");
            if ((MalNum)args[0] >= (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        //------------------ Builtins for printing -------------------------------------------

        // Calls pr_str on each argument with print_readably set to true, joins the results
        // with " " and returns the new string.
        public static MalFunc BuiltinPrStr = new MalFunc(args =>
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < args.Count(); i++)
            {
                if( i > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(Printer.pr_str(args[i], true));
            }
            return new MalString(sb.ToString());
        });

        // calls pr_str on each argument with print_readably set to false, concatenates the
        // results together ("" separator), and returns the new string.
        public static MalFunc BuiltinStr = new MalFunc(args =>
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < args.Count(); i++)
            {
                if (i > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(Printer.pr_str(args[i], false));
            }
            return new MalString(sb.ToString());
        });

        // Calls pr_str on each argument with print_readably set to true, joins the results
        // with " ", prints the string to the screen and then returns nil.
        public static MalFunc BuiltinPrn = new MalFunc(args =>
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < args.Count(); i++)
            {
                if (i > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(Printer.pr_str(args[i], true));
            }
            Console.WriteLine(sb.ToString());
            return malNil;
        });

        // Calls pr_str on each argument with print_readably set to false, joins the results
        // with " ", prints the string to the screen and then returns nil.
        public static MalFunc BuiltinPrintLn = new MalFunc(args =>
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < args.Count(); i++)
            {
                if (i > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(Printer.pr_str(args[i], false));
            }
            Console.WriteLine(sb.ToString());
            return malNil;
        });

        // -------------------- Atoms ---------------------------------------------

        /*
                swap!: Takes an atom, a function, and zero or more function arguments. The atom's value is modified to the
                result of applying the function with the atom's value as the first argument and the optionally given function
                arguments as the rest of the arguments.The new atom's value is returned. 
        */

        // atom: Takes a Mal value and returns a new atom which points to that Mal value.
        public static MalFunc BuiltinAtom = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "atom");
            return new MalAtom(args[0]);
        });

        // atom?: Takes an argument and returns true if the argument is an atom.
        public static MalFunc BuiltinIsAtom = new MalFunc(args =>
        {
            // args.Count()
            CheckArgCount(args, 1, "atom?");
            if ((object)args[0] is MalAtom)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        // deref: Takes an atom argument and returns the Mal value referenced by this atom.
        public static MalFunc BuiltinDeref = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "deref");
            if(args[0] == null)
            {
                throw new MalEvalError("deref expected atom but got '" + args[0] + "'");

            }
            if ((object)args[0] is MalAtom atom)
            {
                return atom.Unbox();
            }
            throw new MalEvalError("deref expected atom but got '" + args[0].ToString(true) + "'");
        });

        // Modify an atom to refer to a new Mal value, return the latter.
        public static MalFunc BuiltinReset = new MalFunc(args =>
        {
            CheckArgCount(args, 2, "reset");
            if ((object)args[0] is MalAtom atom)
            {
                return atom.Reset(args[1]);
            }
            else
            {
                throw new MalEvalError("reset expected atom but got '" + args[0].ToString(true) + "'");
            }
        });

        // swap (poor name but as per guide) expects its arg list to contain an atom, a function and
        // optional additional args. swap builds a new arg list containing the atom's current value
        // followed by any additional args passed to swap. It then calls the function on the new
        // arg list, and then resets the atom's value to be the function's return value, which
        // is returned as the overall result. To see what this can do, consider
        // (def inc-it(fn (a) (+ 1 a)))
        // (def atm (atom 7))
        // (def f (fn () (swap atm inc-it)))
        // This creates a fn  called f. Repeatedly calling (f) will successively increment and
        // return the value of atm, e.g. 8, 9, etc
        public static MalFunc BuiltinSwap = new MalFunc(args =>
        {
            if ((object)args[0] is MalAtom atom)
            {
                if((object)args[1] is MalFunc func)
                {
                    // Create a list of args to be passed to the function passed to swap
                    MalList fnArgs = new MalList();

                    // Use the atom's current value as the first arg of the function.
                    fnArgs.Add(atom.Unbox());

                    // Use any remaining args to swap as additional args to the function.
                    for (var i = 2; i < args.Count(); i++)
                    {
                        fnArgs.Add(args[i]);
                    }

                    // Apply the function, using it as the atoms's new value;
                    MalVal result = func.Apply(fnArgs);

                    // Store the new value in the atom and return the new value.
                    atom.Reset(result);
                    return result;
                }
                else
                {
                    throw new MalEvalError("swap expected func but got '" + args[1].ToString(true) + "'");
                }
            }
            else
            {
                throw new MalEvalError("swap expected atom but got '" + args[0].ToString(true) + "'");
            }
        });

        // -------------------- File support ---------------------------------------------

        // Exposes the read_str function from the reader.
        public static MalFunc BuiltinReadString = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "read-string");
            if ((object)args[0] is MalString s)
            {
                return read_str(s.unbox());
            }
            else
            {
                throw new MalEvalError("read-string expected string but got '" + args[0].ToString(true) + "'");
            }
        });

        // "F:\Mal-development\mal-tests\mal-step6-test-01.txt"
        // "F:\Mal-development\mal-tests\mal-code-01.txt"

        // Read a specified file and return it as a single Mal string.
        public static MalFunc BuiltinSlurp = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "slurp");
            if ((object)args[0] is MalString s)
            {
                StringBuilder sb = new StringBuilder();
                try
                {
                    using (StreamReader sr = new StreamReader(s.unbox()))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            sb.Append(line);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new MalEvalError(e.Message);
                }
                return new MalString(sb.ToString());
            }
            else
            {
                throw new MalEvalError("slurp expected filename string but got '" + args[0].ToString(true) + "'");
            }
        });

        // Exposes the read_str function from the reader.
        //public static MalFunc BuiltinEval = new MalFunc(args =>
        //{
        //    CheckArgCount(args, 1, "eval");
        //    if ((object)args[0] is MalString s)
        //    {
        //        return read_str(s.unbox());
        //    }
        //    else
        //    {
        //        throw new MalEvalError("read-string expected string but got '" + args[0].ToString(true) + "'");
        //    }
        //});


        // -------------------- The Mal Namespace ---------------------------------------------

        // The Mal Namespace to be loaded by the main REPL.
        // I've taken off the '*' and '!' chars from the special forms.

        public static Dictionary<string, MalFunc> MalNameSpace = new Dictionary<string, MalFunc>
        {
            // Arithmetic.
            {"+", BuiltInAdd},
            {"*", BuiltInMultiply},
            {"-", BuiltInSubtract },
            {"/", BuiltInDivide},

            // List manipulation.
            {"list", BuiltinList},
            {"list?", BuiltinIsList},
            {"empty?", BuiltinEmpty},
            {"count", BuiltinCount},

            // Equality and friends.
            {"=", BuiltinEQ},
            {"<", BuiltinLT},
            {"<=", BuiltinLToE},
            {">", BuiltinGT},
            {">=", BuiltinGToE},

            // Printing.
            {"pr-str", BuiltinPrStr},
            {"str", BuiltinStr},
            {"prn", BuiltinPrn},
            {"println", BuiltinPrintLn},

            // Atoms.
            {"atom", BuiltinAtom},
            {"atom?", BuiltinIsAtom},
            {"deref", BuiltinDeref},
            {"reset", BuiltinReset},
            {"swap", BuiltinSwap},

            // File support.
            {"read-string", BuiltinReadString},
            {"slurp", BuiltinSlurp}
            // eval => BuiltInEval is defined in the REPL so that the closure can access the REPL env.
        };

    }
}
