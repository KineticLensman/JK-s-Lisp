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
        // The eval environment (keys and symbols).
        Env myEnv;

        // Setup the REPL.
        public MalRepl()
        {
            // Load built-in functions into the initial eval environment.
            try
            {
                myEnv = new Env(null);
                // Arithmetic.
                myEnv.Set("+", BuiltInAdd);
                myEnv.Set("*", BuiltInMultiply);
                myEnv.Set("-", BuiltInSubtract);
                myEnv.Set("/", BuiltInDivide);

                // List manipulation.
                myEnv.Set("list", BuiltinList);
                myEnv.Set("list?", BuiltinIsList);
                myEnv.Set("empty?", BuiltinEmpty);
                myEnv.Set("count", BuiltinCount);

            }
            catch (MalEvalError e)
            {
                Console.WriteLine(e.Message);
            }
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

        static MalVal eval_ast(MalVal ast, Env env)
        {
            // TODO - handle malHashMap.
            // Switch on the type of the ast.
            switch ((Object) ast)
            {
                case MalSym malSym:
                    return env.Get(malSym);

                case MalList malList:
                    // Return a new list that is the result of calling EVAL on each of the members of the list.
                    MalList derivedList = new MalList();
                    for (int i = 0; i < malList.Count(); i++)
                    {
                        derivedList.Add(EVAL(malList.Get(i), env));
                    }
                    return derivedList;

                case MalVector malVec:
                    // Return a new vector that is the result of calling EVAL on each of the members of the vector.
                    MalVector derivedVec = new MalVector();
                    for (int i = 0; i < malVec.Count(); i++)
                    {
                        derivedVec.Add(EVAL(malVec.Get(i), env));
                    }
                    return derivedVec;

                case MalHashMap malHashMap:
                    throw new MalInternalError("Can't evaluate a HashMap yet '" + malHashMap.ToString() + "'");

                default:
                    // It's not a symbol or a list.
                    return ast;
            }
        }

        static MalVal EvalDef(MalList astList, Env env)
        {
            // Evaluate all elements of list using eval_ast, retun the final eval'd element
            // Should be something like (def a b). Set() symbol a to be the result of evaluating b.
            if (astList.Count() != 3 || !(astList[1] is MalSym symbol))
            {
                throw new MalEvalError("'def' should be followed by a symbol and a value");
            }
            MalVal result = EVAL(astList[2], env);
            env.Set(symbol.getName(), result);
            return result;
        }

        static MalVal EvalLet(MalList astList, Env env)
        {
            // Let* syntax is (let* <bindings-list> <result>), e.g. (let (p ( + 2 3) q (+ 2 p)) (+ p q)).
            // Bindings can refer to earlier bindings in the new let environment or to symbols
            // in the outer environment.

            // Confirm the structure of the let* form.
            if (astList.Count() != 3)
            {
                throw new MalEvalError("'let' should have two arguments (bindings-list and result), but instead had " + (astList.Count() - 1));
            }
            // Extract the first parameter - the bindings list. E.g. (p (+ 2 3) q (+ 2 p))
            if (!(astList[1] is MalList bindingsList))
            {
                throw new MalEvalError("'let' should be followed by a non-empty bindings list and a result form");
            }
            if (bindingsList.Count() <= 1 || bindingsList.Count() % 2 != 0)
            {
                throw new MalEvalError("'let' bindings list should have an even number of entries");
            }

            // Create a new Env - the scope of the Let form.
            Env LetEnv = new Env(env);

            // Process each pair of entries in the bindings list.
            for (int i = 0; i < bindingsList.Count(); i += 2)
            {
                // The first element should be a 'key' symbol. E.g. 'p'.
                if (!(bindingsList[i] is MalSym bindingKey))
                {
                    throw new MalEvalError("'let' expected symbol but got: '" + bindingsList[i].ToString(true) + "'");
                }
                // The second element is the value of the key symbol in Let's environment, E.g. (+ 2 3)
                MalVal val = EVAL(bindingsList[i + 1], LetEnv);
                // This expression can refer to earlier let bindings.

                // Now, store the new value in the environment.
                LetEnv.Set(bindingKey.getName(), val);
                // Note - this can silently redefine built-in functions e.g. if something like '+' is used.
            }

            // Using the populated Let* environment, evaluate and return the result form.
            // The Let* environment itself is discarded, unshadowing symbols in outer environments.
            return EVAL(astList[2], LetEnv);
        }

        static MalVal EvalDo(MalList astList, Env env)
        {
            // Evaluate all elements of list using eval_ast, retun the final eval'd element.
            // I peeked at the reference to get this right although in my defence I added error checking
            MalList el = (MalList)eval_ast(astList.Rest(), env);

            return (el.Count() > 0) ? el[el.Count() - 1] : new MalNil();
        }

        static MalVal EvalIf(MalList astList, Env env)
        {
            // If has the syntax (if <cond> <true-branch> <optional-false-branch>)
            if( astList.Count() < 3 || astList.Count() > 4)
            {
                throw new MalEvalError("'if' should have a condition, true branch and optional false branch");
            }
            // Evaluate the Cond part of the if.
            MalVal cond = EVAL(astList[1], env);
            if (cond is MalNil || cond is MalFalse)
            {
                // Cond is nil or false. Eval the 'false' branch, if any.
                if (astList.Count() == 4)
                {
                    return EVAL(astList[3], env);
                }
                else
                {
                    return new MalNil();
                }
            } 
            else
            {
                // Eval the 'true' branch.
                return EVAL(astList[2], env);
            }
        }

        static MalVal EvalFnDef(MalList astList, Env env)
        {
            // return new function closure. The body of that closure does the following
            //   create a new env using Env (closed over from outer scope) as the outer param, the first param (second el of ast from outer scope) as the binds param
            //   and the params to the closure as the exprs parameter.
            // Call EVAL on the second parameter (third list el of ast from outer scope_) using the new environment
            // Use the result as the return value of the closure.
            throw new MalInternalError("'fn' not implemented yet");
        }

        static MalVal EvalMalFunc(MalVal ast, Env env)
        {
            // Try to evaluate an ast as a MalFunc.
            MalList evaledList = (MalList)eval_ast(ast, env);

            MalVal listFirst = evaledList.First();
            MalList listRest = evaledList.Rest();

            if (listFirst is MalFunc func)
            {
                // Ast *is* a function, so call it.
                return func.Apply(listRest);
            }
            else
            {
                throw new MalEvalError("Can't use '" + listFirst.ToString(true) + "' as a function.");
            }
        }


        // Evaluate the ast in the supplied environment. 
        // NOTE - I've taken off the '*' and '!' chars from the special forms.
        static MalVal EVAL(MalVal ast, Env env)
        {
            if (ast is MalList astList)
            {
                if (astList.Count() <= 0)
                {
                    // Empty list, return unchanged.
                    return ast;
                }
                else
                {
                    // ast is a non-empty list. [0] should be a special form or a function name.
                    if (!(astList[0] is MalSym sym))
                    {
                        // Something like ([ 1 2]) perhaps.
                        throw new MalEvalError("Expected function name or special form but got '" + astList[0] + "'");
                    }

                    switch (sym.getName())
                    {
                        case "def": return EvalDef(astList, env);
                        case "let": return EvalLet(astList, env);
                        case "do":  return EvalDo(astList, env);
                        case "if":  return EvalIf(astList, env);
                        case "fn":  return EvalFnDef(astList, env);
                        default:
                            // Ast isn't a special form, so it should be a function.
                            return EvalMalFunc(ast, env);
                    }
                }
            }
            else
            {
                // If ast is not a list (e.g. a vector), return the result of calling eval_ast on it.
                return eval_ast(ast, env);
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
                        Console.WriteLine(PRINT(EVAL(ast, myEnv)));
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
                catch (MalInternalError e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
