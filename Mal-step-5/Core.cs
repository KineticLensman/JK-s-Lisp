using System;
using System.Collections.Generic;
using System.Text;
using static Mal.Types;

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
            if (args.Count() != 1)
            {
                throw new MalEvalError("list? expects a single argument but got " + args.Count());
            }
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
            if (args.Count() != 1)
            {
                throw new MalEvalError("empty? expects a single argument but got " + args.Count());
            }
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
            if (args.Count() != 1)
            {
                throw new MalEvalError("count expects a single argument");
            }
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
            if (args.Count() != 2)
            {
                throw new MalEvalError("Comparison operator expects two arguments but got " + args.Count());
            }
            return MalVal.EQ(args[0], args[1]);
        });

        // Helper for functions that expect two numeric args.
        // TODO - handle monotonic argument lists
        private static void CheckComparisonParams(MalList args)
        {
            if (args.Count() != 2)
            {
                throw new MalEvalError("Comparison operator expects two arguments but got " + args.Count());
            }
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
            CheckComparisonParams(args);
            if ((MalNum)args[0] < (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        public static MalFunc BuiltinLToE = new MalFunc(args =>
        {
            CheckComparisonParams(args);
            if ((MalNum)args[0] <= (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        public static MalFunc BuiltinGT = new MalFunc(args =>
        {
            CheckComparisonParams(args);
            if ((MalNum)args[0] > (MalNum)args[1])
            {
                return malTrue;
            }
            return malFalse;
        });

        public static MalFunc BuiltinGToE = new MalFunc(args =>
        {
            CheckComparisonParams(args);
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

            // Printing
            {"pr-str", BuiltinPrStr},
            {"str", BuiltinStr},
            {"prn", BuiltinPrn},
            {"println", BuiltinPrintLn}
        };

    }
}
