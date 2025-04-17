using Sunlighter.OptionLib;
using System.Collections.Immutable;

namespace Sunlighter.LexerGenLib.RegexParsing
{
    public interface IStringTraits<TString, TChar>
    {
        int Length(TString str);
        TChar GetChar(TString str, int index);
    }

    public sealed class StringTraits : IStringTraits<string, char>
    {
        private static readonly StringTraits value = new StringTraits();

        private StringTraits() { }

        public static StringTraits Value => value;

        public int Length(string str) => str.Length;

        public char GetChar(string str, int index) => str[index];
    }

    public interface IStringInputSourceTraits<TSource, TString, TSet, TChar>
    {
        ILexerCharTraits<TChar> CharTraits { get; }

        IStringTraits<TString, TChar> StringTraits { get; }

        ICharSetTraits<TSet, TChar> CharSetTraits { get; }

        TSource Create(TString str);

        bool IsEOF(TSource source);

        Option<(TChar, TSource)> TryReadChar(TSource source, TSet constraint);

        Option<(TChar, TSource)> TryReadExactChar(TSource source, TChar chDesired);
    }

    public sealed class StringInputSourceTraits<TString, TSet, TChar> : IStringInputSourceTraits<(TString, int), TString, TSet, TChar>
    {
        private readonly ILexerCharTraits<TChar> charTraits;
        private readonly IStringTraits<TString, TChar> stringTraits;
        private readonly ICharSetTraits<TSet, TChar> charSetTraits;

        public StringInputSourceTraits
        (
            ILexerCharTraits<TChar> charTraits,
            IStringTraits<TString, TChar> stringTraits,
            ICharSetTraits<TSet, TChar> charSetTraits
        )
        {
            this.charTraits = charTraits;
            this.stringTraits = stringTraits;
            this.charSetTraits = charSetTraits;

        }

        public ILexerCharTraits<TChar> CharTraits => charTraits;
        public IStringTraits<TString, TChar> StringTraits => stringTraits;
        public ICharSetTraits<TSet, TChar> CharSetTraits => charSetTraits;

        public (TString, int) Create(TString str) => (str, 0);

        public bool IsEOF((TString, int) source) => source.Item2 >= stringTraits.Length(source.Item1);

        public Option<(TChar, (TString, int))> TryReadChar((TString, int) source, TSet constraint)
        {
            if (IsEOF(source))
            {
                return Option<(TChar, (TString, int))>.None;
            }
            else
            {
                TChar ch = stringTraits.GetChar(source.Item1, source.Item2);
                if (charSetTraits.Contains(constraint, ch))
                {
                    return Option<(TChar, (TString, int))>.Some((ch, (source.Item1, source.Item2 + 1)));
                }
                else
                {
                    return Option<(TChar, (TString, int))>.None;
                }
            }
        }

        public Option<(TChar, (TString, int))> TryReadExactChar((TString, int) source, TChar chDesired)
        {
            if (IsEOF(source))
            {
                return Option<(TChar, (TString, int))>.None;
            }
            else
            {
                TChar ch = stringTraits.GetChar(source.Item1, source.Item2);
                if (charTraits.TypeTraits.Compare(ch, chDesired) == 0)
                {
                    return Option<(TChar, (TString, int))>.Some((ch, (source.Item1, source.Item2 + 1)));
                }
                else
                {
                    return Option<(TChar, (TString, int))>.None;
                }
            }
        }
    }

    public static partial class RegexParser
    {
        private static readonly Lazy<StringInputSourceTraits<string, ImmutableList<char>, char>> defaultStringInputSourceTraits =
            new Lazy<StringInputSourceTraits<string, ImmutableList<char>, char>>
            (
                () =>
                {
                    var charTraits = LexerCharTraits.Value;
                    var stringTraits = StringTraits.Value;
                    var charSetTraits = new ImmutableListRangeSetTraits<char>(charTraits);
                    return new StringInputSourceTraits<string, ImmutableList<char>, char>(charTraits, stringTraits, charSetTraits);
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );

        public static StringInputSourceTraits<string, ImmutableList<char>, char> DefaultStringInputSourceTraits => defaultStringInputSourceTraits.Value;
    }
}