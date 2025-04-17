using Sunlighter.OptionLib;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Sunlighter.LexerGenLib.RegexParsing
{
    public sealed class ParserState<TStackItem, TSource>
    {
        private readonly ImmutableList<TStackItem> stack;
        private readonly TSource source;

        public ParserState(ImmutableList<TStackItem> stack, TSource source)
        {
            this.stack = stack;
            this.source = source;
        }

        public ImmutableList<TStackItem> Stack => stack;

        public TSource Source => source;
    }

    public interface ICombinatorTraits<TParser, TStackItem, TSource, TString, TSet, TChar>
    {
        IStringInputSourceTraits<TSource, TString, TSet, TChar> InputSourceTraits { get; }
        ILexerCharTraits<TChar> CharTraits { get; }
        IStringTraits<TString, TChar> StringTraits { get; }
        ICharSetTraits<TSet, TChar> CharSetTraits { get; }

        TParser Drop { get; }

        TParser Fail { get; }

        TParser Nop { get; }

        TParser SpecificChar(TChar ch, Func<TChar, Option<TStackItem>> convertOpt);

        TParser CharFromSet(TSet set, Func<TChar, Option<TStackItem>> convertOpt);

        TParser Sequence(TParser first, TParser second);

        TParser Sequence(ImmutableList<TParser> items);

        TParser Alternative(TParser first, TParser second);

        TParser Alternative(ImmutableList<TParser> items);

        TParser OptRep(TParser body, bool opt, bool rep);

        TParser OnlyIfFollowedBy(TParser body);

        TParser OnlyIfNotFollowedBy(TParser body);

        TParser PushLiteral(TStackItem item);

        TParser Reduce(int count, Func<ImmutableList<TStackItem>, TStackItem> reduction);

        TParser TryReduce(int count, Func<ImmutableList<TStackItem>, Option<TStackItem>> tryReduction);

        TParser Call(StrongBox<TParser> target);

        Option<TStackItem> TryParse(TParser parser, TSource input);
    }

    public delegate Option<ParserState<TStackItem, TSource>> ParserFunc<TStackItem, TSource>(ParserState<TStackItem, TSource> state);

    public sealed class CombinatorTraits<TStackItem, TSource, TString, TSet, TChar> :
        ICombinatorTraits<ParserFunc<TStackItem, TSource>, TStackItem, TSource, TString, TSet, TChar>
    {
        private readonly IStringInputSourceTraits<TSource, TString, TSet, TChar> inputSourceTraits;
        private readonly ParserFunc<TStackItem, TSource> dropFunc;
        private readonly ParserFunc<TStackItem, TSource> failFunc;
        private readonly ParserFunc<TStackItem, TSource> nopFunc;

        public CombinatorTraits
        (
            IStringInputSourceTraits<TSource, TString, TSet, TChar> inputSourceTraits
        )
        {
            this.inputSourceTraits = inputSourceTraits;
            this.dropFunc = DropInternal;
            this.failFunc = FailInternal;
            this.nopFunc = NopInternal;
        }

        private Option<ParserState<TStackItem, TSource>> DropInternal(ParserState<TStackItem, TSource> state)
        {
            if (state.Stack.IsEmpty)
            {
                throw new InvalidOperationException("Stack underflow");
            }
            else
            {
                return Option<ParserState<TStackItem, TSource>>.Some
                (
                    new ParserState<TStackItem, TSource>
                    (
                        state.Stack.RemoveAt(state.Stack.Count - 1),
                        state.Source
                    )
                );
            }
        }

        private Option<ParserState<TStackItem, TSource>> FailInternal(ParserState<TStackItem, TSource> state)
        {
            return Option<ParserState<TStackItem, TSource>>.None;
        }

        private Option<ParserState<TStackItem, TSource>> NopInternal(ParserState<TStackItem, TSource> state)
        {
            return Option<ParserState<TStackItem, TSource>>.Some(state);
        }

        public IStringInputSourceTraits<TSource, TString, TSet, TChar> InputSourceTraits => inputSourceTraits;

        public ILexerCharTraits<TChar> CharTraits => inputSourceTraits.CharTraits;
        public IStringTraits<TString, TChar> StringTraits => inputSourceTraits.StringTraits;
        public ICharSetTraits<TSet, TChar> CharSetTraits => inputSourceTraits.CharSetTraits;

        public ParserFunc<TStackItem, TSource> Drop => dropFunc;
        public ParserFunc<TStackItem, TSource> Fail => failFunc;
        public ParserFunc<TStackItem, TSource> Nop => nopFunc;

        public ParserFunc<TStackItem, TSource> SpecificChar(TChar ch, Func<TChar, Option<TStackItem>> convertOpt)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var result = inputSourceTraits.TryReadExactChar(state.Source, ch);
                if (result.HasValue)
                {
                    var (chRead, newState) = result.Value;
                    Option<TStackItem> stackOpt = convertOpt(chRead);
                    if (stackOpt.HasValue)
                    {
                        return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(state.Stack.Add(stackOpt.Value), newState));
                    }
                    else
                    {
                        return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(state.Stack, newState));
                    }
                }
                else
                {
                    return Option<ParserState<TStackItem, TSource>>.None;
                }
            }

            return impl;
        }

        public ParserFunc<TStackItem, TSource> CharFromSet(TSet set, Func<TChar, Option<TStackItem>> convertOpt)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var result = inputSourceTraits.TryReadChar(state.Source, set);
                if (result.HasValue)
                {
                    var (chRead, newState) = result.Value;
                    Option<TStackItem> stackOpt = convertOpt(chRead);
                    if (stackOpt.HasValue)
                    {
                        return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(state.Stack.Add(stackOpt.Value), newState));
                    }
                    else
                    {
                        return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(state.Stack, newState));
                    }
                }
                else
                {
                    return Option<ParserState<TStackItem, TSource>>.None;
                }
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> Sequence(ParserFunc<TStackItem, TSource> first, ParserFunc<TStackItem, TSource> second)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var firstResult = first(state);
                if (firstResult.HasValue)
                {
                    var secondResult = second(firstResult.Value);
                    return secondResult;
                }
                else
                {
                    return Option<ParserState<TStackItem, TSource>>.None;
                }
            }

            return impl;
        }

        public ParserFunc<TStackItem, TSource> Sequence(ImmutableList<ParserFunc<TStackItem, TSource>> items)
        {
            if (items.IsEmpty)
            {
                return nopFunc;
            }
            else if (items.Count == 1)
            {
                return items[0];
            }
            else
            {
                ParserFunc<TStackItem, TSource> result = items[0];
                for (int i = 1; i < items.Count; i++)
                {
                    result = Sequence(result, items[i]);
                }
                return result;
            }
        }

        public ParserFunc<TStackItem, TSource> Alternative(ParserFunc<TStackItem, TSource> first, ParserFunc<TStackItem, TSource> second)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var firstResult = first(state);
                if (firstResult.HasValue)
                {
                    return firstResult;
                }
                else
                {
                    return second(state);
                }
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> Alternative(ImmutableList<ParserFunc<TStackItem, TSource>> items)
        {
            if (items.IsEmpty)
            {
                return failFunc;
            }
            else if (items.Count == 1)
            {
                return items[0];
            }
            else
            {
                ParserFunc<TStackItem, TSource> result = items[0];
                for (int i = 1; i < items.Count; i++)
                {
                    result = Alternative(result, items[i]);
                }
                return result;
            }
        }

        public ParserFunc<TStackItem, TSource> OptRep(ParserFunc<TStackItem, TSource> body, bool opt, bool rep)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var firstResult = body(state);
                if (firstResult.HasValue)
                {
                    var nextResult = firstResult.Value;
                    while(true)
                    {
                        var nextResultOpt = body(nextResult);
                        if (nextResultOpt.HasValue)
                        {
                            nextResult = nextResultOpt.Value;
                        }
                        else
                        {
                            return Option<ParserState<TStackItem, TSource>>.Some(nextResult);
                        }
                    }
                }
                else
                {
                    if (opt)
                    {
                        return Option<ParserState<TStackItem, TSource>>.Some(state);
                    }
                    else
                    {
                        return Option<ParserState<TStackItem, TSource>>.None;
                    }
                }
            }
            
            return impl;
        }

        public ParserFunc<TStackItem, TSource> OnlyIfFollowedBy(ParserFunc<TStackItem, TSource> body)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var result = body(state);
                if (result.HasValue)
                {
                    return Option<ParserState<TStackItem, TSource>>.Some(state);
                }
                else
                {
                    return Option<ParserState<TStackItem, TSource>>.None;
                }
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> OnlyIfNotFollowedBy(ParserFunc<TStackItem, TSource> body)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                var result = body(state);
                if (result.HasValue)
                {
                    return Option<ParserState<TStackItem, TSource>>.None;
                }
                else
                {
                    return Option<ParserState<TStackItem, TSource>>.Some(state);
                }
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> PushLiteral(TStackItem item)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(state.Stack.Add(item), state.Source));
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> Reduce(int count, Func<ImmutableList<TStackItem>, TStackItem> reduction)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                if (state.Stack.Count < count)
                {
                    throw new InvalidOperationException("Stack underflow");
                }
                else
                {
                    var items = state.Stack.RemoveRange(0, state.Stack.Count - count);
                    var newItem = reduction(items);
                    var newStack = state.Stack.RemoveRange(state.Stack.Count - count, count).Add(newItem);
                    return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(newStack, state.Source));
                }
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> TryReduce(int count, Func<ImmutableList<TStackItem>, Option<TStackItem>> tryReduction)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                if (state.Stack.Count < count)
                {
                    throw new InvalidOperationException("Stack underflow");
                }
                else
                {
                    var items = state.Stack.RemoveRange(0, state.Stack.Count - count);
                    var newItemOpt = tryReduction(items);
                    if (newItemOpt.HasValue)
                    {
                        var newItem = newItemOpt.Value;
                        var newStack = state.Stack.RemoveRange(state.Stack.Count - count, count).Add(newItem);
                        return Option<ParserState<TStackItem, TSource>>.Some(new ParserState<TStackItem, TSource>(newStack, state.Source));
                    }
                    else
                    {
                        return Option<ParserState<TStackItem, TSource>>.None;
                    }
                }
            }
            return impl;
        }

        public ParserFunc<TStackItem, TSource> Call(StrongBox<ParserFunc<TStackItem, TSource>> target)
        {
            Option<ParserState<TStackItem, TSource>> impl(ParserState<TStackItem, TSource> state)
            {
                if (target.Value is not null)
                {
                    return target.Value(state);
                }
                else
                {
                    throw new InvalidOperationException("Target is null");
                }
            }
            return impl;
        }

        public Option<TStackItem> TryParse(ParserFunc<TStackItem, TSource> parser, TSource input)
        {
            var initialState = new ParserState<TStackItem, TSource>(ImmutableList<TStackItem>.Empty, input);
            var result = parser(initialState);
            if (result.HasValue)
            {
                ImmutableList<TStackItem> stack = result.Value.Stack;
                if (stack.IsEmpty)
                {
                    throw new InvalidOperationException("Stack underflow");
                }
                else
                {
                    return Option<TStackItem>.Some(stack[^1]);
                }
            }
            else
            {
                return Option<TStackItem>.None;
            }
        }
    }

    public static partial class RegexParser
    {
        private static readonly Lazy<CombinatorTraits<StackItem<ImmutableList<char>, char>, (string, int), string, ImmutableList<char>, char>> defaultCombinatorTraits
            = new Lazy<CombinatorTraits<StackItem<ImmutableList<char>, char>, (string, int), string, ImmutableList<char>, char>>
            (
                GetDefaultCombinatorTraits,
                LazyThreadSafetyMode.ExecutionAndPublication
            );

        private static CombinatorTraits<StackItem<ImmutableList<char>, char>, (string, int), string, ImmutableList<char>, char> GetDefaultCombinatorTraits()
        {
            return new CombinatorTraits<StackItem<ImmutableList<char>, char>, (string, int), string, ImmutableList<char>, char>(defaultStringInputSourceTraits.Value);
        }

        public static CombinatorTraits<StackItem<ImmutableList<char>, char>, (string, int), string, ImmutableList<char>, char> DefaultCombinatorTraits => defaultCombinatorTraits.Value;
    }
}
