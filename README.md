<!-- -*- coding: utf-8; fill-column: 118 -*- -->

# LexerGenLib

A lexical analyzer generator.

This is a lexical analyzer generator that follows the classical algorithms in the Dragon Book. (*Compilers:
Principles, Techniques, and Tools* by Aho, Sethi, and Ullman, 1986).

It is now available as a NuGet package, **Sunlighter.LexerGenLib**.

This project is a library and does not generate code. Instead, it generates tables at runtime. However, using the
**Sunlighter.TypeTraitsLib** library, it is possible to compare, hash, serialize, and deserialize both the tables and
the lexer specification used to make them. So caching is possible. (Further, the data structures are sufficiently
exposed that you could, in principle, write your own code generator.)

The code has been generalized by using further &ldquo;traits&rdquo; interfaces. Although the implementation is
designed around `char`, it might be possible to use the generalization to easily support lexical analyzers based on
`byte` or `Rune`. However, the traits generalization itself introduces a lot of complexity. (There are a couple of
underlying assumptions, one that a character code fits in a 32-bit `int`, and another that hexadecimal ranges like
`[0-9]` are contiguous. I think these assumptions are true for ASCII, ISO-8859-1, UTF-8, and UTF-32, but possibly not
EBCDIC. Most of these assumptions are only made when parsing regular expressions.)

The code also uses *range sets* to represent character sets. Range sets are more efficient than storing lists or sets
of individual characters, particularly given that the `char` type is 16-bit.

## Usage

In the `Sunligher.LexerGenLib` namespace, the main class is `LexerGen`. It has two `GenerateLexer` functions, either
of which may be suitable. One takes an immutable list of &ldquo;rules,&rdquo; where each rule can be a literal string
to be matched, or a regular expression. These rules are always case-sensitive. Each rule must also include an accept
code. This `GenerateLexer` function computes and returns a DFA (deterministic finite automaton).

The other `GenerateLexer` function takes a dictionary that goes from &ldquo;lexer states&rdquo; to lists of rules. It
returns a dictionary that goes from the same &ldquo;lexer states&rdquo; to the corresponding DFAs.

Then there are two `Lex` functions. One takes a single DFA and a string, and returns a list of tuples each of which
has a matched string and accept code. (In the event that nothing matches the input, you will get a single character
and no accept code.) The other `Lex` function takes a dictionary from states to DFAs, and a string, and also requires
a function that maps the accept codes to their &ldquo;next states.&rdquo; It returns a similar list of tuples.

## Caching

Because it takes time to generate DFAs, a caching strategy may be helpful. Right now there is an interface called
`ICacheStorage` which represents a single-entry cache. It is expected to implement two functions: `TryGet` and `Set`.
The `TryGet` function takes no arguments and returns an `Option<byte[]>` with the bytes from the cache, or `None` if
the cache is empty. The `Set` function takes an array of bytes and replaces the cache content with the given bytes.

There is an extension method called `GetCachedValue` which takes a key and a value and traits for their types, and
tries to retrieve the cached value. If the cache value exists and the hash of the key matches, the computation can be
skipped; otherwise, the computation is carried out, and the cached value is replaced.

So for example you can write:

```csharp
class FileCache : ICacheStorage
{
    private readonly string _fileName;

    public FileCache(string fileName)
    {
        _fileName = fileName;
    }

    public Option<byte[]> TryGet()
    {
        if (File.Exists(_fileName))
        {
            return Option<byte[]>.Some(File.ReadAllBytes(_fileName));
        }
        else return Option<byte[]>.None;
    }

    public void Set(byte[] bytes)
    {
        File.WriteAllBytes(_fileName, bytes);
    }
}

class MyCode
{
    const int WHITE_SPACE = 0;
    const int IDENT = 1;
    const int INTEGER = 2;
    // etc

    static void MyFunc(string input)
    {
        ImmutableList<LexerRule<int>> rules =
        [
            new RegexLexerRule<int>("[ |\\r|\\n|\\t|\\f|\\v]+", WHITE_SPACE),
            new RegexLexerRule<int>("[A-Z|a-z|_][A-Z|a-z|0-9|_]*", IDENT),
            new RegexLexerRule<int>("-?[1-9][0-9]*", INTEGER),
            // etc
        ];

        FileCache cache = new FileCache
        (
            Path.Combine
            (
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "LexerCache.bin"
            )
        );

        DFA<ImmutableList<char>, int> dfa = cache.GetCachedValue
        (
            LexerGen.GetRuleListTypeTraits(Int32TypeTraits.Value),
            LexerGen.GetDFATypeTraits(Int32TypeTraits.Value),
            rules,
            r => LexerGen.GenerateLexer(r, Int32TypeTraits.Value)
        );

        ImmutableList<(string, Option<int>)> lexResults = dfa.Lex(input);

        // etc
    }
}
```

## Regular Expression Syntax

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
