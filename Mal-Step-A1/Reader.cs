using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;



using static JKLApp.Types;
using static JKLApp.Core;


namespace JKLApp
{
    public class Reader
    {
        // A stateful reader that gives out a list of tokens. Being able to 
        // create multiple Reader instances means that we can handle multiple
        // files simultaneously (e.g. when evaluating a file loaded by slurp).

            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Should be TokenQueue or similar
        public class TokenQueue
        {
            // A queue of tokens that can be handed out to the reader. 
            // This allows more flexibility than the C#-Mal, which uses a finite
            // list of tokens with a cursor, and which raises an error if a form
            // spans multiple lines.
            private Queue<string> ourTokens = new Queue<string>();

            private void QueueNewTokens(List<string> tokenList)
            {
                // Queue the tokens for subsequent access.
                foreach (var str in tokenList)
                {
                    ourTokens.Enqueue(str);
                }
            }

            // Construct a Reader instance for an initial set of tokens.
            public TokenQueue(List<string> tokenList)
            {
                QueueNewTokens(tokenList);
            }

            // Pop the first token. Callers are responsible for ensuring it is safe to do so.
            public string Next()
            {
                if (ourTokens.Count <= 0)
                {
                    throw new JKLInternalError("Tried to read an empty token queue");
                }
                return ourTokens.Dequeue();
            }

            // Return first token or null if there are no tokens.
            public string Peek()
            {
                if (ourTokens.TryPeek(out string s))
                {
                    return s;
                }
                else
                {
                    return null;
                }
            }

            // Clear the token queue, e.g. if there are extraneous tokens left after a read.
            public void Flush()
            {
                ourTokens.Clear();
            }

            // Try to read more tokens. Supports multi-line forms.
            public void LoadMoreTokens(char start, char end)
            {
                Console.Write("...>");
                Console.Out.Flush();
                string source = Console.ReadLine();

                if (source == null)
                {
                    // EOF during a form.
                    throw new JKLParseError("'" + start + "' doesn't have a matching '" + end + "'");
                }
                else
                {
                    // Make the new tokens available for reads.
                    QueueNewTokens(Tokenizer(source));
                }
            }
        }

        // This one is what I used for most of the steps, until I got to step A. Perhaps I missed somethiong out earlier.
        //            string pattern = @"[\s ,]*(~@|[\[\]{}()'`~@]|""(?:[\\].|[^\\""])*""|;.*|[^\s \[\]{}()'""`~@,;]*)";


        // Takes an input string and return substrings corresponding to MAL tokens.
        // Additional analysis is performed elsewhere (e.g. to detect / parse ints and strings).
        static public List<string> Tokenizer(string source)
        {
            // Initialise the token list.
            List<string> tokens = new List<string>();

            // Define a regex pattern (as per the MAL guide) whose groups match the MAL syntax.
            //            string pattern = @"[\s ,]*(~@|[\[\]{}()'`~@]|""(?:[\\].|[^\\""])*""|;.*|[^\s \[\]{}()'""`~@,;]*)";
            string pattern = @"[\s ,]*(~@|[\[\]{}()'`~@]|""(?:[\\].|[^\\""])*""|;.*|[^\s \[\]{}()'""`~@,;]*)";
            //                 empty  ~@ | specials     |   double quotes      |;  | non-specials

            // Break the input string into its constituent tokens.
            string[] result = Regex.Split(source, pattern);

            // Process the candidate tokens resulting from the split.
            for (int ctr = 0; ctr < result.Length; ctr++)
            {
                string candidate = result[ctr];

                // Process each match, ignoring empty strings inserted by Split().
                if (candidate.Length > 0)
                {
                    // Stop if the match is a comment.
                    if (candidate.StartsWith(';'))
                    {
                        // TODO - this was a break - but changed to continue while trying to parse Mal files containing comments in step A.
                        continue;
                    }
                    // Disallow input strings that aren't terminated. 
                    if (candidate.Length == 1 && candidate.StartsWith('\"'))
                    {
                        throw new JKLParseError("Non-terminating or multi-line string");
                    }
                    tokens.Add(result[ctr]);
                }
            }
            return tokens;
        }

        // Read a JKLVal form - which is either an atom or a sequence.
        static public JKLVal read_form(TokenQueue TQ)
        {
            if (TQ.Peek() == null)
            {
                // Reader is empty - caused by a comment line in the input.
                return null;
            }
            else if (TQ.Peek().StartsWith('('))
            {
                // Create a new List and read it's body.
                return read_list(TQ, new JKLList(), '(', ')');
            }
            else if (TQ.Peek().StartsWith('['))
            {
                // Create a new Vector and read it's body.
                return read_list(TQ, new JKLVector(), '[', ']');
            }
            else if (TQ.Peek().StartsWith('{'))
            {
                // Create a new HashMap and read it's body. EVAL checks it has valid key val pairs.
                return read_list(TQ, new JKLHashMap(), '{', '}');
            }
            else if (TQ.Peek().StartsWith(')') || TQ.Peek().StartsWith(']') || TQ.Peek().StartsWith('}'))
            {
                // A sequence close character that doesn't match a start.
                // This correctly handles a case like [1 ( 2 ] 3).
                throw new JKLParseError("Expecting sequence or atom but got '" + TQ.Peek() + "'");
            }

            else if (TQ.Peek().StartsWith('&'))
            {
                // Reader macro. We have '&atomName'. Convert this into (deref atomName);
                string varArgAtom = TQ.Peek();
                if (varArgAtom.Length == 1)
                {
                    // Treat a solo '&' as a varargs symbol,
                    TQ.Next();
                    return jklVarArgsChar;
                }
                else
                {
                    throw new JKLParseError("'&' can't start a symbol name: '" + varArgAtom.ToString() + "'");

                }
            }
            else if (TQ.Peek().StartsWith('@'))
            {
                TQ.Next();
                // Build a deref form.
                JKLList derefForm = new JKLList();
                derefForm.Add(new JKLSym("deref"));
                derefForm.Add(read_form(TQ));
                return derefForm;
            }
            else if (TQ.Peek().StartsWith('\''))
            {
                // Return a list containing a quote symbol and the quoted form.
                TQ.Next();
                JKLList quoteForm = new JKLList();
                quoteForm.Add(new JKLSym("quote"));
                quoteForm.Add(read_form(TQ));
                return quoteForm;
            }
            else if (TQ.Peek().StartsWith('`'))
            {
                // Return a list containing a quasiquote symbol and the quasiquoted form.
                TQ.Next();
                JKLList quasiquoteForm = new JKLList();
                quasiquoteForm.Add(new JKLSym("quasiquote"));
                quasiquoteForm.Add(read_form(TQ));
                return quasiquoteForm;
            }
            else if (TQ.Peek().StartsWith("~@"))
            {
                // Return a list containing a splice-unquote symbol and the next form.
                // Dammit! I'd missed the '~' here and spent several days wondering why (or ...) didn't work.
                TQ.Next();
                JKLList quasiquoteForm = new JKLList();
                quasiquoteForm.Add(new JKLSym("splice-unquote"));
                quasiquoteForm.Add(read_form(TQ));
                return quasiquoteForm;
            }
            else if (TQ.Peek().StartsWith('~'))
            {
                // Return a list containing an unquote symbol and the next form.
                TQ.Next();
                JKLList quasiquoteForm = new JKLList();
                quasiquoteForm.Add(new JKLSym("unquote"));
                quasiquoteForm.Add(read_form(TQ));
                return quasiquoteForm;
            }
            else if (TQ.Peek().StartsWith('^'))
            {
                // Return a new list that contains the symbol "with-meta" and the result of reading the
                // next next form (2nd argument) (read_form) and the next form (1st argument) in that order 
                TQ.Next();
                JKLList withMetaForm = new JKLList();
                withMetaForm.Add(new JKLSym("with-meta"));
                JKLVal firstArg = read_form(TQ);
                JKLVal secondArg = read_form(TQ);
                withMetaForm.Add(secondArg);
                withMetaForm.Add(firstArg);
                return withMetaForm;
            }
            else
            {
                // This isn't a list so parse it as an atom.
                return read_token(TQ);
            }
        }

        // Read a JKLSequence, checking that it starts and terminates correctly.
        // Named read_list to follow the ref, but has been genericized to handle vectors as well.
        static public JKLSeqBase read_list(TokenQueue TQ, JKLSeqBase sequence, char start, char end)
        {
            // Check that we are in fact at the start of a list.
            string token = TQ.Next();
            if (token[0] != start)
            {
                // Parse error - probably internal if the list code is correct.
                throw new JKLInternalError("Sequence expected '" + start + "' but got: " + token);
            }

            // Use read_form to get the list's contents, accumulating them into the list.
            while (true)
            {
                token = TQ.Peek();

                if (token != null)
                {
                    // We are in the list or at the end.
                    if (token[0] == end)
                    {
                        // Reached valid end of list. Consume the end char.
                        TQ.Next();
                        // And we are done.
                        break;
                    }
                    // Mutually recurse to read the next list element.
                    JKLVal newVal = read_form(TQ);
                    sequence.Add(newVal);
                }
                else
                {
                    // The input has finished but the list hasn't. Try to get more input.
                    TQ.LoadMoreTokens(start, end);
                }
            }

            return sequence;
        }

        static protected bool IsDigit(string tokenStart)
        {
            return tokenStart.IndexOfAny("0123456789".ToCharArray()) >= 0;
        }

        // Read a number. The sign has already been determined.
        static protected JKLVal ParseNumber(string token, bool isPositive)
        {
            if (double.TryParse(token, out double number))
            {
                if (!isPositive)
                {
                    number = -number;
                }
                return new JKLNum(number);
            }
            throw new JKLParseError("Badly formed float: '" + token + "'");
        }

        static public JKLVal read_token(TokenQueue TQ)
        {
            // "If you have a problem and you think regex is the answer, now you have two problems!"
            // Unlike the referenceC#-Mal, read_token handles floats and badly-formed symbols.
            // In the Mal Guide this is called read_atom but I renamed it to avoid confusion
            // with Mal Atoms.
            string tokenToRead = TQ.Next();

            if (tokenToRead.Length <= 0)
            {
                // TODO  - this may stop comments being handled correctly.
                throw new JKLInternalError("Reader has returned empty string");
            }
            switch (tokenToRead[0])
            {
                case '+':
                    if (tokenToRead.Length == 1)
                    {
                        // Token is a solo '+', not the beginning of a number.
                        return new JKLSym(tokenToRead);
                    }
                    // Skip the sign and extract a positive number;
                    return ParseNumber(tokenToRead.Substring(1), true);
                case '-':
                    if (tokenToRead.Length == 1)
                    {
                        // Token is a solo '-', not the beginning of a number.
                        return new JKLSym(tokenToRead);
                    }
                    // Skip the sign and extract a negative number;
                    return ParseNumber(tokenToRead.Substring(1), false);
                case '.':
                    // An initial '.' is only allowed at the start of a number, as in '.2'.
                    return ParseNumber(tokenToRead, true);
                case '\"':
                    if (tokenToRead.EndsWith("\""))
                    {
                        // Get rid of the quotes before storing the string. Seems right although 
                        // I haven't confirmed by checking the reference version.
                        char[] charsToTrim = { '"' };
                        tokenToRead = tokenToRead.Trim(charsToTrim);
                        return new JKLString(tokenToRead);
                    }
                    // TODO - never reaches this point. The reader regex seems to throw away '"' chars if there is only 1.
                    throw new JKLParseError("String '" + tokenToRead + "' lacks a closing thingy");
                case ':':
                    // Handle a keyword
                    if (tokenToRead.Length == 1)
                    {
                        // Can't have a solo colon.
                        throw new JKLParseError("':' must be followed by a keyword");
                    }
                    return new JKLKeyword(tokenToRead);
                default:
                    if (IsDigit(tokenToRead[0].ToString()))
                    {
                        // Token seems to be an unsigned number.
                        return ParseNumber(tokenToRead, true);
                    }
                    else if (tokenToRead == "nil")
                    {
                        return jklNil;
                    }
                    else if (tokenToRead == "true")
                    {
                        return malTrue;
                    }
                    else if (tokenToRead == "false")
                    {
                        return jklFalse;
                    }
                    else
                    {
                        // If here it is 'just' a symbol.
                        return new JKLSym(tokenToRead);
                    }
            }
            throw new JKLInternalError("Can't process '" + tokenToRead + "'");
        }

        // Tokenise a string and then read the resultant form.
        // This version is as per the reference C#-Mal, and used by the read-string core function.
        static public JKLVal read_str(string str)
        {
            return read_form(new TokenQueue(Tokenizer(str)));
        }

        // This version is the actual entry point from my REPL. It handles multi-form lines.
        static public JKLVal read_str_multiline(TokenQueue TQ)
        {
            return read_form(TQ);
        }
    }
}
