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
            catch (MalLookupError e)
            {
                Console.WriteLine(e.Message);
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

        // Helper functions for EVAL -----------------------------------------------------------

        // Helper function for quasiquotes. Return true if the arg is a non-empty list.
        static Boolean is_pair(MalVal ast)
        {
            // This name might make sense in the sense of a cons cell / dotted pair.
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
        // TODO logic here is broken - see the above for the right if nesting. 
        static MalVal ProcessQuasiquote(MalVal ast)
        {
            if (!is_pair(ast))
            {
                // Case 1.
                // If is_pair of ast is false: return a new list containing: a symbol named "quote" and ast.
                MalList qList = new MalList();

                qList.Add(new MalSym("quote"));
                qList.Add(ast);
                return qList;
            }
            else
            {
                MalList astList = (MalList)ast;
                MalVal a0 = astList[0];

                // Case 2:  if the first element of ast is a symbol named "unquote": return the second element of ast.
                if (a0 is MalSym a0Sym && a0Sym.getName() == "unquote")
                {
                    return astList[1];
                }
                else if (is_pair(a0))
                {
                    // TODO - badly need some error checking here.
                    MalVal a00 = ((MalList)a0)[0];
                    MalList a0First = (MalList)a0;
                    if (a00 is MalSym a00Sym && (a00Sym.getName() == "splice-unquote"))
                    {
                        // Case 3: 
                        MalList newConcatForm = new MalList();
                        newConcatForm.Add(new MalSym("concat"));

                        // Second element of first element of ast (ast[0][1])
                        newConcatForm.Add(a0First[1]);
                        newConcatForm.Add(ProcessQuasiquote(astList.Rest()));
                        return newConcatForm;
                    }
                }
                MalList newConsForm = new MalList();

                // Case 4: 
                newConsForm.Add(new MalSym("cons"));
                newConsForm.Add(ProcessQuasiquote(astList[0]));
                newConsForm.Add(ProcessQuasiquote(astList.Rest()));
                return newConsForm;
            }
        }
       

        static Boolean IsMacroCall(MalVal ast, Env env, out MalFunc macro)
        {
            // Returns true if ast is a list that contains a symbol as the first element and that symbol
            // refers to a function in the env environment and that function has the is_macro attribute
            // set to true. Otherwise, it returns false

            // In the guide, this doesn't provide the function to the caller, which does the lookup again.

            // It's not a macro until proven otherwise.
            macro = null;
            if (ast is MalList malList)
            {
                if (malList.Count() > 0)
                {
                    if (malList[0] is MalSym mSym)
                    {
                        // The find check avoids a Get exception. Had to cheat to pick this up. D'oh!
                        if (env.Find(mSym) != null)
                        {
                            MalVal val = env.Get(mSym);
                            if (val is MalFunc macroFunc)
                            {
                                if (macroFunc.isMacro)
                                {
                                    //Console.WriteLine("IsMacroCall true " + macroFunc.ToString(true));
                                    macro = macroFunc;
                                    return true;
                                }

                            }
                        }
                        else
                        {
                            // This picks up the names of special formss
                            //Console.WriteLine("oops '" + mSym.ToString(true) + "'");
                        }
                    }
                }
            }
            return false;
        }

        static MalVal MacroExpand(MalVal ast, Env env)
        {

            while (IsMacroCall(ast, env, out MalFunc macroFunc))
            {
                if (ast is MalList macroArgs)
                {
                    //                    Console.WriteLine("MacroExpand " + macroFunc.ToString(true));
//                    MalFunc mac = (MalFunc)env.Get((MalSym)macroArgs[0]);
                    ast  = macroFunc.Apply(macroArgs.Rest());
                }
                else
                {
                    throw new MalInternalError("MacroExpand expected list but got '" + ast + "'");
                }

                // throw new MalInternalError("MacroExpand not implemented, but got: " + macroFunc.ToString(true));

            }
            // It calls is_macro_call with ast and env and loops while that condition is true
            // Inside the loop, the first element of the ast list(a symbol), is looked up in the
            // environment to get the macro function.This macro function is then called / applied
            // with the rest of the ast elements(2nd through the last) as arguments. The return
            // value of the macro call becomes the new value of ast. When the loop completes
            // because ast no longer represents a macro call, the current value of ast is returned
            return ast;
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
        // return that value, the TCO version instead sets ast and env to whatever would have been
        // passed recursively, and goes back to the beginning of its loop, thus replacing 
        // recursion with (stack efficient) iteration. Recursion is still used in cases where
        // the recursive call isn't the last thing EVAL does before returning the value, e.g. in
        // (def )  where the returned value must be stored in the env.
        // The downside of TCO is the increased complexity of the EVAL function itself, which
        // is now harder to refactor into helper-functions for the various special forms.
        static MalVal EVAL(MalVal OrigAst, Env env)
        {
            // The naming here reflects the C# version which I chedked while debugging quasiquote.
            //Somne local vars for clarity.
            MalVal a1, a2, res;

            // The TCO loop.
            while (true)
            {
                if (OrigAst is MalList)
                {
                    // Try macro expansion before looking for special forms
                    MalVal expanded = MacroExpand(OrigAst, env);
                    if (!(expanded is MalList))
                    {
                        // it's no longer a list after macroexpansion - e.g. a macro that evaluates to  a number.
                        return eval_ast(expanded, env);
                    }

                    if(!(expanded is MalList ast))
                    {
                        throw new MalInternalError("Macroexpand didn't return a List");
                    }

                    if (ast.Count() <= 0)
                    {
                        // Cheated. I used to have this before the macroexpand. 
                        // Empty list, return unchanged.
                        return ast;
                    }

                        // See if we have a special form or something else.
                    string a0sym = ast[0] is MalSym ? ((MalSym)ast[0]).getName() : "__<*fn*>__";

                    switch (a0sym)
                    {
                        // If astList[0] is a symbol (e.g. let, do, if) for a special form, use the rest of the astList
                        // elements as the body of the special form.
                        case "def":
                            // Should be something like (def a b). Set() symbol a to be the result of evaluating b
                            // and store the result in the env.
                            if (ast.Count() != 3 || !(ast[1] is MalSym symbol))
                            {
                                throw new MalEvalError("'def' should be followed by a symbol and a value");
                            }
                            // Can't TCO loop because we must store the result of the EVAL before returning it.
                            MalVal result = EVAL(ast[2], env);
                            env.Set(symbol, result);
                            return result;

                        case "let":
                            // Let syntax is (let <bindings-list> <result>), e.g. (let (p ( + 2 3) q (+ 2 p)) (+ p q)).
                            // Bindings can refer to earlier bindings in the new let environment or to symbols
                            // in the outer environment. Let defs can hide same-named symbols in the outer scope.

                            if (ast.Count() < 3)
                            {
                                throw new MalEvalError("'let' should have two arguments (bindings-list and result), but instead had " + (ast.Count() - 1));
                            }
                            a1 = ast[1];
                            a2 = ast[2];

                            // Extract the first parameter - the bindings list. E.g. (p (+ 2 3) q (+ 2 p))
                            if (!(a1 is MalList a1List))
                            {
                                throw new MalEvalError("'let' should be followed by a non-empty bindings list and a result form, but got '" + a1 + "'");
                            }

                            if (a1List.Count() <= 1 || a1List.Count() % 2 != 0)
                            {
                                throw new MalEvalError("'let' bindings list should have an even number of entries");
                            }

                            // Create a new Env - the scope of the Let form. It's discarded when done.
                            Env TempLetEnv = new Env(env);

                            // Process each pair of entries in the bindings list.
                            for (int i = 0; i < a1List.Count(); i += 2)
                            {
                                // The first element should be a 'key' symbol. E.g. 'p'.
                                if (!(a1List[i] is MalSym bindingKey))
                                {
                                    throw new MalEvalError("'let' expected symbol but got: '" + a1List[i].ToString(true) + "'");
                                }
                                // The second element (e.g. (+ 2 3)) is the value of the key symbol in Let's environment
                                MalVal val = EVAL(a1List[i + 1], TempLetEnv);

                                // Store the new value in the environment.
                                TempLetEnv.Set(bindingKey, val);
                            }

                            // TCO loop instead of EVAL(astList[2], TempLetEnv)
                            OrigAst = a2;
                            env = TempLetEnv;
                            break;

                        case "quote":
                            if(ast.Count() != 2)
                            {
                                throw new MalEvalError("quote expects a single form but got '" + ast.ToString(true) + "'");
                            }
                            return ast[1];

                        case "quasiquote":
                            OrigAst = ProcessQuasiquote(ast[1]);
                            break;

                        case "macroexpand":
                            if (ast.Count() != 2)
                            {
                                throw new MalEvalError("macroexpand expects a single form but got '" + ast.ToString(true) + "'");
                            }
                            return MacroExpand(ast[1], env);

                        case "do":
                            // Something like (do <form> <form>). EVAL all forms in the (do ) body in current environment.

                            if (!(ast is MalList))
                            {
                                throw new MalEvalError("'do' should be followed by a non-empty bindings list and a result form, but got '" + ast + "'");
                            }
                            MalList formsInDoBody = ast.Rest();

                            if (formsInDoBody.Count() == 0)
                            {
                                // Empty (do )
                                throw new MalEvalError("Empty 'do'");
                            }

                            if (formsInDoBody.Count() > 1)
                            {
                                // If body contains a single form, make sure it doesn't get called twice.
                                eval_ast(formsInDoBody.GetRange(0, formsInDoBody.Count() - 2), env);
                            }
                            // Use TCO to evaluate the last form.
                            OrigAst = formsInDoBody[formsInDoBody.Count() - 1];
                            break;

                        case "if":
                            // If has the syntax (if <cond> <true-branch> <optional-false-branch>)
                            if (ast.Count() < 3 || ast.Count() > 4)
                            {
                                throw new MalEvalError("'if' should have a condition, true branch and optional false branch");
                            }
                            a1 = ast[1];
                            // Evaluate the Cond part of the if.
                            MalVal cond = EVAL(a1, env);

                            if (cond is MalNil || cond is MalFalse)
                            {
                                // Cond is nil or false.
                                if (ast.Count() > 3)
                                {
                                    // Eval the 'false' branch via TCO loop.
                                    OrigAst = ast[3];
                                    break;
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
                                OrigAst = ast[2];
                                break;
                            }
                        case "fn":
                            // TODO - this is per the guide but it is really create-lambda rather than def-fn.
                            // e.g.  (def a (fn (i) (* i 2))) or (def b (fn () (prn 1) (prn 2)))
                            if (ast.Count() < 3)
                            {
                                throw new MalEvalError("fn must have an arg list and at least one body form");
                            }
                            if (!(ast[1] is MalList FnParamsList))
                            {
                                // The 2nd element must be an arg list (possibly empty).
                                throw new MalEvalError("Expected fn arg list but got: '" + ast[1].ToString(true) + "'");
                            }

                            MalVal FnBodyForms;
                            // Make a list of the forms in the funcion body (following the fn name and paramd list).
                            if( ast.Count() == 3)
                            {
                                FnBodyForms = ast[2];
                            }
                            else
                            {
                                // Either I've misunderstood something or the Reference doesn't handle some functions correctly.
                                FnBodyForms = ast.GetRange(2, ast.Count()-1);
                            }

                            Env cur_env = env;

                            // This is different from C#-Mal but their version doesn't compile. Perhaps it's a C# version thing?

                            return new MalFunc(FnBodyForms, env, FnParamsList, args => EVAL(FnBodyForms, new Env(cur_env, FnParamsList, args)));

                        case "defmacro":
                            // E.g.  (defmacro unless (fn (pred a b) `(if ~pred ~b ~a)))

                            if (ast.Count() != 3)
                            {
                                throw new MalEvalError("defmacro expects a macro name and a macro body but got '" + ast.ToString(true) + "'");
                            }
                            a1 = ast[1];
                            a2 = ast[2];
                            res = EVAL(a2, env);

                            if(res is MalFunc macroFunc)
                            {
                                macroFunc.isMacro = true;
                            }
                            if(a1 is MalSym a1MacroSym)
                            {
                                env.Set((a1MacroSym), res);
                                return res;
                            }
                            throw new MalEvalError("'defmacro' should be followed by a symbol and a function, but got '" + ast.ToString(true) + "'");

                        default:
                            // Ast isn't a special form (it mey be a symbol or a function).
                            // eval_ast the ast 
                            MalList evaledList = (MalList)eval_ast(ast, env);

                            // Look at the head of the result.
                            MalVal listFirst = evaledList.First();

                            if (listFirst is MalFunc func)
                            {

                                if(func.IsCore)
                                {
                                    // Core funcs don't have any special env or bindings so can be applied directly.
                                    return func.Apply(evaledList.Rest());
                                }
                                else
                                {
                                    // Non-core function, i.e. one created by (fn...).
                                    // The ast to EVAL is the func's stored ast.
                                    OrigAst = func.ast;

                                    // It's a mal func.
                                    // The new EVAL Env is made as follows...
                                    // The outer env is the func's env
                                    // The 'binds' are the func's params
                                    // The exprs are are the rest of the current ast.
                                    env = new Env(func.env, func.fparams, evaledList.Rest());

                                    // Apply = EVAL the function indirectly via the TCO loop.
                                    break;
                                }
                            }
                            else
                            {
                                // This is typically the value of a symbol looked up by eval_ast.
                                return listFirst;
                            }
                    }
                }
                else
                {
                    // If ast is not a list (e.g. a vector), return the result of calling eval_ast on it.
                    return eval_ast(OrigAst, env);
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
                catch (MalLookupError e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
