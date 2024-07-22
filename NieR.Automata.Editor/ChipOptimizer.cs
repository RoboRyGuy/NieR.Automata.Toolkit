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

            public int GetCountOf(ChipCode code)
            {
                if (_set.TryGetValue(code, out var chips))
                    return chips.Count;
                return 0;
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
                return new ChipCode(Type, Level - 1, (int)Math.Floor(Weight - .5 * Math.Max(Level - 1, 1)));
            }

            public ChipCode GetDefuseHigher()
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

            public static int operator <(ChipCode x, ChipCode y)
            {
                if (x.Type == y.Type)
                    if (x.Level == y.Level) return x.Weight - y.Weight;
                    else return x.Level - y.Level;
                else return x.Type - y.Type;
            }

            public static int operator >(ChipCode x, ChipCode y)
            {
                if (x.Type == y.Type)
                    if (x.Level == y.Level) return x.Weight - y.Weight;
                    else return x.Level - y.Level;
                else return x.Type - y.Type;
            }
        }

        // A "virtual" chip; could be an actual chip or a result of a pending fusion
        public class VirtualChip
        {
            // Code containing the stats this chip represents
            public ChipCode Code { get; private set; }

            // A reference to an actual chip this virtual chip wraps. If null, this chip is a fusion
            public Chip Actual { get; private set; } = null;

            // The chip with a lower weight value if this chip is a fusion
            public VirtualChip Lower { get; private set; } = null;

            // The chip with a higher weight value if this chip is a fusion
            public VirtualChip Upper { get; private set; } = null;


            // Name of the chip this virtual chip represents
            public String Name => Chip.Chips[Code.Type].Name;

            // Level of the chip this virtual chip represents
            public int Level => Code.Level;

            // Weight of the chip this virtual chip represents
            public int Weight => Code.Weight;

            
            // True if this chip does not directly contain an actual chip
            public bool IsFusion => Actual == null;

            // True if there is no actual chips associated with this or its children
            public bool IsEmpty => IsFusion && (Lower?.IsEmpty ?? true) && (Upper?.IsEmpty ?? true);
            
            // True if this chip either is a chip or can fuse into a chip
            public bool IsFullChip => (!IsFusion) || CanFuse;

            // True if this chip is a fusion and has children it can fuse together
            public bool CanFuse => IsFusion && (Lower?.IsFullChip ?? false) && (Upper?.IsFullChip ?? false);


            // Constructs this chip in order to wrap an actual chip
            public VirtualChip(Chip chip) { Code = new ChipCode(chip); Actual = chip; }
            
            // Constructs this chip to represent a fusion of some kind
            private VirtualChip(ChipCode code) { Code = code; }


            /// <summary>
            /// Either claims actual chips or creates chip fusions for the requested chip code and qty.
            /// This will only claim chips of the exact weight requested.
            /// </summary>
            /// <param name="set">The ChipSet to try and claim chips from</param>
            /// <param name="code">Code representing the desired chip</param>
            /// <param name="qty">How many chips to acquire</param>
            /// <param name="fuse">If true, will create fusions to make up non-found chips</param>
            /// <returns>
            /// A list of chips. 
            ///   If fuse=true,  this will be qty long.
            ///   If fuse=false, this will be up to qty long and will only contain found chips.
            /// </returns>
            public static List<VirtualChip> AcquireExact(ref ChipSet set, in ChipCode code, int qty = 1, bool fuse = true)
            {
                List<VirtualChip> output = new List<VirtualChip>();
                
                // Try and get existing chips
                while (output.Count < qty)
                {
                    var chip = set.Pop(code);
                    if (chip != null) output.Add(chip);
                    else break;
                }

                // Otherwise, create a new chip based on a fusion
                if ((output.Count != qty) && fuse)
                    output.AddRange(MakeFusion(ref set, code, qty - output.Count));
                return output;
            }

            /// <summary>
            /// Either claims actual chips or creates chip fusions for the requested chip code and qty.
            /// This will claim chips equal to or below the weight requested, with a preference for low weight.
            /// </summary>
            /// <param name="set">The ChipSet to try and claim chips from</param>
            /// <param name="code">Code representing the desired chip</param>
            /// <param name="qty">How many chips to acquire</param>
            /// <param name="fuse">If true, will create fusions to make up non-found chips</param>
            /// <returns>
            /// A list of chips. 
            ///   If fuse=true,  this will be qty long.
            ///   If fuse=false, this will be up to qty long and will only contain found chips.
            /// </returns>
            public static List<VirtualChip> AcquireLowest(ref ChipSet set, in ChipCode code, int qty = 1, bool fuse = true)
            {
                List<VirtualChip> output = new List<VirtualChip>();

                // Try and get existing chips
                for (int i = Chip.MinimumWeightForLevel[code.Level]; i >= code.Weight; i--)
                {
                    while (output.Count < qty)
                    {
                        var chip = set.Pop(new ChipCode(code.Type, code.Level, i));
                        if (chip != null) output.Add(chip);
                        else break;
                    }
                }

                // Otherwise, create a new chip based on a fusion
                if ((output.Count != qty) && fuse)
                    output.AddRange(MakeFusion(ref set, code, qty - output.Count));
                return output;
            }

            /// <summary>
            /// Either claims actual chips or creates chip fusions for the requested chip code and qty.
            /// This will claim chips equal to or below the weight requested, with a preference for high weight.
            /// </summary>
            /// <param name="set">The ChipSet to try and claim chips from</param>
            /// <param name="code">Code representing the desired chip</param>
            /// <param name="qty">How many chips to acquire</param>
            /// <param name="fuse">If true, will create fusions to make up non-found chips</param>
            /// <returns>
            /// A list of chips. 
            ///   If fuse=true,  this will be qty long.
            ///   If fuse=false, this will be up to qty long and will only contain found chips.
            /// </returns>
            public static List<VirtualChip> AcquireHighest(ref ChipSet set, in ChipCode code, int qty = 1, bool fuse = true)
            {
                List<VirtualChip> output = new List<VirtualChip>();

                // Try and get existing chips
                for (int i = code.Weight; i >= Chip.MinimumWeightForLevel[code.Level]; i--)
                {
                    while (output.Count < qty)
                    {
                        var chip = set.Pop(new ChipCode(code.Type, code.Level, i));
                        if (chip != null) output.Add(chip);
                        else break;
                    }
                }

                // Otherwise, create a new chip based on a fusion
                if ((output.Count != qty) && fuse)
                    output.AddRange(MakeFusion(ref set, code, qty - output.Count));
                return output;
            }

            /// <summary>
            /// Creates fusions for the request chip code and qty.
            /// </summary>
            /// <param name="set">The ChipSet to try and claim chips from</param>
            /// <param name="code">Code representing the desired chip</param>
            /// <param name="qty">How many chips to create fusions for</param>
            /// <returns>A list of fusions. This will be qty long</returns>
            public static List<VirtualChip> MakeFusion(ref ChipSet set, in ChipCode code, int qty = 1)
            {
                // This function guarantees delivery of items. Therefore, prep the whole list in advance
                List<VirtualChip> output = new List<VirtualChip>();
                for (int i = 0; i < qty; i++) output.Add(new VirtualChip(code));

                // Can't make fusions for level 0 chips
                if (code.Level <= 0) return output;

                // Even and odd chip levels are processed different to help assert delivery
                if (code.Level % 2 == 1)
                {
                    // Obtain the lower chips needed for fusion
                    ChipCode highestLower = code.GetDefuseLower();
                    {
                        List<VirtualChip> lowers = AcquireLowest(ref set, highestLower, qty);
                        lowers.Sort((x, y) => x.Code > y.Code);
                        for (int i = 0; i < qty; i++) output[i].Lower = lowers[i];
                    }

                    // Obtain the upper chips; since the lower chips may be different levels,
                    //  we count out how many are of a certain level and batch the uppers
                    int lastProcessed = 0;
                    while (lastProcessed < qty)
                    {
                        // This section gets a count of identical chips
                        ChipCode common = output[lastProcessed].Lower?.Code ?? highestLower;
                        int count;
                        for (count = 1; count < qty - lastProcessed; count++) 
                            if ((output[lastProcessed + count].Lower?.Code ?? highestLower) != common) break;

                        // This section obtains those chips and organizes them
                        ChipCode complement = common.GetFuseComplement(code);
                        List<VirtualChip> uppers = AcquireHighest(ref set, complement, count);
                        uppers.Sort((x, y) => x.Code > y.Code);
                        for (int i = lastProcessed; i < lastProcessed + count; i++) 
                            output[lastProcessed + i].Upper = uppers[i];

                        // Mark how many chips have been finished
                        lastProcessed += count;
                    }
                }
                else
                {
                    // Obtain as many below-average chips as possible without making fusions
                    ChipCode highestLower = code.GetDefuseLower();
                    List<VirtualChip> children = AcquireLowest(ref set, new ChipCode(highestLower.Type, highestLower.Level, highestLower.Weight - 1), qty, false);
                    int lowers = children.Count;

                    // Grab as many strictly-average chips as we need
                    if (lowers < qty)
                        children.AddRange(AcquireExact(ref set, highestLower, 2 * (qty - lowers)));

                    // Since all the below-average chips can be of different levels, we
                    //  group together identical ones and aim for their true complement
                    int lastProcessed = 0;
                    while (lastProcessed < lowers)
                    {
                        // This section gets a count of identical chips
                        ChipCode common = children[lastProcessed].Code;
                        int count;
                        for (count = 1; count < lowers - lastProcessed; count++)
                            if (output[lastProcessed + count].Code != common) break;

                        // This section obtains those chips and organizes them
                        ChipCode complement = common.GetFuseComplement(code);
                        List<VirtualChip> uppers = AcquireHighest(ref set, complement, count);
                        uppers.Sort((x, y) => x.Code > y.Code);
                        children.AddRange(uppers);

                        // Mark how many chips have been finished
                        lastProcessed += count;
                    }

                    // Move all the chips we've collected into fusions
                    for (int i = 0; i < qty; i++)
                    {
                        output[i].Lower = children[i];
                        output[i].Upper = children[2 * qty - i - 1];
                    }
                }

                // Return our results!
                return output;
            }

            /// <summary>
            /// (Of a VirtualChip) Removes empty sub-chips and filles a list with ready-to-fuse chips
            /// </summary>
            /// <param name="fusions">List to output fusions in to</param>
            public void PruneAndReport(ref List<VirtualChip> fusions)
            {
                // No reports or pruning on actual chips
                if (!IsFusion) return;

                // Handle the children
                if (Lower?.IsEmpty ?? true) Lower = null;
                else Lower.PruneAndReport(ref fusions);

                if (Upper?.IsEmpty ?? true) Upper = null;
                else Upper.PruneAndReport(ref fusions);

                // Report!
                if (CanFuse) fusions.Add(this);
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

        public List<VirtualChip> Fusions = new List<VirtualChip>();
        public List<Chip> SellChips = new List<Chip>();

        public ChipOptimizer() { }
        
        public void Load(in List<Chip> chips)
        {
            // Create our chipset from the input chips
            ChipSet chipSet = new ChipSet();
            foreach (Chip chip in chips) 
                if (chip.Type != Chip.Empty.Type && chip.HasLevels) chipSet.Add(new VirtualChip(chip));

            // Create virtual chips and fusions
            foreach (ChipCode target in DesiredChips)
            {
                // Since we request 1 and allow fusions, we're guaranteed 1 chip
                VirtualChip chip = VirtualChip.AcquireLowest(ref chipSet, target)[0];
                chip.PruneAndReport(ref Fusions);
            }

            // Any unused chips after the greedy pass are sold
            foreach (VirtualChip chip in chipSet)
                if (!chip.IsFusion) SellChips.Add(chip.Actual);

            // This sort makes it easier to read through
            Fusions.Sort((x, y) => x.Code > y.Code);
        }

    }
}
