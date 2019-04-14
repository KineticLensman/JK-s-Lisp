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
        public static MalVarArgs malVarArgsChar = new MalVarArgs();

        //------------------ General utilities -------------------------------------------
        // Generic error check for core fns.
        public static void CheckArgCount(MalList args, int expectedCount, string callerName)
        {
            if (args.Count() != expectedCount)
            {
                throw new MalEvalError("'" + callerName + "' expected " + expectedCount + " arg(s) but got " + args.Count());
            }
        }

        public static MalFunc BuiltinXXXTemplate = new MalFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 2, "nth");
            throw new MalEvalError("BuiltinXXXTemplate not implemented yet");

            //if (args[0] is MalList mList)
            //{

            //}
            //return nth;
        });

        //------------------ Predicates -------------------------------------------

        public static MalFunc BuiltinNilP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "nil?");
            if (args[0] == malNil)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinTrueP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "true?");
            if (args[0] == malTrue)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinFalseP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "false?");
            if (args[0] == malFalse)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinSymbolP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "symbol?");
            if (args[0] is MalSym)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        // Return true if first param is a List, otherwise false.
        public static MalFunc BuiltinIsListP = new MalFunc(args =>
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

        public static MalFunc BuiltinEmptyP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "empty?");
            if (args[0] is MalSequence testSeq)
            {
                if (testSeq.Count() == 0)
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
                throw new MalEvalError("empty? expected a sequence but got '" + args[0].ToString(true) + "'");
            }
        });

        // Return true if first param is a List, otherwise false.
        public static MalFunc BuiltinIsKeyWordP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "keyword?");
            if (args[0] is MalKeyword)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinIsVectorP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "vector?");
            if (args[0] is MalVector)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinIsMapP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "map?");
            if (args[0] is MalHashMap)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        public static MalFunc BuiltinIsSequentialP = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "seq?");
            if (args[0] is MalSequence)
            {
                return malTrue;
            }
            else
            {
                return malFalse;
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
                    throw new MalParseError("Non-number while calculating numbers: '" + args[0].ToString(true) + "'");
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

        //------------------ Constructor builtins for various types -------------------------------------------

        // Return a list containing the supplied args (already eval'd). Returns an empty list if no args.
        public static MalFunc BuiltinList = new MalFunc(args =>
        {
            MalList newList = new MalList();

            for (var i = 0; i < args.Count(); i++)
            {
                newList.Add(args[i]);
            }
            return newList;
        });

        // Return a Vector containing the supplied args (already eval'd). Returns an empty vector if no args.
        public static MalFunc BuiltinVector = new MalFunc(args =>
        {
            MalVector newVec = new MalVector();

            for (var i = 0; i < args.Count(); i++)
            {
                newVec.Add(args[i]);
            }
            return newVec;
        });

        // Utility for checking symbol and keyword names
        private static string GetValidName(MalVal candidateName, string caller)
        {
            if (candidateName is MalString mStr)
            {
                string name = mStr.unbox();
                if (name == "")
                {
                    // TODO - check for other illegal names.
                    throw new MalEvalError(caller + " can't use the value '" + name + "'");
                }
                return name;
            }
            throw new MalEvalError(caller + " expected a string but got ;" + candidateName.ToString(true) + "'");
        }

        // Given a valid string, create a symbol with that string as a name.
        public static MalFunc BuiltinSymbol = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "symbol");
            return new MalSym(GetValidName(args[0], "symbol"));
        });

        // Given a valid string, create a keyword using that string as a name.
        public static MalFunc BuiltinKeyword = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "keyword");
            if(args[0] is MalKeyword)
            {
                // It's already a keyword; nothing to do here.
                return args[0];
            }
            return new MalSym(":" + GetValidName(args[0], "keyword"));
        });

        // Programmatic creation of hashmaps
        public static MalFunc BuiltinHashMap = new MalFunc(args =>
        {
            // CheckArgCount(args, 1, "count");
            // args[0]
            throw new MalInternalError("BuiltinHashMap not implemented yet");
            return new MalSym(":" + GetValidName(args[0], "keyword"));
        });

        //------------------ Builtins for List manipulation -------------------------------------------

        public static MalFunc BuiltinCount = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "count");
            if (args[0] is MalSequence testSeq)
            {
                return new MalNum(testSeq.Count());
            }
            else if (args[0] is MalNil)
            {
                return new MalNum(0);
            }
            else
            {
                throw new MalEvalError("count expects a sequence or nil");

            }
        });

        // Take a list as its second parameter and return a new list that has the first argument
        // prepended to it. E.g. (cons 1 (list 2 3)) returns (1 2 3).
        public static MalFunc BuiltinCons = new MalFunc(args =>
        {
            MalList newList = new MalList();
            CheckArgCount(args, 2, "cons");

            // TODO - should nil be an error?
            // TODO - genericise
            if (args[1] is MalSequence seqToCons)
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
                throw new MalEvalError("cons expected a list but got '" + args[1].ToString(true) + "'");
            }
        });


        // Takes 0 or more sequences as parameters and return a list that is a concatenation
        // of all the input sequences. Excludes hashmaps. As a sideeffect this converts vectors
        // to lists.
        public static MalFunc BuiltinConcat = new MalFunc(args =>
        {
            MalList newList = new MalList();
            for (var i = 0; i < args.Count(); i++)
            {
                if (args[i] is MalSequence seqToConcat)
                {
                    for (var j = 0; j < seqToConcat.Count(); j++)
                    {
                        newList.Add(seqToConcat[j]);
                    }
                }
                else
                {
                    throw new MalEvalError("concat expected a sequence but got '" + args[i].ToString(true) + "'");
                }
            }
            return newList;
        });

        public static MalFunc BuiltinNth = new MalFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 2, "nth");

            if (args[0] is MalSequence mSeq)
            {
                if(args[1] is MalNum i)
                {
                    int mSeqLength = mSeq.Count();
                    if ((int)i < 0)
                    {
                        throw new MalEvalError("index '" + args[1].ToString(true) + "' to nth must be >= 0");
                    }
                    if ((int)i >= mSeqLength)
                    {
                        throw new MalEvalError("index '" + args[1].ToString(true) + "' to nth exceeds sequence length " + mSeqLength);
                    }
                    return mSeq[(int)i];
                }
                throw new MalEvalError("nth expected an index but got '" + args[1].ToString(true) + "'");
            }
            throw new MalEvalError("nth expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        public static MalFunc BuiltinFirst = new MalFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 1, "first");
            if (args[0] == malNil)
            {
                return malNil;
            }
            if (args[0] is MalSequence mSeq)
            {
                if(mSeq.Count() == 0)
                {
                    return malNil;
                }
                return mSeq[0];
            }
            throw new MalEvalError("first expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        public static MalFunc BuiltinRest = new MalFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 1, "rest");

            if (args[0] is MalSequence mSeq)
            {
                return mSeq.Rest();
            }
            throw new MalEvalError("rest expected a sequence but got '" + args[0].ToString(true) + "'");
        });

        // Not in MAL but useful to have.
        public static MalFunc BuiltinRemove = new MalFunc(args =>
        {
            // copy this to get a builtin.
            CheckArgCount(args, 2, "remove");

            if (args[0] is MalSequence mSeq)
            {
                return mSeq.Remove(args[1]);
            }
            throw new MalEvalError("remove expected a sequence but got '" + args[0].ToString(true) + "'");
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
                //if (i > 0)
                //{
                //    sb.Append("");
                //}
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

        // Poor name, but consistent with Clojure. Support for mutable data.

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

        // Takes an atom argument and returns the Mal value referenced by this atom.
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

        // --------------------Hashmap manipulation ---------------------------------------------

        // One implementation concern is how much to check that hashmaps are valid, i.e. contain
        // consecutive sets of key-value pairs. We could only check validity when the 
        // things are made (by constructor or assoc). In practice, most of the hashmap
        // manipulation functions do some degree of validation to prevent trappable errors.
        // The general principle is that hashmaps should be valid and that users will appreciate
        // warnings when they are attempting to assemble or use them incorrectly.

        // As per CL, hashmaps can contain duplicate keys, and the first will shadow the others.

        // Error checking utility for hashmaps.
        private static void ValidateHashMap (MalVal candidate, string caller)
        {
            // Have we got a hashmap?
            if(candidate is MalHashMap mHMap)
            {
                // Does it have an even number of entries?
                if (mHMap.Count() % 2 != 0)
                {
                    throw new MalEvalError(caller + ": hashmap does not contain even key/value pairs '" + mHMap.ToString(true) + "'");
                }
                // Are all of the things that should be keys actually keys?
                for (int i = 0; i < mHMap.Count(); i += 2)
                {
                    MalVal key = mHMap.Get(i);
                    if ( !(key is MalString || key is MalKeyword))
                    {
                        throw new MalEvalError(caller + " hashmap contains a non-key in a key position '" + key.ToString(true) + "'");
                    }
                }
                // If here, all okay, nothing to do.
            }
            else
            {
                throw new MalEvalError(caller + ": expected a hashmap but got '" + candidate.ToString(true) + "'");
            }
        }

        // Common code for key searching. This searches for a key and if it is found returns true and passes
        // the key's value back via an out parameter. If the key is not found, returns false.
        // The  equality check is complicated by the fact that MAL allows hashmaps to use
        // both keywords and strings as hashmap keys. We therefore decide, on the basis of the type
        // of the target key (string or keyword) whether to do string or keyword equality checks. 
        // Also checks that the Target is a valid hashmap key but not that the keys in the hashmap
        // are valid. 
        private static bool _InnerHashmapGet(MalVal hMapCandidate, MalVal Target, string caller, out MalVal val)
        {
            val = null;
            if (hMapCandidate is MalHashMap hMap)
            {
                if (hMap.Count() % 2 != 0)
                {
                    // Check that the hashmap has even / odd pairs (should be keys and values).
                    throw new MalEvalError(caller + ": hashmap does not contain even key/value pairs '" + hMap.ToString(true) + "'");
                }
                if (Target is MalKeyword targetKeyWord)
                {
                    // The Target is a keyword, so only do keyword equality checks.
                    for (int i = 0; i < hMap.Count(); i += 2)
                    {
                        if (hMap.Get(i) is MalKeyword testKeyWord)
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
                else if (Target is MalString targetString)
                {
                    // The Target is a string, so only do string equality checks.
                    for (int i = 0; i < hMap.Count(); i += 2)
                    {
                        if (hMap.Get(i) is MalString testString)
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
                    throw new MalEvalError(caller + " expected a keyword or string as a HashMap key but got '" + Target.ToString(true) + "'");
                    // TODO - decide whether this could be 'false' rather than an actual error. 
                }
            }
            else
            {
                throw new MalEvalError(caller + " expected hashmap but got '" + hMapCandidate + "'");
            }
        }


        // takes a hash-map as the first argument and the remaining arguments are odd/even key/value pairs to "associate" (merge) into the hash-map. Note that the original hash-map is unchanged (remember, mal values are immutable), and a new hash-map containing the old hash-maps key/values plus the merged key/value arguments is returned
        // // CL semantic are that new entries are added at the front and can shadow existing entries.
        public static MalFunc BuiltinAssoc = new MalFunc(args =>
        {
            ValidateHashMap(args[0], "assoc");
            MalHashMap original = (MalHashMap)args[0];
            MalHashMap derivedHMap = new MalHashMap();

            MalList remaining = args.Rest();
            if(remaining.Count() > 0)
            {
                if (remaining.Count() % 2 != 0)
                {
                    // Check that the hashmap has even / odd pairs (should be keys and values).
                    throw new MalEvalError("assoc : expected even-length list of keys and values but got '" + remaining.ToString(true) + "'");
                }
                for (int i = 0; i < remaining.Count(); i += 2)
                {
                    MalVal key = remaining[i];
                    if (key is MalString || key is MalKeyword)
                    {
                        derivedHMap.Add(key);
                        derivedHMap.Add(remaining[i + 1]);
                    }
                    else
                    {
                        throw new MalEvalError("assoc encountered non-key in assoc list '" + remaining[i].ToString(true) + "'");
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

        public static MalFunc BuiltinDissoc = new MalFunc(args =>
        {
            CheckArgCount(args, 2, "dissoc");
            if( args[0] is MalHashMap hMap && args[1] is MalSequence keysToDissoc)
            {
                ValidateHashMap(hMap, "dissoc");
                MalHashMap resultHMap = new MalHashMap();

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
            throw new MalEvalError("dissoc expected hashmap and key list but got '" + args[0] + "' and '" + args[1] + "'");
        });



        // takes a hash-map and a key and returns the value of looking up that key in the hash-map, or nil.
        public static MalFunc BuiltinHashMapGet = new MalFunc(args =>
        {
            CheckArgCount(args, 2, "get?");
            if (_InnerHashmapGet(args[0], args[1], "get?", out MalVal val))
            {
                return val;
            }
            else
            {
                return malNil;
            }
        });

        // Returns malTrue if a specified key is in the hash-map, malFalse otherwise.
        public static MalFunc BuiltinHashMapContainsP = new MalFunc(args =>
        {
            CheckArgCount(args, 2, "contains?");
            if (_InnerHashmapGet(args[0], args[1], "contains?", out MalVal val))
            {
                // The value is actually in val but the caller isn't interested.
                return malTrue;
            }
            else
            {
                return malFalse;
            }
        });

        // Returns a mal list of all the keys in a hash-map.
        public static MalFunc BuiltinHashMapKeys = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "keys");
            if (args[0] is MalHashMap hMap)
            {
                MalList keys = new MalList();
                for (int i = 0; i < hMap.Count(); i += 2)
                {
                    keys.Add(hMap[i]);
                }
                return keys;
            }
            else
            {
                throw new MalEvalError("keys expected a hashMap but got '" + args[0].ToString(true) + "'");
            }
        });

        // Returns a mal list of all the values in a hash-map.
        public static MalFunc BuiltinHashMapVals = new MalFunc(args =>
        {
            CheckArgCount(args, 1, "vals");
            if (args[0] is MalHashMap hMap)
            {
                MalList vals = new MalList();
                for (int i = 0; i < hMap.Count(); i += 2)
                {
                    vals.Add(hMap[i + 1]);
                }
                return vals;
            }
            else
            {
                throw new MalEvalError("vals expected a hashMap but got '" + args[0].ToString(true) + "'");
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

        // -------------------- The Mal Namespace ---------------------------------------------

        // The Mal Namespace to be loaded by the main REPL.
        // I've taken off the '*' and '!' chars from the special forms.

        public static Dictionary<string, MalFunc> MalNameSpace = new Dictionary<string, MalFunc>
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
            {"seq?", BuiltinIsSequentialP},

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
            {"hashmap", BuiltinHashMap},

            // List manipulation.
            {"count", BuiltinCount},
            {"cons", BuiltinCons},
            {"concat", BuiltinConcat},
            {"nth", BuiltinNth },
            {"first", BuiltinFirst },
            {"rest", BuiltinRest },
            {"remove", BuiltinRemove },

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

            // Hashmap manipulation.
            {"assoc", BuiltinAssoc},
            {"dissoc", BuiltinDissoc},
            {"get", BuiltinHashMapGet},
            {"contains?", BuiltinHashMapContainsP},
            {"keys", BuiltinHashMapKeys},
            {"vals", BuiltinHashMapVals},


            // File support.
            {"read-string", BuiltinReadString},
            {"slurp", BuiltinSlurp}

            // eval => BuiltInEval is defined in the REPL so that the closure can access the REPL's env.
        };

    }
}
