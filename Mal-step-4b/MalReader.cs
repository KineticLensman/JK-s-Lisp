using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using static Mal.Types;


namespace Mal
{
    public class MalReader
    {
        // A stateful reader that gives out a list of tokens. Being able to 
        // create multiple Reader instances means that we can handle multiple
        // files simultaneously (e.g. when evaluating a file loaded by slurp).
        public class Reader
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
            public Reader(List<string> tokenList)
            {
                QueueNewTokens(tokenList);
            }

            // Pop the first token. Callers are responsible for ensuring it is safe to do so.
            public string Next()
            {
                if(ourTokens.Count <= 0)
                {
                    throw new MalInternalError("Tried to read an empty token queue");
                }
                return ourTokens.Dequeue();
            }

            // Return first token or null if there are no tokens.
            public string Peek()
            {
                if(ourTokens.TryPeek(out string s))
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
                    throw new MalParseError("'" + start + "' doesn't have a matching '" + end + "'");
                }
                else
                {
                    // Make the new tokens available for reads.
                    QueueNewTokens(Tokenizer(source));
                }
            }
        }

        // Takes an input string and return substrings corresponding to MAL tokens.
        // Additional analysis is performed elsewhere (e.g. to detect / parse ints and strings).
        static public List<string> Tokenizer(string source)
        {
            // Initialise the token list.
            List<string> tokens = new List<string>();

            // Define a regex pattern (as per the MAL guide) whose groups match the MAL syntax.
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
                    if(candidate.StartsWith(';'))
                    {
                        break;
                    }
                    // Disallow input strings that aren't terminated. 
                    if (candidate.Length == 1 && candidate.StartsWith('\"'))
                    {
                        throw new MalParseError("Non-terminating or multi-line string");
                    }
                    tokens.Add(result[ctr]);
                }
            }
            return tokens;
        }

        // Read a MalVal form - which is either an atom or a sequence.
        static public MalVal read_form(Reader reader)
        {
            if(reader.Peek() == null)
            {
                // Reader is empty - caused by a comment line in the input.
                return null;
            }
            else if (reader.Peek().StartsWith('\''))
            {
                // Create a list containing the quote symbol and the quoted form.
                // Skip the quote symbol.
                reader.Next();

                // Now read the quoted thing, and build a quote form.
                return new MalList(new MalQuote(), read_form(reader));
                // TODO handle quasiquotes and splices in the same way.
            }
            else if( reader.Peek().StartsWith('('))
            {
                // Create a new List and read it's body.
                return read_list(reader, new MalList(), '(', ')');
            }
            else if (reader.Peek().StartsWith('['))
            {
                // Create a new Vector and read it's body.
                return read_list(reader, new MalVector(), '[', ']');
            }
            else if (reader.Peek().StartsWith('{'))
            {
                // Create a new HashMap and read it's body.
                // TODO - check that hashmap contains a list of keywords and values.
                return read_list(reader, new MalHashMap(), '{', '}');
            }
            else if (reader.Peek().StartsWith(')') || reader.Peek().StartsWith(']') || reader.Peek().StartsWith('}'))
            {
                // A sequence close character that doesn't match a start.
                // This correctly handles a case like [1 ( 2 ] 3).
                throw new MalParseError("Expecting sequence or atom but got '" + reader.Peek() + "'");
            }
            else
            {
                // This isn't a list try so parse it as an atom.
                return read_atom(reader);
            }
        }

        // Read a MalSequence, checking that it starts and terminates correctly.
        // Named read_list to follow the ref, but has been genericized to handle vectors as well.
        static public MalSeq read_list(Reader reader, MalSeq sequence, char start, char end)
        {
            // Check that we are in fact at the start of a list.
            string token = reader.Next();
            if (token[0] != start)
            {
                // Parse error - probably internal if the list code is correct.
                throw new MalInternalError("Sequence expected '" + start + "' but got: " + token);
            }

            // Use read_form to get the list's contents, accumulating them into the list.
            while (true)
            {
                token = reader.Peek();

                if(token != null)
                {
                    // We are in the list or at the end.
                    if(token[0] == end)
                    {
                        // Reached valid end of list. Consume the end char.
                        reader.Next();
                        // And we are done.
                        break;
                    }
                    // Mutually recurse to read the next list element.
                    MalVal newVal = read_form(reader);
                    sequence.Add(newVal);
                }
                else
                {
                    // The input has finished but the list hasn't. Try to get more input.
                    reader.LoadMoreTokens(start, end);
                }
            }

            return sequence;
        }

        static protected bool IsDigit(string tokenStart)
        {
            return tokenStart.IndexOfAny("0123456789".ToCharArray()) >= 0;
        }

        // Read a number. The sign has already been determined.
        static protected MalVal ParseNumber(string token, bool isPositive)
        {
            // Use C#'s int and float parsing to extract numbers / spot format errors.
            if(token.Contains("."))
            {
                // It seems to be a float.
                if (float.TryParse(token, out float number))
                {
                    if (!isPositive)
                    {
                        number = -number;
                    }
                    return new MalFloat(number);
                }
                throw new MalParseError("Badly formed float: '" + token + "'");
            }
            else
            {
                // It seems to be an integer.
                if (int.TryParse(token, out int number))
                {
                    if (!isPositive)
                    {
                        number = -number;
                    }
                    return new MalInt(number);
                }
                throw new MalParseError("Badly formed int: '" + token + "'");
            }
        }

        static public MalVal read_atom(Reader reader)
        {
            // "If you have a problem and you think regex is the answer, now you have two problems!"
            // Unlike the referenceC#-Mal, read_atom handles floats and badly-formed symbols.
            string tokenToRead = reader.Next();

            if (tokenToRead.Length <= 0)
            {
                throw new MalInternalError("Reader has returned empty string");
            }
            switch (tokenToRead[0])
            {
                case '+':
                    if(tokenToRead.Length == 1)
                    {
                        // Token is a solo '+', not the beginning of a number.
                        return new MalSym(tokenToRead);
                    }
                    // Skip the sign and extract a positive number;
                    return ParseNumber(tokenToRead.Substring(1), true);
                case '-':
                    if (tokenToRead.Length == 1)
                    {
                        // Token is a solo '-', not the beginning of a number.
                        return new MalSym(tokenToRead);
                    }
                    // Skip the sign and extract a negative number;
                    return ParseNumber(tokenToRead.Substring(1), false);
                case '.':
                    // An initial '.' is only allowed at the start of a number, as in '.2'.
                    return ParseNumber(tokenToRead, true);
                case '\"':
                    if (tokenToRead.EndsWith("\""))
                    {
                        return new MalString(tokenToRead);
                    }
                    // TODO - never reaches this point. The reader regex seems to throw away '"' chars if there is only 1.
                    throw new MalParseError("String '" + tokenToRead + "' lacks a closing thingy");
                case ':':
                    // Handle a keyword
                    if (tokenToRead.Length == 1)
                    {
                        // Can't have a solo colon.
                        throw new MalParseError("':' must be followed by a keyword");
                    }
                    return new MalKeyword(tokenToRead);
                default:
                    if(IsDigit(tokenToRead[0].ToString()))
                    {
                        // Token seems to be an unsigned number.
                        return ParseNumber(tokenToRead, true);
                    }
                    else if (tokenToRead == "nil")
                    {
                        return new MalNil();
                    }
                    else if (tokenToRead == "true")
                    {
                        return new MalTrue();
                    }
                    else if (tokenToRead == "false")
                    {
                        return new MalFalse();
                    }
                    else
                    {
                        // If here it is 'just' a symbol.
                        return new MalSym(tokenToRead);
                    }
            }
            throw new MalInternalError("Can't process '" + tokenToRead + "'");
        }

        // Tokenise a string and then read the resultant form. This is the entry point called from the REPL.
        // This version is as per the reference C#-Mal
        static public MalVal read_str(string str)
        {
            return read_form(new Reader(Tokenizer(str)));
        }

        static public MalVal read_str_multiline (Reader rdr)
        {
            return read_form(rdr);
        }
    }
}
