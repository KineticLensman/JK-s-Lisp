# JK's Lisp
My take on https://github.com/kanaka/mal

Variations (hopefully improvements!) on the Mal guide's version and the C# reference implementation.

* Reader uses a queue of tokens that can be reloaded - allows multi-line forms
* `READ` and `EVAL` broken out to handle multi-form lines
* Numeric ops can have variable length arg lists (e.g. (+ 1 2 3)) - ref just has dyadic numerics
* Tokeniser can handle negative numbers, decimal-only numbers (e.g. .2) and detect bad numbers (e.g. 1.2.3). To achieve this I replaced the regex-based tokeniser in the C#-Mal ref with a hand-rolled lexer.
* Lots more error checking
* Function bodies can have multiple exprs in their body

