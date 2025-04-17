<!-- -*- coding: utf-8; fill-column: 118 -*- -->

# LexerGenLib

A lexical analyzer generator.

This is a lexical analyzer generator that follows the classical algorithms in the Dragon Book. (*Compilers:
Principles, Techniques, and Tools* by Aho, Sethi, and Ullman, 1986).

The code has been generalized by using *traits* interfaces. Although the implementation is designed around `char`, it
might be possible to use the generalization to easily support lexical analyzers based on `byte` or `Rune`. However,
the traits generalization itself seems to introduce a lot of complexity. (There are a couple of underlying
assumptions, one that a character code fits in a 32-bit `int`, and another that hexadecimal ranges like `[0-9]` are
contiguous.)

The code also uses *range sets* to represent character sets. This is more efficient than storing lists or sets of
individual characters, particularly given that the `char` type is 16-bit.

The main entry point is `LegerGen.GenerateLexer`, which takes a list of rules with their accept codes, and generates a
minimized DFA.

The DFA can be serialized with type traits (use `GetDFATypeTraits` and pass the type traits for the accept codes) and
there is an extension method `TryMatchPrefix` which will return the longest matching prefix (using the greatest accept
code as a tie-breaker).

`GenerateLexer` supports regular expressions, but these regexes have a slightly different syntax from Perl-compatible
regexes, which makes sense given their different capabilities. Regular expressions are parsed into a parse tree using
parser combinators, and then the parse tree is evaluated to produce a function that inserts states into a
non-deterministic state machine, which is then made deterministic and optimized.

Regular expressions support the following:

* `|` for alternatives. This has lowest precedence so `ab|cd` is the same as `(ab)|(cd)`.

* concatenation for sequence.

* `?`, `+`, and `*` for optional, repeating, or both. Note that these have higher precedence than concatenation, so
  `ab*` is the same as `a(b*)` and not `(ab)*`.

* Parentheses for grouping.

* You can escape characters with `\` and hex escapes are available with `\x`. Hex escapes must be terminated by a
  semicolon (which allows you to use any number of digits). So `\x1b;` would match the escape character and `\\` would
  match a backslash. You also get the traditional C escape characters such as `\a`, `\b`, `\t`, `\n`, `\v`, `\f`, and
  `\r`. These must be lowercase. Unrecognized characters are &ldquo;passed through&rdquo; so `\A` would be the same as
  `A`.

* Character sets in square brackets such as `[A-Z]`.

* `|` for unions inside character sets, like `[A-Z|a-z]`. This is different from Perl-compatable regexes, where you
  can express unions inside character sets with simple concatenation. So you have to write `[A|a|B|b]` instead of
  `[AaBb]`.

* `&` for intersections inside character sets. This has higher precedence than `|`.

* `~` for complement inside character sets. This has higher precedence than `|` or `&`. So you could write `[A-Z&~E]`
  to indicate &ldquo;A to Z, and not E,&rdquo; or `[~R&~r]` to indicate &ldquo;not R and not r.&rdquo;

* Parentheses are supported inside character sets. They have precedence higher than `~` but cannot be used inside
  character ranges. But you can write `[(A|C)&(C|E-Z)]` which works out to `[C]`.

* Character ranges are represented with hyphens and are always inclusive, but you only need one end or the other. So
  `[E-]` would match any character `E` or greater, and `[-P]` would match any character `P` or less. So `[E-&-P]` is
  the same as `[E-P]`.

* In a character set, you can use `<` before a character to indicate &ldquo;the character with the code before&rdquo;
  and `>` after a character to indicate &ldquo;the character with the code after.&rdquo; This has precedence higher
  than the hyphen. So you can use something like `[-<0]` to match any character strictly less than `0`, and another
  way to write `[~0-9]` would be `[-<0|9>-]`.

* Character sets also support escapes and hex escapes. Hex escapes require a semicolon. So `[\x0;-\x1f;]` would match
  any character with a code from `0` to hexadecimal `1f`.

Right now case-insensitivity is not supported. (There are some complexities with case-insensitivity and Unicode,
particularly when it comes to character ranges: `[A-Z]` should become `[A-Z|a-z]`, but what if somebody writes `[A-a]`
or `[A-あ]`?)
