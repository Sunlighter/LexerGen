using Sunlighter.LexerGenLib;
using Sunlighter.LexerGenLib.RegexParsing;
using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using System;
using System.Collections.Immutable;

namespace LexerGenTest
{
    [TestClass]
    public class LexerGenTests
    {
        [TestMethod]
        public void RangeSetTest()
        {
            var traits = new ImmutableListRangeSetTraits<char>(LexerCharTraits.Value);

            var s1 = traits.Intersection(traits.GreaterEqual('C'), traits.LessEqual('K'));

            var s2 = traits.Union
            (
                traits.Intersection(traits.GreaterEqual('G'), traits.LessEqual('O')),
                traits.Intersection(traits.GreaterEqual('d'), traits.LessEqual('g'))
            );

            var s3 = traits.Union(s1, s2);

            void check (char ch, bool s1Has, bool s2Has)
            {
                Assert.IsTrue(traits.Contains(s1, ch) == s1Has, $"s1 should {(s1Has ? "" : "not ")}contain {ch}");
                Assert.IsTrue(traits.Contains(s2, ch) == s2Has, $"s2 should {(s2Has ? "" : "not ")}contain {ch}");
                Assert.IsTrue(traits.Contains(s3, ch) == (s1Has || s2Has), $"s3 should {(s1Has || s2Has ? "" : "not ")}contain {ch}");
            }

            check('A', false, false);
            check('C', true, false);
            check('E', true, false);
            check('I', true, true);
            check('M', false, true);
            check('Q', false, false);
            check('c', false, false);
            check('f', false, true);
            check('i', false, false);
        }

        [TestMethod]
        public void DFATest()
        {
            var charSetTraits = new ImmutableListRangeSetTraits<char>(LexerCharTraits.Value);
            var nfaTraits = new NFATraits<char, byte>(LexerCharTraits.Value, charSetTraits);

            var nfaFunc = nfaTraits.Alternative
            (
                [
                    nfaTraits.ExactString(charSetTraits, "dog"),
                    nfaTraits.ExactString(charSetTraits, "pig"),
                    nfaTraits.ExactString(charSetTraits, "horse")
                ]
            );

            var nfa = nfaTraits.Finalize([(nfaFunc, 0)]);

            var dfaBuilder = new DFABuilder<ImmutableList<char>, char, byte>(charSetTraits);

            static byte ByteMax(byte a, byte b) => (a > b) ? a : b;

            var dfaResult = nfa.ToDFA(dfaBuilder, ByteMax);

            var dfa2 = dfaBuilder.ToDFA(dfaResult.StartState, dfaResult.DeadState);

            Assert.IsTrue(dfaResult.StateSetToDFAState.Count > 0);

            var dfa2Result = dfa2.Minimize(charSetTraits, ByteTypeTraits.Value);

            Assert.IsTrue(dfa2Result.TryMatchPrefix("dog").HasValue);
            Assert.IsTrue(dfa2Result.TryMatchPrefix("pig").HasValue);
            Assert.IsTrue(dfa2Result.TryMatchPrefix("horse").HasValue);
            Assert.IsTrue(!dfa2Result.TryMatchPrefix("banana").HasValue);
        }

        [TestMethod]
        public void RegexParserTest()
        {
            var regexParser = RegexParser.DefaultParser;

            string[] regexes = [ "[A-Z|a-z]+", "ABC", "(A|a)bc", "\\x1B;a", "(((())))" ];

            ITypeTraits<Option<StackItem<ImmutableList<char>, char>>> stackItemOptionTraits =
                new OptionTypeTraits<StackItem<ImmutableList<char>, char>>
                (
                    StackItem<ImmutableList<char>, char>.GetTypeTraits
                    (
                        new ListTypeTraits<char>(CharTypeTraits.Value),
                        CharTypeTraits.Value
                    )
                );

            foreach (string regex in regexes)
            {
                Option<StackItem<ImmutableList<char>, char>> result = RegexParser.DefaultCombinatorTraits.TryParse
                (
                    regexParser,
                    RegexParser.DefaultStringInputSourceTraits.Create(regex)
                );

                System.Diagnostics.Debug.WriteLine(regex.Quoted() + " -> " + stackItemOptionTraits.ToDebugString(result));

                Assert.IsTrue(result.HasValue);
            }
        }

        [TestMethod]
        public void LexerTest()
        {
            var lexer = GenerateLexer();

            string input = "(a b c)";

            var (lexResult, nextState) = LexerGen.Lex(lexer, "main", _ => "main", input);

            Assert.AreEqual(7, lexResult.Count);
        }

        private ImmutableSortedDictionary<string, DFA<ImmutableList<char>, string>> GenerateLexer()
        {
            return LexerGen.GenerateLexer
            (
                ImmutableSortedDictionary<string, ImmutableList<LexerRule<string>>>.Empty.Add
                (
                    "main",
                    [
                        new RegexLexerRule<string>("[ |\\r|\\n|\\t|\\f|\\v]+", "WhiteSpace"),
                        new LiteralLexerRule<string>("(", "LParen"),
                        new RegexLexerRule<string>("-?(0|[1-9][0-9]*)", "Integer"),
                        new LiteralLexerRule<string>(")", "RParen"),
                        new RegexLexerRule<string>("[A-Z|a-z|_|$][A-Z|A-z|0-9|_|$]*", "Identifier")
                    ]
                ),
                StringTypeTraits.Value,
                StringTypeTraits.Value
            );
        }

    }
}
