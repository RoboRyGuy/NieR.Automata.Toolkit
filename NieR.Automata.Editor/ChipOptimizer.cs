using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Xml.Linq;

namespace NieR.Automata.Toolkit
{

    internal class ChipOptimizer
    {
        // Contains all the chips in use; an unordered multi-set for chips
        public class ChipSet : IEnumerable<VirtualChip>
        {
            // Enumerator for the ChipSet enumeration
            public class ChipSet_Enumerator : IEnumerator<VirtualChip>
            {
                private Dictionary<ChipCode, Stack<VirtualChip>>.Enumerator _dictionaryEnumerator;
                private IEnumerator<VirtualChip> _stackEnumerator;

                public object Current => _stackEnumerator.Current;
                VirtualChip IEnumerator<VirtualChip>.Current => _stackEnumerator.Current;

                public ChipSet_Enumerator(ChipSet set)
                {
                    _dictionaryEnumerator = set._set.GetEnumerator();
                    if (_dictionaryEnumerator.MoveNext())
                        _stackEnumerator = _dictionaryEnumerator.Current.Value.GetEnumerator();
                    else
                        _stackEnumerator = new Stack<VirtualChip>().GetEnumerator();
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


            private Dictionary<ChipCode, Stack<VirtualChip>> _set;
            public ChipSet() { _set = new Dictionary<ChipCode, Stack<VirtualChip>>(); }

            public void Add(VirtualChip chip)
            {
                if (!_set.ContainsKey(chip.Code))
                    _set[chip.Code] = new Stack<VirtualChip>();
                _set[chip.Code].Push(chip);
            }

            public VirtualChip Pop(ChipCode code)
            {
                if (_set.TryGetValue(code, out var chips))
                    if (chips.Count > 0) return chips.Pop();
                return null;
            }

            public VirtualChip Peek(ChipCode code)
            {
                if (_set.TryGetValue(code, out var chips))
                    if (chips.Count > 0) return chips.Peek();
                return null;
            }

            public IEnumerator<VirtualChip> GetEnumerator()
            {
                return new ChipSet_Enumerator(this);
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator) new ChipSet_Enumerator(this);
            }
        }

        // ChipCode refers to set of chip specs. Chip refers to an actual chip in the inventory
        public struct ChipCode
        {
            public int Type;
            public int Level;
            public int Weight;

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
                return new ChipCode(Type, Level - 1, (int)Math.Floor(Weight - .5 * (Level - 1)));
            }

            public ChipCode GetDefuseHigher()
            {
                if (Level <= 0) throw new ArgumentException("Cannot defuse Level 0 chips");
                return new ChipCode(Type, Level - 1, (int)Math.Ceiling(Weight - .5 * (Level - 1)));
            }

            public ChipCode GetFuseComplement(ChipCode target)
            {
                if (Level == 8) throw new ArgumentException("Cannot fuse level 8 chips!");
                if ((Level + 1) != target.Level) throw new ArgumentException("Fusions must go up by exactly one level!");
                return new ChipCode(Type, Level, 2 * target.Weight - Weight - Level);
            }

            public override bool Equals(object o)
            {
                if (o is not ChipCode) return false;
                else
                {
                    ChipCode other = (ChipCode)o;
                    return Type == other.Type && Level == other.Level && Weight == other.Weight;
                }
            }

            public override int GetHashCode()
            {
                // Create unique int based on our stats and hash that instead
                return (Type << 16 | Level << 8 | Weight).GetHashCode();
            }

            public static bool operator ==(ChipCode x, ChipCode y)
            {
                return x.Type == y.Type && x.Level == y.Level && x.Weight == y.Weight;
            }

            public static bool operator !=(ChipCode x, ChipCode y)
            {
                return x.Type != y.Type || x.Level != y.Level || x.Weight != y.Weight;
            }

            public static bool operator <(ChipCode x, ChipCode y)
            {
                if (x.Type == y.Type)
                    if (x.Level == y.Level) return x.Weight < y.Weight;
                    else return x.Level < y.Level;
                else return x.Type < y.Type;
            }

            public static bool operator >(ChipCode x, ChipCode y)
            {
                if (x.Type == y.Type)
                    if (x.Level == y.Level) return x.Weight > y.Weight;
                    else return x.Level > y.Level;
                else return x.Type > y.Type;
            }
        }

        // Simple struct to associate two chips prior to a fusion
        public class ChipFusion
        {
            public VirtualChip Lower;
            public VirtualChip Upper;

            private ChipFusion() { Lower = null; Upper = null; }
            private ChipFusion(VirtualChip lower, VirtualChip upper)
            {
                Lower = lower;
                Upper = upper;
            }

            public bool IsComplete()
            {
                if (Lower == null) return false;
                if (!(Lower.Fusion?.IsComplete() ?? true)) return false;

                if (Upper == null) return false;
                if (!(Upper.Fusion?.IsComplete() ?? true)) return false;

                return true;
            }

            public bool IsPartial() { return Lower != null ^ Upper != null; }
            public bool IsEmpty() { return Lower == null && Upper == null; }

            public static ChipFusion Make(ChipCode code, ref ChipSet set)
            {
                // Can't make a fusion for a level 0
                if (code.Level <= 0) return null;

                ChipFusion fusion = new ChipFusion();

                ChipCode highestLower = code.GetDefuseLower();
                fusion.Lower = VirtualChip.AcquireLowest(highestLower, ref set);

                ChipCode complement = fusion.Lower?.Code.GetFuseComplement(code) ?? highestLower.GetFuseComplement(code);
                fusion.Upper = VirtualChip.AcquireExact(complement, ref set);

                return fusion.IsEmpty() ? null : fusion;
            }
        }

        // A "virtual" chip; could be an actual chip or a result of a pending fusion
        public class VirtualChip
        {
            public ChipCode Code { get; private set; }
            public Chip Actual { get; private set; } = null;
            public ChipFusion Fusion { get; private set; } = null;

            public String Name => Chip.Chips[Code.Type].Name;
            public int Level => Code.Level;
            public int Weight => Code.Weight;

            public VirtualChip(Chip chip) { Code = new ChipCode(chip); Actual = chip; }
            private VirtualChip(ChipCode code) { Code = code; }

            public bool IsValid() { return Actual == null ^ Fusion == null; }
            public bool IsFusion() { return Actual == null; }

            public static VirtualChip AcquireExact(in ChipCode code, ref ChipSet set)
            {
                // Try and get an existing chip
                VirtualChip chip = set.Pop(code);
                if (chip != null) return chip;

                // Otherwise, create a new chip based on a fusion
                chip = new VirtualChip(code);
                chip.Fusion = ChipFusion.Make(code, ref set);
                return chip.Fusion == null ? null : chip;
            }

            public static VirtualChip AcquireLowest(in ChipCode code, ref ChipSet set)
            {
                // Try and get an existing chip
                VirtualChip chip;
                for (int i = Chip.MinimumWeightForLevel[code.Level]; i <= code.Level; i++)
                {
                    chip = set.Pop(new ChipCode(code.Type, code.Level, i));
                    if (chip == null) return chip;
                }

                // Otherwise, create a new chip based on a fusion
                chip = new VirtualChip(code);
                chip.Fusion = ChipFusion.Make(code, ref set);
                return chip.Fusion == null ? null : chip;
            }
        }

        public static readonly ChipCode[] DesiredChips =
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

        public List<ChipFusion> Fusions { get; set; } = new List<ChipFusion>();
        public List<Chip> SellChips { get; set; } = new List<Chip>();

        public ChipOptimizer() { }
        
        public void Load(in List<Chip> chips)
        {
            // Create our chipset from the input chips
            ChipSet chipSet = new ChipSet();
            foreach (Chip chip in chips) 
                if (chip.Type != Chip.Empty.Type) chipSet.Add(new VirtualChip(chip));

            // Create virtual chips and fusions
            Queue<VirtualChip> queue = new Queue<VirtualChip>();
            foreach (ChipCode target in DesiredChips)
            {
                VirtualChip chip = VirtualChip.AcquireLowest(target, ref chipSet);
                if (chip != null) queue.Enqueue(chip);
            }
            
            // TODO: Optimize the partial fusions. For now, mark unused chips as sell
            foreach (VirtualChip chip in chipSet)
            {
                if (!chip.IsFusion())
                {
                    if (chip.Actual.HasLevels)
                        SellChips.Add(chip.Actual);
                }
            }


            while (queue.Count > 0)
            {
                VirtualChip current = queue.Dequeue();
                if (current.IsFusion())
                {
                    if (current.Fusion.Lower != null)
                        queue.Enqueue(current.Fusion.Lower);
                    if (current.Fusion.Upper != null)
                        queue.Enqueue(current.Fusion.Upper);

                    if (current.Fusion.IsComplete())
                        Fusions.Add(current.Fusion);
                }
            }

            Fusions.Reverse();
        }

    }
}
