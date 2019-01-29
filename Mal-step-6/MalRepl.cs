using System;
using System.Collections.Generic;
//using Mal;
using static Mal.Types;
using static Mal.Core;
using static Mal.Env;

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

                // Load core functions.
                foreach(var thing in MalNameSpace)
                {
                    myEnv.Set(new MalSym(thing.Key), thing.Value);
                }
                // Load the special eval core function. We have to do it here to access the REPL env.
                myEnv.Set(new MalSym("eval"), new MalFunc(args =>
                {
                    return EVAL(args[0], myEnv);
                }));

                // some convenient test files
                // "F:\Mal-development\mal-tests\mal-step6-test-01.txt"
                // "F:\Mal-development\mal-tests\mal-code-01.txt"
                // (load-file "F:\Mal-development\mal-tests\mal-code-01.txt")

                // (eval (read-string (slurp "F:\Mal-development\mal-tests\mal-code-01.txt")))

                // Add 'core' functions defined using Mal itself.
                EVAL(READ("(def not (fn (a) (if a false true)))"), myEnv);
                // TODO -number of brackets is wrong and returns nil.
                EVAL(READ("(def load-file (fn (f) (eval (read-string (str \"(do \" (slurp f) \"))\")))))"), myEnv);

                // Add some of the test functions for convenience.
                EVAL(READ("(def sumdown (fn (N) (if (> N 0) (+ N (sumdown  (- N 1))) 0)))"), myEnv);


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

        // EVAL is handled by two functions - EVAL which decides whether or not the ast is a special
        // form or not and eval_ast which evaluates the remaining symbols, lists, etc. 
        // EVAL is tail-call optimised, as per Mal guide step 5

        static MalVal eval_ast(MalVal ast, Env env)
        {
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
                    // Return a new hash-map which consists of key-value pairs where the key is a key from the hash-map
                    // and the value is the result of calling EVAL on the corresponding value.
                    if (malHashMap.Count() % 2 != 0)
                    {
                        throw new MalEvalError("Hashmap requires an even number of elements forming key value pairs '" + malHashMap.ToString(true) + "'");
                    }

                    MalHashMap derivedHashMap = new MalHashMap();
                    // Work through successive key value pairs.
                    // Note - C#-Mal creates a dictionary, loads it and then passes the result to a Hashmap c'tor that takes a Dict.
                    for (int i = 0; i < malHashMap.Count(); i+= 2)
                    {
                        MalVal key = malHashMap.Get(i);
                        if (key is MalString || key is MalKeyword)
                        {
                            derivedHashMap.Add(key);
                            derivedHashMap.Add(EVAL(malHashMap.Get(i + 1), env)); 
                        }
                        else
                        {
                            throw new MalEvalError("Expecting a keyword or string as a HashMap key but got '" + key.ToString(true) + "'");
                        }

                    }
                    return derivedHashMap; 

                default:
                    // It's not a symbol or a list.
                    return ast;
            }
        }

        // Evaluate the ast in the supplied environment. This is a bit of a monster but 
        // has been left as per the guide to simplify Tail Call Optimisation (TCO)
        static MalVal EVAL(MalVal InitialAst, Env IntialEnv)
        {
            // The ast and env to be EVAL'd. Initially set to those passed to EVAL but
            // may be updated if there is a TCO loop.
            MalVal ast = InitialAst;
            Env env = IntialEnv;

            // Some eval cases will return out of the function, others keep looping (as per TCO) with
            // an updated ast / Env. The alternative is to call EVAL recursively, which has simpler
            // logic but which can blow up the stack.
            while (true)
            {
                if (ast is MalList astList)
                {
                    // Console.WriteLine("EVAL: ast is " + astList.ToString(true));
                    if (astList.Count() <= 0)
                    {
                        // Empty list, return unchanged.
                        return ast;
                    }
                    else
                    {

                        // I used to check astList[0] was a symbol but this dissallows forms whose first el is an anon (fn..) special form.
                        string symbolZero = astList[0] is MalSym ? ((MalSym)astList[0]).getName() : "__<*fn*>__";

                        switch (symbolZero)
                        {
                            case "def":
                                // Evaluate all elements of list using eval_ast, retun the final eval'd element.
                                // Should be something like (def a b). Set() symbol a to be the result of evaluating b.
                                if (astList.Count() != 3 || !(astList[1] is MalSym symbol))
                                {
                                    throw new MalEvalError("'def' should be followed by a symbol and a value");
                                }
                                // Must add the result of the EVAL to the environment so can't TCO loop here.
                                MalVal result = EVAL(astList[2], env);
                                env.Set(symbol, result);
                                return result;

                            case "let":
                                // Let syntax is (let <bindings-list> <result>), e.g. (let (p ( + 2 3) q (+ 2 p)) (+ p q)).
                                // Bindings can refer to earlier bindings in the new let environment or to symbols
                                // in the outer environment. Let defs hide hide same-named symbols in the outer scope.

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

                                // Create a new Env - the scope of the Let form. It's discarded when done.
                                Env TempLetEnv = new Env(env);

                                // Process each pair of entries in the bindings list.
                                for (int i = 0; i < bindingsList.Count(); i += 2)
                                {
                                    // The first element should be a 'key' symbol. E.g. 'p'.
                                    if (!(bindingsList[i] is MalSym bindingKey))
                                    {
                                        throw new MalEvalError("'let' expected symbol but got: '" + bindingsList[i].ToString(true) + "'");
                                    }
                                    // The second element (e.g. (+ 2 3)) is the value of the key symbol in Let's environment
                                    MalVal val = EVAL(bindingsList[i + 1], TempLetEnv);

                                    // Store the new value in the environment.
                                    TempLetEnv.Set(bindingKey, val);
                                }

                                // TCO loop instead of EVAL(astList[2], TempLetEnv)
                                ast = astList[2];
                                env = TempLetEnv;
                                continue;

                            case "do":
                                // Something like (do <form> <form>)
                                MalList formsToDo = astList.Rest();
                                int formsToDoCount = formsToDo.Count();
                                if (formsToDoCount == 0)
                                {
                                    // Empty (do )
                                    return malNil;
                                }
                                else
                                {
                                    // EVAL all forms in the (do ) body. 


                                    // up to but not including the last one.
                                    if(formsToDoCount > 1)
                                    {
                                        eval_ast(formsToDo.GetRange(0, formsToDoCount - 2), env);
                                    }

                                    // EVAL the last form in the (do ) body via TCO loop, keeping the same env.
                                    ast = formsToDo[formsToDoCount - 1];

                                    // TCO loop.
                                    continue;
                                }

                            case "if":
                                // If has the syntax (if <cond> <true-branch> <optional-false-branch>)
                                if (astList.Count() < 3 || astList.Count() > 4)
                                {
                                    throw new MalEvalError("'if' should have a condition, true branch and optional false branch");
                                }
                                // Evaluate the Cond part of the if.
                                MalVal cond = EVAL(astList[1], env);
                                if (cond is MalNil || cond is MalFalse)
                                {
                                    // Cond is nil or false.
                                    if (astList.Count() == 4)
                                    {
                                        // Eval the 'false' branch via TCO loop.
                                        ast = astList[3];
                                        continue;
                                    }
                                    else
                                    {
                                        // No 'false' branch. 
                                        return malNil;
                                    }
                                }
                                else
                                {
                                    // Eval the 'true' branch via TCO loop.
                                    ast = astList[2];
                                    continue;
                                }
                            case "fn":
                                // e.g.  (def a (fn (i) (* i 2))) or (def b (fn () (prn 1) (prn 2)))
                                if (astList.Count() < 3)
                                {
                                    throw new MalEvalError("fn must have an arg list and at least one body form");
                                }
                                if (!(astList[1] is MalList FnArgList))
                                {
                                    // The 2nd element must be an arg list (possibly empty).
                                    throw new MalEvalError("Expected arg list but got: '" + astList[1].ToString(true) + "'");
                                }

                                MalVal FnBodyExprs;
                                // Unlike the Mal reference, this handles fns that have multiple expr forms.
                                if( astList.Count() == 3)
                                {
                                    FnBodyExprs = astList[2];
                                }
                                else
                                {
                                    // Either I've misunderstood something or the Reference doesn't handle some functions correctly.
                                    FnBodyExprs = astList.GetRange(2, astList.Count()-1);
                                }

                                Env cur_env = env;

                                // This is different from C#-Mal but their version doesn't work.
                                return new MalFunc(FnBodyExprs, env, FnArgList, args => EVAL(FnBodyExprs, new Env(cur_env, FnArgList, args)));


                            default:
                                // Ast isn't a special form, so try to evaluate it as a function.
                                MalList evaledList = (MalList)eval_ast(ast, env);

                                MalVal listFirst = evaledList.First();

                                if (listFirst is MalFunc func)
                                {
                                    if(func.IsCore)
                                    {
                                        // Can't TCO a core function so directly apply func.
                                        return func.Apply(evaledList.Rest());
                                    }
                                    else
                                    {
                                        // Non-ore function. 
                                        // The ast is the one stored by the func.
                                        ast = func.ast;

                                        // Create a new env using func's env and params as the outer and binds arguments
                                        // and the rest of the current ast as the exprs argument.
                                        env = new Env(func.env, func.fparams, evaledList.Rest());

                                        // and now 'apply' via the TCO loop.
                                        continue;
                                    }
                                }
                                else
                                {
                                    return listFirst;
                                }
                        }
                    }
                }
                else
                {
                    // If ast is not a list (e.g. a vector), return the result of calling eval_ast on it.
                    return eval_ast(ast, env);
                }
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
