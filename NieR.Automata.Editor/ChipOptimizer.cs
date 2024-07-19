using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

namespace NieR.Automata.Toolkit
{
    internal class ChipOptimizer
    {
        private class ChipComparer : Comparer<Chip>
        {
            public override int Compare(Chip a, Chip b)
            {
                return MakeCode(a) - MakeCode(b);
            }

            private int MakeCode(Chip chip)
            {
                return (chip.Type << 16) | ((8 - chip.Level) << 8) | chip.Weight;
            }
        }

        public class ChipCount
        {
            public int code;
            public Stack<Chip> held = new Stack<Chip>();
            public int needed = 0;

            public override string ToString()
            {
                return Chip.Chips[ToType(code)].Name + " +" + ToLevel(code) + " [" + ToWeight(code) + "], held = " + held.Count.ToString() + ", needed = " + needed.ToString();
            }
        }

        public class ChipFusion
        {
            public Chip lower;
            public Chip upper;
        }

        public static readonly int[] DesiredChips =
        {
            ToCode(0x01, 8, 21), // Name = "Weapon Attack Up"
            ToCode(0x02, 8, 21), // Name = "Down-Attack Up"
            ToCode(0x03, 8, 21), // Name = "Critical Up"
            ToCode(0x04, 8, 21), // Name = "Ranged Attack Up"
            ToCode(0x05, 8, 21), // Name = "Fast Cooldown"
            ToCode(0x06, 8, 21), // Name = "Melee Defence Up"
            ToCode(0x07, 8, 21), // Name = "Ranged Defence Up"
            ToCode(0x08, 8, 21), // Name = "Anti Chain Damage"
            ToCode(0x09, 8, 21), // Name = "Max HP Up"
            ToCode(0x0A, 8, 21), // Name = "Offensive Heal"
            ToCode(0x0B, 8, 21), // Name = "Deadly Heal"
            ToCode(0x0C, 8, 21), // Name = "Auto-Heal"
            ToCode(0x0D, 8, 21), // Name = "Evade Range Up"
            ToCode(0x0E, 3, 7 ), // Name = "Moving Speed Up"
            ToCode(0x0E, 3, 7 ), // Name = "Moving Speed Up"
            ToCode(0x0F, 4, 9 ), // Name = "Drop Rate Up"
            ToCode(0x0F, 3, 7 ), // Name = "Drop Rate Up"
            ToCode(0x10, 8, 21), // Name = "EXP Gain Up"
            ToCode(0x11, 8, 21), // Name = "Shock Wave"
            ToCode(0x12, 8, 21), // Name = "Last Stand"
            ToCode(0x13, 8, 21), // Name = "Damage Absorb"
            ToCode(0x14, 8, 21), // Name = "Vengeance"
            ToCode(0x15, 8, 21), // Name = "Reset"
            ToCode(0x16, 8, 21), // Name = "Overclock"
            ToCode(0x17, 8, 21), // Name = "Resilience"
            ToCode(0x18, 8, 21), // Name = "Counter"
            ToCode(0x19, 8, 21), // Name = "Taunt Up"
            ToCode(0x1A, 8, 21), // Name = "Charge Attack"
            ToCode(0x1B, 8, 21), // Name = "Auto-use Item"
            ToCode(0x1D, 8, 21), // Name = "Hijack Boost"
            ToCode(0x1E, 8, 21), // Name = "Stun"
            ToCode(0x1F, 8, 21), // Name = "Combust"
            ToCode(0x22, 8, 21), // Name = "Heal Drops Up"
        };
        public static readonly double[] AvgWeights = { 7, 7.5, 8.5, 10, 12, 14.5, 17.5, 21, 21 };
        public static readonly Dictionary<int, List<int>> PreferredFusions = new Dictionary<int, List<int>>()
        {
            [ToCode(0, 0, 4 )] = new List<int>() { 5, 9, 7, 11, 13 },
            [ToCode(0, 0, 5 )] = new List<int>() { 6, 8, 10, 12 },
            [ToCode(0, 0, 6 )] = new List<int>() { 7, 9, 11 },
            [ToCode(0, 0, 7 )] = new List<int>() { 8, 10 },
            [ToCode(0, 0, 8 )] = new List<int>() { 9 },
            [ToCode(0, 1, 5 )] = new List<int>() { 6, 8, 10, 12 },
            [ToCode(0, 1, 6 )] = new List<int>() { 7, 9, 11 },
            [ToCode(0, 1, 7 )] = new List<int>() { 8, 10 },
            [ToCode(0, 1, 8 )] = new List<int>() { 9 },
            [ToCode(0, 2, 6 )] = new List<int>() { 6, 10, 8, 12 },
            [ToCode(0, 2, 7 )] = new List<int>() { 7, 11, 9 },
            [ToCode(0, 2, 8 )] = new List<int>() { 8, 10 },
            [ToCode(0, 2, 9 )] = new List<int>() { 9 },
            [ToCode(0, 3, 7 )] = new List<int>() { 8, 12, 10 },
            [ToCode(0, 3, 8 )] = new List<int>() { 9, 11 },
            [ToCode(0, 3, 9 )] = new List<int>() { 10 },
            [ToCode(0, 4, 9 )] = new List<int>() { 9, 13, 11 },
            [ToCode(0, 4, 10)] = new List<int>() { 10, 12 },
            [ToCode(0, 4, 11)] = new List<int>() { 11 },
            [ToCode(0, 5, 11)] = new List<int>() { 12, 14 },
            [ToCode(0, 5, 12)] = new List<int>() { 13 },
            [ToCode(0, 6, 14)] = new List<int>() { 14, 16 },
            [ToCode(0, 6, 15)] = new List<int>() { 15 },
            [ToCode(0, 7, 17)] = new List<int>() { 18 },
        };

        public SortedList<int, ChipCount> Counts { get; protected set; }
        public List<Chip> DesiredsObtained = new List<Chip>();
        public List<ChipFusion> Fusions = new List<ChipFusion>();
        public List<Chip> SellChips = new List<Chip>();
        public List<Chip> HoldOntoChips = new List<Chip>();

        public ChipOptimizer() { }
        
        public void Load(in List<Chip> chips)
        {
            // Start by loading all these chips into our model
            Counts = new SortedList<int, ChipCount>();
            foreach (Chip chip in chips)
            {
                // If the chip cannot be fused or is invalid, skip it
                if (chip.Type <= 0 || !chip.HasLevels || chip.Type > 0x22) continue;

                // Add the chip to the relevant list
                ChipCount count = GetOrCreate(Counts, ToCode(chip));
                count.held.Push(chip);
            }

            // Add in our desired chips
            foreach (int code in DesiredChips)
            {
                ChipCount count = GetOrCreate(Counts, code);
                count.needed++;
            }

            // Iterate through and generate a list of needed chips. Start with high-level chips and work down
            for (int i = 0; i < Counts.Count; i++)
            {
                ChipCount count = Counts.ElementAt(i).Value;
                
                // Subtract what we already have for a more accurate need count
                count.needed -= count.held.Count;

                if (count.needed > 0)
                {
                    // Calculate the two 'optimal' chips needed
                    int code = Counts.ElementAt(i).Key;
                    int type = ToType(code);
                    int level = ToLevel(code) - 1;

                    if (level == 0) continue; // We can skip level 0; we don't need it

                    // These weights could turn out to be the same. It works out either way
                    int lWeight = (int)Math.Floor  (ToWeight(code) - (level * .5));
                    int hWeight = (int)Math.Ceiling(ToWeight(code) - (level * .5));

                    // Simply add the needs to the children; they'll be processed later, since they're a lower level
                    ChipCount lChild = GetOrCreate(Counts, ToCode(type, level, lWeight));
                    lChild.needed += count.needed;
                    ChipCount hChild = GetOrCreate(Counts, ToCode(type, level, hWeight));
                    hChild.needed += count.needed;
                }
            }

            // Start fusing chips. Starting from level 0, make pairs based on needs. Decide fuse, keep, and sell
            while (Counts.Count > 0)
            {
                ChipCount count = Counts.Last().Value;
                List<int> pairWeights = null;
                PreferredFusions.TryGetValue(ToCode(0, ToLevel(Counts.Last().Key), ToWeight(Counts.Last().Key)), out pairWeights);

                while (count.held.Count > 0)
                {
                    // Current chip stats
                    Chip current = count.held.Pop();
                    int levelWeight = current.Level == 0 ? 1 : current.Level;

                    if (pairWeights != null)
                    {
                        foreach (int weight in pairWeights)
                        {
                            int targetCode = ToCode(current.Type, current.Level, (int)Math.Ceiling((current.Weight + weight + levelWeight) * .5));
                            ChipCount targetCount = GetOrCreate(Counts, targetCode);
                            if (targetCount.needed > 0)
                            {
                                ChipCount pairCount = GetOrCreate(Counts, ToCode(current.Type, current.Level, weight));
                                if (pairCount.held.Count > 0)
                                    Fusions.Add(new ChipFusion() { lower = current, upper = pairCount.held.Pop() });
                                else
                                    HoldOntoChips.Add(current);
                                targetCount.needed--;
                                current = null;
                                break;
                            }
                        }
                    }

                    // If this chip was not needed
                    if (current != null)
                        SellChips.Add(current);
                }

                Counts.Remove(Counts.Last().Key);
            }
        }

        private static int ToCode(in Chip chip)
        {
            return ToCode(chip.Type, chip.Level, chip.Weight);
        }

        private static int ToCode(int type, int level, int weight)
        {
            return (type << 16) | ((8 - level) << 8) | (255 - weight);
        }

        private static int ToType(int code)
        {
            return (code >> 16) & 0xFF;
        }
        private static int ToLevel(int code)
        {
            return 8 - ((code >> 8) & 0xFF);
        }
        private static int ToWeight(int code)
        {
            return 255 - (code & 0xFF);
        }

        private static ChipCount GetOrCreate(IDictionary<int, ChipCount> dict, int key)
        {
            if (!dict.TryGetValue(key, out ChipCount val))
            {
                val = new ChipCount() { code = key };
                dict.Add(key, val);
            }

            return val;

        }

    }
}
