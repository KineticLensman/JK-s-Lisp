using System;
using System.Collections.Generic;
using System.Text;

using static JKLApp.Core;

namespace JKLApp
{
    // The various types (symbols, lists, numbers, etc) understood by JKL.
    public class Types
    {
        // TODO - see C# docs = need to add all four exception constructors.
        // JKL exceptions.
        public class JKLException : Exception
        {
            public JKLException() : base("Unknown JKL exception")
            {
                // Nothing more to construct here.
            }

            public JKLException(string message) : base(message)
            {
                // Nothing more to construct here.
            }
        }

        // Errors trapped by the reader.
        public class JKLParseError : JKLException
        {
            public JKLParseError(string message) : base("Read error: " + message) { }
        }

        // Errors trapped during EVAL.
        public class JKLEvalError : JKLException
        {
            public JKLEvalError(string message) : base("Eval error: " + message) { }
        }

        // Internal errors - e.g. functions not yet implemented.
        public class JKLInternalError : JKLException
        {
            public JKLInternalError(string message) : base("Internal error: " + message) { }
        }

        // Errors during symbol lookup.
        public class JKLLookupError : JKLException
        {
            public JKLLookupError(string message) : base("Lookup error: " + message) { }
        }

        // Errors explicitly trapped by hosted Lisp programmers from within hosted code.
        public class JKLHostedLispError : JKLException
        {
            private JKLVal myException;

            public JKLHostedLispError(JKLVal exceptionVal) : base(exceptionVal.ToString(true))
            {
                myException = exceptionVal;
            }
            public JKLVal GetExceptionValue()
            {
                return myException;
            }
        }

        // Base class for the JKL type system.
        public abstract class JKLVal
        {
            public virtual string ToString(bool printReadably)
            {
                // Default is the regular behaviour.
                return this.ToString();
            }

            // Support the built-in '=' function. False until explicitly proven otherwise.
            // Should be used in preference to == for internal JKLVal equality checks.
            public static JKLVal EQ(JKLVal a, JKLVal b)
            {
                if (a.GetType() != b.GetType())
                {
                    // TODO - allow equality comparisons between ints and floats.
                    return jklFalse;
                }
                // If here, they are of the same Mal type. Do they have the same value?
                switch ((object)a)
                {
                    case JKLSym aSym:
                        if (aSym.getName() == ((JKLSym)b).getName()) { return malTrue; }
                        break;

                    case JKLNum aNumk:
                        if (aNumk.Unbox() == ((JKLNum)b).Unbox()) { return malTrue; }
                        break;

                    case JKLString aString:
                        if (aString.unbox() == ((JKLString)b).unbox()) { return malTrue; }
                        break;

                    case JKLKeyword aKeyWord:
                        if (aKeyWord.unbox() == ((JKLKeyword)b).unbox()) { return malTrue; }
                        break;

                    case JKLNil aNil:
                        if (aNil == ((JKLNil)b)) { return malTrue; }
                        break;
                    case JKLTrue aTrue:
                        if (aTrue == ((JKLTrue)b)) { return malTrue; }
                        break;
                    case JKLFalse aFalse:
                        if (aFalse == ((JKLFalse)b)) { return malTrue; }
                        break;

                    case JKLSeqBase aSeq:
                        // Sequences must be the same length, and each element must be the same.
                        JKLSeqBase bSeq = (JKLSeqBase)b;
                        if (aSeq.Count() != bSeq.Count())
                        {
                            // They are not of equal length.
                            return jklFalse;
                        }
                        for (var i = 0; i < aSeq.Count(); i++)
                        {
                            // At least one of the elements is not equal.
                            if (EQ(aSeq[i], bSeq[i]) == jklFalse)
                            {
                                return jklFalse;
                            }
                        }
                        return malTrue;

                    case JKLAtom aAtom:
                        // The atoms must be the same object. Two atoms containing the same value are not equal.
                        // TODO check whether this should be true if dereferencing them should be used instead. This isn't specified in the tests.
                        if (aAtom == ((JKLAtom)b)) { return malTrue; }
                        break;

                    default:
                        throw new JKLInternalError("Can't yet compare '" + a.GetType() + "' with '" + b.GetType() + "'");
                }
                return jklFalse;
            }
        }

        // ----------------------------- Sequences ----------------------------------------

        // Base class for lists, vectors and other sequences.
        // JK approach. In the C#-Mal reference, Vector is derived from List, not Sequence.
        // Here we have a JKLSeqBase which is used for (structured) MalHashMaps and 
        // (unstructured) MalSequences, from which we derive MalLists and MalVectors.  

        // This distinction lets us treat List and Vector as similar structureless things, whereas
        // HashMaps can't survive first, rest, [] etc on hashmap.
        public abstract class JKLSeqBase : JKLVal
        {
            // TODO - use C# lists and arrays in the interests of efficiency. 
            // They are currently all lists under the skin.

            // TODO - move the sequence things into sequence so hashmap can't use them.
            protected List<JKLVal> MyElements;

            protected JKLSeqBase()
            {
                MyElements = new List<JKLVal>();
            }

            protected string PrintSeq(bool printReadably, char startChar, char endChar)
            {
                StringBuilder sb = new StringBuilder();
                bool middle = false;

                sb.Append(startChar);
                foreach (JKLVal mv in MyElements)
                {
                    if (!middle)
                    {
                        middle = true;
                    }
                    else
                    {
                        sb.Append(" ");
                    }
                    sb.Append(mv.ToString(printReadably));
                }
                sb.Append(endChar);

                return sb.ToString();
            }

            public void Add(JKLVal newVal)
            {
                MyElements.Add(newVal);
            }

            public int Count()
            {
                // TODO - check whether this is correct for a hashmap. If not, override.
                return MyElements.Count;
            }

            // Return the first element. 
            public JKLVal First()
            {
                if (MyElements.Count > 0)
                {
                    return MyElements[0];
                }
                else
                {
                    return jklNil;
                }
            }

            // Return the tail of the list. 
            public JKLList Rest()
            {
                if (MyElements.Count > 0)
                {
                    // We return a JKLList, not a List<JKLVal>.
                    JKLList newList = new JKLList();

                    foreach (var element in MyElements.GetRange(1, MyElements.Count - 1))
                    {
                        newList.Add(element);
                    }
                    return newList;
                }
                else
                {
                    return new JKLList();
                }
            }

            // Analagous to the C# equivalent, shallow copy of a JKLList.
            public JKLList GetRange(int first, int last)
            {
                if (first > last)
                {
                    throw new JKLInternalError("GetRange - first '" + first + "' must be less than or equal to '" + last + "'");

                }
                JKLList newList = new JKLList();

                for (var i = 0; i < MyElements.Count; i++)
                {
                    if (i >= first && i <= last)
                    {
                        newList.Add(MyElements[i]);
                    }
                }

                return newList;
            }


            public JKLVal Get(int index)
            {
                if (index < 0 || index >= MyElements.Count)
                {
                    if (MyElements.Count == 0)
                    {
                        throw new JKLEvalError("Can't Get(" + index.ToString() + ") from an empty sequence");
                    }
                    throw new JKLEvalError("Attempt to Get(" + index.ToString() + ")from a sequence with " + MyElements.Count.ToString() + " element(s)");
                }
                return MyElements[index];
            }

            // Indexer that allows range checked array-style access.
            public JKLVal this[int index]
            {
                get
                {
                    if (index < 0 || index >= MyElements.Count)
                    {
                        if (MyElements.Count == 0)
                        {
                            throw new JKLEvalError("Attempt to get element [" + index.ToString() + "] of an empty sequence");
                        }
                        throw new JKLEvalError("Attempt to get element [" + index.ToString() + "] of a sequence with " + MyElements.Count.ToString() + " element(s)");
                    }
                    return MyElements[index];
                }
                set
                {
                    if (index < 0 || index >= MyElements.Count)
                    {
                        if (MyElements.Count == 0)
                        {
                            throw new JKLEvalError("Attempt to set element [" + index.ToString() + "] of an empty sequence");
                        }
                        throw new JKLEvalError("Attempt to set element [" + index.ToString() + "] of a sequence with " + MyElements.Count.ToString() + " element(s)");
                    }
                    MyElements[index] = value;
                }
            }
        }

        // Base for unstructured sequences, i.e. Lists and Vectors.
        public abstract class JKLSequence : JKLSeqBase
        {
            public bool Contains(JKLVal target)
            {
                for (var i = 0; i < MyElements.Count; i++)
                {
                    if (EQ(MyElements[i], target) == malTrue)
                    {
                        return true;
                    }
                }
                return false;
            }

            // Non-destructively remove a target item from a supplied list.
            // This has CL-like semantics, do only removes the first target encountered.
            public JKLSequence Remove(JKLVal target)
            {
                // Create a list to which the non-target items will be copied.
                JKLSequence RemoveSeq;
                bool removedP = false;

                // Instantiate a sequence of the same type.
                if (this is JKLList)
                {
                    RemoveSeq = new JKLList();
                }
                else if (this is JKLVector)
                {
                    RemoveSeq = new JKLVector();
                }
                else
                {
                    throw new JKLEvalError("remove expected list or vector but got '" + this.ToString() + "'");
                }
                for (var i = 0; i < MyElements.Count; i++)
                {
                    if (removedP)
                    {
                        // target already removed so always okay to keep this one.
                        RemoveSeq.Add(MyElements[i]);
                    }
                    else
                    {
                        if (EQ(MyElements[i], target) == malTrue)
                        {
                            // This is the target to remove, so don't copy it.
                            removedP = true;
                        }
                        else
                        {
                            RemoveSeq.Add(MyElements[i]);
                        }
                    }
                }
                return RemoveSeq;
            }
        }

        // Derived class for Mal lists
        public class JKLList : JKLSequence
        {
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '(', ')');
            }

            // Default constuctor - for an empty list.
            public JKLList()
            {
                // Base class does the heavy lifting.
            }

            //public JKLList Conj(params JKLVal[] mvs)
            //{
            //    // TODO. Wrote when trying to solve macro problem - not tested or used.
            //    Console.WriteLine("mvs: " + mvs.Length);
            //    JKLList newList = new JKLList();
            //    for (var i = 0; i < mvs.Length; i++)
            //    {
            //        newList.Add(mvs[i]);
            //    }
            //    return newList;
            //}
        }

        // Derived class for Mal vectors
        public class JKLVector : JKLSequence
        {
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '[', ']');
            }
        }

        // Derived class for Mal vectors
        public class JKLHashMap : JKLSeqBase
        {
            // TODO - the reader should actually be able to construct these
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '{', '}');
            }
        }

        // ------------------------- Symbols ------------------------------------------------

        // Derived class for Mal symbols
        public class JKLSym : JKLVal
        {
            private readonly string MySymbol;

            public JKLSym(string symbol)
            {
                MySymbol = symbol;
            }

            public override string ToString(bool printReadably)
            {
                return MySymbol;
            }

            public string getName() { return MySymbol; }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override string ToString()
            {
                return base.ToString();
            }

            public static bool operator ==(JKLSym a, JKLSym b)
            {
                return a.MySymbol == b.MySymbol;
            }
            public static bool operator !=(JKLSym a, JKLSym b)
            {
                return a.MySymbol != b.MySymbol;
            }
        }

        // ------------------------- Numbers ------------------------------------------------

        // Derived class for Mal numbers. The C#-Mal reference only handles
        // integers but JKL uses (and can parse) doubles. I used to have
        // separate int and float classes but these complicated lot of ops.
        public class JKLNum : JKLVal
        {
            double myVal = 0;

            public JKLNum(double num)
            {
                myVal = num;
            }

            public double Unbox()
            {
                return myVal;
            }

            public override string ToString(bool printReadably)
            {
                return myVal.ToString();
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static explicit operator int(JKLNum n)
            {
                return (int)n.myVal;
            }
            public static explicit operator float(JKLNum n)
            {
                return (float)n.myVal;
            }
            public static explicit operator double(JKLNum n)
            {
                return n.myVal;
            }

            public static JKLNum operator +(JKLNum a, JKLNum b)
            {
                return new JKLNum(a.myVal + b.myVal);
            }

            public static bool operator ==(JKLNum a, JKLNum b)
            {
                return a.myVal == b.myVal;
            }
            public static bool operator !=(JKLNum a, JKLNum b)
            {
                return a.myVal != b.myVal;
            }

            public static bool operator >=(JKLNum a, JKLNum b)
            {
                return a.myVal >= b.myVal;
            }
            public static bool operator <=(JKLNum a, JKLNum b)
            {
                return a.myVal <= b.myVal;
            }
            public static bool operator >(JKLNum a, JKLNum b)
            {
                return a.myVal > b.myVal;
            }
            public static bool operator <(JKLNum a, JKLNum b)
            {
                return a.myVal < b.myVal;
            }
        }

        // ------------------------- Various ------------------------------------------------


        // Derived class for Mal nil
        public class JKLNil : JKLVal
        {
            public override string ToString(bool printReadably)
            {
                return "nil";
            }
        }

        // Derived class for Mal false
        public class JKLFalse : JKLVal
        {
            public override string ToString(bool printReadably)
            {
                return "false";
            }
        }

        // Derived class for Mal true
        public class JKLTrue : JKLVal
        {
            public override string ToString(bool printReadably)
            {
                return "true";
            }
        }

        // Derived class for Varargs character. It's not a symbol to 
        // prevent the user overriding it.
        public class JKLVarArgs : JKLVal
        {
            public override string ToString(bool printReadably)
            {
                return "&";
            }
        }

        // ------------------------- Strings ------------------------------------------------

        // Derived class for Mal strings.
        public class JKLString : JKLVal
        {
            private string MyValue;

            public JKLString(string str)
            {
                MyValue = str;
            }

            public string unbox()
            {
                return MyValue;
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

            public override bool Equals(object obj)
            {
                var @string = obj as JKLString;
                return @string != null &&
                       MyValue == @string.MyValue;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(MyValue);
            }

            public static bool operator ==(JKLString a, JKLString b)
            {
                return a.MyValue == b.MyValue;
            }
            public static bool operator !=(JKLString a, JKLString b)
            {
                return a.MyValue != b.MyValue;
            }
        }

        // Derived class for Mal keywords. The C#-Mal Ref uses annnotated strings instead.
        public class JKLKeyword : JKLVal
        {
            private string MyKeyword;

            public JKLKeyword(string str)
            {
                MyKeyword = str;
            }

            public string unbox()
            {
                return MyKeyword;
            }

            //public override bool Equals(object obj)
            //{
            //    var keyword = obj as JKLKeyword;
            //    return keyword != null &&
            //           MyKeyword == keyword.MyKeyword;
            //}

            //public override int GetHashCode()
            //{
            //    return HashCode.Combine(MyKeyword);
            //}

            public override string ToString()
            {
                return MyKeyword;
            }

            public override string ToString(bool printReadably)
            {
                return MyKeyword;
            }

            public static bool operator ==(JKLKeyword a, JKLKeyword b)
            {
                return a.MyKeyword == b.MyKeyword;
            }
            public static bool operator !=(JKLKeyword a, JKLKeyword b)
            {
                return a.MyKeyword != b.MyKeyword;
            }
        }

        // ------------------------- Functions ------------------------------------------------

        // Derived class to hold Mal functions, both core and created by Def*. I admit,
        // I looked at C#-Mal to get this right.
        public class JKLFunc : JKLVal
        {
            // From C#-Mal
            public readonly Func<JKLList, JKLVal> fn = null;
            public readonly JKLVal ast = null;
            public readonly Env env = null;
            public readonly JKLSequence fparams = null;
            public bool isMacro = false;

            // Influences how to handle TCO.
            public readonly bool IsCore = true;

            // This says it's a lambda that takes a JKLList (its args) and returns a JKLVal
            public JKLFunc(Func<JKLList, JKLVal> fn)
            {
                this.fn = fn;
            }

            public JKLFunc(JKLVal ast, Env e, JKLSequence fparams, Func<JKLList, JKLVal> fn)
            {
                this.ast = ast;
                this.env = e;
                this.fparams = fparams;
                this.fn = fn;
                IsCore = false;
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
            public JKLVal Apply(JKLList args)
            {
                //                Console.Write("applying function built-in = " + myCreatedByFn);
                return fn(args);
            }
        }

        // ------------------------- Atoms ------------------------------------------------

        public class JKLAtom : JKLVal
        {
            private JKLVal myAtom;

            public JKLAtom(JKLVal atom)
            {
                if (atom == null)
                {
                    throw new JKLInternalError("Attempt to create null Atom");
                }
                if (((object)atom is JKLNil) || ((object)atom is JKLTrue) || ((object)atom is JKLNil))
                {
                    throw new JKLEvalError("'" + atom.ToString(true) + "' cannot be an Atom");
                }
                myAtom = atom;
            }

            public override string ToString(bool printReadably)
            {
                return "<Atom>:" + myAtom.ToString(printReadably);
            }

            public JKLVal Unbox()
            {
                return myAtom;
            }

            public JKLVal Reset(JKLVal newVal)
            {
                // some interesting error cases here, e.g. (reset a (list a)).
                if (newVal == this)
                {
                    throw new JKLEvalError("Can't reset atom '" + this.ToString(true) + "' to itself");
                }
                myAtom = newVal;
                return newVal;
            }
        }

    }
}
