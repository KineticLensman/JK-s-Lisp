﻿using System;
using static Mal.Types;
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

        public Env(Env outer, MalList binds, MalList exprs)
        {
            // A key-MalVal dictionary.
            data = new Dictionary<string, MalVal>();

            // The scope we are created in. Null is taken to mean the REPL itself.
            this.outer = outer;

            if(binds.Count() != exprs.Count())
            {
                throw new MalEvalError("Incorrect number of fn arguments?");
            }
            // Bind (set) each element (symbol) to the respective elements of the exprs list
            for ( var i = 0; i < binds.Count(); i++)
            {
                if(binds[i] is MalSym symbol)
                {
                    Set(symbol, exprs[i]);
                }
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
            if (e != null && e.data.TryGetValue(keySymbol.getName(), out MalVal value))
            {
                return value;
            }
            throw new MalEvalError("Symbol not found '" + keySymbol.ToString(true) + "'");
        }
    }
}
