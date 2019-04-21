using System;
using static Mal.Types;
using static Mal.Core;
using System.Collections.Generic;
using System.Text;

namespace Mal
{
    public class Env
    {
        // Map strings to MalVals. Allows use of string equality to detect dupes.
        private Dictionary<string, MalVal> data;
        private Env outer;

        public Env (Env outer)
        {
            // A key-MalVal dictionary.
            data = new Dictionary<string, MalVal>();
            
            // The scope we are created in. Null is taken to mean the REPL itself.
            this.outer = outer;
        }

        // Given a sequence of symbols (binds) and a list of expressions, work through the
        // sequences, setting each bind in turn to the value of its corresponding expression.
        // The primary application is setting function arguments (binds) to their values (expressions).
        // If one of the binds is the Var args character ('&') the next bind is associated with
        // all of the remaining exprs. Trapped errors include non-symbols being used as binds
        // or cases where the binds and expr sequences are of incompatible lengths.
        public Env(Env outer, MalSequence binds, MalSequence exprs)
        {
            // A key-MalVal dictionary.
            data = new Dictionary<string, MalVal>();

            // The scope we are created in. Null is taken to mean the outermost REPL's env.
            this.outer = outer;
            int bindsProcessed = 0;

            //  (def f (fn (a & b) (list a b))) -- okay
            // Bind (set) each element (symbol) to the respective elements of the exprs list. 
            for (bindsProcessed = 0; bindsProcessed < binds.Count(); bindsProcessed++)
            {
                if (binds[bindsProcessed] == malVarArgsChar)
                {
                    // E.g. (fn f (a1 a2 & restArgs) (list a1 a2 restargs )) called with (f 1 2 3 4)
                    if(bindsProcessed == binds.Count()-1)
                    {
                        // malVarArgsChar isn't followed by the name of the varargs parameter. 
                        throw new MalEvalError("Expected symbol after & in arg list");
                        // If there are multiple symbols after '&' all but the first will be trapped as 
                        // unbound symbols later on.
                    }
                    if (binds[bindsProcessed + 1] is MalSym symbol)
                    {
                        // Bind the rest of the expressions to the symbol following the varargs char.
                        Set(symbol, exprs.GetRange(bindsProcessed, exprs.Count()));
                        return;
                    }
                    else
                    {
                        // Not sure if this can heppen but just in case.
                        throw new MalEvalError("Expected symbol after & but got '" + binds[bindsProcessed + 1].ToString(true));
                    }
                    //
                }
                else if (binds[bindsProcessed] is MalSym symbol)
                {
                    // Bind an expression to a binding symbol.
                    if (bindsProcessed >= exprs.Count())
                    {
                        throw new MalEvalError("Incorrect number of arguments " + exprs.Count());
                    }
                    Set(symbol, exprs[bindsProcessed]);
                }
                else
                {
                    throw new MalEvalError("expected symbol to bind but got '" + binds[bindsProcessed].ToString(true) + '"');
                }
            }
            if(bindsProcessed < exprs.Count())
            {
                // We ran out of bindings although there are expressions remaining.
                throw new MalEvalError(exprs.ToString(true) + " - more expressions (" + exprs.Count() + ") than bindings: " + bindsProcessed);
            }
        }

        public void Set(MalSym keySymbol, MalVal value)
        {
            // Takes a symbol key and a mal value and adds them to the environment.
            if( ! data.TryAdd(keySymbol.getName(), value))
            {
                // Symbol can shadow an equivalent in an outer scope but cannot be duped. 
                throw new MalEvalError("Attempt to redefine '" + keySymbol.ToString(true) + "'");
            }
        }

        // Search for and return the environment (scope) that contains a target symbol.
        public Env Find(MalSym keySymbol)
        {
            if(data.ContainsKey(keySymbol.getName()))
            {
                // Symbol exists in the current scope.
                return this;
            }
            else if (outer != null)
            {
                // Recurse to search for the symbol in an outer scope.
                return outer.Find(keySymbol);
            }
            else
            {
                // Symbol is not defined.
                return null;
            }
        }

        // Return the value of a target symbol in this or an outer scope.
        public MalVal Get(MalSym keySymbol)
        {

            Env e = Find(keySymbol);
            if (e == null)
            {
                switch (keySymbol.ToString(true))
                {
                    // Some symbols are only valid when used in a particular context.

                    case "unquote":
                        throw new MalEvalError("'unquote' used incorrectly (missing macro?)");
                    case "quasiquote":
                        throw new MalEvalError("'quasiquote' used incorrectly (missing macro?)");
                    case "splice-unquote":
                        throw new MalEvalError("'splice-unquote' used incorrectly (missing macro?)");
                }
                //// If here the symbol simply hasn't been defined or is mis-spelt.
                throw new MalLookupError("Get - Symbol not found '" + keySymbol.ToString(true) + "'");
            }
            else
            {
                if (e.data.TryGetValue(keySymbol.getName(), out MalVal value))
                {
                    return value;
                }
                else
                {
                    throw new MalInternalError("Get - Find successful but symbol retrieval failed '" + keySymbol.ToString(true) + "'");
                }
            }
        }
    }
}
