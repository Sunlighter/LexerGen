using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
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

    public abstract class RegexCharForSet<TChar>
    {
        public static ITypeTraits<RegexCharForSet<TChar>> GetTypeTraits(ITypeTraits<TChar> charTypeTraits)
        {
            RecursiveTypeTraits<RegexCharForSet<TChar>> recurse = new RecursiveTypeTraits<RegexCharForSet<TChar>>();

            ITypeTraits<RegexCharForSet<TChar>> traits = new UnionTypeTraits<string, RegexCharForSet<TChar>>
            (
                StringTypeTraits.Value,
                [
                    new UnionCaseTypeTraits2<string, RegexCharForSet<TChar>, CharEscapeForSet<TChar>>
                    (
                        "CharEscape",
                        new ConvertTypeTraits<CharEscapeForSet<TChar>, TChar>
                        (
                            x => x.Value,
                            charTypeTraits,
                            x => new CharEscapeForSet<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharForSet<TChar>, HexEscapeForSet<TChar>>
                    (
                        "HexEscape",
                        new ConvertTypeTraits<HexEscapeForSet<TChar>, ImmutableList<TChar>>
                        (
                            x => x.HexChars,
                            new ListTypeTraits<TChar>(charTypeTraits),
                            x => new HexEscapeForSet<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharForSet<TChar>, OffsetCharForSet<TChar>>
                    (
                        "OffsetChar",
                        new ConvertTypeTraits<OffsetCharForSet<TChar>, (int, RegexCharForSet<TChar>)>
                        (
                            x => (x.Offset, x.Value),
                            new ValueTupleTypeTraits<int, RegexCharForSet<TChar>>
                            (
                                Int32TypeTraits.Value,
                                recurse
                            ),
                            x => new OffsetCharForSet<TChar>(x.Item1, x.Item2)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharForSet<TChar>, SingleCharForSet<TChar>>
                    (
                        "SingleChar",
                        new ConvertTypeTraits<SingleCharForSet<TChar>, TChar>
                        (
                            x => x.Value,
                            charTypeTraits,
                            x => new SingleCharForSet<TChar>(x)
                        )
                    )
                ]
            );

            recurse.Set(traits);

            return traits;
        }

        public abstract TChar Eval(IRegexCharTraits<TChar> charTraits);
    }

    public sealed class CharEscapeForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly TChar value;

        public CharEscapeForSet(TChar value)
        {
            this.value = value;
        }

        public TChar Value => value;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            Option<TChar> actualOpt = charTraits.EscapeToActual(value);
            return actualOpt.HasValue ? actualOpt.Value : value;
        }
    }

    public sealed class HexEscapeForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly ImmutableList<TChar> hexChars;

        public HexEscapeForSet(ImmutableList<TChar> hexChars)
        {
            this.hexChars = hexChars;
        }

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

    public sealed class OffsetCharForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly int offset;
        private readonly RegexCharForSet<TChar> value;

        public OffsetCharForSet(int offset, RegexCharForSet<TChar> value)
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

        public int Offset => offset;
        public RegexCharForSet<TChar> Value => value;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            TChar baseValue = value.Eval(charTraits);
            return charTraits.Offset(offset, baseValue);
        }
    }

    public sealed class SingleCharForSet<TChar> : RegexCharForSet<TChar>
    {
        private readonly TChar value;
        public SingleCharForSet(TChar value)
        {
            this.value = value;
        }
        public TChar Value => value;

        public override TChar Eval(IRegexCharTraits<TChar> charTraits)
        {
            return value;
        }
    }

    public abstract class RegexCharSet<TChar>
    {
        public static ITypeTraits<RegexCharSet<TChar>> GetTypeTraits
        (
            ITypeTraits<TChar> charTypeTraits,
            ITypeTraits<RegexCharForSet<TChar>> regexCharForSetTypeTraits
        )
        {
            RecursiveTypeTraits<RegexCharSet<TChar>> recurse = new RecursiveTypeTraits<RegexCharSet<TChar>>();

            ITypeTraits<RegexCharSet<TChar>> val = new UnionTypeTraits<string, RegexCharSet<TChar>>
            (
                StringTypeTraits.Value,
                [
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetClosedRange<TChar>>
                    (
                        "ClosedRange",
                        new ConvertTypeTraits<RegexCharSetClosedRange<TChar>, (RegexCharForSet<TChar>, RegexCharForSet<TChar>)>
                        (
                            x => (x.Start, x.End),
                            new ValueTupleTypeTraits<RegexCharForSet<TChar>, RegexCharForSet<TChar>>
                            (
                                regexCharForSetTypeTraits,
                                regexCharForSetTypeTraits
                            ),
                            x => new RegexCharSetClosedRange<TChar>(x.Item1, x.Item2)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetGreaterEqual<TChar>>
                    (
                        "GreaterEqual",
                        new ConvertTypeTraits<RegexCharSetGreaterEqual<TChar>, RegexCharForSet<TChar>>
                        (
                            x => x.Start,
                            regexCharForSetTypeTraits,
                            x => new RegexCharSetGreaterEqual<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetLessEqual<TChar>>
                    (
                        "LessEqual",
                        new ConvertTypeTraits<RegexCharSetLessEqual<TChar>, RegexCharForSet<TChar>>
                        (
                            x => x.End,
                            regexCharForSetTypeTraits,
                            x => new RegexCharSetLessEqual<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetOnly<TChar>>
                    (
                        "Only",
                        new ConvertTypeTraits<RegexCharSetOnly<TChar>, RegexCharForSet<TChar>>
                        (
                            x => x.Value,
                            regexCharForSetTypeTraits,
                            x => new RegexCharSetOnly<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetComplement<TChar>>
                    (
                        "Complement",
                        new ConvertTypeTraits<RegexCharSetComplement<TChar>, RegexCharSet<TChar>>
                        (
                            x => x.Set,
                            recurse,
                            x => new RegexCharSetComplement<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetUnion<TChar>>
                    (
                        "Union",
                        new ConvertTypeTraits<RegexCharSetUnion<TChar>, ImmutableList<RegexCharSet<TChar>>>
                        (
                            x => x.Sets,
                            new ListTypeTraits<RegexCharSet<TChar>>(recurse),
                            x => new RegexCharSetUnion<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexCharSet<TChar>, RegexCharSetIntersection<TChar>>
                    (
                        "Intersection",
                        new ConvertTypeTraits<RegexCharSetIntersection<TChar>, ImmutableList<RegexCharSet<TChar>>>
                        (
                            x => x.Sets,
                            new ListTypeTraits<RegexCharSet<TChar>>(recurse),
                            x => new RegexCharSetIntersection<TChar>(x)
                        )
                    )
                ]
            );

            recurse.Set(val);

            return val;
        }

        public abstract TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits);
    }

    public sealed class RegexCharSetClosedRange<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> start;
        private readonly RegexCharForSet<TChar> end;

        public RegexCharSetClosedRange(RegexCharForSet<TChar> start, RegexCharForSet<TChar> end)
        {
            this.start = start;
            this.end = end;
        }

        public RegexCharForSet<TChar> Start => start;

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

    public sealed class RegexCharSetGreaterEqual<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> start;

        public RegexCharSetGreaterEqual(RegexCharForSet<TChar> start)
        {
            this.start = start;
        }

        public RegexCharForSet<TChar> Start => start;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cStart = start.Eval(charTraits);
            return charSetTraits.GreaterEqual(cStart);
        }
    }

    public sealed class RegexCharSetLessEqual<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> end;

        public RegexCharSetLessEqual(RegexCharForSet<TChar> end)
        {
            this.end = end;
        }

        public RegexCharForSet<TChar> End => end;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cEnd = end.Eval(charTraits);
            return charSetTraits.LessEqual(cEnd);
        }
    }

    public sealed class RegexCharSetOnly<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharForSet<TChar> value;

        public RegexCharSetOnly(RegexCharForSet<TChar> value)
        {
            this.value = value;
        }

        public RegexCharForSet<TChar> Value => value;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TChar cValue = value.Eval(charTraits);
            return charSetTraits.Only(cValue);
        }
    }

    public sealed class RegexCharSetComplement<TChar> : RegexCharSet<TChar>
    {
        private readonly RegexCharSet<TChar> set;

        public RegexCharSetComplement(RegexCharSet<TChar> set)
        {
            this.set = set;
        }

        public RegexCharSet<TChar> Set => set;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            TSet setValue = set.Eval(charTraits, charSetTraits);
            return charSetTraits.Complement(setValue);
        }
    }

    public sealed class RegexCharSetUnion<TChar> : RegexCharSet<TChar>
    {
        private readonly ImmutableList<RegexCharSet<TChar>> sets;

        public RegexCharSetUnion(ImmutableList<RegexCharSet<TChar>> sets)
        {
            this.sets = sets;
        }

        public ImmutableList<RegexCharSet<TChar>> Sets => sets;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            ImmutableList<TSet> setValues = sets.Select(x => x.Eval(charTraits, charSetTraits)).ToImmutableList();
            return charSetTraits.Union(setValues);
        }
    }

    public sealed class RegexCharSetIntersection<TChar> : RegexCharSet<TChar>
    {
        private readonly ImmutableList<RegexCharSet<TChar>> sets;

        public RegexCharSetIntersection(ImmutableList<RegexCharSet<TChar>> sets)
        {
            this.sets = sets;
        }

        public ImmutableList<RegexCharSet<TChar>> Sets => sets;

        public override TSet Eval<TSet>(IRegexCharTraits<TChar> charTraits, ICharSetTraits<TSet, TChar> charSetTraits)
        {
            ImmutableList<TSet> setValues = sets.Select(x => x.Eval(charTraits, charSetTraits)).ToImmutableList();
            return charSetTraits.Intersection(setValues);
        }
    }

    public abstract class RegexSyntax<TChar>
    {
        public static ITypeTraits<RegexSyntax<TChar>> GetTypeTraits
        (
            ITypeTraits<TChar> charTypeTraits,
            ITypeTraits<RegexCharForSet<TChar>> regexCharForSetTypeTraits,
            ITypeTraits<RegexCharSet<TChar>> regexCharSetTypeTraits
        )
        {
            RecursiveTypeTraits<RegexSyntax<TChar>> recurse = new RecursiveTypeTraits<RegexSyntax<TChar>>();

            ITypeTraits<RegexSyntax<TChar>> val = new UnionTypeTraits<string, RegexSyntax<TChar>>
            (
                StringTypeTraits.Value,
                [
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexEmptyString<TChar>>
                    (
                        "EmptyString",
                        new UnitTypeTraits<RegexEmptyString<TChar>>
                        (
                            0x273A5B31u,
                            RegexEmptyString<TChar>.Value
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexSingleChar<TChar>>
                    (
                        "SingleChar",
                        new ConvertTypeTraits<RegexSingleChar<TChar>, TChar>
                        (
                            x => x.Value,
                            charTypeTraits,
                            x => new RegexSingleChar<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexCharEscape<TChar>>
                    (
                        "Escape",
                        new ConvertTypeTraits<RegexCharEscape<TChar>, TChar>
                        (
                            x => x.Value,
                            charTypeTraits,
                            x => new RegexCharEscape<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexHexEscape<TChar>>
                    (
                        "HexEscape",
                        new ConvertTypeTraits<RegexHexEscape<TChar>, ImmutableList<TChar>>
                        (
                            x => x.HexChars,
                            new ListTypeTraits<TChar>(charTypeTraits),
                            x => new RegexHexEscape<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexCharFromSet<TChar>>
                    (
                        "CharFromSet",
                        new ConvertTypeTraits<RegexCharFromSet<TChar>, RegexCharSet<TChar>>
                        (
                            x => x.Set,
                            regexCharSetTypeTraits,
                            x => new RegexCharFromSet<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexSequence<TChar>>
                    (
                        "Sequence",
                        new ConvertTypeTraits<RegexSequence<TChar>, ImmutableList<RegexSyntax<TChar>>>
                        (
                            x => x.Items,
                            new ListTypeTraits<RegexSyntax<TChar>>(recurse),
                            x => new RegexSequence<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexAlternative<TChar>>
                    (
                        "Alternative",
                        new ConvertTypeTraits<RegexAlternative<TChar>, ImmutableList<RegexSyntax<TChar>>>
                        (
                            x => x.Items,
                            new ListTypeTraits<RegexSyntax<TChar>>(recurse),
                            x => new RegexAlternative<TChar>(x)
                        )
                    ),
                    new UnionCaseTypeTraits2<string, RegexSyntax<TChar>, RegexOptRep<TChar>>
                    (
                        "OptRep",
                        new ConvertTypeTraits<RegexOptRep<TChar>, (RegexSyntax<TChar>, bool, bool)>
                        (
                            x => (x.Syntax, x.Optional, x.Repeating),
                            new ValueTupleTypeTraits<RegexSyntax<TChar>, bool, bool>
                            (
                                recurse,
                                BooleanTypeTraits.Value,
                                BooleanTypeTraits.Value
                            ),
                            x => new RegexOptRep<TChar>(x.Item1, x.Item2, x.Item3)
                        )
                    ),
                ]
            );

            recurse.Set(val);

            return val;
        }
        public abstract TNFA Eval<TNFAFinal, TNFA, TCharSet, TAccept>
        (
            IRegexCharTraits<TChar> charTraits,
            ICharSetTraits<TCharSet, TChar> charSetTraits,
            INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept> nfaTraits
        );
    }

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

    public sealed class RegexSingleChar<TChar> : RegexSyntax<TChar>
    {
        private readonly TChar value;

        public RegexSingleChar(TChar value)
        {
            this.value = value;
        }

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

    public sealed class RegexCharEscape<TChar> : RegexSyntax<TChar>
    {
        private readonly TChar value;

        public RegexCharEscape(TChar value)
        {
            this.value = value;
        }

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

    public sealed class RegexHexEscape<TChar> : RegexSyntax<TChar>
    {
        private readonly ImmutableList<TChar> hexChars;

        public RegexHexEscape(ImmutableList<TChar> hexChars)
        {
            this.hexChars = hexChars;
        }

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

    public sealed class RegexCharFromSet<TChar> : RegexSyntax<TChar>
    {
        private readonly RegexCharSet<TChar> set;

        public RegexCharFromSet(RegexCharSet<TChar> set)
        {
            this.set = set;
        }

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

    public sealed class RegexSequence<TChar> : RegexSyntax<TChar>
    {
        private readonly ImmutableList<RegexSyntax<TChar>> items;

        internal RegexSequence(ImmutableList<RegexSyntax<TChar>> items)
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

    public sealed class RegexAlternative<TChar> : RegexSyntax<TChar>
    {
        private readonly ImmutableList<RegexSyntax<TChar>> items;

        public RegexAlternative(ImmutableList<RegexSyntax<TChar>> items)
        {
            this.items = items;
        }

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

    public sealed class RegexOptRep<TChar> : RegexSyntax<TChar>
    {
        private readonly RegexSyntax<TChar> syntax;
        private readonly bool opt;
        private readonly bool rep;

        public RegexOptRep(RegexSyntax<TChar> syntax, bool opt, bool rep)
        {
            this.syntax = syntax;
            this.opt = opt;
            this.rep = rep;
        }

        public RegexSyntax<TChar> Syntax => syntax;

        public bool Optional => opt;

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
