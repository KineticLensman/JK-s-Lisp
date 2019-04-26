﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using static JKLApp.Types;
using static JKLApp.Reader;

// Useful notes https://docs.microsoft.com/en-us/dotnet/csharp/lambda-expressions

// The builtin functions available from within Mal. They are written as lambda
// expressions that take a list of args. These builtins are added to the Mal Env at 
// start-up and may be extended by programmes that call def.


namespace JKLApp
{
    class Core
    {
        // Some constants.
        public static JKLTrue malTrue = new JKLTrue();
        public static JKLFalse jklFalse = new JKLFalse();
        public static JKLNil jklNil = new JKLNil();
        public static JKLVarArgs jklVarArgsChar = new JKLVarArgs();

        //------------------ General utilities -------------------------------------------
        // Generic error check for core fns.
        public static void CheckArgCount(JKLList args, int expectedCount, string callerName)
        {
            if (args.Count() != expectedCount)
            {
                throw new JKLEvalError("'" + callerName + "' expected " + expectedCount + " arg(s) but got " + args.Count());
            }
        }

        public static JKLFunc BuiltinXXXTemplate = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 2, "nth");
            throw new JKLEvalError("BuiltinXXXTemplate not implemented yet");

            //if (args[0] is JKLList mList)
            //{

            //}
            //return nth;
        });

        //------------------ Predicates -------------------------------------------

        public static JKLFunc BuiltinNilP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "nil?");
            if (args[0] == jklNil)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinTrueP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "true?");
            if (args[0] == malTrue)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinFalseP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "false?");
            if (args[0] == jklFalse)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinSymbolP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "symbol?");
            if (args[0] is JKLSym)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        // Return true if first param is a List, otherwise false.
        public static JKLFunc BuiltinIsListP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "list?");
            if (args[0] is JKLList)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinEmptyP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "empty?");
            if (args[0] is JKLSequence testSeq)
            {
                if (testSeq.Count() == 0)
                {
                    return malTrue;
                }
                else
                {
                    return jklFalse;
                }
            }
            else
            {
                throw new JKLEvalError("empty? expected a sequence but got '" + args[0].ToString(true) + "'");
            }
        });

        // Return true if first param is a List, otherwise false.
        public static JKLFunc BuiltinIsKeyWordP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "keyword?");
            if (args[0] is JKLKeyword)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinIsVectorP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "vector?");
            if (args[0] is JKLVector)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinIsMapP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "map?");
            if (args[0] is JKLHashMap)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinIsSequentialP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "seq?");
            if (args[0] is JKLSequence)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinIsNumberP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "number?");
            if (args[0] is JKLNum)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        public static JKLFunc BuiltinIsStringP = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "string?");
            if (args[0] is JKLString)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        //------------------ Utilities for Numbers and numeric ops -------------------------------------------
        // C#-Mal only allows integers and thus avoids a lot of complexity. JKL handles doubles but
        // to keep things simple, doesn't have a separate int type.
        // Also unlike C#-Mal, numeric args can have arg lists of variable  length, e.g. (+ 1 2 3).

        // Numeric functions that can be applied to lists of numbers, used to handle special cases.
        protected enum NumericOp { Plus, Multiply, Subtract, Divide };

        // Make sure that the arguments are all numeric and then do the desired calculations handling
        // special cases such as div by zero.
        static protected JKLNum ProcessNumbers(JKLList args, NumericOp op)
        {
            double result = 0;

            // Initiate the calculation with the first arg.
            switch (args[0])
            {
                case JKLNum num:
                    result = (double)num;
                    // Handle the special case of (- 1).
                    if (args.Count() == 1 && op == NumericOp.Subtract)
                    {
                        double negative = (double)num;
                        return new JKLNum(-negative);
                    }
                    break;

                default:
                    // Should have already detected this but just in case.
                    throw new JKLParseError("Non-number while calculating numbers: '" + args[0].ToString(true) + "'");
            }
            double divTest = 0;

            // Now apply the op to the remaining args.
            for (var i = 1; i < args.Count(); i++)
            {
                switch (args[i])
                {

                    case JKLNum num:
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
                                    throw new JKLEvalError("Can't divide by zero");
                                }
                                result /= divTest;
                                break;
                        }
                        break;
                    default:
                        throw new JKLInternalError("Non-number while calculating numbers: '" + args[i].ToString(true));
                }
            }
            return new JKLNum(result);
        }

        //------------------ Builtins for Numbers and numeric ops -------------------------------------------

        public static JKLFunc BuiltInAdd = new JKLFunc(args =>
        {
            return ProcessNumbers(args, NumericOp.Plus);
        });

        public static JKLFunc BuiltInMultiply = new JKLFunc(args =>
        {
            return ProcessNumbers(args, NumericOp.Multiply);
        });

        public static JKLFunc BuiltInSubtract = new JKLFunc(args =>
        {
            return ProcessNumbers(args, NumericOp.Subtract);
        });

        public static JKLFunc BuiltInDivide = new JKLFunc(args =>
        {

            return ProcessNumbers(args, NumericOp.Divide);
        });

        //------------------ Constructor builtins for various types -------------------------------------------

        // Return a list containing the supplied args (already eval'd). Returns an empty list if no args.
        public static JKLFunc BuiltinList = new JKLFunc(args =>
        {
            JKLList newList = new JKLList();

            for (var i = 0; i < args.Count(); i++)
            {
                newList.Add(args[i]);
            }
            return newList;
        });

        // Return a Vector containing the supplied args (already eval'd). Returns an empty vector if no args.
        public static JKLFunc BuiltinVector = new JKLFunc(args =>
        {
            JKLVector newVec = new JKLVector();

            for (var i = 0; i < args.Count(); i++)
            {
                newVec.Add(args[i]);
            }
            return newVec;
        });

        // Utility for checking symbol and keyword names
        private static string GetValidName(JKLVal candidateName, string caller)
        {
            if (candidateName is JKLString mStr)
            {
                string name = mStr.unbox();
                if (name == "")
                {
                    // TODO - check for other illegal names.
                    throw new JKLEvalError(caller + " can't use the value '" + name + "'");
                }
                return name;
            }
            throw new JKLEvalError(caller + " expected a string but got ;" + candidateName.ToString(true) + "'");
        }

        // Given a valid string, create a symbol with that string as a name.
        public static JKLFunc BuiltinSymbol = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "symbol");
            return new JKLSym(GetValidName(args[0], "symbol"));
        });

        // Given a valid string, create a keyword using that string as a name.
        public static JKLFunc BuiltinKeyword = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "keyword");
            if (args[0] is JKLKeyword)
            {
                // It's already a keyword; nothing to do here.
                return args[0];
            }
            return new JKLSym(":" + GetValidName(args[0], "keyword"));
        });

        // Programmatic creation of hashmaps
        public static JKLFunc BuiltinHashMap = new JKLFunc(args =>
        {
            int argCount = args.Count();
            if (argCount % 2 != 0)
            {
                throw new JKLEvalError("hashmap requires an even number of elements forming key value pairs '" + args.ToString(true) + "'");
            }

            JKLHashMap builtHashMap = new JKLHashMap();
            for (int i = 0; i < argCount; i += 2)
            {
                JKLVal key = args[i];
                if (key is JKLString || key is JKLKeyword)
                {
                    builtHashMap.Add(key);
                    builtHashMap.Add(args[i + 1]);
                }
                else
                {
                    throw new JKLEvalError("hashmap: Expected a keyword or string as a HashMap key but got '" + key.ToString(true) + "'");
                }
            }
            return builtHashMap;
        });

        //------------------ Builtins for List manipulation -------------------------------------------

        public static JKLFunc BuiltinCount = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "count");
            if (args[0] is JKLSequence testSeq)
            {
                return new JKLNum(testSeq.Count());
            }
            else if (args[0] is JKLNil)
            {
                return new JKLNum(0);
            }
            else
            {
                throw new JKLEvalError("count expects a sequence or nil");

            }
        });

        // Take a list as its second parameter and return a new list that has the first argument
        // prepended to it. E.g. (cons 1 (list 2 3)) returns (1 2 3).
        public static JKLFunc BuiltinCons = new JKLFunc(args =>
        {
            JKLList newList = new JKLList();
            CheckArgCount(args, 2, "cons");

            // TODO - should nil be an error?
            // TODO - genericise
            if (args[1] is JKLSequence seqToCons)
            {
                newList.Add(args[0]);
                for (var i = 0; i < seqToCons.Count(); i++)
                {
                    newList.Add(seqToCons[i]);
                }
                return newList;
            }
            else
            {
                throw new JKLEvalError("cons expected a list but got '" + args[1].ToString(true) + "'");
            }
        });


        // Takes 0 or more sequences as parameters and return a list that is a concatenation
        // of all the input sequences. Excludes hashmaps. As a sideeffect this converts vectors
        // to lists.
        public static JKLFunc BuiltinConcat = new JKLFunc(args =>
        {
            JKLList newList = new JKLList();
            for (var i = 0; i < args.Count(); i++)
            {
                if (args[i] is JKLSequence seqToConcat)
                {
                    for (var j = 0; j < seqToConcat.Count(); j++)
                    {
                        newList.Add(seqToConcat[j]);
                    }
                }
                else
                {
                    throw new JKLEvalError("concat expected a sequence but got '" + args[i].ToString(true) + "'");
                }
            }
            return newList;
        });

        public static JKLFunc BuiltinNth = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 2, "nth");

            if (args[0] is JKLSequence mSeq)
            {
                if (args[1] is JKLNum i)
                {
                    int mSeqLength = mSeq.Count();
                    if ((int)i < 0)
                    {
                        throw new JKLEvalError("index '" + args[1].ToString(true) + "' to nth must be >= 0");
                    }
                    if ((int)i >= mSeqLength)
                    {
                        throw new JKLEvalError("index '" + args[1].ToString(true) + "' to nth exceeds sequence length " + mSeqLength);
                    }
                    return mSeq[(int)i];
                }
                throw new JKLEvalError("nth expected an index but got '" + args[1].ToString(true) + "'");
            }
            throw new JKLEvalError("nth expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        public static JKLFunc BuiltinFirst = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 1, "first");
            if (args[0] == jklNil)
            {
                return jklNil;
            }
            if (args[0] is JKLSequence mSeq)
            {
                if (mSeq.Count() == 0)
                {
                    return jklNil;
                }
                return mSeq[0];
            }
            throw new JKLEvalError("first expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        public static JKLFunc BuiltinRest = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 1, "rest");

            if (args[0] is JKLSequence mSeq)
            {
                return mSeq.Rest();
            }
            throw new JKLEvalError("rest expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        // Not in MAL but useful to have.
        public static JKLFunc BuiltinRemove = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 2, "remove");

            if (args[0] is JKLSequence mSeq)
            {
                return mSeq.Remove(args[1]);
            }
            throw new JKLEvalError("remove expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        public static JKLFunc BuiltinConj = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            //            CheckArgCount(args, 2, "nth");
            throw new JKLEvalError("BuiltinConj not implemented yet");

            //if (args[0] is JKLList mList)
            //{

            //}
            //return nth;
        });

        public static JKLFunc BuiltinSeq = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            //            CheckArgCount(args, 2, "nth");
            throw new JKLEvalError("BuiltinSeq not implemented yet");

            //if (args[0] is JKLList mList)
            //{

            //}
            //return nth;
        });

        //------------------ Builtins for boolean comparisons -------------------------------------------

        public static JKLFunc BuiltinEQ = new JKLFunc(args =>
        {
            CheckArgCount(args, 2, "=");
            return JKLVal.EQ(args[0], args[1]);
        });

        // Helper for functions that expect two numeric args.
        // TODO - handle monotonic argument lists
        private static void CheckComparisonParams(JKLList args, string op)
        {
            CheckArgCount(args, 2, op);

            if (!((object)args[0] is JKLNum))
            {
                throw new JKLEvalError("Number expected but got '" + args[0].ToString(true) + "'");
            }
            if (!((object)args[1] is JKLNum))
            {
                throw new JKLEvalError("Number expected but got '" + args[1].ToString(true) + "'");
            }
        }

        public static JKLFunc BuiltinLT = new JKLFunc(args =>
        {
            CheckComparisonParams(args, "<");
            if ((JKLNum)args[0] < (JKLNum)args[1])
            {
                return malTrue;
            }
            return jklFalse;
        });

        public static JKLFunc BuiltinLToE = new JKLFunc(args =>
        {
            CheckComparisonParams(args, "<=");
            if ((JKLNum)args[0] <= (JKLNum)args[1])
            {
                return malTrue;
            }
            return jklFalse;
        });

        public static JKLFunc BuiltinGT = new JKLFunc(args =>
        {
            CheckComparisonParams(args, ">");
            if ((JKLNum)args[0] > (JKLNum)args[1])
            {
                return malTrue;
            }
            return jklFalse;
        });

        public static JKLFunc BuiltinGToE = new JKLFunc(args =>
        {
            CheckComparisonParams(args, ">=");
            if ((JKLNum)args[0] >= (JKLNum)args[1])
            {
                return malTrue;
            }
            return jklFalse;
        });

        //------------------ Builtins for printing and reading -------------------------------------------

        // Calls pr_str on each argument with print_readably set to true, joins the results
        // with " " and returns the new string.
        public static JKLFunc BuiltinPrStr = new JKLFunc(args =>
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
            return new JKLString(sb.ToString());
        });

        // calls pr_str on each argument with print_readably set to false, concatenates the
        // results together ("" separator), and returns the new string.
        public static JKLFunc BuiltinStr = new JKLFunc(args =>
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < args.Count(); i++)
            {
                sb.Append(Printer.pr_str(args[i], false));
            }
            return new JKLString(sb.ToString());
        });

        // Calls pr_str on each argument with print_readably set to true, joins the results
        // with " ", prints the string to the screen and then returns nil.
        public static JKLFunc BuiltinPrn = new JKLFunc(args =>
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
            return jklNil;
        });

        // Calls pr_str on each argument with print_readably set to false, joins the results
        // with " ", prints the string to the screen and then returns nil.
        public static JKLFunc BuiltinPrintLn = new JKLFunc(args =>
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
            return jklNil;
        });

        public static JKLFunc BuiltinReadline = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 1, "readline");
            if (args[0] is JKLString mStrPrompt)
            {
                Console.Write(mStrPrompt.unbox());
                string line;

                line = Console.ReadLine();
                if (line == null)
                {
                    return jklNil;
                }
                return new JKLString(line.ToString());
            }
            throw new JKLEvalError("readline expected a string prompt but got '" + args[0].ToString(true) + "'");
        });

        // -------------------- Atoms ---------------------------------------------

        // Poor name, but consistent with Clojure. Support for mutable data.

        // atom: Takes a Mal value and returns a new atom which points to that Mal value.
        public static JKLFunc BuiltinAtom = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "atom");
            return new JKLAtom(args[0]);
        });

        // atom?: Takes an argument and returns true if the argument is an atom.
        public static JKLFunc BuiltinIsAtom = new JKLFunc(args =>
        {
            // args.Count()
            CheckArgCount(args, 1, "atom?");
            if ((object)args[0] is JKLAtom)
            {
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        // Takes an atom argument and returns the Mal value referenced by this atom.
        public static JKLFunc BuiltinDeref = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "deref");
            if (args[0] == null)
            {
                throw new JKLEvalError("deref expected atom but got '" + args[0] + "'");
            }
            if ((object)args[0] is JKLAtom atom)
            {
                return atom.Unbox();
            }
            throw new JKLEvalError("deref expected atom but got '" + args[0].ToString(true) + "'");
        });

        // Modify an atom to refer to a new Mal value, return the latter.
        public static JKLFunc BuiltinReset = new JKLFunc(args =>
        {
            CheckArgCount(args, 2, "reset");
            if ((object)args[0] is JKLAtom atom)
            {
                return atom.Reset(args[1]);
            }
            else
            {
                throw new JKLEvalError("reset expected atom but got '" + args[0].ToString(true) + "'");
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
        public static JKLFunc BuiltinSwap = new JKLFunc(args =>
        {
            if ((object)args[0] is JKLAtom atom)
            {
                if ((object)args[1] is JKLFunc func)
                {
                    // Create a list of args to be passed to the function passed to swap
                    JKLList fnArgs = new JKLList();

                    // Use the atom's current value as the first arg of the function.
                    fnArgs.Add(atom.Unbox());

                    // Use any remaining args to swap as additional args to the function.
                    for (var i = 2; i < args.Count(); i++)
                    {
                        fnArgs.Add(args[i]);
                    }

                    // Apply the function, using it as the atoms's new value;
                    JKLVal result = func.Apply(fnArgs);

                    // Store the new value in the atom and return the new value.
                    atom.Reset(result);
                    return result;
                }
                else
                {
                    throw new JKLEvalError("swap expected func but got '" + args[1].ToString(true) + "'");
                }
            }
            else
            {
                throw new JKLEvalError("swap expected atom but got '" + args[0].ToString(true) + "'");
            }
        });

        // --------------------Hashmap manipulation ---------------------------------------------

        // One implementation concern is how much to check that hashmaps are valid, i.e. contain
        // consecutive sets of key-value pairs. We could only check validity when the 
        // things are made (by constructor or assoc). In practice, most of the hashmap
        // manipulation functions do some degree of validation to prevent trappable errors.
        // The general principle is that hashmaps should be valid and that users will appreciate
        // warnings when they are attempting to assemble or use them incorrectly.

        // As per CL, hashmaps can contain duplicate keys, and the first will shadow the others.

        // Error checking utility for hashmaps.
        private static void ValidateHashMap(JKLVal candidate, string caller)
        {
            // Have we got a hashmap?
            if (candidate is JKLHashMap mHMap)
            {
                // Does it have an even number of entries?
                if (mHMap.Count() % 2 != 0)
                {
                    throw new JKLEvalError(caller + ": hashmap does not contain even key/value pairs '" + mHMap.ToString(true) + "'");
                }
                // Are all of the things that should be keys actually keys?
                for (int i = 0; i < mHMap.Count(); i += 2)
                {
                    JKLVal key = mHMap.Get(i);
                    if (!(key is JKLString || key is JKLKeyword))
                    {
                        throw new JKLEvalError(caller + " hashmap contains a non-key in a key position '" + key.ToString(true) + "'");
                    }
                }
                // If here, all okay, nothing to do.
            }
            else
            {
                throw new JKLEvalError(caller + ": expected a hashmap but got '" + candidate.ToString(true) + "'");
            }
        }

        // Common code for key searching. This searches for a key and if it is found returns true and passes
        // the key's value back via an out parameter. If the key is not found, returns false.
        // The  equality check is complicated by the fact that MAL allows hashmaps to use
        // both keywords and strings as hashmap keys. We therefore decide, on the basis of the type
        // of the target key (string or keyword) whether to do string or keyword equality checks. 
        // Also checks that the Target is a valid hashmap key but not that the keys in the hashmap
        // are valid. 
        private static bool _InnerHashmapGet(JKLVal hMapCandidate, JKLVal Target, string caller, out JKLVal val)
        {
            val = null;
            if (hMapCandidate is JKLHashMap hMap)
            {
                if (hMap.Count() % 2 != 0)
                {
                    // Check that the hashmap has even / odd pairs (should be keys and values).
                    throw new JKLEvalError(caller + ": hashmap does not contain even key/value pairs '" + hMap.ToString(true) + "'");
                }
                if (Target is JKLKeyword targetKeyWord)
                {
                    // The Target is a keyword, so only do keyword equality checks.
                    for (int i = 0; i < hMap.Count(); i += 2)
                    {
                        if (hMap.Get(i) is JKLKeyword testKeyWord)
                        {
                            if (targetKeyWord == testKeyWord)
                            {
                                val = hMap.Get(i + 1);
                                return true;
                            }
                        }
                    }
                    // Target not found. 
                    return false;
                }
                else if (Target is JKLString targetString)
                {
                    // The Target is a string, so only do string equality checks.
                    for (int i = 0; i < hMap.Count(); i += 2)
                    {
                        if (hMap.Get(i) is JKLString testString)
                        {
                            if (targetString == testString)
                            {
                                val = hMap.Get(i + 1);
                                return true;
                            }
                        }
                    }
                    // Target not found.
                    return false;
                }
                else
                {
                    // Target wasn't a keyword or a string. 
                    throw new JKLEvalError(caller + " expected a keyword or string as a HashMap key but got '" + Target.ToString(true) + "'");
                    // TODO - decide whether this could be 'false' rather than an actual error. 
                }
            }
            else
            {
                throw new JKLEvalError(caller + " expected hashmap but got '" + hMapCandidate + "'");
            }
        }


        // takes a hash-map as the first argument and the remaining arguments are odd/even key/value pairs
        // to "associate" (merge) into the hash-map. Note that the original hash-map is unchanged (remember,
        // JKL values are immutable), and a new hash-map containing the old hash-maps key/values plus the
        // merged key/value arguments is returned.
        // CL semantic are that new entries are added at the front and can shadow existing entries.
        public static JKLFunc BuiltinAssoc = new JKLFunc(args =>
        {
            ValidateHashMap(args[0], "assoc");
            JKLHashMap original = (JKLHashMap)args[0];
            JKLHashMap derivedHMap = new JKLHashMap();

            JKLList remaining = args.Rest();
            if (remaining.Count() > 0)
            {
                if (remaining.Count() % 2 != 0)
                {
                    // Check that the hashmap has even / odd pairs (should be keys and values).
                    throw new JKLEvalError("assoc : expected even-length list of keys and values but got '" + remaining.ToString(true) + "'");
                }
                for (int i = 0; i < remaining.Count(); i += 2)
                {
                    JKLVal key = remaining[i];
                    if (key is JKLString || key is JKLKeyword)
                    {
                        derivedHMap.Add(key);
                        derivedHMap.Add(remaining[i + 1]);
                    }
                    else
                    {
                        throw new JKLEvalError("assoc encountered non-key in assoc list '" + remaining[i].ToString(true) + "'");
                    }
                }
            }
            for (int i = 0; i < original.Count(); i += 2)
            {
                derivedHMap.Add(original[i]);
                derivedHMap.Add(original[i + 1]);
            }
            return derivedHMap;
        });

        // Takes a hash-map and a list of keys to dissociate from the hash-map. Returns 
        // a new hashmap without the specified keys and their values.
        // Keys that do not exist in the hash-map are ignored.
        // The CL-like semantics match assoc - if there are multiple (shadowed) identical keys
        // in the hashmap, only delete the first one. To delete multiple
        // identical keys, multiple dissoc keys are required.

        public static JKLFunc BuiltinDissoc = new JKLFunc(args =>
        {
            CheckArgCount(args, 2, "dissoc");
            if (args[0] is JKLHashMap hMap && args[1] is JKLSequence keysToDissoc)
            {
                ValidateHashMap(hMap, "dissoc");
                JKLHashMap resultHMap = new JKLHashMap();

                for (int i = 0; i < hMap.Count(); i += 2)
                {
                    if (keysToDissoc.Contains(hMap[i]))
                    {
                        // This hashmap key matches one of the dissoc keys.
                        // We 'remove' the key-val pair simply by not copying them to the result hmap.

                        // Delete the dissoc key from the dissoc list to 'inactivate' it.
                        keysToDissoc = keysToDissoc.Remove(hMap[i]);
                    }
                    else
                    {
                        // Key was never in the dissoc list, so copy <key,val> into the result hashmap.
                        resultHMap.Add(hMap[i]);
                        resultHMap.Add(hMap[i + 1]);
                    }
                }
                return resultHMap;
            }
            throw new JKLEvalError("dissoc expected hashmap and key list but got '" + args[0] + "' and '" + args[1] + "'");
        });

        // takes a hash-map and a key and returns the value of looking up that key in the hash-map, or nil.
        public static JKLFunc BuiltinHashMapGet = new JKLFunc(args =>
        {
            CheckArgCount(args, 2, "get?");
            if (args[0] == jklNil)
            {
                return jklNil;

            }
            if (_InnerHashmapGet(args[0], args[1], "get?", out JKLVal val))
            {
                return val;
            }
            else
            {
                return jklNil;
            }
        });

        // Returns malTrue if a specified key is in the hash-map, jklFalse otherwise.
        public static JKLFunc BuiltinHashMapContainsP = new JKLFunc(args =>
        {
            CheckArgCount(args, 2, "contains?");
            if (_InnerHashmapGet(args[0], args[1], "contains?", out JKLVal val))
            {
                // The value is actually in val but the caller isn't interested.
                return malTrue;
            }
            else
            {
                return jklFalse;
            }
        });

        // Returns a list of all the keys in a hash-map.
        public static JKLFunc BuiltinHashMapKeys = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "keys");
            if (args[0] is JKLHashMap hMap)
            {
                JKLList keys = new JKLList();
                for (int i = 0; i < hMap.Count(); i += 2)
                {
                    keys.Add(hMap[i]);
                }
                return keys;
            }
            else
            {
                throw new JKLEvalError("keys expected a hashMap but got '" + args[0].ToString(true) + "'");
            }
        });

        // Returns a JKL list of all the values in a hash-map.
        public static JKLFunc BuiltinHashMapVals = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "vals");
            if (args[0] is JKLHashMap hMap)
            {
                JKLList vals = new JKLList();
                for (int i = 0; i < hMap.Count(); i += 2)
                {
                    vals.Add(hMap[i + 1]);
                }
                return vals;
            }
            else
            {
                throw new JKLEvalError("vals expected a hashMap but got '" + args[0].ToString(true) + "'");
            }
        });

        // -------------------- File support ---------------------------------------------

        // Exposes the read_str function from the reader.
        public static JKLFunc BuiltinReadString = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "read-string");
            if ((object)args[0] is JKLString s)
            {
                return read_str(s.unbox());
            }
            else
            {
                throw new JKLEvalError("read-string expected string but got '" + args[0].ToString(true) + "'");
            }
        });

        // Utility used by slurp and slurp-do
        public static string InnerSlurp(JKLString s, string callerID)
        {
            try
            {
                string FileText = File.ReadAllText(s.unbox());
                return FileText;
            }
            catch (Exception e)
            {
                throw new JKLEvalError(callerID + ": " + e.Message);
            }
        }

        // Read a specified file and return it as a single Mal string.
        public static JKLFunc BuiltinSlurp = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "slurp");
            if ((object)args[0] is JKLString s)
            {
                return new JKLString(InnerSlurp(s, "slurp"));
            }
            else
            {
                throw new JKLEvalError("slurp expected filename string but got '" + args[0].ToString(true) + "'");
            }
        });

        // Read a specified file and return it as a single Mal string. Unlike slurp,
        // this wraps a (do...) form around the file contents so that they can be
        // processed  by a single JKL read-string, without the caller having to
        // faff around to achieve the same effect.
        public static JKLFunc BuiltinSlurpDo = new JKLFunc(args =>
        {
            CheckArgCount(args, 1, "slurp-do");
            if ((object)args[0] is JKLString s)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("(do ");
                sb.Append(InnerSlurp(s, "slurp-do"));
                sb.Append(" )");

                return new JKLString(sb.ToString());
            }
            else
            {
                throw new JKLEvalError("slurp-do expected filename string but got '" + args[0].ToString(true) + "'");
            }
        });

        // -------------------- Exception handling ---------------------------------------------

        public static JKLFunc BuiltinThrow = new JKLFunc(args =>
        {
            // Part of the Mal exception handling mechanism.
            CheckArgCount(args, 1, "throw");

            if ((object)args[0] is JKLVal mv)
            {
                throw new JKLHostedLispError(mv);
            }
            throw new JKLEvalError("throw expected Mal value but got '" + args[0].ToString(true) + "'");
        });

        // -------------------- Map, Apply and friends ---------------------------------------------

        public static JKLFunc BuiltinMap = new JKLFunc(args =>
        {
            CheckArgCount(args, 2, "map");
            if (args[0] is JKLFunc f)
            {
                if (args[1] is JKLSequence ms)
                {
                    JKLList mapResult = new JKLList();
                    for (var i = 0; i < ms.Count(); i++)
                    {
                        // Make the arg into an arg list for function application.
                        JKLList ml = new JKLList();
                        ml.Add(ms[i]);

                        // Apply the function to its single argument and store the result.
                        mapResult.Add(f.Apply(ml));
                    }
                    return mapResult;
                }
                throw new JKLEvalError("map: expected sequence but got '" + args[1].ToString(true) + "'");
            }
            throw new JKLEvalError("map: expected function but got '" + args[0].ToString(true) + "'");
        });

        public static JKLFunc BuiltinApply = new JKLFunc(args =>
        {
            // E.g. (apply + 4 [5]) ==> 9
            int argCount = args.Count();
            if (argCount == 0)
            {
                throw new JKLEvalError("apply - expected function and arguments");
            }
            if (argCount == 1)
            {
                throw new JKLEvalError("apply '" + args[0].ToString(true) + "': no args to apply");
            }
            if (args[0] is JKLFunc f)
            {
                JKLList ApplyCall = new JKLList();
                for (var i = 1; i < argCount; i++)
                {
                    // Add all of the args to the apply call
                    if (args[i] is JKLSequence ms)
                    {
                        // If one of the args is a sequence, flatten it.
                        int msCount = ms.Count();
                        for (var msI = 0; msI < msCount; msI++)
                        {
                            ApplyCall.Add(ms[msI]);
                        }
                    }
                    else
                    {
                        ApplyCall.Add(args[i]);
                    }
                }
                // Now that we have built an arg list, apply the function f to it.
                return f.Apply(ApplyCall);

            }
            throw new JKLEvalError("apply: expected function but got '" + args[0].ToString(true) + "'");
        });

        // --------------------- Meta data -------------------------------------------------------
        public static JKLFunc BuiltinMeta = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            // CheckArgCount(args, 2, "nth");
            throw new JKLEvalError("BuiltinMeta not implemented yet");

            //if (args[0] is JKLList mList)
            //{

            //}
            //return nth;
        });

        public static JKLFunc BuiltinWithMeta = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            // CheckArgCount(args, 2, "nth");
            throw new JKLEvalError("BuiltinWithMeta not implemented yet");

            //if (args[0] is JKLList mList)
            //{

            //}
            //return nth;
        });

        // --------------------- Various -------------------------------------------------------

        public static JKLFunc BuiltinTimeMS = new JKLFunc(args =>
        {
            // copy this to get a builtin.
            throw new JKLEvalError("BuiltinTimeMS not implemented yet");
            CheckArgCount(args, 2, "nth");

            //if (args[0] is JKLList mList)
            //{

            //}
            //return nth;
        });

        // -------------------- The Mal Namespace ---------------------------------------------

        // The Mal Namespace to be loaded by the main REPL.
        // I've taken off the '*' and '!' chars from the special forms.

        public static Dictionary<string, JKLFunc> JKLNameSpace = new Dictionary<string, JKLFunc>
        {
            // Predicates.
            {"nil?", BuiltinNilP},
            {"true?", BuiltinTrueP},
            {"false?", BuiltinFalseP},
            {"symbol?", BuiltinSymbolP},
            {"list?", BuiltinIsListP},
            {"empty?", BuiltinEmptyP},
            {"keyword?", BuiltinIsKeyWordP},
            {"vector?", BuiltinIsVectorP},
            {"map?", BuiltinIsMapP},
            {"sequential?", BuiltinIsSequentialP},
            {"number?", BuiltinIsNumberP},
            {"string?", BuiltinIsStringP},

            // Arithmetic.
            {"+", BuiltInAdd},
            {"*", BuiltInMultiply},
            {"-", BuiltInSubtract },
            {"/", BuiltInDivide},

            // Constructors.
            {"list", BuiltinList},
            {"vector", BuiltinVector},
            {"symbol", BuiltinSymbol},
            {"keyword", BuiltinKeyword},
            {"hash-map", BuiltinHashMap},

            // List manipulation.
            {"count", BuiltinCount},
            {"cons", BuiltinCons},
            {"concat", BuiltinConcat},
            {"nth", BuiltinNth },
            {"first", BuiltinFirst },
            {"rest", BuiltinRest },
            {"remove", BuiltinRemove },
            {"conj", BuiltinConj },
            {"seq", BuiltinSeq },

            // Equality and friends.
            {"=", BuiltinEQ},
            {"<", BuiltinLT},
            {"<=", BuiltinLToE},
            {">", BuiltinGT},
            {">=", BuiltinGToE},

            // Printing and reading
            {"pr-str", BuiltinPrStr},
            {"str", BuiltinStr},
            {"prn", BuiltinPrn},
            {"println", BuiltinPrintLn},
            {"readline", BuiltinReadline},


            // Atoms.
            {"atom", BuiltinAtom},
            {"atom?", BuiltinIsAtom},
            {"deref", BuiltinDeref},
            {"reset!", BuiltinReset},
            {"swap!", BuiltinSwap},

            // Hashmap manipulation.
            {"assoc", BuiltinAssoc},
            {"dissoc", BuiltinDissoc},
            {"get", BuiltinHashMapGet},
            {"contains?", BuiltinHashMapContainsP},
            {"keys", BuiltinHashMapKeys},
            {"vals", BuiltinHashMapVals},

            // File support.
            {"read-string", BuiltinReadString},
            {"slurp", BuiltinSlurp},
            {"slurp-do", BuiltinSlurpDo},

            // Exception handling
            {"throw", BuiltinThrow},
            
            // Map, Apply and friends
            {"map", BuiltinMap},
            {"apply", BuiltinApply},

            // Meta-data
            {"meta", BuiltinMeta},
            {"with-meta", BuiltinWithMeta},
            
            // misc
            {"time-ms", BuiltinTimeMS}

            // eval => BuiltInEval is defined in the REPL so that the closure can access the REPL's env.
        };

    }
}
