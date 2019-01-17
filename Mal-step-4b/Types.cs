using System;
using System.Collections.Generic;
using System.Text;
// Mal;
//using static Mal.Types;
using static Mal.Env;
using static Mal.Core;

namespace Mal
{
    // The various types (symbols, lists, numbers, etc) understood by Mal.
    public class Types
    {
        // TODO - see C# docs = need to add all four exception constructors.
        // Mal exceptions.
        public class MalException : Exception
        {
            public MalException(): base("Unknown MAL exception")
            {
                // Nothing more to construct here.
            }

            public MalException(string message) : base(message)
            {
                // Nothing more to construct here.
            }
        }

        public class MalParseError : MalException
        {
            public MalParseError(string message) : base("Read error: " + message) { }
        }

        public class MalEvalError : MalException
        {
            public MalEvalError(string message) : base("Eval error: " + message) { }
        }

        public class MalInternalError : MalException
        {
            public MalInternalError(string message) : base("Internal error: " + message) { }
        }
        
        // Base class for the Mal type system.
        public abstract class MalVal
        {
            public virtual string ToString(bool printReadably)
            {
                // Should have been overriden, but just in case.
                return "Abstract-MalVal";
            }
        }

        // Base class for lists, vectors and other sequences.
        // JK approach. In the C#-Mal reference, Vector is derived from List, not Sequence.
        public abstract class MalSeq : MalVal
        {
            // They are all lists under the skin.
            protected List<MalVal> MyElements;

            protected MalSeq()
            {
                MyElements = new List<MalVal>();
            }

            protected string PrintSeq(bool printReadably, char startChar, char endChar)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(startChar);
                foreach (MalVal mv in MyElements)
                {
                    sb.Append(mv.ToString(printReadably));
                    sb.Append(" ");
                }
                sb.Append(endChar);

                return sb.ToString();
            }

            public void Add(MalVal newVal)
            {
                MyElements.Add(newVal);
            }

            public int Count()
            {
                // TODO - check whether this is correct for a hashmap. If not, override.
                return MyElements.Count;
            }

            // Return the first element. Equivalent to CL's (CAR..).
            public MalVal First()
            {
                if(MyElements.Count > 0)
                {
                    return MyElements[0];
                }
                else
                {
                    return new MalNil();
                }
            }

            // Return the tail of the list. Equivalent to CL's (CDR..).
            public MalList Rest()
            {
                if (MyElements.Count > 0)
                {
                    // We return a MalList, not a List<MalVal>.
                    MalList newList = new MalList();

                    foreach( var element in MyElements.GetRange(1, MyElements.Count - 1))
                    {
                        newList.Add(element);
                    }
                    return newList;
                }
                else
                {
                    return new MalList();
                }
            }

            public MalVal Get(int index)
            {
                if( index < 0 || index >= MyElements.Count)
                {
                    throw new MalEvalError("Attempt to access element " + index.ToString() + " of a list with " + MyElements.Count.ToString() + "elements");
                }
                return MyElements[index];
            }

            // Indexer that allows range checked array-style access.
            public MalVal this[int index]
            {
                get
                {
                    if (index < 0 || index >= MyElements.Count)
                    {
                        throw new MalEvalError("Attempt to access element " + index.ToString() + " of a sequence with " + MyElements.Count.ToString() + "elements");
                    }
                    return MyElements[index];
                }
                set
                {
                    if (index < 0 || index >= MyElements.Count)
                    {
                        throw new MalEvalError("Attempt to access element " + index.ToString() + " of a sequence with " + MyElements.Count.ToString() + "elements");
                    }
                    MyElements[index] = value;
                }
            }
        }

        // Derived class for Mal lists
        public class MalList : MalSeq
        {
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '(', ')');
            }

            // Default constuctor - for an empty list.
            public MalList()
            {
                // Base class does the heavy lifting.
            }

            // Constructor for internal use, e.g. when read_form handles a MalQuote.
            public MalList(MalVal firstElement, MalVal secondElement)
            {
                // TODO - explore var arg lists if required.
                Add(firstElement);
                Add(secondElement);
            }
        }

        // Derived class for Mal vectors
        public class MalVector : MalSeq
        {
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '[', ']');
            }
        }

        // Derived class for Mal vectors
        public class MalHashMap : MalSeq
        {
            // TODO - the reader should actually be able to construct these
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '{', '}');
            }
        }

        // Derived class for Mal symbols
        public class MalSym : MalVal
        {
            private readonly string MySymbol;

            public MalSym(string symbol)
            {
                MySymbol = symbol;
            }

            public override string ToString(bool printReadably)
            {
                return MySymbol;
            }

            public string getName() { return MySymbol; }




            //public static bool operator ==(MalSym a, MalSym b)
            //{
            //    return a.MySymbol == b.MySymbol;
            //}
            //public static bool operator !=(MalSym a, MalSym b)
            //{
            //    return a.MySymbol != b.MySymbol;
            //}
        }

        // Derived class for Mal numbers. The C#-Mal reference doesn't distinguish
        // between ints and floats. 
        public class MalNumber : MalVal
        {
            // Nothing yet, just a base class for convenience.
        }

        // Derived class for Mal integers. Not in the reference implementation.
        public class MalInt : MalNumber
        {
            private int MyInt;

            public MalInt(int number)
            {
                MyInt = number;
            }

            public override string ToString(bool printReadably)
            {
                return MyInt.ToString();
            }

            public static explicit operator int (MalInt n)
            {
                return n.MyInt;
            }
            public static explicit operator float(MalInt n)
            {
                return n.MyInt;
            }
            public static MalInt operator +(MalInt a, MalInt b)
            {
                return new MalInt(a.MyInt + b.MyInt);
            }
        }

        // Derived class for Mal floats. Not in the reference implementation.
        public class MalFloat : MalVal
        {
            private float MyFloat;

            public MalFloat(float number)
            {
                MyFloat = number;
            }

            public override string ToString(bool printReadably)
            {
                return MyFloat.ToString();
            }

            public static explicit operator float(MalFloat n)
            {
                return n.MyFloat;
            }
        }

        // Derived class for Mal nil
        public class MalNil : MalVal
        {
            public override string ToString(bool printReadably)
            {
                return "nil";
            }
        }

        // Derived class for Mal false
        public class MalFalse : MalVal
        {
            public override string ToString(bool printReadably)
            {
                return "false";
            }
        }

        // Derived class for Mal true
        public class MalTrue : MalVal
        {
            public override string ToString(bool printReadably)
            {
                return "true";
            }
        }

        // Derived class for Mal strings.
        public class MalString : MalVal
        {
            private string MyValue;

            public MalString(string str)
            {
                MyValue = str;
            }

            public override string ToString()
            {
                // Copied from the reference version.
                return "\"" + MyValue + "\"";
            }

            public override string ToString(bool print_readably)
            {
                // Varies from the reference version which mixes up strings and keywords.
                if (print_readably)
                {
                    return "\"" + MyValue.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n") + "\"";
                }
                else
                {
                    return MyValue;
                }
            }
        }

        // Derived class for Mal keywords. The C#-Mal Ref uses annnotated strings instead.
        public class MalKeyword : MalVal
        {
            private string MyKeyword;

            public MalKeyword(string str)
            {
                MyKeyword = str;
            }

            public override string ToString(bool printReadably)
            {
                return MyKeyword;
            }
        }

        public class MalQuote : MalVal
        {
            // Doesn't have its own value.
            public override string ToString(bool printReadably)
            {
                return "'";
            }
        }

        // Derived class to hold Mal functions, both core and created by Def*. I admit,
        // I looked at C#-Mal to get this right.
        public class MalFunc : MalVal
        {
            // From C#-Mal
            Func<MalList, MalVal> fn = null;
            MalVal ast = null;
            Env env = null;
            MalList fparams = null;

            // This says it's a lambda that takes a MalList (its args) and returns a MalVal
            public MalFunc(Func<MalList, MalVal> fn)
            {
                this.fn = fn;
            }

            public MalFunc(MalVal ast, Env e, MalList fparams, Func<MalList, MalVal> fn)
            {
                this.ast = ast;
                this.env = e;
                this.fparams = fparams;
                this.fn = fn;
            }

            public override string ToString(bool printReadably)
            {
                if (ast != null)
                {
                    return "<fn " + Printer.pr_str(fparams, true) +
                           " " + Printer.pr_str(ast, true) + ">";
                }
                else
                {
                    return "<builtin_function " + fn.ToString() + ">";
                }
            }

            // Call the stored func, passing it a list containing args, if any.
            public MalVal Apply(MalList args)
            {
                return fn(args);
            }
        }
    }
}
