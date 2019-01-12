# JK-s-Lisp
My take on https://github.com/kanaka/mal

Variations (hopefully improvements!) on the Mal guide's version and the C# reference implementation.

* reader uses a queue of tokens that can be reloaded - allows multi-line forms
* read and eval broken out to handle multi-form lines
* Numeric ops can have variable length arg lists (e.g. (+ 1 2 3)) - ref just has dyadic numerics
* tokeniser can handle negative numbers, decimal-only numbers (e.g. .2) and detect bad numbers (e.g. 1.2.3). To achieve this I replaced the regex-based tokeniser in the C#-Mal ref with a hand-rolled lexer.

