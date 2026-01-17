using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using Sunlighter.TypeTraitsLib.Building;
using System.Collections.Immutable;

namespace Sunlighter.LexerGenLib.RegexParsing
{
    public interface IRegexCharTraits<TChar>
    {
        public ITypeTraits<TChar> CharTypeTraits { get; }

        public ILexerCharTraits<TChar> LexerTraits { get; }

        public TChar Offset(int offset, TChar ch);

        public Option<int> HexValue(TChar ch);

        public Option<TChar> EscapeToActual(TChar escapeChar);

        public TChar CodeToChar(int code);
    }

    class RegexCharTraits : IRegexCharTraits<char>
    {
        private static readonly RegexCharTraits value = new RegexCharTraits();

        private RegexCharTraits() { }

        public static RegexCharTraits Value => value;

        public ITypeTraits<char> CharTypeTraits => TypeTraitsLib.CharTypeTraits.Value;

        public ILexerCharTraits<char> LexerTraits => LexerCharTraits.Value;

        public char CodeToChar(int code) => checked((char)code);

        private static Lazy<ImmutableSortedDictionary<char, char>> escapeToActualDict =
            new Lazy<ImmutableSortedDictionary<char, char>>(GetEscapeToActualDict, LazyThreadSafetyMode.ExecutionAndPublication);

        private static ImmutableSortedDictionary<char, char> GetEscapeToActualDict()
        {
            ImmutableSortedDictionary<char, char>.Builder builder = ImmutableSortedDictionary.CreateBuilder<char, char>();
            builder.Add('a', '\a');
            builder.Add('b', '\b');
            builder.Add('t', '\t');
            builder.Add('n', '\n');
            builder.Add('v', '\v');
            builder.Add('f', '\f');
            builder.Add('r', '\r');
            return builder.ToImmutable();
        }

        public Option<char> EscapeToActual(char escapeChar)
        {
            return escapeToActualDict.Value.GetValueOption(escapeChar);
        }

        public Option<int> HexValue(char ch)
        {
            if (ch >= '0' && ch <= '9') return Option<int>.Some(ch - '0');
            if (ch >= 'a' && ch <= 'f') return Option<int>.Some(ch - 'a' + 10);
            if (ch >= 'A' && ch <= 'F') return Option<int>.Some(ch - 'A' + 10);
            return Option<int>.None;
        }

        public char Offset(int offset, char ch)
        {
            return (char)(ch + offset);
        }
    }

    [UnionOfDescendants]
    public abstract class RegexCharForSet<TChar>
    {
        public abstract TChar Eval(IRegexCharTraits<TChar> charTraits);
    }

    [Record]
    [UnionCaseName("charEscapeForSet")]
    public sealed class CharEscapeForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly TChar value;

        public CharEscapeForSet([Bind("value")] TChar value)
        {
            this.value = value;
        }

        [Bind("value")]
        public TChar Value => value;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            Option<TChar> actualOpt = charTraits.EscapeToActual(value);
            return actualOpt.HasValue ? actualOpt.Value : value;
        }
    }

    [Record]
    [UnionCaseName("hexEscapeForSet")]
    public sealed class HexEscapeForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly ImmutableList<TChar> hexChars;

        public HexEscapeForSet([Bind("hexChars")] ImmutableList<TChar> hexChars)
        {
            this.hexChars = hexChars;
        }

        [Bind("hexChars")]
        public ImmutableList<TChar> HexChars => hexChars;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            int value = 0;
            foreach(TChar hexChar in hexChars)
            {
                Option<int> hexValueOpt = charTraits.HexValue(hexChar);
                if (hexValueOpt.HasValue)
                {
                    value = (value << 4) + hexValueOpt.Value;
                }
                else
                {
                    throw new InvalidOperationException("Invalid hex character");
                }
            }
            return charTraits.CodeToChar(value);
        }
    }

    [Record]
    [UnionCaseName("offsetCharForSet")]
    public sealed class OffsetCharForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly int offset;
        private readonly RegexCharForSet<TChar> value;

        public OffsetCharForSet([Bind("offset")] int offset, [Bind("value")] RegexCharForSet<TChar> value)
        {
            this.value = value;
            this.offset = offset;
        }

        public static RegexCharForSet<TChar> Create(int offset, RegexCharForSet<TChar> value)
        {
            if (offset == 0)
            {
                return value;
            }
            else
            {
                return new OffsetCharForSet<TChar>(offset, value);
            }
        }

        [Bind("offset")]
        public int Offset => offset;

        [Bind("value")]
        public RegexCharForSet<TChar> Value => value;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            TChar baseValue = value.Eval(charTraits);
            return charTraits.Offset(offset, baseValue);
        }
    }

    [Record]
    [UnionCaseName("singleCharForSet")]
    public sealed class SingleCharForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly TChar value;

        public SingleCharForSet([Bind("value")] TChar value)
        {
            this.value = value;
        }

        [Bind("value")]
        public TChar Value => value;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            return value;
        }
    }

    [UnionOfDescendants]
    public abstract class RegexCharSet<TChar>
    {
        public abstract TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits);
    }

    [Record]
    [UnionCaseName("regexCharSetClosedRange")]
    public sealed class RegexCharSetClosedRange<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> start;
        private readonly RegexCharForSet<TChar> end;

        public RegexCharSetClosedRange([Bind("start")] RegexCharForSet<TChar> start, [Bind("end")] RegexCharForSet<TChar> end)
        {
            this.start = start;
            this.end = end;
        }

        [Bind("start")]
        public RegexCharForSet<TChar> Start => start;

        [Bind("end")]
        public RegexCharForSet<TChar> End => end;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cStart = start.Eval(charTraits);
            TChar cEnd = end.Eval(charTraits);
            if (charTraits.CharTypeTraits.Compare(cStart, cEnd) > 0)
            {
                return charSetTraits.Intersection(charSetTraits.GreaterEqual(cEnd), charSetTraits.LessEqual(cStart));
            }
            else if (charTraits.CharTypeTraits.Compare(cStart, cEnd) == 0)
            {
                return charSetTraits.Only(cStart);
            }
            else
            {
                return charSetTraits.Intersection(charSetTraits.GreaterEqual(cStart), charSetTraits.LessEqual(cEnd));
            }
                
        }
    }

    [Record]
    [UnionCaseName("regexCharSetGreaterEqual")]
    public sealed class RegexCharSetGreaterEqual<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> start;

        public RegexCharSetGreaterEqual([Bind("start")] RegexCharForSet<TChar> start)
        {
            this.start = start;
        }

        [Bind("start")]
        public RegexCharForSet<TChar> Start => start;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cStart = start.Eval(charTraits);
            return charSetTraits.GreaterEqual(cStart);
        }
    }

    [Record]
    [UnionCaseName("regexCharSetLessEqual")]
    public sealed class RegexCharSetLessEqual<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> end;

        public RegexCharSetLessEqual([Bind("end")] RegexCharForSet<TChar> end)
        {
            this.end = end;
        }

        [Bind("end")]
        public RegexCharForSet<TChar> End => end;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cEnd = end.Eval(charTraits);
            return charSetTraits.LessEqual(cEnd);
        }
    }

    [Record]
    [UnionCaseName("regexCharSetOnly")]
    public sealed class RegexCharSetOnly<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> value;

        public RegexCharSetOnly([Bind("value")] RegexCharForSet<TChar> value)
        {
            this.value = value;
        }
        
        [Bind("value")]
        public RegexCharForSet<TChar> Value => value;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cValue = value.Eval(charTraits);
            return charSetTraits.Only(cValue);
        }
    }

    [Record]
    [UnionCaseName("regexCharSetComplement")]
    public sealed class RegexCharSetComplement<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharSet<TChar> set;

        public RegexCharSetComplement([Bind("set")] RegexCharSet<TChar> set)
        {
            this.set = set;
        }

        [Bind("set")]
        public RegexCharSet<TChar> Set => set;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TSet setValue = set.Eval(charTraits, charSetTraits);
            return charSetTraits.Complement(setValue);
        }
    }

    [Record]
    [UnionCaseName("regexCharSetUnion")]
    public sealed class RegexCharSetUnion<TChar> : RegexCharSet<TChar>
    {
        private readonly ImmutableList<RegexCharSet<TChar>> sets;

        public RegexCharSetUnion([Bind("sets")] ImmutableList<RegexCharSet<TChar>> sets)
        {
            this.sets = sets;
        }

        [Bind("sets")]
        public ImmutableList<RegexCharSet<TChar>> Sets => sets;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            ImmutableList<TSet> setValues = sets.Select(x => x.Eval(charTraits, charSetTraits)).ToImmutableList();
            return charSetTraits.Union(setValues);
        }
    }

    [Record]
    [UnionCaseName("regexCharSetIntersection")]
    public sealed class RegexCharSetIntersection<TChar> : RegexCharSet<TChar>
    {
        private readonly ImmutableList<RegexCharSet<TChar>> sets;

        public RegexCharSetIntersection([Bind("sets")] ImmutableList<RegexCharSet<TChar>> sets)
        {
            this.sets = sets;
        }

        [Bind("sets")]
        public ImmutableList<RegexCharSet<TChar>> Sets => sets;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            ImmutableList<TSet> setValues = sets.Select(x => x.Eval(charTraits, charSetTraits)).ToImmutableList();
            return charSetTraits.Intersection(setValues);
        }
    }

    [UnionOfDescendants]
    public abstract class RegexSyntax<TChar>
    {
        public abstract TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        );
    }

    [Singleton(0x9B064CECu)]
    [UnionCaseName("regexEmptyString")]
    public sealed class RegexEmptyString<TChar> : RegexSyntax<TChar>
    {
        private static readonly RegexEmptyString<TChar> value = new RegexEmptyString<TChar>();

        private RegexEmptyString() { }

        public static RegexEmptyString<TChar> Value => value;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            return nfaTraits.EmptyString;
        }
    }

    [Record]
    [UnionCaseName("regexSingleChar")]
    public sealed class RegexSingleChar<TChar> : RegexSyntax<TChar>
    {
        private readonly TChar value;

        public RegexSingleChar([Bind("value")] TChar value)
        {
            this.value = value;
        }

        [Bind("value")]
        public TChar Value => value;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            return nfaTraits.CharFromSet(charSetTraits.Only(value));
        }
    }

    [Record]
    [UnionCaseName("regexCharEscape")]
    public sealed class RegexCharEscape<TChar> : RegexSyntax<TChar>
    {
        private readonly TChar value;

        public RegexCharEscape([Bind("value")] TChar value)
        {
            this.value = value;
        }

        [Bind("value")]
        public TChar Value => value;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            Option<TChar> actualOpt = charTraits.EscapeToActual(value);
            TChar effectiveChar = actualOpt.HasValue ? actualOpt.Value : value;
            return nfaTraits.CharFromSet(charSetTraits.Only(effectiveChar));
        }
    }

    [Record]
    [UnionCaseName("regexHexEscape")]
    public sealed class RegexHexEscape<TChar> : RegexSyntax<TChar>
    {
        private readonly ImmutableList<TChar> hexChars;

        public RegexHexEscape([Bind("hexChars")] ImmutableList<TChar> hexChars)
        {
            this.hexChars = hexChars;
        }

        [Bind("hexChars")]
        public ImmutableList<TChar> HexChars => hexChars;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            int value = 0;
            foreach (TChar hexChar in hexChars)
            {
                Option<int> hexValueOpt = charTraits.HexValue(hexChar);
                if (hexValueOpt.HasValue)
                {
                    value = (value << 4) + hexValueOpt.Value;
                }
                else
                {
                    throw new InvalidOperationException("Invalid hex character");
                }
            }
            TChar effectiveChar = charTraits.CodeToChar(value);
            return nfaTraits.CharFromSet(charSetTraits.Only(effectiveChar));
        }
    }

    [Record]
    [UnionCaseName("regexCharFromSet")]
    public sealed class RegexCharFromSet<TChar> : RegexSyntax<TChar>
    {
        private readonly RegexCharSet<TChar> set;

        public RegexCharFromSet([Bind("set")] RegexCharSet<TChar> set)
        {
            this.set = set;
        }

        [Bind("set")]
        public RegexCharSet<TChar> Set => set;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            return nfaTraits.CharFromSet(set.Eval(charTraits, charSetTraits));
        }
    }

    [Record]
    [UnionCaseName("regexSequence")]
    public sealed class RegexSequence<TChar> : RegexSyntax<TChar>
    {
        private readonly ImmutableList<RegexSyntax<TChar>> items;

        /// <summary>
        /// Only deserialization should use this; prefer CreateSequence to create sequences.
        /// </summary>
        public RegexSequence([Bind("items")] ImmutableList<RegexSyntax<TChar>> items)
        {
            this.items = items;
        }

        public static RegexSyntax<TChar> CreateSequence(ImmutableList<RegexSyntax<TChar>> items)
        {
            ImmutableList<RegexSyntax<TChar>> nonEmpty = ImmutableList<RegexSyntax<TChar>>.Empty;
            
            void add(RegexSyntax<TChar> syntaxItem)
            {
                if (syntaxItem is RegexEmptyString<TChar>)
                {
                    // skip it
                }
                else if (syntaxItem is RegexSequence<TChar> seq)
                {
                    foreach(RegexSyntax<TChar> subItem in seq.Items)
                    {
                        add(subItem);
                    }
                }
                else
                {
                    nonEmpty = nonEmpty.Add(syntaxItem);
                }
            }

            foreach(RegexSyntax<TChar> item in items)
            {
                add(item);
            }

            if (nonEmpty.IsEmpty)
            {
                return RegexEmptyString<TChar>.Value;
            }
            else if (nonEmpty.Count == 1)
            {
                return nonEmpty[0];
            }
            else
            {
                return new RegexSequence<TChar>(nonEmpty);
            }
        }

        [Bind("items")]
        public ImmutableList<RegexSyntax<TChar>> Items => items;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            return nfaTraits.Sequence(items.Select(x => x.Eval(charTraits, charSetTraits, nfaTraits)).ToImmutableList());
        }
    }

    [Record]
    [UnionCaseName("regexAlternative")]
    public sealed class RegexAlternative<TChar> : RegexSyntax<TChar>
    {
        private readonly ImmutableList<RegexSyntax<TChar>> items;

        public RegexAlternative([Bind("items")] ImmutableList<RegexSyntax<TChar>> items)
        {
            this.items = items;
        }

        [Bind("items")]
        public ImmutableList<RegexSyntax<TChar>> Items => items;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            return nfaTraits.Alternative(items.Select(x => x.Eval(charTraits, charSetTraits, nfaTraits)).ToImmutableList());
        }
    }

    [Record]
    [UnionCaseName("regexOptRep")]
    public sealed class RegexOptRep<TChar> : RegexSyntax<TChar>
    {
        private readonly RegexSyntax<TChar> syntax;
        private readonly bool opt;
        private readonly bool rep;

        public RegexOptRep([Bind("syntax")] RegexSyntax<TChar> syntax, [Bind("opt")] bool opt, [Bind("rep")] bool rep)
        {
            this.syntax = syntax;
            this.opt = opt;
            this.rep = rep;
        }

        [Bind("syntax")]
        public RegexSyntax<TChar> Syntax => syntax;

        [Bind("opt")]
        public bool Optional => opt;

        [Bind("rep")]
        public bool Repeating => rep;

        public override TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        )
        {
            return nfaTraits.OptRep(syntax.Eval(charTraits, charSetTraits, nfaTraits), opt, rep);
        }
    }
}
