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

        // -------------------- Quoting and macros ---------------------------------------------
        // Helper function for quasiquotes. Return true if the arg is a non-empty list.
        static Boolean is_pair(MalVal ast)
        {
            if (ast is MalList malList)
            {
                if (malList.Count() > 0)
                {
                    return true;
                }
            }
            if (ast is MalVector malVec)
            {
                if (malVec.Count() > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Name changed from the guide to highlight that the outcome is complicated.
        static MalVal ProcessQuasiquote(MalVal ast)
        {
            if (!is_pair(ast))
            {
                // If is_pair of ast is false: return a new list containing: a symbol named "quote" and ast.
                MalList qList = new MalList();

                qList.Add(new MalSym("quote"));
                qList.Add(ast);
                return qList;
            }
            else
            {
                // TODO - is this a top-level if?
                // If the first element of ast is a symbol named "unquote": return the second element of ast.
                if (ast is MalList astList)
                {
                    if (astList.Count() == 2)
                    {
                        if (astList[0] is MalSym mSym)
                        {
                            if (mSym.getName() == "unquote")
                            {
                                return astList[1];
                            }
                        }
                    }
                }
            }

            // Check for and handle a splice-unquote (SU)
            if (ast is MalList astListSU && is_pair(astListSU[0]))
            {
                // The first el of the list ast is itself a list... (a[0])
                if(astListSU[0] is MalList firstEl)
                {
                    // And the first element of the astList first element (a[0][0]) is a symbol...
                    if (firstEl.Count() > 0 && firstEl[0] is MalSym mSym)
                    {
                        // And that symbol is the splice-unquote
                        if (mSym.getName() == "splice-unquote")
                        {
                            // return a new list containing: a symbol named "concat", the second element
                            // of first element of ast(ast[0][1]), and the result of calling quasiquote
                            // with the second through last element of ast.
                            MalList newConcatForm = new MalList();

                            newConcatForm.Add(new MalSym("concat"));
                            if(firstEl.Count() < 2)
                            {
                                throw new MalInternalError("splice-unquote expected at least two elements");
                            }

                            newConcatForm.Add(firstEl[1]);
                            newConcatForm.Add(ProcessQuasiquote(astListSU.Rest()));
                            return newConcatForm;
                        }
                    }
                }

            }

            if (ast is MalList astListCons)
            {
                if (astListCons.Count() > 0)
                {
                    // Return a new list containing: a symbol named "cons", the result of
                    // calling quasiquote on first element of ast(ast[0]), and the result of
                    // calling quasiquote with the second through last element of ast.
                    MalList newConsForm = new MalList();

                    newConsForm.Add(new MalSym("cons"));
                    newConsForm.Add(ProcessQuasiquote(astListCons[0]));
                    newConsForm.Add(ProcessQuasiquote(astListCons.Rest()));
                    return newConsForm;
                }
            }
            throw new MalInternalError("Quasiquote bad syntax?");
        }

        // A helper function for EVAL that handles symbols (it looks them up) and
        // sequences (it EVAL's their elements by mutual recursion with EVAL). 

        static MalVal eval_ast(MalVal ast, Env env)
        {
            // Switch on the type of the ast.
            switch ((Object) ast)
            {
                case MalSym malSym:
                    // Simplest case - look up and return a symbol's value.
                    return env.Get(malSym);

                case MalList malList:
                    // Build and return a new list that contains the result of calling EVAL
                    // on each member of the original list.
                    MalList derivedList = new MalList();
                    for (int i = 0; i < malList.Count(); i++)
                    {
                        derivedList.Add(EVAL(malList.Get(i), env));
                    }
                    return derivedList;

                case MalVector malVec:
                    // Build and return a new vector.
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
                    // Build and return a new HashMap. it checks that keys are in fact keys and
                    // evaluates the values before storing them.
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



        // EVALuate the ast in the supplied env. EVAL directly handles special forms
        // and functions (core or otherwise) and passes symbols and sequences to its helper
        // function eval_ast (with which it is mutually recursive).

        // Conceptually, EVAL invokes itself recursively to evaluate  some forms, sometimes in their
        // own new environment (e.g. (let)). In this implementation, Tail Call Optimisation (TCO)
        // is used to avoid recursion if possible. Specifically, the TCO version EVAL has ast and env
        // variables. It sets these to those passed in by the caller and then starts a while(true)
        // loop. If the last act of a pass through EVAL would have been to invoke EVAL recursively and
        // return that value, the TCO version sets the ast and env to whatever would have been
        // passed recursively, and goes back to the beginning of its loop, thus replacing 
        // recursion with (stack efficient) iteration. Recursion is still used in cases where
        // the recursive call isn't the last thing EVAL does before returning the value, e.g. in
        // (def ) clause where the returned value must be stored in the env.
        // The downside of TCO is the increased complexity of the EVAL function itself, which
        // is now harder to refactor into helper-functions for the various special forms.
        static MalVal EVAL(MalVal InitialAst, Env IntialEnv)
        {
            // The ast and env to be EVAL'd (and which might be reset in a TCO loop).
            MalVal ast = InitialAst;
            Env env = IntialEnv;

            // The TCO loop.
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

                        // See if we have a special form or something else.
                        string astListHead = astList[0] is MalSym ? ((MalSym)astList[0]).getName() : "__<*fn*>__";

                        switch (astListHead)
                        {
                            // Look for cases where the head of the ast list is a symbol (e.g. let, do, if) associated
                            // with a special form. If it is, we can discard th head symbol, and the remaining elements of the
                            // astList are the forms in the body of the special form. What happens to these depends on the form
                            // that has been encountered,
                            case "def":
                                // Should be something like (def a b). Set() symbol a to be the result of evaluating b
                                // and store the result in the env.
                                // Evaluate all elements of list using eval_ast, retun the final eval'd element.
                                if (astList.Count() != 3 || !(astList[1] is MalSym symbol))
                                {
                                    throw new MalEvalError("'def' should be followed by a symbol and a value");
                                }
                                // Can't TCO loop because we must store the result of the EVAL before returning it.
                                MalVal result = EVAL(astList[2], env);
                                env.Set(symbol, result);
                                return result;

                            case "let":
                                // Let syntax is (let <bindings-list> <result>), e.g. (let (p ( + 2 3) q (+ 2 p)) (+ p q)).
                                // Bindings can refer to earlier bindings in the new let environment or to symbols
                                // in the outer environment. Let defs can hide same-named symbols in the outer scope.

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
                                // Something like (do <form> <form>). EVAL all forms in the (do ) body in current environment.
                                // Some vars that make meaning clearer.
                                MalList formsInDoBody = astList.Rest();
                                int formsInDoBodyCount = formsInDoBody.Count();
                                if (formsInDoBodyCount == 0)
                                {
                                    // Empty (do )
                                    return malNil;
                                }
                                else
                                {

                                    // Use eval_ast to evaluate the DoBody forms up to but NOT including the last one.
                                    if (formsInDoBodyCount > 1)
                                    {
                                        // If there is just one DoBody form, this won't get called.
                                        eval_ast(formsInDoBody.GetRange(0, formsInDoBodyCount - 2), env);
                                    }

                                    // Prep for the TCO by using the last DoBody form as the new ast.
                                    ast = formsInDoBody[formsInDoBodyCount - 1];

                                    // TCO loop to process the final DoBody form.
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
                                if (!(astList[1] is MalList FnParamsList))
                                {
                                    // The 2nd element must be an arg list (possibly empty).
                                    throw new MalEvalError("Expected arg list but got: '" + astList[1].ToString(true) + "'");
                                }

                                MalVal FnBodyForms;
                                // Make a list of the forms in the funcion body (following the fn name and paramd list).
                                if( astList.Count() == 3)
                                {
                                    FnBodyForms = astList[2];
                                }
                                else
                                {
                                    // Either I've misunderstood something or the Reference doesn't handle some functions correctly.
                                    FnBodyForms = astList.GetRange(2, astList.Count()-1);
                                }

                                Env cur_env = env;

                                // This is different from C#-Mal but their version doesn't compile. Perhaps it's a C# version thing?
                                return new MalFunc(FnBodyForms, env, FnParamsList, args => EVAL(FnBodyForms, new Env(cur_env, FnParamsList, args)));

                            case "quote":
                                if (astList.Count() == 2)
                                {
                                    // Return the quoted thing.
                                    return astList[1];
                                }
                                throw new MalEvalError("quote expected 1 arg but had " + (astList.Count()-1).ToString());

                            case "quasiquote":
                                if (astList.Count() == 2)
                                {
                                    // Return the result of processing the quasiquote.
                                    ast = ProcessQuasiquote(astList[1]);
                                    continue;
                                }
                                throw new MalEvalError("quasiquotequote expected 1 arg but had " + (astList.Count() - 1).ToString());
                            default:
                                // Ast isn't a special form (it mey be a symbol or a function).
                                // eval_ast the ast 
                                MalList evaledList = (MalList)eval_ast(ast, env);

                                // Look at the head of the result.
                                MalVal listFirst = evaledList.First();

                                if (listFirst is MalFunc func)
                                {
                                    // It's a mal func.
                                    if(func.IsCore)
                                    {
                                        // Core funcs can be immediately applied and the value returned.
                                        return func.Apply(evaledList.Rest());
                                    }
                                    else
                                    {
                                        // Non-core function, i.e. one created by (fn...).
                                        // Use the Fn's ast and env info to set up a new EVAL (via TCO).

                                        // The ast to EVAL is the func's stored ast.
                                        ast = func.ast;

                                        // The new EVAL Env is made as follows...
                                        // The outer env is the func's env
                                        // The 'binds' are the func's params
                                        // The exprs are are the rest of the current ast.
                                        env = new Env(func.env, func.fparams, evaledList.Rest());

                                        // Apply = EVAL the function indirectly via the TCO loop.
                                        continue;
                                    }
                                }
                                else
                                {
                                    // This is typically the value of a symbol looked up by eval_ast.
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
