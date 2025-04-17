using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Sunlighter.LexerGenLib
{
    public class NFA_AddResult
    {
        private readonly int newStartState;
        private readonly int newAcceptState;

        public NFA_AddResult
        (
            int newStartState,
            int newAcceptState
        )
        {
            this.newStartState = newStartState;
            this.newAcceptState = newAcceptState;
        }

        public int NewStartState => newStartState;
        public int NewAcceptState => newAcceptState;
    }
    
    public sealed class NFA_AddResultLong : NFA_AddResult
    {
        private readonly ImmutableSortedDictionary<int, int> oldToNew;
        private readonly ImmutableSortedDictionary<int, int> newToOld;

        public NFA_AddResultLong
        (
            int newStartState,
            int newAcceptState,
            ImmutableSortedDictionary<int, int> oldToNew,
            ImmutableSortedDictionary<int, int> newToOld
        )
            : base(newStartState, newAcceptState)
        {
            
            this.oldToNew = oldToNew;
            this.newToOld = newToOld;
        }
        
        public ImmutableSortedDictionary<int, int> OldToNew => oldToNew;
        public ImmutableSortedDictionary<int, int> NewToOld => newToOld;
    }

    public sealed class NFABuilder<TSet, TAccept>
    {
        private int nextState;
        private ImmutableSortedDictionary<int, ImmutableList<(Option<TSet>, int)>> transitions;
        private ImmutableSortedDictionary<int, TAccept> acceptCodes;

        public NFABuilder()
        {
            nextState = 0;
            transitions = ImmutableSortedDictionary<int, ImmutableList<(Option<TSet>, int)>>.Empty;
            acceptCodes = ImmutableSortedDictionary<int, TAccept>.Empty;
        }

        public int AddState()
        {
            int state = nextState;
            ++nextState;
            return state;
        }

        public void AddTransition(int fromState, TSet on, int toState)
        {
            transitions = transitions.Add(fromState, (Option<TSet>.Some(on), toState));
        }

        public void AddEpsilonTransition(int fromState, int toState)
        {
            transitions = transitions.Add(fromState, (Option<TSet>.None, toState));
        }

        public void SetAcceptCode(int state, TAccept acceptCode)
        {
            acceptCodes = acceptCodes.SetItem(state, acceptCode);
        }

        public NFA<TSet, TAccept> ToNFA(int startState, Func<TSet, bool> isNonEmpty)
        {
            return new NFA<TSet, TAccept>(startState, transitions, acceptCodes, isNonEmpty);
        }
    }

    public sealed class NFA<TSet, TAccept>
    {
        private readonly int startState;
        private readonly ImmutableSortedDictionary<int, ImmutableList<(Option<TSet>, int)>> transitions;
        private readonly ImmutableSortedDictionary<int, TAccept> acceptCodes;

        private readonly Lazy<int> minState;
        private readonly Lazy<int> maxStatePlusOne;
        private readonly Lazy<ImmutableSortedSet<int>> allStates;
        private readonly Lazy<ImmutableSortedSet<int>> reachableStates;
        private readonly Lazy<ImmutableSortedSet<int>> deterministicStartState;

        private readonly StrongBox<ImmutableSortedDictionary<ImmutableSortedSet<int>, ImmutableSortedSet<int>>> epsilonClosures;

        public NFA
        (
            int startState,
            ImmutableSortedDictionary<int, ImmutableList<(Option<TSet>, int)>> transitions,
            ImmutableSortedDictionary<int, TAccept> acceptCodes,
            Func<TSet, bool> isNonEmpty
        )
        {
            this.startState = startState;
            this.transitions = transitions;
            this.acceptCodes = acceptCodes;

            minState = new Lazy<int>
            (
                () =>
                {
                    int result = this.startState;
                    if (!this.transitions.IsEmpty)
                    {
                        result = Math.Min(result, this.transitions.Keys.Min());
                        result = Math.Min(result, this.transitions.Values.SelectMany(t => t.Select(t2 => t2.Item2)).Min());
                    }
                    if (!this.acceptCodes.IsEmpty)
                    {
                        result = Math.Min(result, this.acceptCodes.Keys.Min());
                    }
                    return result;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );

            maxStatePlusOne = new Lazy<int>
            (
                () =>
                {
                    int result = this.startState;
                    if (!this.transitions.IsEmpty)
                    {
                        result = Math.Max(result, this.transitions.Keys.Max());
                        result = Math.Max(result, this.transitions.Values.SelectMany(t => t.Select(t2 => t2.Item2)).Max());
                    }
                    if (!this.acceptCodes.IsEmpty)
                    {
                        result = Math.Max(result, this.acceptCodes.Keys.Max());
                    }
                    return result + 1;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );

            allStates = new Lazy<ImmutableSortedSet<int>>
            (
                () =>
                {
                    ImmutableSortedSet<int> result = ImmutableSortedSet<int>.Empty.Add(startState).Union(acceptCodes.Keys);
                    foreach(KeyValuePair<int, ImmutableList<(Option<TSet>, int)>> kvp in transitions)
                    {
                        result = result.Add(kvp.Key);
                        foreach(var (on, toState) in kvp.Value)
                        {
                            result = result.Add(toState);
                        }
                    }
                    return result;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );

            bool allowsReach(Option<TSet> optionCharSet)
            {
                return (!optionCharSet.HasValue) || isNonEmpty(optionCharSet.Value);
            }

            reachableStates = new Lazy<ImmutableSortedSet<int>>
            (
                () =>
                {
                    return Utility.Closure
                    (
                        ImmutableSortedSet<int>.Empty.Add(startState),
                        s =>
                        {
                            if (transitions.TryGetValue(s, out var transitionsForState))
                            {
                                return transitionsForState.Where(t => allowsReach(t.Item1)).Select(t => t.Item2);
                            }
                            else
                            {
                                return [];
                            }
                        }
                    );
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );

            epsilonClosures = new StrongBox<ImmutableSortedDictionary<ImmutableSortedSet<int>, ImmutableSortedSet<int>>>
            (
                ImmutableSortedDictionary<ImmutableSortedSet<int>, ImmutableSortedSet<int>>.Empty.WithComparers(Utility.IntSetAdapter)
            );

            deterministicStartState = new Lazy<ImmutableSortedSet<int>>
            (
                () =>
                {
                    ImmutableSortedSet<int> result = EpsilonClosure([startState]);
                    return result;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );
        }

        public int StartState => startState;

        public ImmutableSortedDictionary<int, ImmutableList<(Option<TSet>, int)>> Transitions => transitions;

        public ImmutableSortedDictionary<int, TAccept> AcceptCodes => acceptCodes;

        public int MinState => minState.Value;

        public int MaxStatePlusOne => maxStatePlusOne.Value;

        public ImmutableSortedSet<int> AllStates => allStates.Value;

        public ImmutableSortedSet<int> ReachableStates => reachableStates.Value;

        public void ForEachTransition(Action<int, Option<TSet>, int> action)
        {
            foreach (KeyValuePair<int, ImmutableList<(Option<TSet>, int)>> kvp in transitions)
            {
                foreach (var (on, toState) in kvp.Value)
                {
                    action(kvp.Key, on, toState);
                }
            }
        }

        public void ForEachTransition(ImmutableSortedSet<int> stateSet, Action<int, Option<TSet>, int> action)
        {
            foreach (int state in stateSet)
            {
                if (transitions.TryGetValue(state, out var transitionsForState))
                {
                    foreach (var (on, toState) in transitionsForState)
                    {
                        action(state, on, toState);
                    }
                }
            }
        }

        private ImmutableSortedSet<int> EpsilonClosure_Internal(ImmutableSortedSet<int> stateSet)
        {
            return Utility.Closure
            (
                stateSet,
                s =>
                {
                    if (transitions.TryGetValue(s, out var transitionsForState))
                    {
                        return transitionsForState.Where(t => !t.Item1.HasValue).Select(t => t.Item2);
                    }
                    else
                    {
                        return [];
                    }
                }
            );
        }

        public ImmutableSortedSet<int> EpsilonClosure(ImmutableSortedSet<int> stateSet)
        {
            return Utility.WithCache
            (
                epsilonClosures,
                stateSet,
                EpsilonClosure_Internal
            );
        }

        public ImmutableSortedSet<int> DeterministicStartState => deterministicStartState.Value;

        public ImmutableList<(TSet, ImmutableSortedSet<int>)> DeterministicTransitions<TChar>
        (
            ICharSetTraits<TSet, TChar> charSetTraits,
            ImmutableSortedSet<int> stateSet
        )
        {
            CharSetSplitter<TSet, TChar> splitter = new CharSetSplitter<TSet, TChar>(charSetTraits);

            ImmutableSortedDictionary<ImmutableSortedSet<int>, TSet> resultsByDest =
                ImmutableSortedDictionary<ImmutableSortedSet<int>, TSet>.Empty.WithComparers(Utility.IntSetAdapter);

            ForEachTransition
            (
                stateSet,
                (fromState, onCharSetOption, toState) =>
                {
                    if (onCharSetOption.HasValue)
                    {
                        TSet onCharSet = onCharSetOption.Value;

                        splitter.Split(onCharSet);
                    }
                    else if (stateSet.Contains(toState))
                    {
                        // this is ok to ignore
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(false, "Epsilon transition to state not in set");
                    }
                }
            );

            foreach(TSet set in splitter.DistinctSets)
            {
                ImmutableSortedSet<int> destStates = [];

                ForEachTransition
                (
                    stateSet,
                    (fromState, onCharSetOption, toState) =>
                    {
                        if (onCharSetOption.HasValue)
                        {
                            TSet onCharSet = onCharSetOption.Value;
                            if (!charSetTraits.IsEmpty(charSetTraits.Intersection(onCharSet, set)))
                            {
                                destStates = destStates.Add(toState);
                            }
                        }
                    }
                );

                destStates = EpsilonClosure(destStates);

                resultsByDest = resultsByDest.SetItem
                (
                    destStates,
                    charSetTraits.Union(resultsByDest.GetValueOrDefault(destStates, charSetTraits.Empty), set)
                );
            }

            ImmutableList<(TSet, ImmutableSortedSet<int>)> results = resultsByDest.Select(kvp => (kvp.Value, kvp.Key)).ToImmutableList();

            return results;
        }

        public Option<TAccept> DeterministicAcceptCode(ImmutableSortedSet<int> stateSet, Func<TAccept, TAccept, TAccept> maxAcceptCode)
        {
            Option<TAccept> result = Option<TAccept>.None;
            foreach(int state in stateSet)
            {
                if (acceptCodes.TryGetValue(state, out var acceptCode))
                {
                    if (!result.HasValue)
                    {
                        result = Option<TAccept>.Some(acceptCode);
                    }
                    else
                    {
                        result = Option<TAccept>.Some(maxAcceptCode(result.Value, acceptCode));
                    }
                }
            }
            return result;
        }

        public DFAResult ToDFA<TChar>(DFABuilder<TSet, TChar, TAccept> dest, Func<TAccept, TAccept, TAccept> maxAcceptCode)
        {
            ImmutableSortedSet<ImmutableSortedSet<int>> toExplore =
                ImmutableSortedSet<ImmutableSortedSet<int>>.Empty.WithComparer(Utility.IntSetAdapter).Add(DeterministicStartState);

            ImmutableSortedDictionary<ImmutableSortedSet<int>, int> stateSetToDfaState =
                ImmutableSortedDictionary<ImmutableSortedSet<int>, int>.Empty.WithComparers(Utility.IntSetAdapter);

            ImmutableSortedDictionary<int, ImmutableSortedSet<int>> dfaStateToStateSet =
                ImmutableSortedDictionary<int, ImmutableSortedSet<int>>.Empty;

            ImmutableSortedDictionary<ImmutableSortedSet<int>, ImmutableList<Action<int>>> fixups =
                ImmutableSortedDictionary<ImmutableSortedSet<int>, ImmutableList<Action<int>>>.Empty.WithComparers(Utility.IntSetAdapter);

            void AddFixup(ImmutableSortedSet<int> dest, Action<int> fixup)
            {
                fixups = fixups.SetItem(dest, fixups.GetValueOrDefault(dest, []).Add(fixup));
            }

            while (!toExplore.IsEmpty)
            {
                ImmutableSortedSet<int> stateSet = toExplore[0];
                toExplore = toExplore.Remove(stateSet);

                if (!stateSetToDfaState.ContainsKey(stateSet))
                {
                    Option<TAccept> acceptCodeOpt = DeterministicAcceptCode(stateSet, maxAcceptCode);
                    int dfaState = dest.AddState(acceptCodeOpt);

                    stateSetToDfaState = stateSetToDfaState.Add(stateSet, dfaState);
                    dfaStateToStateSet = dfaStateToStateSet.Add(dfaState, stateSet);

                    if (fixups.TryGetValue(stateSet, out var fixupList))
                    {
                        foreach (var fixup in fixups.GetValueOrDefault(stateSet, []))
                        {
                            fixup(dfaState);
                        }

                        fixups = fixups.Remove(stateSet);
                    }

                    ImmutableList<(TSet, ImmutableSortedSet<int>)> dfaTransitions = DeterministicTransitions(dest.CharSetTraits, stateSet);

                    foreach (var (onCharSet, destStateSet) in dfaTransitions)
                    {
                        if (stateSetToDfaState.TryGetValue(destStateSet, out int destDfaState1))
                        {
                            dest.AddTransition(dfaState, onCharSet, destDfaState1);
                        }
                        else
                        {
                            toExplore = toExplore.Add(destStateSet);

                            AddFixup
                            (
                                destStateSet,
                                destDfaState =>
                                {
                                    dest.AddTransition(dfaState, onCharSet, destDfaState);
                                }
                            );
                        }
                    }
                }
            }

            if (!fixups.IsEmpty)
            {
                System.Diagnostics.Debug.Assert(false, "Unresolved fixups");

                foreach(KeyValuePair<ImmutableSortedSet<int>, ImmutableList<Action<int>>> kvp in fixups)
                {
                    if (stateSetToDfaState.TryGetValue(kvp.Key, out int dfaState))
                    {
                        foreach (var fixup in kvp.Value)
                        {
                            fixup(dfaState);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(false, "Unresolved fixups for NFA state");
                    }
                }
            }

            return new DFAResult
            (
                stateSetToDfaState[DeterministicStartState],
                stateSetToDfaState.GetValueOption([]),
                stateSetToDfaState,
                dfaStateToStateSet
            );
        }
    }

    public interface INFATraits<TNFAFinal, TNFA, TCharSet, TChar, TAccept>
    {
        public TNFA Nothing { get; }

        public TNFA EmptyString { get; }

        public TNFA CharFromSet(TCharSet charSet);

        public TNFA Sequence(TNFA nfa1, TNFA nfa2);

        public TNFA Sequence(ImmutableList<TNFA> nfaList);

        public TNFA Alternative(TNFA nfa1, TNFA nfa2);

        public TNFA Alternative(ImmutableList<TNFA> nfaList);

        public TNFA OptRep(TNFA nfa, bool optional, bool repeating);

        public TNFAFinal Finalize(ImmutableList<(TNFA, TAccept)> nfas);
    }

    public sealed class NFATraits<TChar, TAccept> :
        INFATraits<
            NFA<ImmutableList<TChar>, TAccept>,
            Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult>,
            ImmutableList<TChar>,
            TChar,
            TAccept>
    {
        private readonly ILexerCharTraits<TChar> charTraits;
        private readonly ICharSetTraits<ImmutableList<TChar>, TChar> rangeSetTraits;

        public NFATraits
        (
            ILexerCharTraits<TChar> charTraits,
            ICharSetTraits<ImmutableList<TChar>, TChar> rangeSetTraits
        )
        {
            this.charTraits = charTraits;
            this.rangeSetTraits = rangeSetTraits;

        }

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> Nothing => delegate (NFABuilder<ImmutableList<TChar>, TAccept> builder)
        {
            int startState = builder.AddState();
            int acceptState = builder.AddState();
            return new NFA_AddResult(startState, acceptState);
        };

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> EmptyString => delegate(NFABuilder<ImmutableList<TChar>, TAccept> builder)
        {
            int startState = builder.AddState();
            int acceptState = builder.AddState();
            builder.AddEpsilonTransition(startState, acceptState);
            return new NFA_AddResult(startState, acceptState);
        };

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> CharFromSet(ImmutableList<TChar> charSet)
        {
            return delegate(NFABuilder<ImmutableList<TChar>, TAccept> builder)
            {
                int startState = builder.AddState();
                int acceptState = builder.AddState();
                builder.AddTransition(startState, charSet, acceptState);
                return new NFA_AddResult(startState, acceptState);
            };
        }

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> Sequence
        (
            Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> nfa1,
            Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> nfa2
        )
        {
            return delegate (NFABuilder<ImmutableList<TChar>, TAccept> builder)
            {
                int startState = builder.AddState();
                var nfa1Result = nfa1(builder);
                var nfa2Result = nfa2(builder);
                int acceptState = builder.AddState();
                builder.AddEpsilonTransition(startState, nfa1Result.NewStartState);
                builder.AddEpsilonTransition(nfa1Result.NewAcceptState, nfa2Result.NewStartState);
                builder.AddEpsilonTransition(nfa2Result.NewAcceptState, acceptState);
                return new NFA_AddResult(startState, acceptState);
            };
        }

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> Sequence
        (
            ImmutableList<Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult>> nfaList
        )
        {
            if (nfaList.IsEmpty)
            {
                return EmptyString;
            }

            return delegate (NFABuilder<ImmutableList<TChar>, TAccept> builder)
            {
                int startState = builder.AddState();
                int nextStartState = startState;
                foreach(var nfa in nfaList)
                {
                    var nfaResult = nfa(builder);
                    builder.AddEpsilonTransition(nextStartState, nfaResult.NewStartState);
                    nextStartState = nfaResult.NewAcceptState;
                }
                int acceptState = builder.AddState();
                builder.AddEpsilonTransition(nextStartState, acceptState);
                return new NFA_AddResult(startState, acceptState);
            };
        }

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> Alternative
        (
            Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> nfa1,
            Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> nfa2
        )
        {
            return delegate (NFABuilder<ImmutableList<TChar>, TAccept> builder)
            {
                int startState = builder.AddState();
                var nfa1Result = nfa1(builder);
                var nfa2Result = nfa2(builder);
                int acceptState = builder.AddState();
                builder.AddEpsilonTransition(startState, nfa1Result.NewStartState);
                builder.AddEpsilonTransition(startState, nfa2Result.NewStartState);
                builder.AddEpsilonTransition(nfa1Result.NewAcceptState, acceptState);
                builder.AddEpsilonTransition(nfa2Result.NewAcceptState, acceptState);
                return new NFA_AddResult(startState, acceptState);
            };
        }

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> Alternative
        (
            ImmutableList<Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult>> nfaList
        )
        {
            if (nfaList.IsEmpty)
            {
                return Nothing;
            }

            return delegate (NFABuilder<ImmutableList<TChar>, TAccept> builder)
            {
                int startState = builder.AddState();
                ImmutableList<NFA_AddResult> nfaResults = nfaList.Select(nfa => nfa(builder)).ToImmutableList();
                int acceptState = builder.AddState();

                foreach(NFA_AddResult nfaResult in nfaResults)
                {
                    builder.AddEpsilonTransition(startState, nfaResult.NewStartState);
                    builder.AddEpsilonTransition(nfaResult.NewAcceptState, acceptState);
                }

                return new NFA_AddResult(startState, acceptState);
            };
        }

        public Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> OptRep
        (
            Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult> nfa,
            bool optional,
            bool repeating
        )
        {
            if (!optional && !repeating)
            {
                return nfa;
            }

            return delegate (NFABuilder<ImmutableList<TChar>, TAccept> builder)
            {
                int startState = builder.AddState();
                var nfaResult = nfa(builder);
                int acceptState = builder.AddState();
                builder.AddEpsilonTransition(startState, nfaResult.NewStartState);
                builder.AddEpsilonTransition(nfaResult.NewAcceptState, acceptState);
                if (optional)
                {
                    builder.AddEpsilonTransition(startState, acceptState);
                }
                if (repeating)
                {
                    builder.AddEpsilonTransition(acceptState, startState);
                }
                return new NFA_AddResult(startState, acceptState);
            };
        }

        public NFA<ImmutableList<TChar>, TAccept> Finalize(ImmutableList<(Func<NFABuilder<ImmutableList<TChar>, TAccept>, NFA_AddResult>, TAccept)> nfas)
        {
            NFABuilder<ImmutableList<TChar>, TAccept> builder = new NFABuilder<ImmutableList<TChar>, TAccept>();
            int startState = builder.AddState();
            foreach (var (nfa, acceptCode) in nfas)
            {
                NFA_AddResult result = nfa(builder);
                builder.AddEpsilonTransition(startState, result.NewStartState);
                builder.SetAcceptCode(result.NewAcceptState, acceptCode);
            }

            return builder.ToNFA(startState, rs => !rangeSetTraits.IsEmpty(rs));
        }
    }

    public sealed class DFAResult
    {
        private readonly int startState;
        private readonly Option<int> deadState;
        private readonly ImmutableSortedDictionary<ImmutableSortedSet<int>, int> stateSetToDfaState;
        private readonly ImmutableSortedDictionary<int, ImmutableSortedSet<int>> dfaStateToStateSet;

        public DFAResult
        (
            int startState,
            Option<int> deadState,
            ImmutableSortedDictionary<ImmutableSortedSet<int>, int> stateSetToDfaState,
            ImmutableSortedDictionary<int, ImmutableSortedSet<int>> dfaStateToStateSet
        )
        {
            this.startState = startState;
            this.deadState = deadState;
            this.stateSetToDfaState = stateSetToDfaState;
            this.dfaStateToStateSet = dfaStateToStateSet;
        }

        public int StartState => startState;

        public Option<int> DeadState => deadState;

        public ImmutableSortedDictionary<ImmutableSortedSet<int>, int> StateSetToDFAState => stateSetToDfaState;

        public ImmutableSortedDictionary<int, ImmutableSortedSet<int>> DFAStateToStateSet => dfaStateToStateSet;
    }

    public sealed class DFABuilder<TSet, TChar, TAccept>
    {
        private int nextState;
        private ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>> transitions;
        private ImmutableSortedDictionary<int, TAccept> acceptCodes;
        private readonly ICharSetTraits<TSet, TChar> charSetTraits;

        public DFABuilder(ICharSetTraits<TSet, TChar> charSetTraits)
        {
            nextState = 0;
            transitions = ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>>.Empty;
            acceptCodes = ImmutableSortedDictionary<int, TAccept>.Empty;
            this.charSetTraits = charSetTraits;
        }

        public ICharSetTraits<TSet, TChar> CharSetTraits => charSetTraits;

        public int AddState(Option<TAccept> acceptCode)
        {
            int state = nextState;
            ++nextState;
            if (acceptCode.HasValue)
            {
                acceptCodes = acceptCodes.Add(state, acceptCode.Value);
            }
            return state;
        }

        public void AddTransition(int fromState, TSet on, int toState)
        {
            ImmutableList<(TSet, int)> stateTransitions = transitions.GetValueOrDefault(fromState, []);
#if DEBUG
            foreach(var (on2, toState2) in stateTransitions)
            {
                System.Diagnostics.Debug.Assert(charSetTraits.IsEmpty(charSetTraits.Intersection(on2, on)), "Adding overlapping transition");
            }
#endif
            stateTransitions = stateTransitions.Add((on, toState));
            transitions = transitions.SetItem(fromState, stateTransitions);
        }

        public DFA<TSet, TAccept> ToDFA(int startState, Option<int> deadState)
        {
            return new DFA<TSet, TAccept>(startState, deadState, transitions, acceptCodes);
        }
    }

    public sealed class DFA<TSet, TAccept>
    {
        private readonly int startState;
        private readonly Option<int> deadState;
        private readonly ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>> transitions;
        private readonly ImmutableSortedDictionary<int, TAccept> acceptCodes;

        private readonly Lazy<ImmutableSortedSet<int>> allStates;

        public DFA
        (
            int startState,
            Option<int> deadState,
            ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>> transitions,
            ImmutableSortedDictionary<int, TAccept> acceptCodes
        )
        {
            this.startState = startState;
            this.deadState = deadState;
            this.transitions = transitions;
            this.acceptCodes = acceptCodes;

            allStates = new Lazy<ImmutableSortedSet<int>>
            (
                () => ImmutableSortedSet<int>.Empty.Add(startState).AddIfPresent(deadState).Union(transitions.Keys).Union(acceptCodes.Keys),
                LazyThreadSafetyMode.ExecutionAndPublication
            );
        }

        public int StartState => startState;

        public Option<int> DeadState => deadState;

        public ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>> Transitions => transitions;

        public ImmutableSortedDictionary<int, TAccept> AcceptCodes => acceptCodes;

        public ImmutableSortedSet<int> AllStates => allStates.Value;

        public void ForEachTransition(int state, Action<TSet, int> action)
        {
            if (transitions.TryGetValue(state, out var stateTransitions))
            {
                foreach (var (on, toState) in stateTransitions)
                {
                    action(on, toState);
                }
            }
        }

        public void ForEachTransition(ImmutableSortedSet<int> stateSet, Action<TSet, int> action)
        {
            foreach (int state in stateSet)
            {
                ForEachTransition(state, action);
            }
        }

        public ImmutableSortedSet<int> GetPossibleNextStates<TChar>(ICharSetTraits<TSet, TChar> charSetTraits, int state, TSet possibleInputs)
        {
            ImmutableSortedSet<int> results = ImmutableSortedSet<int>.Empty;
            ForEachTransition
            (
                state,
                (on, toState) =>
                {
                    if (!charSetTraits.IsEmpty(charSetTraits.Intersection(on, possibleInputs)))
                    {
                        results = results.Add(toState);
                    }
                }
            );
            return results;
        }

        public int GetNextState<TChar>(ICharSetTraits<TSet, TChar> charSetTraits, int state, TChar input)
        {
            Option<int> result = Option<int>.None;
            ForEachTransition
            (
                state,
                (on, toState) =>
                {
                    if (charSetTraits.Contains(on, input))
                    {
                        if (result.HasValue)
                        {
                            throw new InvalidOperationException("Multiple possible next states for DFA transition");
                        }
                        else
                        {
                            result = Option<int>.Some(toState);
                        }
                    }
                }
            );
            if (result.HasValue)
            {
                return result.Value;
            }
            else
            {
                throw new InvalidOperationException("Next state not found for DFA transition");
            }
        }

        public ImmutableSortedSet<int> GetPossibleNextStates<TChar>(ICharSetTraits<TSet, TChar> charSetTraits, ImmutableSortedSet<int> stateSet, TSet possibleInputs)
        {
            ImmutableSortedSet<int> results = ImmutableSortedSet<int>.Empty;
            ForEachTransition
            (
                stateSet,
                (on, toState) =>
                {
                    if (!charSetTraits.IsEmpty(charSetTraits.Intersection(on, possibleInputs)))
                    {
                        results = results.Add(toState);
                    }
                }
            );
            return results;
        }

        public bool AreStatesTriviallyDistinguishable(int a, int b, ITypeTraits<Option<TAccept>> acceptCodeOptTraits)
        {
            if (a == b) return false;

            Option<TAccept> aType = acceptCodes.GetValueOption(a);
            Option<TAccept> bType = acceptCodes.GetValueOption(b);

            return acceptCodeOptTraits.Compare(aType, bType) != 0;
        }

        public bool AreStatesDistinguishable<TChar>(ICharSetTraits<TSet, TChar> charSetTraits, int a, int b, Func<int, int, bool> prevDistinguishable)
        {
            if (a == b) return false;

            if (prevDistinguishable(a, b))
            {
                return true;
            }

            CharSetSplitter<TSet, TChar> splitter = new CharSetSplitter<TSet, TChar>(charSetTraits);

            ForEachTransition(a, (cs, _) => splitter.Split(cs));
            ForEachTransition(b, (cs, _) => splitter.Split(cs));

            foreach(TSet cs in splitter.DistinctSets)
            {
                int NextState(int state)
                {
                    ImmutableSortedSet<int> nextSet = GetPossibleNextStates(charSetTraits, state, cs);
                    System.Diagnostics.Debug.Assert(nextSet.Count == 1, "Multiple possible next states (unexpected) for DFA transition");
                    return nextSet[0];
                }

                int aNextState = NextState(a);
                int bNextState = NextState(b);

                if (prevDistinguishable(aNextState, bNextState))
                {
                    return true;
                }
            }


            return false;
        }

        public DFA<TSet, TAccept> Minimize<TChar>(ICharSetTraits<TSet, TChar> charSetTraits, ITypeTraits<TAccept> acceptTraits)
        {
            PartitionSetTraits<int> psTraits = new PartitionSetTraits<int>(Int32TypeTraits.Value);
            
            PartitionSetTraits<int>.PartitionSet BuildPartitionSet(Func<int, int, bool> isDistinguishable)
            {
                PartitionSetTraits<int>.PartitionSet ps = psTraits.CreatePartitionSet();

                foreach (int state in allStates.Value)
                {
                    ps = ps.Add(state, isDistinguishable);
                }

                return ps;
            }

            ITypeTraits<Option<TAccept>> acceptCodeOptTraits = new OptionTypeTraits<TAccept>(acceptTraits);

            PartitionSetTraits<int>.PartitionSet ps = BuildPartitionSet((i, j) => AreStatesTriviallyDistinguishable(i, j, acceptCodeOptTraits));

            Option<ImmutableSortedSet<ImmutableSortedSet<int>>> lastGroupsSetOpt =
                Option<ImmutableSortedSet<ImmutableSortedSet<int>>>.None;

            while(!lastGroupsSetOpt.HasValue || psTraits.GroupSetTraits.Compare(lastGroupsSetOpt.Value, ps.GroupsSet) != 0)
            {
                ImmutableSortedDictionary<int, int> psIndex = ps.Index;
                int PSLookup(int state)
                {
                    int index = psIndex[state];
                    System.Diagnostics.Debug.Assert(ps.Groups[index].Contains(state));
                    return index;
                }
                PartitionSetTraits<int>.PartitionSet ps2 = BuildPartitionSet((i, j) => AreStatesDistinguishable(charSetTraits, i, j, (i, j) => PSLookup(i) != PSLookup(j)));
                lastGroupsSetOpt = Option<ImmutableSortedSet<ImmutableSortedSet<int>>>.Some(ps.GroupsSet);
                ps = ps2;
            }

            System.Diagnostics.Debug.Assert(lastGroupsSetOpt.HasValue, "lastGroupsSetOpt should have value");
            ImmutableSortedSet<ImmutableSortedSet<int>> lastGroupsSet = lastGroupsSetOpt.Value;

            if (lastGroupsSet.All(s => s.Count == 1)) return this;

            // now build a new DFA

            DFABuilder<TSet, TChar, TAccept> dFABuilder = new DFABuilder<TSet, TChar, TAccept>(charSetTraits);

            ImmutableSortedDictionary<int, int> oldStateToNewState = ImmutableSortedDictionary<int, int>.Empty;

            foreach(ImmutableSortedSet<int> stateSet in lastGroupsSet)
            {
                int dfaState = dFABuilder.AddState(acceptCodes.GetValueOption(stateSet[0]));
                foreach(int oldState in stateSet)
                {
                    oldStateToNewState = oldStateToNewState.Add(oldState, dfaState);
                }
            }

            ITypeTraits<ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>>> thisTransitionTraits =
                new DictionaryTypeTraits<int, ImmutableList<(TSet, int)>>
                (
                    Int32TypeTraits.Value,
                    new ListTypeTraits<(TSet, int)>
                    (
                        new ValueTupleTypeTraits<TSet, int>
                        (
                            charSetTraits.SetTypeTraits, Int32TypeTraits.Value
                        )
                    )
                );

            string thisTransitions = thisTransitionTraits.ToDebugString(this.transitions);

            foreach (ImmutableSortedSet<int> stateSet in lastGroupsSet)
            {
                int dfaState = oldStateToNewState[stateSet[0]];

                CharSetSplitter<TSet, TChar> splitter = new CharSetSplitter<TSet, TChar>(charSetTraits);
                ForEachTransition
                (
                    stateSet,
                    (onCharSet, toState) =>
                    {
                        splitter.Split(onCharSet);
                    }
                );

                ImmutableSortedDictionary<int, TSet> transitions = ImmutableSortedDictionary<int, TSet>.Empty;

                foreach (TSet charSet in splitter.DistinctSets)
                {
                    ImmutableSortedSet<int> nextStateSet = GetPossibleNextStates(charSetTraits, stateSet, charSet);

                    ImmutableSortedSet<int> newStateSet = nextStateSet.Select(state => oldStateToNewState[state]).ToImmutableSortedSet();

                    System.Diagnostics.Debug.Assert(newStateSet.Count == 1, "Multiple possible next states");

                    int nextState = newStateSet[0];

                    transitions = transitions.SetItem(nextState, charSetTraits.Union(transitions.GetValueOrDefault(nextState, charSetTraits.Empty), charSet));
                }

                foreach (KeyValuePair<int, TSet> kvp in transitions)
                {
                    dFABuilder.AddTransition(dfaState, kvp.Value, kvp.Key);
                }
            }

            int Lookup(int oldState, Action error)
            {
                if (oldStateToNewState.TryGetValue(oldState, out int dfaState))
                {
                    return dfaState;
                }
                else
                {
                    error();
                    return -1;
                }
            }

            int dfaStartState = Lookup(startState, () => System.Diagnostics.Debug.Assert(false, "Start state not found in new DFA"));
            Option<int> dfaDeadState = deadState.Map(ds => Lookup(ds, () => System.Diagnostics.Debug.Assert(false, "Dead state not found in new DFA")));

            return dFABuilder.ToDFA(dfaStartState, dfaDeadState);
        }

        public static ITypeTraits<DFA<TSet, TAccept>> GetTypeTraits
        (
            ITypeTraits<TSet> charSetTypeTraits,
            ITypeTraits<TAccept> acceptCodeTypeTraits
        )
        {
            return new ConvertTypeTraits<DFA<TSet, TAccept>, ((int, Option<int>), (ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>>, ImmutableSortedDictionary<int, TAccept>))>
            (
                dfa => ((dfa.startState, dfa.deadState), (dfa.transitions, dfa.acceptCodes)),
                new ValueTupleTypeTraits<(int, Option<int>), (ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>>, ImmutableSortedDictionary<int, TAccept>)>
                (
                    new ValueTupleTypeTraits<int, Option<int>>
                    (
                        Int32TypeTraits.Value,
                        new OptionTypeTraits<int>(Int32TypeTraits.Value)
                    ),
                    new ValueTupleTypeTraits<ImmutableSortedDictionary<int, ImmutableList<(TSet, int)>>, ImmutableSortedDictionary<int, TAccept>>
                    (
                        new DictionaryTypeTraits<int, ImmutableList<(TSet, int)>>
                        (
                            Int32TypeTraits.Value,
                            new ListTypeTraits<(TSet, int)>
                            (
                                new ValueTupleTypeTraits<TSet, int>
                                (
                                    charSetTypeTraits, Int32TypeTraits.Value
                                )
                            )
                        ),
                        new DictionaryTypeTraits<int, TAccept>
                        (
                            Int32TypeTraits.Value,
                            acceptCodeTypeTraits
                        )
                    )
                ),
                tuple => new DFA<TSet, TAccept>(tuple.Item1.Item1, tuple.Item1.Item2, tuple.Item2.Item1, tuple.Item2.Item2)
            );
        }
    }

    public sealed class PartitionSetTraits<T>
        where T : notnull
    {
        private readonly ITypeTraits<T> traits;
        private readonly Adapter<T> adapter;
        private readonly ITypeTraits<ImmutableSortedSet<T>> setTraits;
        private readonly Adapter<ImmutableSortedSet<T>> setAdapter;
        private readonly ITypeTraits<ImmutableSortedSet<ImmutableSortedSet<T>>> setOfSetTraits;

        public class PartitionSet
        {
            private readonly PartitionSetTraits<T> parent;
            private readonly ImmutableList<ImmutableSortedSet<T>> groups;
            private readonly ImmutableSortedDictionary<T, int> index;
            private Option<ImmutableSortedSet<ImmutableSortedSet<T>>> groupsSetCache;

            public PartitionSet
            (
                PartitionSetTraits<T> parent,
                ImmutableList<ImmutableSortedSet<T>> groups,
                ImmutableSortedDictionary<T, int> index
            )
            {
                this.parent = parent;
                this.groups = groups;
                this.index = index;
                this.groupsSetCache = Option<ImmutableSortedSet<ImmutableSortedSet<T>>>.None;
            }

            public PartitionSet Add(T item, Func<T, T, bool> isDistinguishable)
            {
                if (index.ContainsKey(item)) return this;

                int groupNum = 0;
                while (groupNum < groups.Count)
                {
                    ImmutableSortedSet<T> group = groups[groupNum];
                    if (isDistinguishable(item, group[0]))
                    {
                        groupNum++;
                    }
                    else
                    {
                        ImmutableList<ImmutableSortedSet<T>> groups3 = groups.SetItem(groupNum, group.Add(item));
                        ImmutableSortedDictionary<T, int> index3 = index.SetItem(item, groupNum);
                        return new PartitionSet(parent, groups3, index3);
                    }
                }

                ImmutableList<ImmutableSortedSet<T>> groups2 = groups.Add(ImmutableSortedSet<T>.Empty.WithComparer(parent.adapter).Add(item));
                ImmutableSortedDictionary<T, int> index2 = index.SetItem(item, groupNum);
                return new PartitionSet(parent, groups2, index2);
            }

            public ImmutableList<ImmutableSortedSet<T>> Groups => groups;

            public ImmutableSortedSet<ImmutableSortedSet<T>> GroupsSet
            {
                get
                {
                    if (groupsSetCache.HasValue)
                    {
                        return groupsSetCache.Value;
                    }
                    else
                    {
                        ImmutableSortedSet<ImmutableSortedSet<T>> gs = groups.ToImmutableSortedSet(parent.setAdapter);
                        groupsSetCache = Option<ImmutableSortedSet<ImmutableSortedSet<T>>>.Some(gs);
                        return groupsSetCache.Value;
                    }
                }
            }

            public ImmutableSortedDictionary<T, int> Index => index;
        }


        public PartitionSetTraits(ITypeTraits<T> traits)
        {
            this.traits = traits;
            this.adapter = Adapter<T>.Create(traits);
            setTraits = new SetTypeTraits<T>(ImmutableSortedSet<T>.Empty.WithComparer(adapter), traits);
            setAdapter = Adapter<ImmutableSortedSet<T>>.Create(setTraits);
            setOfSetTraits = new SetTypeTraits<ImmutableSortedSet<T>>(ImmutableSortedSet<ImmutableSortedSet<T>>.Empty.WithComparer(setAdapter), setTraits);
        }

        public PartitionSet CreatePartitionSet()
        {
            return new PartitionSet(this, [], ImmutableSortedDictionary<T, int>.Empty.WithComparers(adapter));
        }

        public Adapter<ImmutableSortedSet<T>> SetAdapter => setAdapter;

        public ITypeTraits<ImmutableSortedSet<ImmutableSortedSet<T>>> GroupSetTraits => setOfSetTraits;
    }

    public static partial class Extensions
    {
        public static ImmutableSortedDictionary<K, ImmutableList<V>> Add<K, V>
        (
            this ImmutableSortedDictionary<K, ImmutableList<V>> dict,
            K key,
            V value
        )
            where K : notnull
        {
            return dict.SetItem(key, dict.GetValueOrDefault(key, []).Add(value));
        }

        public static Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult> ExactString<TAccept>
        (
            this INFATraits<NFA<ImmutableList<char>, TAccept>, Func<NFABuilder<ImmutableList<char>, TAccept>, NFA_AddResult>, ImmutableList<char>, char, TAccept> nfaTraits,
            ICharSetTraits<ImmutableList<char>, char> rangeSetTraits,
            string str
        )
        {
            return nfaTraits.Sequence([.. str.Select(c => nfaTraits.CharFromSet(rangeSetTraits.Only(c)))]);
        }

        public static Option<V> GetValueOption<K, V>(this ImmutableSortedDictionary<K, V> dict, K key)
            where K : notnull
        {
            if (dict.TryGetValue(key, out var value))
            {
                return Option<V>.Some(value);
            }
            else
            {
                return Option<V>.None;
            }
        }

        public static ImmutableSortedSet<T> AddIfPresent<T>(this ImmutableSortedSet<T> set, Option<T> opt)
        {
            if (opt.HasValue)
            {
                return set.Add(opt.Value);
            }
            else
            {
                return set;
            }
        }

        public static Option<(int, TAccept)> TryMatchPrefix<TAccept>(this DFA<ImmutableList<char>, TAccept> dfa, string input)
        {
            int state = dfa.StartState;
            int pos = 0;

            ImmutableListRangeSetTraits<char> charSetTraits = new ImmutableListRangeSetTraits<char>(LexerCharTraits.Value);

            Option<(int, TAccept)> longestMatch = Option<(int, TAccept)>.None;

            while (pos < input.Length)
            {
                char c = input[pos];
                int nextState = dfa.GetNextState(charSetTraits, state, c);
                ++pos;
                if (dfa.AcceptCodes.TryGetValue(nextState, out TAccept? acceptCode))
                {
                    longestMatch = Option<(int, TAccept)>.Some((pos, acceptCode));
                }
                else if (dfa.DeadState.HasValue && dfa.DeadState.Value == nextState)
                {
                    return longestMatch;
                }
                else
                {
                    state = nextState;
                }
            }

            return longestMatch;
        }
    }

    internal static partial class Utility
    {
        internal static V WithCache<K, V>(StrongBox<ImmutableSortedDictionary<K, V>> cacheBox, K key, Func<K, V> calculateValue)
            where K : notnull
        {
            if (cacheBox.Value is null)
            {
                throw new NullReferenceException($"Argument {nameof(cacheBox)} has null value");
            }

            if (cacheBox.Value.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                value = calculateValue(key);
                cacheBox.Value = cacheBox.Value.Add(key, value);
                return value;
            }
        }

        internal static ImmutableSortedSet<T> Closure<T>(this ImmutableSortedSet<T> items, Func<T, IEnumerable<T>> getChildren)
        {
            ImmutableSortedSet<T> results = items.Clear();
            ImmutableList<T> toExamine = [.. items];

            while (!toExamine.IsEmpty)
            {
                T item = toExamine[0];
                toExamine = toExamine.RemoveAt(0);
                if (!results.Contains(item))
                {
                    results = results.Add(item);
                    toExamine = toExamine.AddRange(getChildren(item));
                }
            }

            return results;
        }

        private static readonly Lazy<ITypeTraits<ImmutableSortedSet<int>>> intSetTypeTraits =
            new Lazy<ITypeTraits<ImmutableSortedSet<int>>>
            (
                () => new SetTypeTraits<int>(Int32TypeTraits.Value),
                LazyThreadSafetyMode.ExecutionAndPublication
            );

        internal static ITypeTraits<ImmutableSortedSet<int>> IntSetTypeTraits => intSetTypeTraits.Value;

        private static readonly Lazy<Adapter<ImmutableSortedSet<int>>> intSetAdapter =
            new Lazy<Adapter<ImmutableSortedSet<int>>>
            (
                () => Adapter<ImmutableSortedSet<int>>.Create(intSetTypeTraits.Value),
                LazyThreadSafetyMode.ExecutionAndPublication
            );

        internal static Adapter<ImmutableSortedSet<int>> IntSetAdapter => intSetAdapter.Value;
    }
}
