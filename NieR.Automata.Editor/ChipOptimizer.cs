using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;

namespace NieR.Automata.Toolkit
{

    internal class ChipOptimizer
    {
        // Contains all the chips in use; an unordered multi-set for chips
        public class ChipSet : IEnumerable<Chip>
        {
            // Enumerator for the ChipSet enumeration
            public class ChipSet_Enumerator : IEnumerator<Chip>
            {
                private Dictionary<ChipCode, Stack<Chip>>.Enumerator _dictionaryEnumerator;
                private IEnumerator<Chip> _stackEnumerator;

                public object Current => _stackEnumerator.Current;
                Chip IEnumerator<Chip>.Current => _stackEnumerator.Current;

                public ChipSet_Enumerator(ChipSet set)
                {
                    _dictionaryEnumerator = set._set.GetEnumerator();
                    if (_dictionaryEnumerator.MoveNext())
                        _stackEnumerator = _dictionaryEnumerator.Current.Value.GetEnumerator();
                    else
                        _stackEnumerator = new Stack<Chip>().GetEnumerator();
                }

                public void Dispose()
                {
                    _dictionaryEnumerator.Dispose();
                    _stackEnumerator.Dispose();
                }

                public bool MoveNext()
                {
                    while (!_stackEnumerator.MoveNext())
                    {
                        if (!_dictionaryEnumerator.MoveNext())
                            return false;
                        _stackEnumerator.Dispose();
                        _stackEnumerator = _dictionaryEnumerator.Current.Value.GetEnumerator();
                    }
                    return true;
                }

                public void Reset()
                {
                    ((IEnumerator)_dictionaryEnumerator).Reset();
                    _stackEnumerator.Dispose();
                    _stackEnumerator = _dictionaryEnumerator.Current.Value.GetEnumerator();
                }
            }

            private Dictionary<ChipCode, Stack<Chip>> _set;
            public ChipSet() { _set = new Dictionary<ChipCode, Stack<Chip>>(); }

            public void Add(Chip chip)
            {
                ChipCode code = new ChipCode(chip);
                if (!_set.ContainsKey(code))
                    _set[code] = new Stack<Chip>();
                _set[code].Push(chip);
            }

            public Chip Pop(ChipCode code)
            {
                if (_set.TryGetValue(code, out var chips))
                {
                    if (chips.Count > 0)
                    {
                        var chip = chips.Pop();
                        if (chips.Count == 0) _set.Remove(code);
                        return chip;
                    }
                }
                return null;
            }

            public bool TryPop(ChipCode code, out Chip chip)
            {
                chip = Pop(code);
                return chip != null;
            }

            public Chip Peek(ChipCode code)
            {
                if (_set.TryGetValue(code, out var chips))
                    if (chips.Count > 0) return chips.Peek();
                return null;
            }

            public bool TryPeek(ChipCode code, out Chip chip)
            {
                chip = Peek(code);
                return chip != null;
            }

            public int GetCountOf(ChipCode code)
            {
                if (_set.TryGetValue(code, out var chips))
                    return chips.Count;
                return 0;
            }

            public IEnumerator<Chip> GetEnumerator()
            {
                return new ChipSet_Enumerator(this);
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator) new ChipSet_Enumerator(this);
            }
        }

        // Priority queue, because this version of c# doesn't have one and all the sorted collections suck
        // Note that this isn't optimal, it's just near-optimal for my specific use case
        public class PriorityQueue<TValue>
        {
            private List<TValue> _values;
            private IComparer<TValue> _comparer;

            public PriorityQueue() { _values = new List<TValue>(); _comparer = Comparer<TValue>.Default; }
            public PriorityQueue(IComparer<TValue> comparer) { _values = new List<TValue>(); _comparer = comparer; }

            public void Enqueue(TValue value)
            {
                int index;
                for (index = 0; index < _values.Count; index++)
                    if (_comparer.Compare(value, _values[index]) > 0) break;
                _values.Insert(index, value);
            }

            public TValue Dequeue()
            {
                if (_values.Count == 0) throw new InvalidOperationException("Cannot dequeue empty queue!");

                TValue value = _values.Last();
                _values.RemoveAt(_values.Count - 1);
                return value;
            }

            public bool TryDequeue(out TValue value)
            {
                if (_values.Count == 0)
                {
                    value = default(TValue);
                    return false;
                }
                else
                {
                    value = _values.Last();
                    _values.RemoveAt(_values.Count - 1);
                    return true;
                }
            }
        }

        // ChipCode refers to set of chip specs. Chip refers to an actual chip in the inventory
        public struct ChipCode : IComparable<ChipCode>
        {
            public int Type;
            public int Level;
            public int Weight;

            public String Name => Chip.Chips[Type].Name;

            public ChipCode(int type, int level, int weight)
            {
                this.Type = type;
                this.Level = level;
                this.Weight = weight;
            }

            public ChipCode(Chip chip)
            {
                Type = chip.Type;
                Level = chip.Level;
                Weight = chip.Weight;
            }

            public ChipCode Fuse(ChipCode alt)
            {
                return ChipCode.Fuse(this, alt);
            }

            public static ChipCode Fuse(ChipCode x, ChipCode y)
            {
                if (x.Type != y.Type) throw new ArgumentException("Cannot fuse chips of different types");
                if (x.Level != y.Level) throw new ArgumentException("Cannot fuse chips of different levels");
                if (x.Level >= 8) throw new ArgumentException("Chips cannot go above level 8");
                return new ChipCode(x.Type, x.Level + 1, (int)Math.Ceiling((x.Weight + y.Weight + x.Level) * .5));
            }

            public ChipCode GetDefuseLower()
            {
                if (Level <= 0) throw new ArgumentException("Cannot defuse Level 0 chips");
                return new ChipCode(Type, Level - 1, (int)Math.Floor(Weight - .5 * Math.Max(Level - 1, 1)));
            }

            public ChipCode GetDefuseUpper()
            {
                if (Level <= 0) throw new ArgumentException("Cannot defuse Level 0 chips");
                return new ChipCode(Type, Level - 1, (int)Math.Ceiling(Weight - .5 * Math.Max(Level - 1, 1)));
            }

            public ChipCode GetFuseComplement(ChipCode target)
            {
                if (Level == 8) throw new ArgumentException("Cannot fuse level 8 chips!");
                if ((Level + 1) != target.Level) throw new ArgumentException("Fusions must go up by exactly one level!");
                return new ChipCode(Type, Level, 2 * target.Weight - Weight - Math.Max(Level, 1));
            }

            public int CompareTo(ChipCode other)
            {
                if (Type.CompareTo(other.Type) != 0) return Type.CompareTo(other.Type);
                else if (Level.CompareTo(other.Level) != 0) return Level.CompareTo(other.Level);
                else if (Weight.CompareTo(other.Weight) != 0) return Weight.CompareTo(other.Weight);
                else return 0;
            }

            public override int GetHashCode()
            {
                // Create unique int based on our stats and hash that instead
                return (Type << 16 | Level << 8 | Weight).GetHashCode();
            }

            public override string ToString()
            {
                return String.Format("{0} +{1} [{2}]",
                    Name,
                    Level,
                    Weight
                    );
            }
        }

        // A "virtual" chip; could be an actual chip or a result of a pending fusion
        public class VirtualChip : IComparable<VirtualChip>
        {
            // Code containing the stats this chip represents
            public ChipCode Code { get; private set; }

            // A reference to an actual chip this virtual chip wraps. If null, this chip is a fusion
            private Chip _actual = null;
            public Chip Actual { get => _actual; private set => _actual = value; }

            // If there is no actual chip, this will be the lower chip in a fusion; said chip will have a complement
            private VirtualChip _fusion = null;
            public VirtualChip Fusion { get => _fusion; private set => _fusion = value; }

            // If this chip is part of a fusion, this is the chip it will fuse with
            private VirtualChip _complement = null;
            public VirtualChip Complement { get => _complement; private set => _complement = value; }

            // If this chip is an upper fusion, if the lower actually has a chip or not
            private bool _lowerHasActual = false;
            public bool LowerHasActual { get => _lowerHasActual; set => _lowerHasActual = value; }


            // Name of the chip this virtual chip represents
            public String Name => Chip.Chips[Code.Type].Name;

            // Type of the chip this virtual chip represents
            public NA_Int Type => Code.Type;

            // Level of the chip this virtual chip represents
            public NA_Int Level => Code.Level;

            // Weight of the chip this virtual chip represents
            public NA_Int Weight => Code.Weight;


            // True if this chip directly contains an actual chip
            public bool HasActual => Actual != null;

            // True if this chip contains a fusion
            public bool HasFusion => Fusion != null;

            // True if this chip is part of a fusion
            public bool IsFusion => Complement != null;

            // True if this chip is part of a fusion and is the upper chip
            public bool IsFusionLower => Complement != null;


            // True if there is no actual chips associated with this or its children
            public bool IsEmpty => !HasActual && (Fusion?.IsEmpty ?? true) && (Complement?.IsEmpty ?? true);

            // True if this chip either is a chip or can fuse into a chip
            public bool IsFullChip => HasActual || HasCompleteFusion;

            // True if this chip is a fusion and has children it can fuse together
            public bool HasCompleteFusion => Fusion?.CanBeFused ?? false;

            // True if this chip is part of a fusion and both it and its complement can fuse
            public bool CanBeFused => IsFullChip && (Complement?.IsFullChip ?? false);


            // Constructs this chip in order to wrap an actual chip
            public VirtualChip(Chip chip) { Code = new ChipCode(chip); Actual = chip; }

            // Constructs this chip to represent a fusion of some kind
            public VirtualChip(ChipCode code) { Code = code; }

            public int CompareTo(VirtualChip other)
            {
                return SortCode().CompareTo(other.SortCode());
            }

            public int SortCode()
            {
                // Sort order: Type ascending, level descending, weight ascending
                int code = ((Type & 0xFFFF) << 16) | ((8 - (Level & 0xFFFF)) << 8) | (Weight & 0xFFFF);

                // Sub-sort : Lower fusions are standard. Uppers get +1 if the lower has a chip, -1 otherwise
                return 3 * code + (IsFusionLower ? 1 : LowerHasActual ? 0 : 2);
            }

            public void Resolve(ref ChipSet chips, ref PriorityQueue<VirtualChip> queue)
            {
                // Check if we're already resolved
                Debug.Assert(!IsFullChip);

                // Try and claim an actual chip
                int i;
                if (IsFusionLower)
                    for (i = Chip.MinimumWeightForLevel[Level]; i <= Weight; i++)
                        if (chips.TryPop(new ChipCode(Type, Level, i), out _actual)) break; else { }
                else
                    for (i = Weight; i >= Chip.MinimumWeightForLevel[Level]; i--)
                        if (chips.TryPop(new ChipCode(Type, Level, i), out _actual)) break;

                // If we claimed an actual chip, update our complement (if it exists)
                if (HasActual)
                {
                    ChipCode newCode = new ChipCode(Actual);
                    if (IsFusionLower)
                        Complement.Code = newCode.GetFuseComplement(ChipCode.Fuse(Code, Complement.Code));
                    Code = newCode;
                }
                else if (Level != 0) // Can't create fusions for level 0 chips
                {
                    // We couldn't get an actual chip. So we create a fusion instead!
                    Fusion = new VirtualChip(Code.GetDefuseLower());
                    Fusion.Complement = new VirtualChip(Code.GetDefuseUpper());
                    queue.Enqueue(Fusion);
                }

                // If we have a complement, it can now be added to the queue
                if (IsFusionLower)
                {
                    Complement.LowerHasActual = HasActual;
                    queue.Enqueue(Complement);
                }
            }

            public void ReportFusions(ref List<VirtualChip> fusions)
            {
                if (HasFusion) Fusion.ReportFusions(ref fusions);
                if (IsFusionLower) Complement.ReportFusions(ref fusions);
                if (CanBeFused) fusions.Add(this);
            }

            public override string ToString()
            {
                return String.Format("{0} +{1} [{2}] | {3} | {4}", 
                    Name, 
                    Level, 
                    Weight, 
                    IsFusionLower ? "Is  Fusion" : "Not Fusion",
                    HasActual ? "Has Actual" : HasFusion ? "Has Fusion" : "Has None"
                    );
            }

            public string PrintTree()
            {
                List<string> tree = new List<string>();
                tree.Add("");
                int currentLine = 0;
                string header = "";
                PrintTree(ref tree, ref currentLine, ref header);

                string output = "";
                foreach (var s in tree) output += s + '\n';
                return output;
            }

            public void PrintTree(ref List<string> tree, ref int currentLine, ref string header)
            {
                tree[currentLine] += String.Format(
                    "[{0} {1}]",
                    HasActual ? 'A' : IsEmpty ? 'E' : CanBeFused ? 'F' : 'P',
                    ((int)Weight).ToString("00")
                );

                if (HasFusion)
                {
                    tree[currentLine] += ' ';
                    if (IsFusionLower)
                    {
                        string newHeader = header + "|      ";
                        Fusion.PrintTree(ref tree, ref currentLine, ref newHeader);
                    }
                    else
                    {
                        string newHeader = header + "       ";
                        Fusion.PrintTree(ref tree, ref currentLine, ref newHeader);
                    }
                }
                else if (HasActual)
                    tree[currentLine] += new String('-', 62 - tree[currentLine].Length);

                if (IsFusionLower)
                {
                    currentLine++;
                    tree.Add(header);
                    Complement.PrintTree(ref tree, ref currentLine, ref header);
                }
            }
        }
                
        readonly ChipCode[] DesiredChips =
        {
            new ChipCode(0x01, 8, 21), // Name = "Weapon Attack Up"
            new ChipCode(0x02, 8, 21), // Name = "Down-Attack Up"
            new ChipCode(0x03, 8, 21), // Name = "Critical Up"
            new ChipCode(0x04, 8, 21), // Name = "Ranged Attack Up"
            new ChipCode(0x05, 8, 21), // Name = "Fast Cooldown"
            new ChipCode(0x06, 8, 21), // Name = "Melee Defence Up"
            new ChipCode(0x07, 8, 21), // Name = "Ranged Defence Up"
            new ChipCode(0x08, 8, 21), // Name = "Anti Chain Damage"
            new ChipCode(0x09, 8, 21), // Name = "Max HP Up"
            new ChipCode(0x0A, 8, 21), // Name = "Offensive Heal"
            new ChipCode(0x0B, 8, 21), // Name = "Deadly Heal"
            new ChipCode(0x0C, 8, 21), // Name = "Auto-Heal"
            new ChipCode(0x0D, 8, 21), // Name = "Evade Range Up"
            new ChipCode(0x0E, 3, 7 ), // Name = "Moving Speed Up"
            new ChipCode(0x0E, 3, 7 ), // Name = "Moving Speed Up"
            new ChipCode(0x0F, 4, 9 ), // Name = "Drop Rate Up"
            new ChipCode(0x0F, 3, 7 ), // Name = "Drop Rate Up"
            new ChipCode(0x10, 8, 21), // Name = "EXP Gain Up"
            new ChipCode(0x11, 8, 21), // Name = "Shock Wave"
            new ChipCode(0x12, 8, 21), // Name = "Last Stand"
            new ChipCode(0x13, 8, 21), // Name = "Damage Absorb"
            new ChipCode(0x14, 8, 21), // Name = "Vengeance"
            new ChipCode(0x15, 8, 21), // Name = "Reset"
            new ChipCode(0x16, 8, 21), // Name = "Overclock"
            new ChipCode(0x17, 8, 21), // Name = "Resilience"
            new ChipCode(0x18, 8, 21), // Name = "Counter"
            new ChipCode(0x19, 8, 21), // Name = "Taunt Up"
            new ChipCode(0x1A, 8, 21), // Name = "Charge Attack"
            new ChipCode(0x1B, 8, 21), // Name = "Auto-use Item"
            new ChipCode(0x1D, 8, 21), // Name = "Hijack Boost"
            new ChipCode(0x1E, 8, 21), // Name = "Stun"
            new ChipCode(0x1F, 8, 21), // Name = "Combust"
            new ChipCode(0x22, 8, 21), // Name = "Heal Drops Up"
        };

        public VirtualChip[] EndChips;
        public List<VirtualChip> Fusions = new List<VirtualChip>();
        public List<Chip> SellChips = new List<Chip>();

        public ChipOptimizer() { }
        
        public void Load(in List<Chip> chips)
        {
            // Create our chipset from the input chips
            ChipSet chipSet = new ChipSet();
            foreach (Chip chip in chips) 
                if ((chip.Type != Chip.Empty.Type) && chip.HasLevels) chipSet.Add(chip);

            // Create our target chips
            PriorityQueue<VirtualChip> queue = new PriorityQueue<VirtualChip>(Comparer<VirtualChip>.Default);
            EndChips = new VirtualChip[DesiredChips.Length];
            for (int i = 0; i < DesiredChips.Length; i++)
            {
                EndChips[i] = new VirtualChip(DesiredChips[i]);
                queue.Enqueue(EndChips[i]);
            }

            // Evaluate all the chips!
            while (queue.TryDequeue(out var chip))
                chip.Resolve(ref chipSet, ref queue);

            // Second traversal gets all the fusions
            foreach (var chip in EndChips)
                chip.ReportFusions(ref Fusions);
            Fusions.Sort((x, y) => x.Code.CompareTo(y.Code));

            // Any unused chips are marked for sale
            foreach (Chip chip in chipSet)
                SellChips.Add(chip);
            SellChips.Sort((x, y) => new ChipCode(x).CompareTo(new ChipCode(y)));
        }
    }
}
