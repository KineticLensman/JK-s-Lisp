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

        public class MalLookupError : MalException
        {
            public MalLookupError(string message) : base("Lookup error: " + message) { }
        }

        // Base class for the Mal type system.
        public abstract class MalVal
        {
            public virtual string ToString(bool printReadably)
            {
                // Default is the regular behaviour.
                return this.ToString();
            }

            // Support the built-in '=' function. False until explicitly proven otherwise.
            // Should be used in preference to == for internal MalVal equality checks.
            // TODO - test for nil, true and false
            public static MalVal EQ (MalVal a, MalVal b)
            {
                if (a.GetType() != b.GetType())
                {
                    // TODO - allow equality comparisons between ints and floats.
                    return malFalse;
                }
                // If here, they are of the same Mal type. Do they have the same value?
                switch ((object) a)
                {
                    case MalSym aSym:
                        if (aSym.getName() == ((MalSym)b).getName()) { return malTrue; }
                        break;

                    case MalNum aNumk:
                        if (aNumk.Unbox() == ((MalNum)b).Unbox()) { return malTrue; }
                        break;

                    case MalString aString:
                        if (aString.unbox() == ((MalString)b).unbox()) { return malTrue; }
                        break;

                    case MalKeyword aKeyWord:
                        if (aKeyWord.unbox() == ((MalKeyword)b).unbox()) { return malTrue; }
                        break;

                    case MalNil aNil:
                        if (aNil == ((MalNil)b)) { return malTrue; }
                        break;
                    case MalTrue aTrue:
                        if (aTrue == ((MalTrue)b)) { return malTrue; }
                        break;
                    case MalFalse aFalse:
                        if (aFalse == ((MalFalse)b)) { return malTrue; }
                        break;

                    case MalSeqBase aSeq:
                        // Sequences must be the same length, and each element must be the same.
                        MalSeqBase bSeq = (MalSeqBase)b;
                        if (aSeq.Count () != bSeq.Count())
                        {
                            // They are not of equal length.
                            return malFalse;
                        }
                        for (var i = 0; i < aSeq.Count(); i++)
                        {
                            // At least one of the elements is not equal.
                            if (EQ(aSeq[i], bSeq[i]) == malFalse)
                            {
                                return malFalse;
                            }
                        }
                        return malTrue;

                    case MalAtom aAtom:
                        // The atoms must be the same object. Two atoms containing the same value are not equal.
                        // TODO check whether this should be true if dereferencing them should be used instead. This isn't specified in the tests.
                        if (aAtom == ((MalAtom)b)) { return malTrue; }
                        break;

                    default:
                        throw new MalInternalError("Can't yet compare '" + a.GetType() + "' with '" + b.GetType() + "'");
                }
                return malFalse;
            }
        }

        // ----------------------------- Sequences ----------------------------------------

        // Base class for lists, vectors and other sequences.
        // JK approach. In the C#-Mal reference, Vector is derived from List, not Sequence.
        // Here we have a MalSeqBase which is used for (structured) MalHashMaps and 
        // (unstructured) MalSequences, from which we derive MalLists and MalVectors.  

        // This distinction lets us treat List and Vector as similar structureless things, whereas
        // HashMaps can't survive first, rest, [] etc on hashmap.
        public abstract class MalSeqBase : MalVal
        {
            // TODO - use C# lists and arrays in the interests of efficiency. 
            // They are currently all lists under the skin.

            // TODO - move the sequence things into sequence so hashmap can't use them.
            protected List<MalVal> MyElements;

            protected MalSeqBase()
            {
                MyElements = new List<MalVal>();
            }

            protected string PrintSeq(bool printReadably, char startChar, char endChar)
            {
                StringBuilder sb = new StringBuilder();
                bool middle = false;

                sb.Append(startChar);
                foreach (MalVal mv in MyElements)
                {
                    if( ! middle)
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

            public void Add(MalVal newVal)
            {
                MyElements.Add(newVal);
            }

            public int Count()
            {
                // TODO - check whether this is correct for a hashmap. If not, override.
                return MyElements.Count;
            }

            // Return the first element. 
            public MalVal First()
            {
                if(MyElements.Count > 0)
                {
                    return MyElements[0];
                }
                else
                {
                    return malNil;
                }
            }

            // Return the tail of the list. 
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

            // Analagous to the C# equivalent, shallow copy of a MalList.
            public MalList GetRange(int first, int last)
            {
                if( first > last)
                {
                    throw new MalInternalError("GetRange - first '" + first + "' must be less than or equal to '" + last + "'");

                }
                MalList newList = new MalList();

                for(var i = 0; i < MyElements.Count; i++)
                {
                    if (i >= first && i <= last)
                    {
                        newList.Add(MyElements[i]);
                    }
                }

                return newList;
            }


            public MalVal Get(int index)
            {
                if( index < 0 || index >= MyElements.Count)
                {
                    throw new MalEvalError("Attempt to Get element " + index.ToString() + " of a sequence with " + MyElements.Count.ToString() + " elements");
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
                        throw new MalEvalError("Attempt to access element " + index.ToString() + " of a sequence with " + MyElements.Count.ToString() + " elements");
                    }
                    return MyElements[index];
                }
                set
                {
                    if (index < 0 || index >= MyElements.Count)
                    {
                        throw new MalEvalError("Attempt to access element " + index.ToString() + " of a sequence with " + MyElements.Count.ToString() + " elements");
                    }
                    MyElements[index] = value;
                }
            }
        }

        // Base for unstructured sequences, i.e. Lists and Vectors.
        public abstract class MalSequence : MalSeqBase
        {
            public bool Contains(MalVal target)
            {
                for (var i = 0; i < MyElements.Count; i++)
                {
                    if(EQ(MyElements[i],target) == malTrue)
                    {
                        return true;
                    }
                }
                return false;
            }

            // Non-destructively remove a target item from a supplied list.
            // This has CL-like semantics, do only removes the first target encountered.
            public MalSequence Remove(MalVal target)
            {
                // Create a list to which the non-target items will be copied.
                MalSequence RemoveSeq;
                bool removedP = false;

                // Instantiate a sequence of the same type.
                if (this is MalList)
                {
                    RemoveSeq = new MalList();
                }
                else if (this is MalVector)
                {
                    RemoveSeq = new MalVector();
                }
                else
                {
                    throw new MalEvalError("remove expected list or vector but got '" + this.ToString() + "'");
                }
                for (var i = 0; i < MyElements.Count; i++)
                {
                    if(removedP)
                    {
                        // target already removed so always okay to keep this one.
                        RemoveSeq.Add(MyElements[i]);
                    }
                    else
                    {
                        if (EQ(MyElements[i],target) == malTrue)
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
        public class MalList : MalSequence
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
        }

        // Derived class for Mal vectors
        public class MalVector : MalSequence
        {
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '[', ']');
            }
        }

        // Derived class for Mal vectors
        public class MalHashMap : MalSeqBase
        {
            // TODO - the reader should actually be able to construct these
            public override string ToString(bool printReadably)
            {
                return PrintSeq(printReadably, '{', '}');
            }
        }

        // ------------------------- Symbols ------------------------------------------------

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

            public static bool operator ==(MalSym a, MalSym b)
            {
                return a.MySymbol == b.MySymbol;
            }
            public static bool operator !=(MalSym a, MalSym b)
            {
                return a.MySymbol != b.MySymbol;
            }
        }

        // ------------------------- Numbers ------------------------------------------------

        // Derived class for Mal numbers. The C#-Mal reference only handles
        // integers but JKL uses (and can parse) doubles. I used to have
        // separate int and float classes but these complicated lot of ops.
        public class MalNum : MalVal
        {
            double myVal = 0;

            public MalNum(double num)
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

            public static explicit operator int(MalNum n)
            {
                return (int)n.myVal;
            }
            public static explicit operator float(MalNum n)
            {
                return (float)n.myVal;
            }
            public static explicit operator double(MalNum n)
            {
                return n.myVal;
            }

            public static MalNum operator +(MalNum a, MalNum b)
            {
                return new MalNum(a.myVal + b.myVal);
            }

            public static bool operator ==(MalNum a, MalNum b)
            {
                return a.myVal == b.myVal;
            }
            public static bool operator !=(MalNum a, MalNum b)
            {
                return a.myVal != b.myVal;
            }

            public static bool operator >=(MalNum a, MalNum b)
            {
                return a.myVal >= b.myVal;
            }
            public static bool operator <=(MalNum a, MalNum b)
            {
                return a.myVal <= b.myVal;
            }
            public static bool operator >(MalNum a, MalNum b)
            {
                return a.myVal > b.myVal;
            }
            public static bool operator <(MalNum a, MalNum b)
            {
                return a.myVal < b.myVal;
            }
        }

        // ------------------------- Various ------------------------------------------------


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

        // Derived class for Varargs character. It's not a symbol to 
        // prevent the user overriding it.
        public class MalVarArgs : MalVal
        {
            public override string ToString(bool printReadably)
            {
                return "&";
            }
        }

        // ------------------------- Strings ------------------------------------------------

        // Derived class for Mal strings.
        public class MalString : MalVal
        {
            private string MyValue;

            public MalString(string str)
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
                var @string = obj as MalString;
                return @string != null &&
                       MyValue == @string.MyValue;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(MyValue);
            }

            public static bool operator ==(MalString a, MalString b)
            {
                return a.MyValue == b.MyValue;
            }
            public static bool operator !=(MalString a, MalString b)
            {
                return a.MyValue != b.MyValue;
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

            public string unbox()
            {
                return MyKeyword;
            }

            //public override bool Equals(object obj)
            //{
            //    var keyword = obj as MalKeyword;
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

            public static bool operator ==(MalKeyword a, MalKeyword b)
            {
                return a.MyKeyword == b.MyKeyword;
            }
            public static bool operator !=(MalKeyword a, MalKeyword b)
            {
                return a.MyKeyword != b.MyKeyword;
            }
        }

        // ------------------------- Functions ------------------------------------------------

        // Derived class to hold Mal functions, both core and created by Def*. I admit,
        // I looked at C#-Mal to get this right.
        public class MalFunc : MalVal
        {
            // From C#-Mal
            public readonly Func<MalList, MalVal> fn = null;
            public readonly MalVal ast = null;
            public readonly Env env = null;
            public readonly MalSequence fparams = null;
            public bool isMacro = false;

            // Influences how to handle TCO.
            public readonly bool IsCore = true;

            // This says it's a lambda that takes a MalList (its args) and returns a MalVal
            public MalFunc(Func<MalList, MalVal> fn)
            {
                this.fn = fn;
            }

            public MalFunc(MalVal ast, Env e, MalSequence fparams, Func<MalList, MalVal> fn)
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
            public MalVal Apply(MalList args)
            {
//                Console.Write("applying function built-in = " + myCreatedByFn);
                return fn(args);
            }
        }

        // ------------------------- Atoms ------------------------------------------------

        public class MalAtom : MalVal
        {
            private MalVal myAtom;

            public MalAtom(MalVal atom)
            {
                if (atom == null)
                {
                    throw new MalInternalError("Attempt to create null Atom");
                }
                if (((object)atom is MalNil) || ((object)atom is MalTrue) || ((object)atom is MalNil))
                {
                    throw new MalEvalError("'" + atom.ToString(true) + "' cannot be an Atom");
                }
                myAtom = atom;
            }

            public override string ToString(bool printReadably)
            {
                return "<Atom>:" + myAtom.ToString(printReadably);
            }

            public MalVal Unbox ()
            {
                return myAtom;
            }

            public MalVal Reset(MalVal newVal)
            {
                // some interesting error cases here, e.g. (reset a (list a)).
                if (newVal == this)
                {
                    throw new MalEvalError("Can't reset atom '" + this.ToString(true) + "' to itself");
                }
                myAtom = newVal;
                return newVal;
            }
        }

    }
}
