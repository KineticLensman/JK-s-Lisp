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
        //------------------ Utilities for Numbers and numeric ops -------------------------------------------
        // C#-Mal only allows integers and thus avoids a lot of complexity. I allow ints and floats
        // although the implementation is more complex.
        // Also unlike C#-Mal, numeric args can have arg lists of variable  length, e.g. (+ 1 2 3).

        // Decide whether to use int or float arithmetic and check for non-numerics. Applies
        // float contaigon if an expression has a mix of ints and floats.
        protected static bool AllIntegers(MalList args)
        {
            bool allInts = true;
            for (var index = 0; index < args.Count(); index++)
            {
                switch( args[index])
                {
                    case MalInt i:
                        break;
                    case MalFloat f:
                        allInts = false;
                        // Don't return at this point in case there are lurking non-numbers.
                        break;
                    default:
                        throw new MalEvalError("non-number in numeric expression: '" + args[index]);
                }
            }
            return allInts;
        }

        // Numeric functions that can be applied to lists of numbers.
        protected enum NumericOp { Plus, Multiply, Subtract, Divide };

        // Mixing floats and integers introduces loads of special cases. I decided to 
        // move the things that are type-dependent into helper functions to keep the
        // numeric Builtins themselves cleaner.
        static protected MalInt ProcessIntegers (MalList args, NumericOp op)
        {
            int result = (int)(MalInt)args[0];
            int divTest = 0;

            // Handle the special case of (- 1).
            if(args.Count() == 1 && op == NumericOp.Subtract)
            {
                return new MalInt(-result);
            }
            for (var i = 1; i < args.Count(); i++)
            {
                switch (op)
                {
                    case NumericOp.Plus:
                        result += (int)(MalInt)args[i];
                        break;
                    case NumericOp.Multiply:
                        result *= (int)(MalInt)args[i];
                        break;
                    case NumericOp.Subtract:
                        result -= (int)(MalInt)args[i];
                        break;
                    case NumericOp.Divide:
                        divTest = (int)(MalInt)args[i];
                        if( i > 0 && divTest == 0)
                        {
                            throw new MalEvalError("Can't divide by zero");
                        }
                        result /= divTest;
                        break;
                }
            }
            return new MalInt(result);
        }

        static protected MalFloat ProcessNumbers (MalList args, NumericOp op)
        {
            float result = 0;

            // Initiate the calculation with the first arg.
            switch (args[0])
            {
                case MalInt integer:
                    result = (float)integer;
                    // Handle the special case of (- 1).
                    if (args.Count() == 1 && op == NumericOp.Subtract)
                    {
                        float negative = (float)integer;
                        return new MalFloat (-negative);
                    }
                    break;
                case MalFloat floater:
                    result = (float)floater;
                    // Handle the special case of (- 1.0).
                    if (args.Count() == 1 && op == NumericOp.Subtract)
                    {
                        float negative = (float)floater;
                        return new MalFloat(-negative);
                    }
                    break;
                default:
                    // Should have already detected this but just in case.
                    throw new MalInternalError("Non-number while calculating numbers: '" + args[0].ToString(true));
            }
            float divTest = 0;

            // Now apply the op to the remaining args.
            for (var i = 1; i < args.Count(); i++)
            {
                switch (args[i])
                {
                    case MalInt integer:
                        switch (op)
                        {
                            case NumericOp.Plus:
                                result += (float)integer;
                                break;
                            case NumericOp.Multiply:
                                result *= (float)integer;
                                break;
                            case NumericOp.Subtract:
                                result -= (float)integer;
                                break;
                            case NumericOp.Divide:
                                divTest = (float)integer;
                                if (i > 0 && divTest == 0)
                                {
                                    throw new MalEvalError("Can't divide by zero");
                                }
                                result /= divTest;
                                break;
                        }
                        break;
                    case MalFloat floater:
                        switch (op)
                        {
                            case NumericOp.Plus:
                                result += (float)floater;
                                break;
                            case NumericOp.Multiply:
                                result *= (float)floater;
                                break;
                            case NumericOp.Subtract:
                                result -= (float)floater;
                                break;
                            case NumericOp.Divide:
                                divTest = (float)floater;
                                if (i > 0 && divTest == 0)
                                {
                                    throw new MalEvalError("Can't divide by zero");
                                }
                                result /= divTest; break;
                        }
                        break;
                    default:
                        // Should have already detected this but just in case.
                        throw new MalInternalError("Non-number while calculating numbers: '" + args[i].ToString(true));
                }
            }
            return new MalFloat(result);
        }


        //------------------ Builtins for Numbers and numeric ops -------------------------------------------

        public static MalFunc BuiltInAdd = new MalFunc(args =>
        {
            if (AllIntegers(args))
            {
                return ProcessIntegers(args, NumericOp.Plus);
            }
            else
            {
                return ProcessNumbers(args, NumericOp.Plus);
            }
        });

        public static MalFunc BuiltInMultiply = new MalFunc(args =>
        {
            if (AllIntegers(args))
            {
                return ProcessIntegers(args, NumericOp.Multiply);
            }
            else
            {
                return ProcessNumbers(args, NumericOp.Multiply);
            }
        });

        public static MalFunc BuiltInSubtract = new MalFunc(args =>
        {
            if (AllIntegers(args))
            {
                return ProcessIntegers(args, NumericOp.Subtract);
            }
            else
            {
                return ProcessNumbers(args, NumericOp.Subtract);
            }
        });

        public static MalFunc BuiltInDivide = new MalFunc(args =>
        {
 
            if (AllIntegers(args))
            {
                // Unlike the others, handle both cases as floats to avoid
                // non-intuitive results such as (/ 3 2) == 1.
                return ProcessNumbers(args, NumericOp.Divide);
            }
            else
            {
                return ProcessNumbers(args, NumericOp.Divide);
            }
        });

        //------------------ Builtins for List manipulation -------------------------------------------

        // Make a list containing the supplied args (already eval'd).
        public static MalFunc BuiltinList = new MalFunc(args =>
        {
            MalList newList = new MalList();

            for (var i = 0; i < args.Count(); i ++)
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
                throw new MalEvalError("list? expects a single argument");
            }
            if (args[0] is MalList)
            {
                return new MalTrue();
            }
            else
            {
                return new MalFalse();
            }
        });

        public static MalFunc BuiltinEmpty = new MalFunc(args =>
        {
            if (args.Count() != 1)
            {
                throw new MalEvalError("empty? expects a single argument");
            }
            if (args[0] is MalList testList)
            {
                if(testList.Count() == 0)
                {
                    return new MalTrue();
                }
                else
                {
                    return new MalFalse();
                }
            }
            else
            {
                throw new MalEvalError("empty? expects a single list");
            }
        });

        // TODO - finish coding.
        public static MalFunc BuiltinCount = new MalFunc(args =>
        {
            if (args.Count() != 1)
            {
                throw new MalEvalError("count expects a single argument");
            }
            if (args[0] is MalList testList)
            {
                return new MalInt(testList.Count());
            }
            else if (args[0] is MalNil)
            {
                return new MalInt(0);
            }
            else
            {
                throw new MalEvalError("count expects a list or nil");

            }
        });

        //------------------ Builtins for boolean comparisons -------------------------------------------

        // TODO - =. True if the two params are same type and contain same value. If lists, they must be of equal length
        // and each member should be equal.

        // TODO: < <= > >=
        // first two params should be numbers, return true or false.

    }
}
