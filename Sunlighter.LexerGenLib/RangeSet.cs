using Sunlighter.TypeTraitsLib;
using System.Collections.Immutable;

namespace Sunlighter.LexerGenLib
{
    public interface ILexerCharTraits<TChar>
    {
        TChar First { get; }
        bool HasNext(TChar value);
        TChar Next(TChar value);

        ITypeTraits<TChar> TypeTraits { get; }
    }

    public sealed class LexerCharTraits : ILexerCharTraits<char>
    {
        private static readonly LexerCharTraits value = new LexerCharTraits();
        private LexerCharTraits() { }
        public static LexerCharTraits Value => value;

        public char First => char.MinValue;
        public bool HasNext(char value) => value < char.MaxValue;
        public char Next(char value) => (char)(value + 1);

        public ITypeTraits<char> TypeTraits => CharTypeTraits.Value;
    }

    public interface ICharSetTraits<TSet, TChar>
    {
        TSet Empty { get; }

        TSet Full { get; }

        bool IsEmpty(TSet a);

        TSet Only(TChar item);

        TSet AllExcept(TChar item);

        TSet LessThan(TChar item);

        TSet GreaterThan(TChar item);

        TSet LessEqual(TChar item);

        TSet GreaterEqual(TChar item);

        TSet Complement(TSet a);

        TSet Union(TSet a, TSet b);

        TSet Intersection(TSet a, TSet b);

        TSet Difference(TSet minuend, TSet subtrahend);

        TSet SymmetricDifference(TSet a, TSet b);

        TSet DontCare(TSet a, TSet itemsNotCaredAbout);

        bool Contains(TSet a, TChar ch);

        ITypeTraits<TChar> CharTypeTraits { get; }

        ITypeTraits<TSet> SetTypeTraits { get; }
    }

    public sealed class ImmutableListRangeSetTraits<TChar> : ICharSetTraits<ImmutableList<TChar>, TChar>
    {
        private readonly ILexerCharTraits<TChar> charTraits;
        private readonly ImmutableList<TChar> full;
        private readonly ITypeTraits<ImmutableList<TChar>> setTypeTraits;

        public ImmutableListRangeSetTraits(ILexerCharTraits<TChar> charTraits)
        {
            this.charTraits = charTraits;

            full = ImmutableList<TChar>.Empty.Add(charTraits.First);
            setTypeTraits = new ListTypeTraits<TChar>(charTraits.TypeTraits);
        }

        public ImmutableList<TChar> Empty => ImmutableList<TChar>.Empty;

        public ImmutableList<TChar> Full => full;

        public bool IsEmpty(ImmutableList<TChar> set) => set.IsEmpty;

        public ImmutableList<TChar> Only(TChar item)
        {
            return ImmutableList<TChar>.Empty.Add(item).AddIf(charTraits.HasNext(item), () => charTraits.Next(item));
        }

        public ImmutableList<TChar> AllExcept(TChar item)
        {
            if (charTraits.TypeTraits.Compare(item, charTraits.First) == 0)
            {
                if (charTraits.HasNext(item))
                {
                    return ImmutableList<TChar>.Empty.Add(charTraits.Next(item));
                }
                else
                {
                    // this is a degenerate case where there is only one character in the character set
                    return ImmutableList<TChar>.Empty;
                }
            }
            else
            {
                return ImmutableList<TChar>.Empty.Add(charTraits.First).Add(item).AddIf(charTraits.HasNext(item), () => charTraits.Next(item));
            }
        }

        public ImmutableList<TChar> LessThan(TChar item)
        {
            if (charTraits.TypeTraits.Compare(item, charTraits.First) == 0)
            {
                // nothing is less than the first character
                return ImmutableList<TChar>.Empty;
            }
            else
            {
                return ImmutableList<TChar>.Empty.Add(charTraits.First).Add(item);
            }
        }

        public ImmutableList<TChar> GreaterThan(TChar item)
        {
            if (charTraits.HasNext(item))
            {
                return ImmutableList<TChar>.Empty.Add(charTraits.Next(item));
            }
            else
            {
                // nothing is greater than the last character
                return ImmutableList<TChar>.Empty;
            }
        }

        public ImmutableList<TChar> LessEqual(TChar item)
        {
            if (charTraits.HasNext(item))
            {
                return ImmutableList<TChar>.Empty.Add(charTraits.First).Add(charTraits.Next(item));
            }
            else
            {
                // everything is less than or equal to the last character
                return ImmutableList<TChar>.Empty.Add(charTraits.First);
            }
        }

        public ImmutableList<TChar> GreaterEqual(TChar item)
        {
            return ImmutableList<TChar>.Empty.Add(item);
        }

        public ImmutableList<TChar> Complement(ImmutableList<TChar> a)
        {
            if (a.StartsWith(charTraits.First, charTraits.TypeTraits))
            {
                return a.RemoveAt(0);
            }
            else
            {
                return a.Insert(0, charTraits.First);
            }
        }

        public ImmutableList<TChar> Union(ImmutableList<TChar> a, ImmutableList<TChar> b)
        {
            return Utility.LogicMerge(charTraits.TypeTraits, a, b, (leftLogic, rightLogic) => (leftLogic || rightLogic));
        }

        public ImmutableList<TChar> Intersection(ImmutableList<TChar> a, ImmutableList<TChar> b)
        {
            return Utility.LogicMerge(charTraits.TypeTraits, a, b, (leftLogic, rightLogic) => (leftLogic && rightLogic));
        }

        public ImmutableList<TChar> Difference(ImmutableList<TChar> minuend, ImmutableList<TChar> subtrahend)
        {
            return Utility.LogicMerge(charTraits.TypeTraits, minuend, subtrahend, (leftLogic, rightLogic) => (leftLogic && !rightLogic));
        }

        public ImmutableList<TChar> SymmetricDifference(ImmutableList<TChar> a, ImmutableList<TChar> b)
        {
            return Utility.LogicMerge(charTraits.TypeTraits, a, b, (leftLogic, rightLogic) => (leftLogic != rightLogic));
        }

        public ImmutableList<TChar> DontCare(ImmutableList<TChar> a, ImmutableList<TChar> itemsNotCaredAbout)
        {
            return Utility.DontCareMerge(charTraits.TypeTraits, a, itemsNotCaredAbout);
        }

        public bool Contains(ImmutableList<TChar> a, TChar ch)
        {
            int pos = Utility.LowerBound(charTraits.TypeTraits, ch, 0, a.Count, i => a[i]);

            //System.Diagnostics.Debug.WriteLine($"({string.Join(",", a.Select(d => "" + d))}), ch = {ch}, pos = {pos}");

            return ((pos & 1) != 0);
        }

        public ITypeTraits<TChar> CharTypeTraits => charTraits.TypeTraits;

        public ITypeTraits<ImmutableList<TChar>> SetTypeTraits => setTypeTraits;
    }

    public class CharSetSplitter<TSet, TChar>
    {
        private readonly ICharSetTraits<TSet, TChar> charSetTraits;
        private ImmutableList<TSet> distinctSets;

        public CharSetSplitter(ICharSetTraits<TSet, TChar> charSetTraits)
        {
            this.charSetTraits = charSetTraits;
            this.distinctSets = ImmutableList<TSet>.Empty.Add(charSetTraits.Full);
        }

        public void Split(TSet set)
        {
            ImmutableList<TSet> distinctSets2 = ImmutableList<TSet>.Empty;

            void AddIfNotEmpty(TSet set2)
            {
                if (!charSetTraits.IsEmpty(set2))
                {
                    distinctSets2 = distinctSets2.Add(set2);
                }
            }

            foreach (TSet set1 in distinctSets)
            {
                AddIfNotEmpty(charSetTraits.Intersection(set1, set));
                AddIfNotEmpty(charSetTraits.Intersection(set1, charSetTraits.Complement(set)));
            }

            distinctSets = distinctSets2;
        }

        public ImmutableList<TSet> DistinctSets => distinctSets;
    }

    public static partial class Extensions
    {
        public static bool StartsWith<T>(this ImmutableList<T> list, T value, ITypeTraits<T> typeTraits)
        {
            if (list.Count == 0)
                return false;
            return typeTraits.Compare(list[0], value) == 0;
        }

        public static ImmutableList<T> AddIf<T>(this ImmutableList<T> list, bool condition, Func<T> createItem)
        {
            if (condition)
            {
                return list.Add(createItem());
            }
            else
            {
                return list;
            }
        }

        public static TSet AnyOf<TSet, TChar>(this ICharSetTraits<TSet, TChar> charSetTraits, ImmutableList<TChar> desired)
        {
            if (desired.IsEmpty)
            {
                return charSetTraits.Empty;
            }
            else
            {
                TSet result = charSetTraits.Only(desired[0]);
                for (int i = 1; i < desired.Count; i++)
                {
                    result = charSetTraits.Union(result, charSetTraits.Only(desired[i]));
                }
                return result;
            }
        }

        public static TSet AnyExcept<TSet, TChar>(this ICharSetTraits<TSet, TChar> charSetTraits, ImmutableList<TChar> desired)
        {
            if (desired.IsEmpty)
            {
                return charSetTraits.Full;
            }
            else
            {
                TSet result = charSetTraits.AllExcept(desired[0]);
                for (int i = 1; i < desired.Count; i++)
                {
                    result = charSetTraits.Intersection(result, charSetTraits.AllExcept(desired[i]));
                }
                return result;
            }
        }
    }

    public static partial class Extensions
    {
        public static TSet Union<TSet, TChar>(this ICharSetTraits<TSet, TChar> charSetTraits, ImmutableList<TSet> sets)
        {
            if (sets.IsEmpty)
            {
                return charSetTraits.Empty;
            }
            else
            {
                TSet result = sets[0];
                for (int i = 1; i < sets.Count; i++)
                {
                    result = charSetTraits.Union(result, sets[i]);
                }
                return result;
            }
        }

        public static TSet Intersection<TSet, TChar>(this ICharSetTraits<TSet, TChar> charSetTraits, ImmutableList<TSet> sets)
        {
            if (sets.IsEmpty)
            {
                return charSetTraits.Full;
            }
            else
            {
                TSet result = sets[0];
                for (int i = 1; i < sets.Count; i++)
                {
                    result = charSetTraits.Intersection(result, sets[i]);
                }
                return result;
            }
        }
    }

    internal static partial class Utility
    {
        internal static void MergeIterate<T>
        (
            ITypeTraits<T> traits,
            ImmutableList<T> left, ImmutableList<T> right,
            Action<T> doLeft, Action<T> doRight, Action<T> doBoth
        )
        {
            void popLeft()
            {
                doLeft(left[0]);
                left = left.RemoveAt(0);
            }

            void popRight()
            {
                doRight(right[0]);
                right = right.RemoveAt(0);
            }

            void popBoth()
            {
                doBoth(left[0]);
                left = left.RemoveAt(0);
                right = right.RemoveAt(0);
            }

            while (true)
            {
                if (left.IsEmpty && right.IsEmpty)
                {
                    return;
                }
                else if (left.IsEmpty)
                {
                    popRight();
                }
                else if (right.IsEmpty)
                {
                    popLeft();
                }
                else
                {
                    int i = traits.Compare(left[0], right[0]);
                    if (i < 0)
                    {
                        popLeft();
                    }
                    else if (i > 0)
                    {
                        popRight();
                    }
                    else
                    {
                        popBoth();
                    }
                }
            }
        }

        internal static ImmutableList<T> LogicMerge<T>
        (
            ITypeTraits<T> traits,
            ImmutableList<T> left, ImmutableList<T> right,
            Func<bool, bool, bool> logicFunc
        )
        {
            ImmutableList<T>.Builder result = ImmutableList<T>.Empty.ToBuilder();
            bool leftLogic = false;
            bool rightLogic = false;
            bool logicState = false;

            MergeIterate
            (
                traits, left, right,
                leftItem =>
                {
                    leftLogic = !leftLogic;
                    bool newLogicState = logicFunc(leftLogic, rightLogic);
                    if (newLogicState != logicState)
                    {
                        result.Add(leftItem);
                        logicState = newLogicState;
                    }
                },
                rightItem =>
                {
                    rightLogic = !rightLogic;
                    bool newLogicState = logicFunc(leftLogic, rightLogic);
                    if (newLogicState != logicState)
                    {
                        result.Add(rightItem);
                        logicState = newLogicState;
                    }
                },
                bothItem =>
                {
                    leftLogic = !leftLogic;
                    rightLogic = !rightLogic;
                    bool newLogicState = logicFunc(leftLogic, rightLogic);
                    if (newLogicState != logicState)
                    {
                        result.Add(bothItem);
                        logicState = newLogicState;
                    }
                }
            );
            return result.ToImmutable();
        }

        internal static ImmutableList<T> DontCareMerge<T>
        (
            ITypeTraits<T> traits,
            ImmutableList<T> left, ImmutableList<T> right
        )
        {
            ImmutableList<T>.Builder result = ImmutableList<T>.Empty.ToBuilder();
            bool leftLogic = false;
            bool resultLogic = false;
            bool weCare = true;

            MergeIterate
            (
                traits, left, right,
                leftItem =>
                {
                    leftLogic = !leftLogic;
                    if (weCare)
                    {
                        result.Add(leftItem);
                        resultLogic = !resultLogic;
                    }
                },
                rightItem =>
                {
                    weCare = !weCare;
                    if (weCare && resultLogic != leftLogic)
                    {
                        result.Add(rightItem);
                        resultLogic = !resultLogic;
                    }
                },
                bothItem =>
                {
                    leftLogic = !leftLogic;
                    weCare = !weCare;
                    if (weCare && resultLogic != leftLogic)
                    {
                        result.Add(bothItem);
                        resultLogic = !resultLogic;
                    }
                }
            );

            return result.ToImmutable();
        }

        internal static int LowerBound<T>(ITypeTraits<T> traits, T target, int first, int count, Func<int, T> get)
        {
            while (count > 0)
            {
                int step = count / 2;
                int pos = first + step;
                if (traits.Compare(get(pos), target) <= 0)
                {
                    first = pos + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }
            return first;
        }
    }
}
