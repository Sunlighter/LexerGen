using System;
using System.Collections.Immutable;
using Sunlighter.LexerGenLib.RegexParsing;
using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;

namespace Sunlighter.LexerGenLib
{
    public abstract class LexerRule<TAccept>
    {
        private readonly TAccept acceptCode;

        protected LexerRule(TAccept acceptCode)
        {
            this.acceptCode = acceptCode;
        }

        public TAccept AcceptCode => acceptCode;

        public abstract (Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, TAccept) Eval
        (
            ICharSetTraits<ImmutableList<char>, char> charSetTraits,
            INFATraits<NFA<ImmutableList<char>, TAccept>, Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, ImmutableList<char>, char, TAccept> nfaTraits
        );
    }

    public sealed class LiteralLexerRule<TAccept> : LexerRule<TAccept>
    {
        private readonly string value;

        public LiteralLexerRule(string value, TAccept acceptCode)
            : base(acceptCode)
        {
            this.value = value;
        }

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

    public sealed class RegexLexerRule<TAccept> : LexerRule<TAccept>
    {
        private readonly string regex;

        public RegexLexerRule(string regex, TAccept acceptCode)
            : base(acceptCode)
        {
            this.regex = regex;
        }

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
        public static DFA<ImmutableList<char>, TAccept> GenerateLexer<TAccept>(ImmutableList<LexerRule<TAccept>> rules, ITypeTraits<TAccept> acceptCodeTraits)
        {
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

        public static ITypeTraits<DFA<ImmutableList<char>, TAccept>> GetDFATypeTraits<TAccept>(ITypeTraits<TAccept> acceptCodeTypeTraits)
        {
            return DFA<ImmutableList<char>, TAccept>.GetTypeTraits
            (
                new ListTypeTraits<char>(CharTypeTraits.Value),
                acceptCodeTypeTraits
            );
        }
    }
}
