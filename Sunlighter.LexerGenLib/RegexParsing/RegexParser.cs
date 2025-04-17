using Sunlighter.OptionLib;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Sunlighter.LexerGenLib.RegexParsing
{ 
    public sealed class RegexParserBuilder<TParser, TStackItem, TSource, TString, TSet, TChar>
    {
        private readonly ILexerCharTraits<TChar> charTraits;
        private readonly IStringTraits<TString, TChar> stringTraits;
        private readonly ICharSetTraits<TSet, TChar> charSetTraits;
        private readonly IStringInputSourceTraits<TSource, TString, TSet, TChar> stringInputSourceTraits;
        private readonly ICombinatorTraits<TParser, TStackItem, TSource, TString, TSet, TChar> combinatorTraits;
        private readonly IStackItemTraits<TStackItem, TSet, TChar> stackItemTraits;

        public RegexParserBuilder
        (
            ILexerCharTraits<TChar> charTraits,
            IStringTraits<TString, TChar> stringTraits,
            ICharSetTraits<TSet, TChar> charSetTraits,
            IStringInputSourceTraits<TSource, TString, TSet, TChar> stringInputSourceTraits,
            ICombinatorTraits<TParser, TStackItem, TSource, TString, TSet, TChar> combinatorTraits,
            IStackItemTraits<TStackItem, TSet, TChar> stackItemTraits
        )
        {
            this.charTraits = charTraits;
            this.stringTraits = stringTraits;
            this.charSetTraits = charSetTraits;
            this.stringInputSourceTraits = stringInputSourceTraits;
            this.combinatorTraits = combinatorTraits;
            this.stackItemTraits = stackItemTraits;
        }

        public Option<TStackItem> CharToStackItemOption(TChar ch) => Option<TStackItem>.Some(stackItemTraits.CharToStackItem(ch));

        private TParser ParseOnly(char ch) => combinatorTraits.CharFromSet(charSetTraits.Only(stackItemTraits.ConvertChar(ch)), CharToStackItemOption);

        private TParser MakeItemInSet1()
        {
            return combinatorTraits.Alternative
            (
                [
                    combinatorTraits.Sequence
                    (
                        combinatorTraits.CharFromSet
                        (
                            charSetTraits.Intersection
                            (
                                charSetTraits.GreaterEqual(stackItemTraits.ConvertChar(' ')),
                                charSetTraits.AnyExcept(stackItemTraits.ConvertChars("()<>~&|[]\\-"))
                            ),
                            CharToStackItemOption
                        ),
                        combinatorTraits.Reduce
                        (
                            1,
                            items =>
                            {
                                TStackItem item = items[0];
                                Option<TChar> charOpt = stackItemTraits.TryStackItemToChar(item);
                                if (charOpt.HasValue)
                                {
                                    return stackItemTraits.RegexCharForSetToStackItem(new SingleCharForSet<TChar>(charOpt.Value));
                                }
                                else
                                {
                                    throw new InvalidOperationException("Expected character");
                                }
                            }
                        )
                    ),
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('\\'),
                            combinatorTraits.Drop,
                            combinatorTraits.Alternative
                            (
                                combinatorTraits.Sequence
                                (
                                    combinatorTraits.CharFromSet
                                    (
                                        charSetTraits.AnyOf(stackItemTraits.ConvertChars("()<>~&|[]\\-abtnvfr")),
                                        CharToStackItemOption
                                    ),
                                    combinatorTraits.Reduce
                                    (
                                        1,
                                        items =>
                                        {
                                            TStackItem item = items[0];
                                            Option<TChar> charOpt = stackItemTraits.TryStackItemToChar(item);
                                            if (charOpt.HasValue)
                                            {
                                                return stackItemTraits.RegexCharForSetToStackItem(new CharEscapeForSet<TChar>(charOpt.Value));
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Expected character");
                                            }
                                        }
                                    )
                                ),
                                combinatorTraits.Sequence
                                (
                                    [
                                        ParseOnly('x'),
                                        combinatorTraits.Drop,
                                        combinatorTraits.PushLiteral(stackItemTraits.CharListToStackItem(ImmutableList<TChar>.Empty)),
                                        combinatorTraits.OptRep
                                        (
                                            combinatorTraits.Sequence
                                            (
                                                combinatorTraits.CharFromSet
                                                (
                                                    charSetTraits.Union
                                                    (
                                                        charSetTraits.Intersection
                                                        (
                                                            charSetTraits.GreaterEqual(stackItemTraits.ConvertChar('0')),
                                                            charSetTraits.LessEqual(stackItemTraits.ConvertChar('9'))
                                                        ),
                                                        charSetTraits.Union
                                                        (
                                                            charSetTraits.Intersection
                                                            (
                                                                charSetTraits.GreaterEqual(stackItemTraits.ConvertChar('A')),
                                                                charSetTraits.LessEqual(stackItemTraits.ConvertChar('F'))
                                                            ),
                                                            charSetTraits.Intersection
                                                            (
                                                                charSetTraits.GreaterEqual(stackItemTraits.ConvertChar('a')),
                                                                charSetTraits.LessEqual(stackItemTraits.ConvertChar('f'))
                                                            )
                                                        )
                                                    ),
                                                    CharToStackItemOption
                                                ),
                                                combinatorTraits.Reduce
                                                (
                                                    2,
                                                    items =>
                                                    {
                                                        TStackItem listItem = items[0];
                                                        TStackItem chItem = items[1];
                                                        Option<ImmutableList<TChar>> listOpt = stackItemTraits.TryStackItemToCharList(listItem);
                                                        Option<TChar> charOpt = stackItemTraits.TryStackItemToChar(chItem);
                                                        if (listOpt.HasValue)
                                                        {
                                                            if (charOpt.HasValue)
                                                            {
                                                                ImmutableList<TChar> list = listOpt.Value.Add(charOpt.Value);
                                                                return stackItemTraits.CharListToStackItem(list);
                                                            }
                                                            else
                                                            {
                                                                throw new InvalidOperationException("Expected character");

                                                            }
                                                        }
                                                        else
                                                        {
                                                            throw new InvalidOperationException("Expected list of characters");
                                                        }
                                                    }
                                                )
                                            ),
                                            false, true
                                        ),
                                        ParseOnly(';'),
                                        combinatorTraits.Drop,
                                        combinatorTraits.Reduce
                                        (
                                            1,
                                            items =>
                                            {
                                                TStackItem listItem = items[0];
                                                Option<ImmutableList<TChar>> listOpt = stackItemTraits.TryStackItemToCharList(listItem);
                                                if (listOpt.HasValue)
                                                {
                                                    return stackItemTraits.RegexCharForSetToStackItem(new HexEscapeForSet<TChar>(listOpt.Value));
                                                }
                                                else
                                                {
                                                    throw new InvalidOperationException("Expected list of characters");
                                                }
                                            }
                                        )
                                    ]
                                )
                            )
                        ]
                    )
                ]
            );
        }

        private TParser MakeItemInSet2(StrongBox<TParser> itemInSet1)
        {
            return combinatorTraits.Sequence
            (
                [
                    combinatorTraits.PushLiteral(stackItemTraits.IntToStackItem(0)),
                    combinatorTraits.OptRep
                    (
                        combinatorTraits.Sequence
                        (
                            [
                                ParseOnly('<'),
                                combinatorTraits.Drop,
                                combinatorTraits.Reduce
                                (
                                    1,
                                    items =>
                                    {
                                        TStackItem item = items[0];
                                        Option<int> intOpt = stackItemTraits.TryStackItemToInt(item);
                                        if (intOpt.HasValue)
                                        {
                                            return stackItemTraits.IntToStackItem(intOpt.Value - 1);
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Expected integer");
                                        }
                                    }
                                )
                            ]
                        ),
                        true, true
                    ),
                    combinatorTraits.Call(itemInSet1),
                    combinatorTraits.PushLiteral(stackItemTraits.IntToStackItem(0)),
                    combinatorTraits.OptRep
                    (
                        combinatorTraits.Sequence
                        (
                            [
                                ParseOnly('>'),
                                combinatorTraits.Drop,
                                combinatorTraits.Reduce
                                (
                                    1,
                                    items =>
                                    {
                                        TStackItem item = items[0];
                                        Option<int> intOpt = stackItemTraits.TryStackItemToInt(item);
                                        if (intOpt.HasValue)
                                        {
                                            return stackItemTraits.IntToStackItem(intOpt.Value + 1);
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Expected integer");
                                        }
                                    }
                                )
                            ]
                        ),
                        true, true
                    ),
                    combinatorTraits.Reduce
                    (
                        3,
                        items =>
                        {
                            Option<int> offsetLeftOpt = stackItemTraits.TryStackItemToInt(items[0]);
                            Option<RegexCharForSet<TChar>> charForSet = stackItemTraits.TryStackItemToRegexCharForSet(items[1]);
                            Option<int> offsetRightOpt = stackItemTraits.TryStackItemToInt(items[2]);
                            if (offsetLeftOpt.HasValue)
                            {
                                if (charForSet.HasValue)
                                {
                                    if (offsetRightOpt.HasValue)
                                    {
                                        return stackItemTraits.RegexCharForSetToStackItem
                                        (
                                            OffsetCharForSet<TChar>.Create
                                            (
                                                offsetLeftOpt.Value + offsetRightOpt.Value,
                                                charForSet.Value
                                            )
                                        );
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Expected integer");
                                    }
                                }
                                else
                                {
                                    throw new InvalidOperationException("Expected RegexCharForSet");
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException("Expected integer");
                            }
                        }
                    )
                ]
            );
        }

        private TParser MakeItemInSet3(StrongBox<TParser> itemInSet2, StrongBox<TParser> itemInSet6)
        {
            return combinatorTraits.Alternative
            (
                [
                    combinatorTraits.Sequence
                    (
                        [
                            combinatorTraits.Call(itemInSet2),
                            combinatorTraits.Alternative
                            (
                                [
                                    combinatorTraits.Sequence
                                    (
                                        [
                                            ParseOnly('-'),
                                            combinatorTraits.Drop,
                                            combinatorTraits.Alternative
                                            (
                                                [
                                                    combinatorTraits.Sequence
                                                    (
                                                        [
                                                            combinatorTraits.Call(itemInSet2),
                                                            combinatorTraits.Reduce
                                                            (
                                                                2,
                                                                items =>
                                                                {
                                                                    Option<RegexCharForSet<TChar>> startOpt = stackItemTraits.TryStackItemToRegexCharForSet(items[0]);
                                                                    Option<RegexCharForSet<TChar>> endOpt = stackItemTraits.TryStackItemToRegexCharForSet(items[1]);
                                                                    if (startOpt.HasValue && endOpt.HasValue)
                                                                    {
                                                                        return stackItemTraits.RegexCharSetToStackItem
                                                                        (
                                                                            new RegexCharSetClosedRange<TChar>(startOpt.Value, endOpt.Value)
                                                                        );
                                                                    }
                                                                    else
                                                                    {
                                                                        throw new InvalidOperationException("Expected RegexCharForSet");
                                                                    }
                                                                }
                                                            )
                                                        ]
                                                    ),
                                                    combinatorTraits.Reduce
                                                    (
                                                        1,
                                                        items =>
                                                        {
                                                            Option<RegexCharForSet<TChar>> startOpt = stackItemTraits.TryStackItemToRegexCharForSet(items[0]);
                                                            if (startOpt.HasValue)
                                                            {
                                                                return stackItemTraits.RegexCharSetToStackItem
                                                                (
                                                                    new RegexCharSetGreaterEqual<TChar>(startOpt.Value)
                                                                );
                                                            }
                                                            else
                                                            {
                                                                throw new InvalidOperationException("Expected RegexCharForSet");
                                                            }
                                                        }
                                                    )
                                                ]
                                            )
                                        ]
                                    ),
                                    combinatorTraits.Reduce
                                    (
                                        1,
                                        items =>
                                        {
                                            Option<RegexCharForSet<TChar>> onlyOpt = stackItemTraits.TryStackItemToRegexCharForSet(items[0]);
                                            if (onlyOpt.HasValue)
                                            {
                                                return stackItemTraits.RegexCharSetToStackItem
                                                (
                                                    new RegexCharSetOnly<TChar>(onlyOpt.Value)
                                                );
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Expected RegexCharForSet");
                                            }
                                        }
                                    )
                                ]
                            )
                        ]
                    ),
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('-'),
                            combinatorTraits.Drop,
                            combinatorTraits.Call(itemInSet2),
                            combinatorTraits.Reduce
                            (
                                1,
                                items =>
                                {
                                    Option<RegexCharForSet<TChar>> endOpt = stackItemTraits.TryStackItemToRegexCharForSet(items[0]);
                                    if (endOpt.HasValue)
                                    {
                                        return stackItemTraits.RegexCharSetToStackItem(
                                            new RegexCharSetLessEqual<TChar>(endOpt.Value));
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Expected RegexCharForSet");
                                    }
                                }
                            )
                        ]
                    ),
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('('),
                            combinatorTraits.Drop,
                            combinatorTraits.Call(itemInSet6),
                            ParseOnly(')'),
                            combinatorTraits.Drop
                        ]
                    )
                ]
            );
        }

        private TParser MakeItemInSet4(StrongBox<TParser> itemInSet3)
        {
            return combinatorTraits.Alternative
            (
                [
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('~'),
                            combinatorTraits.Drop,
                            combinatorTraits.Call(itemInSet3),
                            combinatorTraits.Reduce
                            (
                                1,
                                items =>
                                {
                                    Option<RegexCharSet<TChar>> setOpt = stackItemTraits.TryStackItemToRegexCharSet(items[0]);
                                    if (setOpt.HasValue)
                                    {
                                        return stackItemTraits.RegexCharSetToStackItem(
                                            new RegexCharSetComplement<TChar>(setOpt.Value));
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Expected RegexCharSet");
                                    }
                                }
                            )
                        ]
                    ),
                    combinatorTraits.Call(itemInSet3)
                ]
            );
        }

        private TParser MakeItemInSet5(StrongBox<TParser> itemInSet4)
        {
            return combinatorTraits.Sequence
            (
                [
                    combinatorTraits.Call(itemInSet4),
                    combinatorTraits.OptRep
                    (
                        combinatorTraits.Sequence
                        (
                            [
                                ParseOnly('&'),
                                combinatorTraits.Drop,
                                combinatorTraits.Call(itemInSet4),
                                combinatorTraits.Reduce
                                (
                                    2,
                                    items =>
                                    {
                                        Option<RegexCharSet<TChar>> set1Opt = stackItemTraits.TryStackItemToRegexCharSet(items[0]);
                                        Option<RegexCharSet<TChar>> set2Opt = stackItemTraits.TryStackItemToRegexCharSet(items[1]);
                                        if (set1Opt.HasValue && set2Opt.HasValue)
                                        {
                                            return stackItemTraits.RegexCharSetToStackItem(
                                                new RegexCharSetIntersection<TChar>(
                                                    ImmutableList.Create(set1Opt.Value, set2Opt.Value)));
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Expected RegexCharSet");
                                        }
                                    }
                                )
                            ]
                        ),
                        true, true
                    )
                ]
            );
        }

        private TParser MakeItemInSet6(StrongBox<TParser> itemInSet5)
        {
            return combinatorTraits.Sequence
            (
                [
                    combinatorTraits.Call(itemInSet5),
                    combinatorTraits.OptRep
                    (
                        combinatorTraits.Sequence
                        (
                            [
                                ParseOnly('|'),
                                combinatorTraits.Drop,
                                combinatorTraits.Call(itemInSet5),
                                combinatorTraits.Reduce
                                (
                                    2,
                                    items =>
                                    {
                                        Option<RegexCharSet<TChar>> set1Opt = stackItemTraits.TryStackItemToRegexCharSet(items[0]);
                                        Option<RegexCharSet<TChar>> set2Opt = stackItemTraits.TryStackItemToRegexCharSet(items[1]);
                                        if (set1Opt.HasValue && set2Opt.HasValue)
                                        {
                                            return stackItemTraits.RegexCharSetToStackItem(
                                                new RegexCharSetUnion<TChar>(
                                                    ImmutableList.Create(set1Opt.Value, set2Opt.Value)));
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Expected RegexCharSet");
                                        }
                                    }
                                )
                            ]
                        ),
                        true, true
                    )
                ]
            );
        }

        private TParser MakeRegex1(StrongBox<TParser> itemInSet6, StrongBox<TParser> regex4)
        {
            return combinatorTraits.Alternative
            (
                [
                    combinatorTraits.Sequence
                    (
                        [
                            combinatorTraits.CharFromSet
                            (
                                charSetTraits.Intersection
                                (
                                    charSetTraits.GreaterEqual(stackItemTraits.ConvertChar(' ')),
                                    charSetTraits.AnyExcept(stackItemTraits.ConvertChars("()+?*|\\[]"))
                                ),
                                CharToStackItemOption
                            ),
                            combinatorTraits.Reduce
                            (
                                1,
                                items =>
                                {
                                    Option<TChar> charOpt = stackItemTraits.TryStackItemToChar(items[0]);
                                    if (charOpt.HasValue)
                                    {
                                        return stackItemTraits.RegexSyntaxToStackItem(
                                            new RegexSingleChar<TChar>(charOpt.Value));
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Expected character");
                                    }
                                }
                            )
                        ]
                    ),
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('\\'),
                            combinatorTraits.Drop,
                            combinatorTraits.Alternative
                            (
                                [
                                    combinatorTraits.Sequence
                                    (
                                        [
                                            combinatorTraits.CharFromSet
                                            (
                                                charSetTraits.AnyOf(stackItemTraits.ConvertChars("()+?*|\\[]abtnvfrsS")),
                                                CharToStackItemOption
                                            ),
                                            combinatorTraits.Reduce
                                            (
                                                1,
                                                items =>
                                                {
                                                    Option<TChar> charOpt = stackItemTraits.TryStackItemToChar(items[0]);
                                                    if (charOpt.HasValue)
                                                    {
                                                        return stackItemTraits.RegexSyntaxToStackItem(
                                                            new RegexCharEscape<TChar>(charOpt.Value));
                                                    }
                                                    else
                                                    {
                                                        throw new InvalidOperationException("Expected character");
                                                    }
                                                }
                                            )
                                        ]
                                    ),
                                    combinatorTraits.Sequence
                                    (
                                        [
                                            ParseOnly('x'),
                                            combinatorTraits.Drop,
                                            combinatorTraits.PushLiteral(stackItemTraits.CharListToStackItem(ImmutableList<TChar>.Empty)),
                                            combinatorTraits.OptRep
                                            (
                                                combinatorTraits.Sequence
                                                (
                                                    [
                                                        combinatorTraits.CharFromSet
                                                        (
                                                            charSetTraits.Union
                                                            (
                                                                charSetTraits.Intersection
                                                                (
                                                                    charSetTraits.GreaterEqual(stackItemTraits.ConvertChar('0')),
                                                                    charSetTraits.LessEqual(stackItemTraits.ConvertChar('9'))
                                                                ),
                                                                charSetTraits.Union
                                                                (
                                                                    charSetTraits.Intersection
                                                                    (
                                                                        charSetTraits.GreaterEqual(stackItemTraits.ConvertChar('a')),
                                                                        charSetTraits.LessEqual(stackItemTraits.ConvertChar('f'))
                                                                    ),
                                                                    charSetTraits.Intersection
                                                                    (
                                                                        charSetTraits.GreaterEqual(stackItemTraits.ConvertChar('A')),
                                                                        charSetTraits.LessEqual(stackItemTraits.ConvertChar('F'))
                                                                    )
                                                                )
                                                            ),
                                                            CharToStackItemOption
                                                        ),
                                                        combinatorTraits.Reduce
                                                        (
                                                            2,
                                                            items =>
                                                            {
                                                                Option<ImmutableList<TChar>> listOpt = stackItemTraits.TryStackItemToCharList(items[0]);
                                                                Option<TChar> charOpt = stackItemTraits.TryStackItemToChar(items[1]);
                                                                if (listOpt.HasValue && charOpt.HasValue)
                                                                {
                                                                    return stackItemTraits.CharListToStackItem(
                                                                        listOpt.Value.Add(charOpt.Value));
                                                                }
                                                                else
                                                                {
                                                                    throw new InvalidOperationException("Expected character or list");
                                                                }
                                                            }
                                                        )
                                                    ]
                                                ),
                                                false, true
                                            ),
                                            ParseOnly(';'),
                                            combinatorTraits.Drop,
                                            combinatorTraits.Reduce
                                            (
                                                1,
                                                items =>
                                                {
                                                    Option<ImmutableList<TChar>> listOpt = stackItemTraits.TryStackItemToCharList(items[0]);
                                                    if (listOpt.HasValue)
                                                    {
                                                        return stackItemTraits.RegexSyntaxToStackItem(
                                                            new RegexHexEscape<TChar>(listOpt.Value));
                                                    }
                                                    else
                                                    {
                                                        throw new InvalidOperationException("Expected list of characters");
                                                    }
                                                }
                                            )
                                        ]
                                    )
                                ]
                            )
                        ]
                    ),
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('['),
                            combinatorTraits.Drop,
                            combinatorTraits.Call(itemInSet6),
                            ParseOnly(']'),
                            combinatorTraits.Drop,
                            combinatorTraits.Reduce
                            (
                                1,
                                items =>
                                {
                                    Option<RegexCharSet<TChar>> setOpt = stackItemTraits.TryStackItemToRegexCharSet(items[0]);
                                    if (setOpt.HasValue)
                                    {
                                        return stackItemTraits.RegexSyntaxToStackItem(
                                            new RegexCharFromSet<TChar>(setOpt.Value));
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Expected RegexCharSet");
                                    }
                                }
                            )
                        ]
                    ),
                    combinatorTraits.Sequence
                    (
                        [
                            ParseOnly('('),
                            combinatorTraits.Drop,
                            combinatorTraits.Call(regex4),
                            ParseOnly(')'),
                            combinatorTraits.Drop,
                        ]
                    )
                ]
            );
        }

        private TParser MakeRegex2(StrongBox<TParser> regex1)
        {
            return combinatorTraits.Sequence
            (
                [
                    combinatorTraits.Call(regex1),
                    combinatorTraits.Alternative
                    (
                        [
                            combinatorTraits.Sequence
                            (
                                [
                                    ParseOnly('?'),
                                    combinatorTraits.Drop,
                                    combinatorTraits.Reduce
                                    (
                                        1,
                                        items =>
                                        {
                                            Option<RegexSyntax<TChar>> syntaxOpt = stackItemTraits.TryStackItemToRegexSyntax(items[0]);
                                            if (syntaxOpt.HasValue)
                                            {
                                                return stackItemTraits.RegexSyntaxToStackItem
                                                (
                                                    new RegexOptRep<TChar>(syntaxOpt.Value, true, false)
                                                );
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Expected RegexSyntax");
                                            }
                                        }
                                    )
                                ]
                            ),
                            combinatorTraits.Sequence
                            (
                                [
                                    ParseOnly('+'),
                                    combinatorTraits.Drop,
                                    combinatorTraits.Reduce
                                    (
                                        1,
                                        items =>
                                        {
                                            Option<RegexSyntax<TChar>> syntaxOpt = stackItemTraits.TryStackItemToRegexSyntax(items[0]);
                                            if (syntaxOpt.HasValue)
                                            {
                                                return stackItemTraits.RegexSyntaxToStackItem
                                                (
                                                    new RegexOptRep<TChar>(syntaxOpt.Value, false, true)
                                                );
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Expected RegexSyntax");
                                            }
                                        }
                                    )
                                ]
                            ),
                            combinatorTraits.Sequence
                            (
                                [
                                    ParseOnly('*'),
                                    combinatorTraits.Drop,
                                    combinatorTraits.Reduce
                                    (
                                        1,
                                        items =>
                                        {
                                            Option<RegexSyntax<TChar>> syntaxOpt = stackItemTraits.TryStackItemToRegexSyntax(items[0]);
                                            if (syntaxOpt.HasValue)
                                            {
                                                return stackItemTraits.RegexSyntaxToStackItem
                                                (
                                                    new RegexOptRep<TChar>(syntaxOpt.Value, true, true)
                                                );
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("Expected RegexSyntax");
                                            }
                                        }
                                    )
                                ]
                            ),
                            combinatorTraits.Nop
                        ]
                    )
                ]
            );
        }

        private TParser MakeRegex3(StrongBox<TParser> regex2)
        {
            return combinatorTraits.Sequence
            (
                [
                    combinatorTraits.Reduce
                    (
                        0,
                        _ => stackItemTraits.RegexSyntaxToStackItem(RegexEmptyString<TChar>.Value)
                    ),
                    combinatorTraits.OptRep
                    (
                        combinatorTraits.Sequence
                        (
                            [
                                combinatorTraits.Call(regex2),
                                combinatorTraits.Reduce
                                (
                                    2,
                                    items =>
                                    {
                                        Option<RegexSyntax<TChar>> syntax1Opt = stackItemTraits.TryStackItemToRegexSyntax(items[0]);
                                        Option<RegexSyntax<TChar>> syntax2Opt = stackItemTraits.TryStackItemToRegexSyntax(items[1]);
                                        if (syntax1Opt.HasValue && syntax2Opt.HasValue)
                                        {
                                            return stackItemTraits.RegexSyntaxToStackItem
                                            (
                                                RegexSequence<TChar>.CreateSequence
                                                (
                                                    [ syntax1Opt.Value, syntax2Opt.Value ]
                                                )
                                            );
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Expected RegexSyntax");
                                        }
                                    }
                                )
                            ]
                        ),
                        true, true
                    )
                ]
            );
        }

        private TParser MakeRegex4(StrongBox<TParser> regex3)
        {
            return combinatorTraits.Sequence
            (
                [
                    combinatorTraits.Call(regex3),
                    combinatorTraits.OptRep
                    (
                        combinatorTraits.Sequence
                        (
                            [
                                ParseOnly('|'),
                                combinatorTraits.Drop,
                                combinatorTraits.Call(regex3),
                                combinatorTraits.Reduce
                                (
                                    2,
                                    items =>
                                    {
                                        Option<RegexSyntax<TChar>> syntax1Opt = stackItemTraits.TryStackItemToRegexSyntax(items[0]);
                                        Option<RegexSyntax<TChar>> syntax2Opt = stackItemTraits.TryStackItemToRegexSyntax(items[1]);
                                        if (syntax1Opt.HasValue && syntax2Opt.HasValue)
                                        {
                                            return stackItemTraits.RegexSyntaxToStackItem
                                            (
                                                new RegexAlternative<TChar>
                                                (
                                                    [ syntax1Opt.Value, syntax2Opt.Value ]
                                                )
                                            );
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Expected RegexSyntax");
                                        }
                                    }
                                )
                            ]
                        ),
                        true, true
                    )
                ]
            );
        }

        public TParser MakeRegexParser()
        {
            StrongBox<TParser> itemInSet1 = new StrongBox<TParser>();
            StrongBox<TParser> itemInSet2 = new StrongBox<TParser>();
            StrongBox<TParser> itemInSet3 = new StrongBox<TParser>();
            StrongBox<TParser> itemInSet4 = new StrongBox<TParser>();
            StrongBox<TParser> itemInSet5 = new StrongBox<TParser>();
            StrongBox<TParser> itemInSet6 = new StrongBox<TParser>();
            StrongBox<TParser> regex1 = new StrongBox<TParser>();
            StrongBox<TParser> regex2 = new StrongBox<TParser>();
            StrongBox<TParser> regex3 = new StrongBox<TParser>();
            StrongBox<TParser> regex4 = new StrongBox<TParser>();

            itemInSet1.Value = MakeItemInSet1();
            itemInSet2.Value = MakeItemInSet2(itemInSet1);
            itemInSet3.Value = MakeItemInSet3(itemInSet2, itemInSet6);
            itemInSet4.Value = MakeItemInSet4(itemInSet3);
            itemInSet5.Value = MakeItemInSet5(itemInSet4);
            itemInSet6.Value = MakeItemInSet6(itemInSet5);
            regex1.Value = MakeRegex1(itemInSet6, regex4);
            regex2.Value = MakeRegex2(regex1);
            regex3.Value = MakeRegex3(regex2);
            regex4.Value = MakeRegex4(regex3);

            return regex4.Value;
        }
    }

    public static partial class RegexParser
    {
        private static readonly Lazy<ParserFunc<StackItem<ImmutableList<char>, char>, (string, int)>> defaultParser =
            new Lazy<ParserFunc<StackItem<ImmutableList<char>, char>, (string, int)>>
            (
                MakeDefaultParser,
                LazyThreadSafetyMode.ExecutionAndPublication
            );

        private static ParserFunc<StackItem<ImmutableList<char>, char>, (string, int)> MakeDefaultParser()
        {
            var charSetTraits = new ImmutableListRangeSetTraits<char>(LexerCharTraits.Value);

            var stringInputSourceTraits = DefaultStringInputSourceTraits;

            var combinatorTraits = DefaultCombinatorTraits;

            var rpb = new RegexParserBuilder
                <
                    ParserFunc<StackItem<ImmutableList<char>, char>, (string, int)>,
                    StackItem<ImmutableList<char>, char>,
                    (string, int),
                    string,
                    ImmutableList<char>,
                    char
                >
                (
                    LexerCharTraits.Value,
                    StringTraits.Value,
                    charSetTraits,
                    stringInputSourceTraits,
                    combinatorTraits,
                    DefaultStackItemTraits.Value
                );

            return rpb.MakeRegexParser();
        }

        public static ParserFunc<StackItem<ImmutableList<char>, char>, (string, int)> DefaultParser => defaultParser.Value;
    }
}
