using System;
using System.Collections.Generic;
//using Mal;
using static Mal.Types;
using static Mal.Core;

namespace Mal
{
    // READ.. EVAL .. PRINT .. LOOP .. until death (or EOF) sets you free.
    public class MalRepl
    {
        // Hold the eval environment
        Dictionary<string, MalFunc> MyReplEnv;

        // Setup the REPL.
        public MalRepl()
        {
            // Load built-in functions into the initial eval environment
            MyReplEnv = new Dictionary<string, MalFunc>()
            {
                { "+", BuiltInAdd },
                { "*", BuiltInMultiply },
                { "-", BuiltInSubtract },
                { "/", BuiltInDivide },
            };
        }

        // As per the Mal guide.
        static MalVal READ(String str)
        {
            return MalReader.read_str(str);
        }

        // Handle multi-lines.
        static MalVal READMULTILINE(MalReader.Reader rdr)
        {
            return MalReader.read_str_multiline(rdr);
        }

        static MalVal eval_ast(MalVal ast, Dictionary<string, MalFunc> replEnv)
        {
            // TODO - handle vectors
            // Switch on the type of the ast.
            switch ((Object) ast)
            {
                case MalSym malSym:
                    // Lookup the symbol in the environment and return the value or raise an error.
                    string malsymstring = malSym.ToString(true);
                    Console.WriteLine("eval_ast - searching for symbol: '" + malsymstring + "'");

                    if ( replEnv.TryGetValue(malsymstring,  out MalFunc func))
                    {
                        Console.WriteLine("eval_ast - found function '" + func.ToString(true) + "'");
                        return func;
                    }
                    else
                    {
                        throw new MalEvalError("Undefined symbol '" + malSym.ToString(true) + "'");
                    }
                case MalList malList:
                    MalList derivedList = new MalList();
                    Console.WriteLine("eval_ast - handling MalList");
                    for (int i = 0; i < malList.Count(); i++)
                    {
                        derivedList.Add(EVAL(malList.Get(i), replEnv));
                    }
                    return derivedList;
                case MalVector malVec:
                    MalVector derivedVec = new MalVector();
                    Console.WriteLine("eval_ast - handling MalVector");
                    // TODO - return a new list that is the result of calling EVAL on each of the members of the list.
                    for (int i = 0; i < malVec.Count(); i++)
                    {
                        derivedVec.Add(EVAL(malVec.Get(i), replEnv));
                    }
                    return derivedVec;
                case MalHashMap malHashMap:
                    throw new MalEvalError("INTERNAL - can't evaluate a HashMap yet '" + malHashMap.ToString() + "'");
                default:
                    // It's not a symbol or a list.
                    return ast;
            }

        }

        static MalVal EVAL(MalVal ast, Dictionary<string, MalFunc> replEnv)
        {
            switch ((Object) ast)
            {
                case MalList mList:
                    // Ast is a list.
                    // TODO - should this also do vectors and hashmaps?
                    if(mList.Count() <= 0)
                    {
                        // Empty list, return unchanged.
                        Console.WriteLine("EVAL - empty list: " + Printer.pr_str(mList, true));
                        return ast;
                    }
                    else
                    {
                        // ast is a non-empty list, so evaluate it.
                        Console.WriteLine("EVAL - non-empty list: " + Printer.pr_str(mList, true));

                        // Evaluate the List.
                        MalList evaledList = (MalList) eval_ast(ast, replEnv);

                        MalVal listFirst = evaledList.First();
                        MalList listRest = evaledList.Rest();

                        switch((Object)listFirst)
                        {
                            case MalFunc func:
                                Console.WriteLine("EVAL - List head is: '" + Printer.pr_str(listFirst, true) + "'. Rest elements: ");
                                // Take the first item of the evaluated list and call it as function using the rest of the evaluated list as its arguments

                                return func.Apply(listRest);

                            //return null;
                            default:
                                throw new MalEvalError("Can't use '" + listFirst.ToString(true) + "' as a function.");

                        }


                    }
                default:
                    // If ast is not a list (e.g. a vector), return the result of calling eval_ast on it.
                    return eval_ast(ast, replEnv);
            }
        }

        String PRINT(MalVal exp)
        {
            return Printer.pr_str(exp, true);
        }


        // The guts of the REPL. It extendsthe C#-Mal reference REPL
        // to handle (1) multi-line forms and (2) multi-form lines.
        public void RunREPL()
        {
            while (true)
            {
                try
                {
                    // Read standard input.
                    string line = ReadLine.Readline("JKL> ");

                    // Exit if EOF or ctrl-Z.
                    if (line == null) { break; }

                    // Go round again if empty line (user hit return).
                    if (line == "") { continue; }

                    // Tokenise the input and load the tokens into a Reader object.
                    MalReader.Reader rdr = new MalReader.Reader(MalReader.Tokenizer(line));

                    // Loop until all tokens on the line have been consumed. This handles lines
                    // like (a b) (c d) - where Mal REPL would silently ignore (c d)
                    while (rdr.Peek() != null)
                    {
                        // Parse input to create a Mal abstract syntax tree. The parser
                        // attempts to reload the reader if it runs out of tokens mid form.
                        MalVal ast = READMULTILINE(rdr);

                        // Evaluate the ast and print the result.
                        Console.WriteLine(PRINT(EVAL(ast, MyReplEnv)));
                    }
                }
                catch (MalParseError e)
                {
                    Console.WriteLine(e.Message);
                }
                catch (MalEvalError e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
