using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using Sunlighter.TypeTraitsLib.Building;
using System.Collections.Immutable;

namespace Sunlighter.LexerGenLib.RegexParsing
{
    public interface IStackItemTraits<TStackItem, TSet, TChar>
    {
        TChar ConvertChar(char ch);

        ImmutableList<TChar> ConvertChars(string str);

        TStackItem CharToStackItem(TChar ch);

        Option<TChar> TryStackItemToChar(TStackItem item);

        TStackItem CharListToStackItem(ImmutableList<TChar> chList);

        Option<ImmutableList<TChar>> TryStackItemToCharList(TStackItem item);

#if false
        TStackItem CharSetToStackItem(TSet set);

        Option<TSet> TryStackItemToCharSet(TStackItem item);
#endif

        TStackItem RegexCharForSetToStackItem(RegexCharForSet<TChar> regexCharForSet);

        Option<RegexCharForSet<TChar>> TryStackItemToRegexCharForSet(TStackItem item);

        TStackItem RegexCharSetToStackItem(RegexCharSet<TChar> regexCharSet);

        Option<RegexCharSet<TChar>> TryStackItemToRegexCharSet(TStackItem item);

        TStackItem RegexSyntaxToStackItem(RegexSyntax<TChar> regexSyntax);

        Option<RegexSyntax<TChar>> TryStackItemToRegexSyntax(TStackItem item);

        TStackItem IntToStackItem(int i);

        Option<int> TryStackItemToInt(TStackItem item);
    }

    [UnionOfDescendants]
    public abstract class StackItem<TSet, TChar>
    {
        
    }

    [Record]
    [UnionCaseName("stackItemChar")]
    public sealed class StackItemChar<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly TChar value;

        public StackItemChar([Bind("value")] TChar value)
        {
            this.value = value;
        }

        [Bind("value")]
        public TChar Value => value;
    }

    [Record]
    [UnionCaseName("stackItemInt")]
    public sealed class StackItemInt<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly int value;

        public StackItemInt([Bind("value")] int value)
        {
            this.value = value;
        }

        [Bind("value")]
        public int Value => value;
    }

#if false
    public sealed class StackItemCharSet<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly TSet value;

        public StackItemCharSet(TSet value)
        {
            this.value = value;
        }

        public TSet Value => value;
    }
#endif

    [Record]
    [UnionCaseName("stackItemCharList")]
    public sealed class StackItemCharList<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly ImmutableList<TChar> value;

        public StackItemCharList([Bind("value")] ImmutableList<TChar> value)
        {
            this.value = value;
        }

        [Bind("value")]
        public ImmutableList<TChar> Value => value;
    }

    [Record]
    [UnionCaseName("stackItemRegexCharForSet")]
    public sealed class StackItemRegexCharForSet<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly RegexCharForSet<TChar> value;

        public StackItemRegexCharForSet([Bind("value")] RegexCharForSet<TChar> value)
        {
            this.value = value;
        }

        [Bind("value")]
        public RegexCharForSet<TChar> Value => value;
    }

    [Record]
    [UnionCaseName("stackItemRegexCharSet")]
    public sealed class StackItemRegexCharSet<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly RegexCharSet<TChar> value;

        public StackItemRegexCharSet([Bind("value")] RegexCharSet<TChar> value)
        {
            this.value = value;
        }
        
        [Bind("value")]
        public RegexCharSet<TChar> Value => value;
    }
    
    [Record]
    [UnionCaseName("stackItemRegexSyntax")]
    public sealed class StackItemRegexSyntax<TSet, TChar> : StackItem<TSet, TChar>
    {
        private readonly RegexSyntax<TChar> value;

        public StackItemRegexSyntax([Bind("value")] RegexSyntax<TChar> value)
        {
            this.value = value;
        }

        [Bind("value")]
        public RegexSyntax<TChar> Value => value;
    }

    public abstract class StackItemTraits<TSet, TChar> : IStackItemTraits<StackItem<TSet, TChar>, TSet, TChar>
    {
        public abstract TChar ConvertChar(char ch);

        public abstract ImmutableList<TChar> ConvertChars(string str);

        public StackItem<TSet, TChar> CharToStackItem(TChar ch)
        {
            return new StackItemChar<TSet, TChar>(ch);
        }

        public Option<TChar> TryStackItemToChar(StackItem<TSet, TChar> item)
        {
            if (item is StackItemChar<TSet, TChar> sch)
            {
                return Option<TChar>.Some(sch.Value);
            }
            else
            {
                return Option<TChar>.None;
            }
        }

        public StackItem<TSet, TChar> CharListToStackItem(ImmutableList<TChar> chList)
        {
            return new StackItemCharList<TSet, TChar>(chList);
        }

        public Option<ImmutableList<TChar>> TryStackItemToCharList(StackItem<TSet, TChar> item)
        {
            if (item is StackItemCharList<TSet, TChar> scl)
            {
                return Option<ImmutableList<TChar>>.Some(scl.Value);
            }
            else
            {
                return Option<ImmutableList<TChar>>.None;
            }
        }

#if false
        public StackItem<TSet, TChar> CharSetToStackItem(TSet set)
        {
            return new StackItemCharSet<TSet, TChar>(set);
        }

        public Option<TSet> TryStackItemToCharSet(StackItem<TSet, TChar> item)
        {
            if (item is StackItemCharSet<TSet, TChar> scs)
            {
                return Option<TSet>.Some(scs.Value);
            }
            else
            {
                return Option<TSet>.None;
            }
        }
#endif

        public StackItem<TSet, TChar> RegexCharForSetToStackItem(RegexCharForSet<TChar> regexCharForSet)
        {
            return new StackItemRegexCharForSet<TSet, TChar>(regexCharForSet);
        }

        public Option<RegexCharForSet<TChar>> TryStackItemToRegexCharForSet(StackItem<TSet, TChar> item)
        {
            if (item is StackItemRegexCharForSet<TSet, TChar> scr)
            {
                return Option<RegexCharForSet<TChar>>.Some(scr.Value);
            }
            else
            {
                return Option<RegexCharForSet<TChar>>.None;
            }
        }

        public StackItem<TSet, TChar> RegexCharSetToStackItem(RegexCharSet<TChar> regexCharSet)
        {
            return new StackItemRegexCharSet<TSet, TChar>(regexCharSet);
        }

        public Option<RegexCharSet<TChar>> TryStackItemToRegexCharSet(StackItem<TSet, TChar> item)
        {
            if (item is StackItemRegexCharSet<TSet, TChar> chs)
            {
                return Option<RegexCharSet<TChar>>.Some(chs.Value);
            }
            else
            {
                return Option<RegexCharSet<TChar>>.None;
            }
        }

        public StackItem<TSet, TChar> RegexSyntaxToStackItem(RegexSyntax<TChar> regexSyntax)
        {
            return new StackItemRegexSyntax<TSet, TChar>(regexSyntax);
        }

        public Option<RegexSyntax<TChar>> TryStackItemToRegexSyntax(StackItem<TSet, TChar> item)
        {
            if (item is StackItemRegexSyntax<TSet, TChar> srs)
            {
                return Option<RegexSyntax<TChar>>.Some(srs.Value);
            }
            else
            {
                return Option<RegexSyntax<TChar>>.None;
            }
        }

        public StackItem<TSet, TChar> IntToStackItem(int i)
        {
            return new StackItemInt<TSet, TChar>(i);
        }

        public Option<int> TryStackItemToInt(StackItem<TSet, TChar> item)
        {
            if (item is StackItemInt<TSet, TChar> si)
            {
                return Option<int>.Some(si.Value);
            }
            else
            {
                return Option<int>.None;
            }
        }
    }

    public sealed class DefaultStackItemTraits : StackItemTraits<ImmutableList<char>, char>
    {
        private static readonly DefaultStackItemTraits value = new DefaultStackItemTraits();

        private DefaultStackItemTraits() { }

        public static DefaultStackItemTraits Value => value;

        public override char ConvertChar(char ch)
        {
            return ch;
        }

        public override ImmutableList<char> ConvertChars(string str)
        {
            return str.ToImmutableList();
        }
    }
}
