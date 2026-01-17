using Sunlighter.LexerGenLib.RegexParsing;
using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using Sunlighter.TypeTraitsLib.Building;
using System.Collections.Immutable;
using System.ComponentModel;

namespace Sunlighter.LexerGenLib
{
    [UnionOfDescendants]
    public abstract class LexerRule<TAccept>
    {
        private readonly TAccept acceptCode;

        protected LexerRule(TAccept acceptCode)
        {
            this.acceptCode = acceptCode;
        }

        [Bind("acceptCode")]
        public TAccept AcceptCode => acceptCode;

        public abstract (Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, TAccept) Eval
        (
            ICharSetTraits<ImmutableList<char>, char> charSetTraits,
            INFATraits<NFA<ImmutableList<char>, TAccept>, Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, ImmutableList<char>, char, TAccept> nfaTraits
        );
    }

    [Record]
    [UnionCaseName("literal")]
    public sealed class LiteralLexerRule<TAccept> : LexerRule<TAccept>
    {
        private readonly string value;

        public LiteralLexerRule([Bind("value")] string value, [Bind("acceptCode")] TAccept acceptCode)
            : base(acceptCode)
        {
            this.value = value;
        }

        [Bind("value")]
        public string Value => value;

        public override (Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, TAccept) Eval
        (
            ICharSetTraits<ImmutableList<char>, char> charSetTraits,
            INFATraits<NFA<ImmutableList<char>, TAccept>, Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, ImmutableList<char>, char, TAccept> nfaTraits
        )
        {
            var parser = nfaTraits.ExactString
            (
                charSetTraits,
                value
            );

            return (parser, AcceptCode);
        }
    }

    [Record]
    [UnionCaseName("regex")]
    public sealed class RegexLexerRule<TAccept> : LexerRule<TAccept>
    {
        private readonly string regex;

        public RegexLexerRule([Bind("regex")] string regex, [Bind("acceptCode")] TAccept acceptCode)
            : base(acceptCode)
        {
            this.regex = regex;
        }

        [Bind("regex")]
        public string Regex => regex;

        public override (Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, TAccept) Eval
        (
            ICharSetTraits<ImmutableList<char>, char> charSetTraits,
            INFATraits<NFA<ImmutableList<char>, TAccept>, Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, ImmutableList<char>, char, TAccept> nfaTraits
        )
        {
            var regexParser = RegexParser.DefaultParser;

            Option<StackItem<ImmutableList<char>, char>> result = RegexParser.DefaultCombinatorTraits.TryParse
            (
                regexParser,
                RegexParser.DefaultStringInputSourceTraits.Create(regex)
            );

            if (result.HasValue && result.Value is StackItemRegexSyntax<ImmutableList<char>, char> stackItemRegexSyntax)
            {
                var evaluatedRegex = stackItemRegexSyntax.Value.Eval(RegexCharTraits.Value, charSetTraits, nfaTraits);
                return (evaluatedRegex, AcceptCode);
            }
            else
            {
                throw new InvalidOperationException($"Failure parsing regex {regex.Quoted()}");
            }
        }
    }

    public static class LexerGen
    {
        public static DFA<ImmutableList<char>, TAccept> GenerateLexer<TAccept>(ImmutableList<LexerRule<TAccept>> rules)
        {
            var acceptCodeTraits = Builder.Instance.GetTypeTraits<TAccept>();

            var charSetTraits = new ImmutableListRangeSetTraits<char>(LexerCharTraits.Value);
            var nfaTraits = new NFATraits<char, TAccept>(LexerCharTraits.Value, new ImmutableListRangeSetTraits<char>(LexerCharTraits.Value));

            ImmutableList<(Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, TAccept)> nfas = rules
                .Select(rule => rule.Eval(charSetTraits, nfaTraits))
                .ToImmutableList();

            TAccept maxAcceptCode(TAccept a, TAccept b) => acceptCodeTraits.Compare(a, b) > 0 ? a : b;

            NFA<ImmutableList<char>, TAccept> finalNfa = nfaTraits.Finalize(nfas);

            DFABuilder<ImmutableList<char>, char, TAccept> dfaBuilder = new DFABuilder<ImmutableList<char>, char, TAccept>(charSetTraits);
            DFAResult dfaResult = finalNfa.ToDFA(dfaBuilder, maxAcceptCode);
            DFA<ImmutableList<char>, TAccept> realDfa = dfaBuilder.ToDFA(dfaResult.StartState, dfaResult.DeadState);

            DFA<ImmutableList<char>, TAccept> minimalDfa = realDfa.Minimize(charSetTraits, acceptCodeTraits);

            return minimalDfa;
        }

        public static ImmutableList<(string, Option<TAccept>)> Lex<TAccept>(this DFA<ImmutableList<char>, TAccept> dfa, string input, int startPos = 0)
        {
            ImmutableList<(string, Option<TAccept>)> results = [];
            int pos = startPos;
            int endPos = input.Length;

            while (pos < endPos)
            {
                Option<(int, TAccept)> matchResult = dfa.TryMatchPrefix(input, pos);
                if (matchResult.HasValue)
                {
                    int len = matchResult.Value.Item1;
                    TAccept acceptCode = matchResult.Value.Item2;
                    string matchedString = input.Substring(pos, len);
                    pos += len;
                    results = results.Add((matchedString, Option<TAccept>.Some(acceptCode)));
                }
                else
                {
                    string junk = input.Substring(pos, 1);
                    pos++;
                    results = results.Add((junk, Option<TAccept>.None));
                }
            }

            return results;
        }

        public static ImmutableSortedDictionary<TState, DFA<ImmutableList<char>, TAccept>> GenerateLexer<TState, TAccept>
        (
            ImmutableSortedDictionary<TState, ImmutableList<LexerRule<TAccept>>> rules
        )
            where TState : notnull
        {
            var stateTraits = Builder.Instance.GetTypeTraits<TState>();
            var acceptCodeTraits = Builder.Instance.GetTypeTraits<TAccept>();

            var stateComparer = Adapter<TState>.Create(stateTraits);
            ImmutableSortedDictionary<TState, DFA<ImmutableList<char>, TAccept>> results =
                ImmutableSortedDictionary<TState, DFA<ImmutableList<char>, TAccept>>.Empty.WithComparers(stateComparer);

            foreach (KeyValuePair<TState, ImmutableList<LexerRule<TAccept>>> kvp in rules)
            {
                TState state = kvp.Key;
                ImmutableList<LexerRule<TAccept>> ruleList = kvp.Value;
                DFA<ImmutableList<char>, TAccept> dfa = GenerateLexer(ruleList);
                results = results.Add(state, dfa);
            }

            return results;
        }

        public static (ImmutableList<(string, Option<TAccept>)>, TState) Lex<TState, TAccept>
        (
            this ImmutableSortedDictionary<TState, DFA<ImmutableList<char>, TAccept>> dfaStates,
            TState initialState,
            Func<TAccept, TState> getNextState,
            string input, int startPos = 0
        )
            where TState : notnull
        {
            ImmutableList<(string, Option<TAccept>)> results = [];
            int pos = startPos;
            int endPos = input.Length;
            TState currentState = initialState;

            while (pos < endPos)
            {
                Option<(int, TAccept)> matchResult = dfaStates[currentState].TryMatchPrefix(input, pos);
                if (matchResult.HasValue)
                {
                    int len = matchResult.Value.Item1;
                    TAccept acceptCode = matchResult.Value.Item2;
                    currentState = getNextState(acceptCode);
                    string matchedString = input.Substring(pos, len);
                    pos += len;
                    results = results.Add((matchedString, Option<TAccept>.Some(acceptCode)));
                }
                else
                {
                    string junk = input.Substring(pos, 1);
                    pos++;
                    results = results.Add((junk, Option<TAccept>.None));
                }
            }

            return (results, currentState);
        }
    }
}
