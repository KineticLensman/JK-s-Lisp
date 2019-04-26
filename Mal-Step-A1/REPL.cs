using System;

using static JKLApp.Types;
using static JKLApp.Core;

namespace JKLApp
{
    // READ.. EVAL .. PRINT .. LOOP .. until death (or EOF) sets you free.
    public class REPL
    {
        // The eval environment (keys and symbols).
        Env myEnv;

        // Setup the REPL.
        public REPL()
        {
            // Load built-in functions into the initial eval environment.
            try
            {
                myEnv = new Env(null);

                // Load core functions.
                foreach (var thing in JKLNameSpace)
                {
                    JKLSym mSym = new JKLSym(thing.Key);

                    if (myEnv.Find(mSym) == null)
                    {
                        myEnv.Set(mSym, thing.Value);
                    }
                    else
                    {
                        // The namespace itself may already have thrown an error but if not.
                        throw new JKLInternalError("Attempt to refine symbol: '" + mSym.ToString(true) + "' with '" + thing.Value.ToString(true) + "'");
                    }
                }
                // Load the special eval core function. We have to do it here to access the REPL env.
                myEnv.Set(new JKLSym("eval"), new JKLFunc(args =>
                {
                    return EVAL(args[0], myEnv);
                }));

                ////                EVAL(READ("(def! load-file (fn* (f) (eval (read-string (str \"(do \" (slurp f) \"))\")))))"), myEnv);
                //myEnv.Set(new JKLSym("load-file"), new JKLFunc(args =>
                //{
                //    if (args[0] is JKLString mStr)
                //    {
                //        try
                //        {
                //            string FileText = File.ReadAllText(mStr.unbox());

                //            // Ee have the text of the file. Now build a file loader to eval.
                //            StringBuilder sb = new StringBuilder();
                //            sb.Append("(do ");
                //            sb.Append(FileText);
                //            sb.Append(") ");


                //            return EVAL(read_str(sb.ToString()), myEnv);
                //        }
                //        catch (Exception e)
                //        {
                //            throw new JKLEvalError("slurp: " + e.Message);
                //        }
                //    }
                //    throw new JKLEvalError("load-file: expected filename but got '" + args[0] + "'");

                //}));


                // Add 'core' functions defined using JKL itself.

                EVAL(READ("(def! *ARGV* (list))"), myEnv);

                EVAL(READ("(def! not (fn* (a) (if a false true)))"), myEnv);

                // Establish a gensym mechanism
                EVAL(READ("(def! *gensym-counter* (atom 0))"), myEnv);
                EVAL(READ("(def! gensym (fn* [] (symbol (str \"G__\" (swap! *gensym-counter* (fn* [x] (+ 1 x)))))))"), myEnv);

                // This differs from the reference in that it uses a JKL-specific function slurp-do to
                // avoid the string quoting problem that can occur when trying to add a (do ... ) form
                // around the text retuned by raw slurp.
                EVAL(READ("(def! load-file (fn* (f) (eval (read-string (slurp-do f)))))"), myEnv);

                EVAL(READ("(defmacro! cond (fn* (& xs) (if (> (count xs) 0) (list 'if (first xs) (if (> (count xs) 1) (nth xs 1) (throw \"odd number of forms to cond\")) (cons 'cond (rest (rest xs)))))))"), myEnv);

                EVAL(READ("(defmacro! or (fn* (& xs) (if (empty? xs) nil (if (= 1 (count xs)) (first xs) (let* (condvar (gensym)) `(let* (~condvar ~(first xs)) (if ~condvar ~condvar (or ~@(rest xs)))))))))"), myEnv);

            }
            catch (System.TypeInitializationException e)
            {
                // Typically happens if there is a duplicate symbol in the namespace list
                Console.WriteLine("Unrecoverable error: " + e.InnerException.Message);
            }
            catch (JKLLookupError e)
            {
                Console.WriteLine(e.Message);
            }
            catch (JKLEvalError e)
            {
                Console.WriteLine(e.Message);
            }
            catch (JKLParseError e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // As per the Mal guide.
        static JKLVal READ(String str)
        {
            return Reader.read_str(str);
        }

        // Handle multi-lines.
        static JKLVal READMULTILINE(Reader.TokenQueue rdr)
        {
            return Reader.read_str_multiline(rdr);
        }

        // Helper functions for EVAL -----------------------------------------------------------

        // Helper function for quasiquotes. Return true if the arg is a non-empty list.
        static Boolean is_pair(JKLVal ast)
        {
            // This name might make sense in the sense of a cons cell / dotted pair.
            if (ast is JKLList jklList)
            {
                if (jklList.Count() > 0)
                {
                    return true;
                }
            }
            if (ast is JKLVector jklVec)
            {
                if (jklVec.Count() > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Name changed from the guide to highlight that the outcome is complicated.
        static JKLVal Quasiquote(JKLVal ast)
        {
            if (!is_pair(ast))
            {
                // Case 1.
                // If is_pair of ast is false: return a new list containing: a symbol named "quote" and ast.
                JKLList qList = new JKLList();

                qList.Add(new JKLSym("quote"));
                qList.Add(ast);

                //qList.Conj(new JKLSym("quote"), ast);
                return qList;
            }
            else
            {
                JKLSequence astSeq = (JKLSequence)ast;
                JKLVal a0 = astSeq[0];

                // Case 2:  if the first element of ast is a symbol named "unquote": return the second element of ast.
                if (a0 is JKLSym a0Sym && a0Sym.getName() == "unquote")
                {
                    // (qq (uq form)) -> form
                    return astSeq[1];
                }
                else if (is_pair(a0))
                {
                    // (qq (sq '(a b c))) -> a b c
                    // TODO - badly need some error checking here.
                    JKLVal a00 = ((JKLList)a0)[0];
                    JKLList a0AsList = (JKLList)a0;
                    if (a00 is JKLSym a00Sym && (a00Sym.getName() == "splice-unquote"))
                    {
                        // Case 3: 
                        JKLList newConcatForm = new JKLList();

                        // newConcatForm.Conj(new JKLSym("concat"), a0AsList[1], Quasiquote(astSeq.Rest()));
                        newConcatForm.Add(new JKLSym("concat"));

                        // Second element of first element of ast (ast[0][1])
                        newConcatForm.Add(a0AsList[1]);
                        newConcatForm.Add(Quasiquote(astSeq.Rest()));
                        return newConcatForm;
                    }
                }
                JKLList newConsForm = new JKLList();
                //                newConsForm.Conj(new JKLSym("cons"), Quasiquote(a0), Quasiquote(astSeq.Rest()));

                // Case 4: 
                // (qq (a b c)) -> (list (qq a) (qq b) (qq c))
                // (qq xs     ) -> (cons (qq (car xs)) (qq (cdr xs)))
                newConsForm.Add(new JKLSym("cons"));
                newConsForm.Add(Quasiquote(a0));

                JKLList restL = astSeq.Rest();
                newConsForm.Add(Quasiquote(astSeq.Rest()));

                //JKLList testConsForm = new JKLList();
                //testConsForm.Conj(new JKLSym("cons"), Quasiquote(a0), Quasiquote(astSeq.Rest()));

                return newConsForm;
            }
        }


        static Boolean IsMacroCall(JKLVal ast, Env env, out JKLFunc macro)
        {
            // Returns true if ast is a list that contains a symbol as the first element and that symbol
            // refers to a function in the env environment and that function has the is_macro attribute
            // set to true. Otherwise, it returns false

            // In the guide, this doesn't provide the function to the caller, which does the lookup again.

            // It's not a macro until proven otherwise.
            macro = null;
            if (ast is JKLList jklList)
            {
                if (jklList.Count() > 0)
                {
                    if (jklList[0] is JKLSym mSym)
                    {
                        // The find check avoids a Get exception. Had to cheat to pick this up. D'oh!
                        if (env.Find(mSym) != null)
                        {
                            JKLVal val = env.Get(mSym);
                            if (val is JKLFunc macroFunc && macroFunc.isMacro)
                            {
                                macro = macroFunc;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        static JKLVal MacroExpand(JKLVal ast, Env env)
        {
            while (IsMacroCall(ast, env, out JKLFunc macroFunc))
            {
                if (ast is JKLList macroArgs)
                {
                    //                    Console.WriteLine("MacroExpand " + macroFunc.ToString(true));
                    //                    JKLFunc mac = (JKLFunc)env.Get((JKLSym)macroArgs[0]);
                    ast = macroFunc.Apply(macroArgs.Rest());
                }
                else
                {
                    throw new JKLInternalError("MacroExpand expected list but got '" + ast + "'");
                }

                // throw new JKLInternalError("MacroExpand not implemented, but got: " + macroFunc.ToString(true));

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
        static JKLVal eval_ast(JKLVal ast, Env env)
        {
            // Switch on the type of the ast.
            switch ((Object)ast)
            {
                case JKLSym jklSym:
                    // Simplest case - look up and return a symbol's value.
                    return env.Get(jklSym);

                case JKLList jklList:
                    // Build and return a new list that contains the result of calling EVAL
                    // on each member of the original list.
                    JKLList derivedList = new JKLList();
                    for (int i = 0; i < jklList.Count(); i++)
                    {
                        derivedList.Add(EVAL(jklList.Get(i), env));
                    }
                    return derivedList;

                case JKLVector jklVec:
                    // Build and return a new vector.
                    JKLVector derivedVec = new JKLVector();
                    for (int i = 0; i < jklVec.Count(); i++)
                    {
                        derivedVec.Add(EVAL(jklVec.Get(i), env));
                    }
                    return derivedVec;

                case JKLHashMap jklHashMap:
                    // Return a new hash-map which consists of key-value pairs where the key is a key from the hash-map
                    // and the value is the result of calling EVAL on the corresponding value.
                    if (jklHashMap.Count() % 2 != 0)
                    {
                        throw new JKLEvalError("Hashmap requires an even number of elements forming key value pairs '" + jklHashMap.ToString(true) + "'");
                    }

                    JKLHashMap derivedHashMap = new JKLHashMap();
                    // Build and return a new HashMap. it checks that keys are in fact keys and
                    // evaluates the values before storing them.
                    // Work through successive key value pairs.
                    // Note - C#-Mal creates a dictionary, loads it and then passes the result to a Hashmap c'tor that takes a Dict.
                    for (int i = 0; i < jklHashMap.Count(); i += 2)
                    {
                        JKLVal key = jklHashMap.Get(i);
                        if (key is JKLString || key is JKLKeyword)
                        {
                            derivedHashMap.Add(key);
                            derivedHashMap.Add(EVAL(jklHashMap.Get(i + 1), env));
                        }
                        else
                        {
                            throw new JKLEvalError("Expecting a keyword or string as a HashMap key but got '" + key.ToString(true) + "'");
                        }

                    }
                    return derivedHashMap;

                default:
                    // It's not a symbol or a list.
                    return ast;
            }
        }


        // The guts of the language.
        // EVALuate the ast in the supplied env. EVAL directly handles special forms
        // and functions (core or otherwise) but passes symbols and sequences to its helper
        // function eval_ast (with which it is mutually recursive).

        // Conceptually, EVAL invokes itself recursively to evaluate some forms, sometimes in their
        // own new environment (e.g. (let)). In this implementation, Tail Call Optimisation (TCO)
        // is used to avoid recursion if possible. If the last act of a pass through EVAL would
        // have been to invoke EVAL recursively and return that value, the TCO version instead
        // sets ast and env to whatever would have been passed recursively, and goes back to the
        // beginning of its loop, thus replacing recursion with (stack efficient) iteration.
        // Recursion is still used in cases where
        // the recursive call isn't the last thing EVAL does before returning the value, e.g. in
        // (def )  where the returned value must be stored in the env.
        // The downside of TCO is the increased complexity of the EVAL function itself, which
        // is now harder to refactor into helper-functions for the various special forms.
        static public JKLVal EVAL(JKLVal OrigAst, Env env)
        {
            // The naming here reflects the C# version which I checked while debugging quasiquote.
            //  Sone local vars for clarity.
            JKLVal a1, a2, res;

            // The TCO loop.
            while (true)
            {
                if (!(OrigAst is JKLList))
                {
                    // The ast is not a list (e.g. a vector), so return the result of calling eval_ast on it.
                    return eval_ast(OrigAst, env);
                }
                // Try macro expansion before looking for special forms
                JKLVal expanded = MacroExpand(OrigAst, env);
                if (!(expanded is JKLList ast))
                {
                    // it's no longer a list after macroexpansion - e.g. a macro that evaluates to  a number.
                    return eval_ast(expanded, env);
                }

                if (ast.Count() <= 0)
                {
                    // Cheated. I used to have this before the macroexpand. 
                    // Empty list, return unchanged.
                    return ast;
                }

                // If here we have a non-empty list.
                // See if we have a special form or something else.
                string a0sym = ast[0] is JKLSym ? ((JKLSym)ast[0]).getName() : "__<*fn*>__";

                //                Console.WriteLine("ast[0]=" + ast[0].ToString(true) + ", a0sym= " + a0sym);

                switch (a0sym)
                {
                    // If astList[0] is a symbol (e.g. let, do, if) for a special form, use the rest of the astList
                    // elements as the body of the special form.
                    case "def!":
                        // Should be something like (def a b). Set() symbol a to be the result of evaluating b
                        // and store the result in the env.
                        if (ast.Count() != 3 || !(ast[1] is JKLSym symbol))
                        {
                            // This won't handle '(def (symbol a) 1)'.
                            throw new JKLEvalError("'def' should be followed by a symbol and a value but got '" + ast[1].ToString(true) + "'");
                        }
                        // Can't TCO loop because we must store the result of the EVAL before returning it.
                        JKLVal result = EVAL(ast[2], env);
                        env.Set(symbol, result);
                        return result;

                    case "let*":
                        // Let syntax is (let <bindings-list> <result>), e.g. (let (p ( + 2 3) q (+ 2 p)) (+ p q)) ==> 12.
                        // Bindings can refer to earlier bindings in the new let environment or to symbols
                        // in the outer environment. Let defs can hide same-named symbols in the outer scope.

                        if (ast.Count() < 3)
                        {
                            throw new JKLEvalError("'let*' should have two arguments (bindings-sequence and result), but instead had " + (ast.Count() - 1));
                        }
                        a1 = ast[1];
                        a2 = ast[2];

                        // Extract the first parameter - the bindings list. E.g. (p (+ 2 3) q (+ 2 p))
                        if (!(a1 is JKLSequence a1Seq))
                        {
                            throw new JKLEvalError("'let*' should be followed by a non-empty bindings-sequence and a result form, but got '" + a1 + "'");
                        }

                        // TODO - commented this out to get Mal step 3 to compile. Other changes may mean that this could come back.
                        //if (a1Seq.Count() <= 1 || a1Seq.Count() % 2 != 0)
                        //{
                        //    throw new JKLEvalError("'let' bindings-sequence should have an even number of entries");
                        //}

                        // Create a new Env - the scope of the Let form. It's discarded when done.
                        Env TempLetEnv = new Env(env);

                        // Process each pair of entries in the bindings list.
                        for (int i = 0; i < a1Seq.Count(); i += 2)
                        {
                            // The first element should be a 'key' symbol. E.g. 'p'.
                            if (!(a1Seq[i] is JKLSym bindingKey))
                            {
                                throw new JKLEvalError("'let' expected symbol but got: '" + a1Seq[i].ToString(true) + "'");
                            }
                            // The second element (e.g. (+ 2 3)) is the value of the key symbol in Let's environment
                            JKLVal val = EVAL(a1Seq[i + 1], TempLetEnv);

                            // Store the new value in the environment.
                            TempLetEnv.Set(bindingKey, val);
                        }

                        // TCO loop instead of EVAL(astList[2], TempLetEnv)
                        OrigAst = a2;
                        env = TempLetEnv;
                        break;

                    case "quote":
                        if (ast.Count() != 2)
                        {
                            throw new JKLEvalError("quote expects a single form but got '" + ast.ToString(true) + "'");
                        }
                        return ast[1];

                    case "quasiquote":
                        OrigAst = Quasiquote(ast[1]);
                        break;

                    case "macroexpand":
                        if (ast.Count() != 2)
                        {
                            throw new JKLEvalError("macroexpand expects a single form but got '" + ast.ToString(true) + "'");
                        }
                        return MacroExpand(ast[1], env);

                    case "do":
                        // Something like (do <form> <form>). EVAL all forms in the (do ) body in current environment.

                        if (!(ast is JKLList))
                        {
                            throw new JKLEvalError("'do' should be followed by a non-empty bindings list and a result form, but got '" + ast + "'");
                        }
                        JKLList formsInDoBody = (JKLList)ast.Rest();

                        if (formsInDoBody.Count() == 0)
                        {
                            // Empty (do )
                            return jklNil;
                        }

                        if (formsInDoBody.Count() > 1)
                        {
                            // If body contains a single form, this won't get calked.
                            eval_ast(formsInDoBody.GetRange(0, formsInDoBody.Count() - 2), env);
                        }
                        // Use TCO to evaluate the last form.
                        OrigAst = formsInDoBody[formsInDoBody.Count() - 1];
                        break;

                    case "if":
                        // If has the syntax (if <cond> <true-branch> <optional-false-branch>)
                        if (ast.Count() < 3 || ast.Count() > 4)
                        {
                            throw new JKLEvalError("'if' should have a condition, true branch and optional false branch");
                        }
                        a1 = ast[1];
                        // Evaluate the Cond part of the if.
                        JKLVal cond = EVAL(a1, env);

                        if (cond is JKLNil || cond is JKLFalse)
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
                                return jklNil;
                            }
                        }
                        else
                        {
                            // Eval the 'true' branch via TCO loop.
                            OrigAst = ast[2];
                            break;
                        }
                    case "try*":
                        // The try...catch form looks like  (try A (catch B C)). The try-form A (ast[1]) is evaluated and if it throws
                        // an exception with the builtin 'throw' function, then handler-form C (ast[2]) is evaluated with a new environment 
                        // that binds the symbol B to the value of the exception that was thrown.

                        // TODO experiment with nested catches / throws.

                        if (ast.Count() != 3)
                        {
                            // Badly formed catch.
                            var count = ast.Count() - 1;
                            throw new JKLEvalError("'try' expected a try clause followed by a catch but got " + count + " arg(s)");
                        }
                        // We could do additional tests here - before the try - to detect errors in the error handler. As it is,
                        // the tests are done as we step through the logic.

                        try
                        {
                            // Can't TCO because we'd drop out of the try. Successful EVAL here means that the 
                            // user code didn't use the builtin throw.
                            return EVAL(ast[1], env);
                        }
                        catch (JKLHostedLispError e)
                        {
                            // The user's code threw an exception.
                            if (ast[2] is JKLList catchList)
                            {
                                if (catchList.Count() != 3)
                                {
                                    throw new JKLEvalError("Expected 3 forms (catch <eBindSymbol> <eHandlerForm>) in catch clause '" + ast[2].ToString(true) + "' but actually got " + catchList.Count());
                                }

                                // TODO Check that catchList[0] is actually the keyword 'catch*' - for the moment anything will do.

                                // Check that we have a symbol to bind the thrown value to.
                                if (!(catchList[1] is JKLSym CatchSymbol))
                                {
                                    throw new JKLEvalError("'catch*' expected <eBindSymbol> but got '" + catchList[1].ToString(true) + "'");
                                }

                                // Create a new Env for the catch binding
                                Env CatchEnv = new Env(env);

                                // Bind the supplied catch symbol to the thrown error value.
                                CatchEnv.Set(CatchSymbol, e.GetExceptionValue());

                                // And TCO to evaluate the supplied error handler form (which may reference the thrown error).
                                OrigAst = catchList[2];
                                env = CatchEnv;
                                break;
                            }
                            else
                            {
                                // The catch clause wasn't a list.
                                throw new JKLEvalError("Expected (catch* <eBindSymbol> <eHandlerForm>) but got '" + ast[2].ToString(true) + "'");
                            }
                        }

                    case "fn*":
                        // TODO - this is per the guide but it is really create-lambda rather than def-fn.
                        // e.g.  (def a (fn (i) (* i 2))) or (def b (fn () (prn 1) (prn 2)))
                        // Also handles params supplied as a Vector rather than a sequence.
                        if (ast.Count() < 3)
                        {
                            throw new JKLEvalError("fn* must have an arg list and at least one body form");
                        }
                        if (!(ast[1] is JKLSequence FnParamsSeq))
                        {
                            // The 2nd element must be an arg list (possibly empty).
                            throw new JKLEvalError("fn* Expected arg sequence but got: '" + ast[1].ToString(true) + "'");
                        }

                        JKLVal FnBodyForms;
                        // Make a list of the forms in the funcion body (following the fn name and paramd list).
                        if (ast.Count() == 3)
                        {
                            FnBodyForms = ast[2];
                        }
                        else
                        {
                            // Either I've misunderstood something or the Reference doesn't handle some functions correctly.
                            FnBodyForms = ast.GetRange(2, ast.Count() - 1);
                        }

                        Env cur_env = env;

                        // This is different from C#-Mal but their version doesn't compile. Perhaps it's a C# version thing?
                        return new JKLFunc(FnBodyForms, env, FnParamsSeq, args => EVAL(FnBodyForms, new Env(cur_env, FnParamsSeq, args)));

                    case "defmacro!":
                        // E.g.  (defmacro unless (fn (pred a b) `(if ~pred ~b ~a)))
                        if (ast.Count() != 3)
                        {
                            throw new JKLEvalError("defmacro expects a macro name and a macro body but got '" + ast.ToString(true) + "'");
                        }

                        res = EVAL(ast[2], env);
                        if (res is JKLFunc macroFunc)
                        {
                            macroFunc.isMacro = true;
                        }
                        if (ast[1] is JKLSym a1MacroSym)
                        {
                            env.Set((a1MacroSym), res);
                            return res;
                        }
                        throw new JKLEvalError("'defmacro' should be followed by a symbol and a function, but got '" + ast.ToString(true) + "'");

                    default:
                        // Ast isn't a special form (it mey be a symbol or a function).
                        // eval_ast the ast 
                        JKLList evaledList = (JKLList)eval_ast(ast, env);

                        // Look at the head of the result.
                        JKLVal listFirst = evaledList.First();

                        if (listFirst is JKLFunc func)
                        {

                            if (func.IsCore)
                            {
                                // Core funcs don't have any special env or bindings so can be applied directly, exiting the TCO loop
                                return func.Apply(evaledList.Rest());
                            }
                            else
                            {
                                // Non-core function, i.e. one created by (fn...).
                                // The ast to EVAL is the func's stored ast.
                                OrigAst = func.ast;

                                // It's a JKL func. Make a new EVAL Env as follows...
                                // The outer env is the func's env,
                                // The 'binds' are the func's params,
                                // The exprs (the fn's args) are the rest of the current ast.
                                env = new Env(func.env, func.fparams, evaledList.Rest());

                                // Apply = EVAL the function indirectly via the TCO loop.
                                break;
                            }
                        }
                        else
                        {
                            // This is typically the value of a symbol looked up by eval_ast.
                            return evaledList;
                        }
                }
            }

        }

        String PRINT(JKLVal exp)
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
                    Reader.TokenQueue rdr = new Reader.TokenQueue(Reader.Tokenizer(line));

                    // Loop until all tokens on the line have been consumed. This handles lines
                    // like (a b) (c d) - where Mal REPL would silently ignore (c d)
                    while (rdr.Peek() != null)
                    {
                        // Parse input to create a JKL abstract syntax tree. The parser
                        // attempts to reload the reader if it runs out of tokens mid form.
                        JKLVal ast = READMULTILINE(rdr);

                        // Evaluate the ast and print the result.
                        Console.WriteLine(PRINT(EVAL(ast, myEnv)));
                    }
                }
                catch (JKLParseError e)
                {
                    Console.WriteLine(e.Message);
                }
                catch (JKLEvalError e)
                {
                    Console.WriteLine(e.Message);
                }
                catch (JKLInternalError e)
                {
                    Console.WriteLine(e.Message);
                }
                catch (JKLLookupError e)
                {
                    Console.WriteLine(e.Message);
                }
                catch (JKLHostedLispError e)
                {
                    // If here, some hosted code contains a throw that isn't inside a corresponding catch.
                    Console.WriteLine("Uncaught hosted lisp exception: '" + e.GetExceptionValue().ToString(true) + "'");
                }
            }
        }
    }
}
