using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    using ManaCost = Tuple<int, int, int, int>;

    public class State {
        public int[] N_FACTORIAL = new int[] { 1, 1, 2, 6 };
        public int[][] PONDER_ORDERS = new int[][] {
            new int[]{ 0, 2, 1 },
            new int[]{ 1, 0, 2 },
            new int[]{ 1, 2, 0 },
            new int[]{ 2, 0, 1 },
            new int[]{ 2, 1, 0 }
        };
        public static Dictionary<Card, string> CARD_NAMES = new Dictionary<Card, string>() {
            { Card.None, "NONE" },
            // lands
            { Card.Plains, "Plains" },
            { Card.Island, "Island" },
            { Card.Mountain, "Mountain" },
            { Card.MysticMonastery, "Mystic Monastery" },
            { Card.EvolvingWilds, "Evolving Wilds" },
            // spells
            { Card.Brainstorm, "Brainstorm" },
            { Card.CeruleanWisps, "Cerulean Wisps" },
            { Card.Fatestitcher, "Fatestitcher" },
            { Card.FranticInventory, "Frantic Inventory" },
            { Card.FranticSearch, "Frantic Search" },
            { Card.GitaxianProbe, "Gitaxian Probe" },
            { Card.JeskaiAscendancy, "Jeskai Ascendancy" },
            { Card.ObsessiveSearch, "Obsessive Search" },
            { Card.Opt, "Opt" },
            { Card.Ponder, "Ponder" },
            { Card.TreasureCruise, "Treasure Cruise" },
        };
        public static Dictionary<Card, ManaCost> MANA_COSTS = new Dictionary<Card, ManaCost>() {
            { Card.Brainstorm, new ManaCost(0, 1, 0, 0) },
            { Card.CeruleanWisps, new ManaCost(0, 1, 0, 0) },
            { Card.FranticInventory, new ManaCost(0, 1, 0, 1) },
            { Card.FranticSearch, new ManaCost(0, 1, 0, 2) },
            { Card.GitaxianProbe, new ManaCost(0, 0, 0, 0) },
            { Card.JeskaiAscendancy, new ManaCost(1, 1, 1, 0) },
            { Card.ObsessiveSearch, new ManaCost(0, 1, 0, 0) },
            { Card.Opt, new ManaCost(0, 1, 0, 0) },
            { Card.Ponder, new ManaCost(0, 1, 0, 0) },
            { Card.TreasureCruise, new ManaCost(0, 1, 0, 7) }, // TODO: Delve.
        };
        public bool[] LAND_ETB_TAPPED = new bool[] { false, false, false, true, false };
        static int SPECIAL_MOVE_END_TURN = -1;
        static int SPECIAL_MOVE_FETCH_PLAINS = -2;
        static int SPECIAL_MOVE_FETCH_ISLAND = -3;
        static int SPECIAL_MOVE_FETCH_MOUNTAIN = -4;
        static int SPECIAL_MOVE_UNEARTH_FATESTITCHER = -5;

        public int turn;

        // Library.
        List<int> topOfDeck;
        int shuffledLibraryCount;
        int[] shuffledLibraryQuantities;
        Queue<int> bottomOfDeck;

        // Hand.
        int[] handQuantities;

        // Battlefield.
        bool landPlay;
        int[] untappedLandsInPlay, tappedLandsInPlay;
        int whiteMana, blueMana, redMana;
        int ascendancies;
        int untappedFatestitchers, tappedFatestitchers;
        int totalPower;

        // Graveyard.
        int graveyardFatestitchers, graveyardInventories, graveyardOther;

        // Stack.
        Card stack; // Which card is waiting for Jeskai Ascendancy trigger(s) to resolve?

        // Choices.
        int choiceDiscard; // Discard N cards.
        int choiceScry; // Scry the top N cards.
        int choiceTop; // Top N cards with Brainstorm.
        bool choicePonder; // Reorder top 3 cards or shuffle.
        int choiceObsessive; // N Obsessive Searches were discarded: pay U to draw a card?
        int ascendancyTriggers; // We have one or more Ascendancy triggers waiting to trigger after we float mana.
        int postScryDraws; // We have one or more draws waiting for a scry.

        public State(Dictionary<Card, int> decklist, int startingHandSize) {
            turn = 1;
            // Library.
            shuffledLibraryCount = 60;
            int maxDeckCardEnumValue = decklist.Keys.Max(c => (int)c);
            shuffledLibraryQuantities = new int[maxDeckCardEnumValue + 1];
            foreach (var kvp in decklist) {
                shuffledLibraryQuantities[(int)kvp.Key] = kvp.Value;
            }
            Debug.Assert(shuffledLibraryQuantities.Sum() == 60, "Starting deck doesn't contain exactly 60 cards.");
            topOfDeck = new List<int>();
            bottomOfDeck = new Queue<int>();
            // Hand.
            handQuantities = new int[maxDeckCardEnumValue + 1];
            // Battlefield.
            landPlay = true;
            untappedLandsInPlay = new int[LAND_ETB_TAPPED.Length + 1];
            tappedLandsInPlay = new int[LAND_ETB_TAPPED.Length + 1];

            // Draw opening hand.
            // SIMPLIFICATION: Mulligans aren't part of the game tree, instead performed with meta-analysis.
            for (int i = 0; i < startingHandSize; i++) {
                Draw();
            }
        }

        public int[] GetMoves() {
            // TODO: Deduplicate all choices: discarding/topping/pondering copies of the same card.
            if (choiceDiscard > 0) {
            }
            if (choiceScry > 0) {
                Debug.Assert(choiceScry == 1, "Scry amounts larger than 1 not supported.");
                return new int[] { -1, 1 };
            }
            if (choiceTop > 0) {
            }
            if (choicePonder) {
            }
            if (choiceObsessive > 0) {
            }
            // Playing lands and casting spells.
            List<int> moves = new List<int>();
            ManaCost maxWhite = GetMaxWhiteMana(), maxBlue = GetMaxBlueMana(), maxRed = GetMaxRedMana(), maxMixed = GetMaxMixedMana();
            for (int i = landPlay ? 1 : LAND_ETB_TAPPED.Length + 1; i < handQuantities.Length; i++) {
                if (handQuantities[i] == 0) {
                    continue;
                }
                Card card = (Card)i;
                if (card == Card.CeruleanWisps && untappedFatestitchers == 0 && tappedFatestitchers == 0) {
                    continue;
                }
                if (card == Card.EvolvingWilds) {
                    if (shuffledLibraryQuantities[(int)Card.Plains] > 0 || topOfDeck.Contains((int)Card.Plains) || bottomOfDeck.Contains((int)Card.Plains)) moves.Add(SPECIAL_MOVE_FETCH_PLAINS);
                    if (shuffledLibraryQuantities[(int)Card.Island] > 0 || topOfDeck.Contains((int)Card.Island) || bottomOfDeck.Contains((int)Card.Island)) moves.Add(SPECIAL_MOVE_FETCH_ISLAND);
                    if (shuffledLibraryQuantities[(int)Card.Mountain] > 0 || topOfDeck.Contains((int)Card.Mountain) || bottomOfDeck.Contains((int)Card.Mountain)) moves.Add(SPECIAL_MOVE_FETCH_MOUNTAIN);
                    continue;
                }
                if (card == Card.Fatestitcher) {
                    // SIMPLIFICATION: No hardcast Fatestitchers.
                    continue;
                }
                if (i > LAND_ETB_TAPPED.Length) {
                    ManaCost cost = MANA_COSTS[card];
                }
                moves.Add(i);
            }
            if (graveyardFatestitchers > 0 && maxBlue.Item2 > 0) {
                moves.Add(SPECIAL_MOVE_UNEARTH_FATESTITCHER);
            }
            moves.Add(SPECIAL_MOVE_END_TURN);
            return moves.ToArray();
        }
        // Returns 1 for deterministic moves, and the probability of the resultant state for stochastic moves.
        public float ExecuteMove(int move) {
            int cardEnumValueLimit = Enum.GetNames(typeof(Card)).Length;
            // Choices.
            if (choiceDiscard > 0) {
                while (move > 0) {
                    int cardIndex = move % cardEnumValueLimit;
                    Card card = (Card)cardIndex;
                    GoToGraveyard(card);
                    handQuantities[cardIndex]--;
                    move /= cardEnumValueLimit;
                }
                choiceDiscard = 0;
                return 1;
            }
            if (choiceScry > 0) {
                if (move == -1) {
                    bottomOfDeck.Enqueue(topOfDeck[0]);
                    topOfDeck.RemoveAt(0);
                }
                if (postScryDraws > 0) {
                    float probability = Draw(postScryDraws);
                    postScryDraws = 0;
                    return probability;
                }
                return 1;
            }
            if (choiceTop > 0) {
                while (move > 0) {
                    int cardIndex = move % cardEnumValueLimit;
                    handQuantities[cardIndex]--;
                    topOfDeck.Insert(0, cardIndex);
                    move /= cardEnumValueLimit;
                }
                choiceTop = 0;
                return 1;
            }
            if (choicePonder) {
                if (move == 6) {
                    Shuffle();
                } else if (move < 5) {
                    int[] ponderOrder = PONDER_ORDERS[move];
                    int zero = topOfDeck[ponderOrder[0]], one = topOfDeck[ponderOrder[1]], two = topOfDeck[ponderOrder[2]];
                    topOfDeck[0] = zero;
                    topOfDeck[1] = one;
                    topOfDeck[2] = two;
                } // (else: don't reorder)
                choicePonder = false;
                return Draw();
            }
            if (choiceObsessive > 0) {
                if (move == 1) {
                    choiceObsessive--;
                    SpendMana(0, 1, 0, 0);
                    return Draw();
                }
                choiceObsessive = 0;
                return 1;
            }
            if (move == SPECIAL_MOVE_END_TURN) {
                EndStepAndUntap();
                // TODO: Discard down to maximum hand size.
                return Draw();
            }
            if (move == SPECIAL_MOVE_FETCH_PLAINS || move == SPECIAL_MOVE_FETCH_ISLAND || move == SPECIAL_MOVE_FETCH_MOUNTAIN) {
                Shuffle();
                landPlay = false;
                handQuantities[(int)Card.EvolvingWilds]--;
                GoToGraveyard(Card.EvolvingWilds);
                if (move == SPECIAL_MOVE_FETCH_PLAINS) {
                    shuffledLibraryQuantities[(int)Card.Plains]--;
                    tappedLandsInPlay[(int)Card.Plains]++;
                } else if (move == SPECIAL_MOVE_FETCH_ISLAND) {
                    shuffledLibraryQuantities[(int)Card.Island]--;
                    tappedLandsInPlay[(int)Card.Island]++;
                } else {
                    shuffledLibraryQuantities[(int)Card.Mountain]--;
                    tappedLandsInPlay[(int)Card.Mountain]++;
                }
                return 1;
            }
            if (move == SPECIAL_MOVE_UNEARTH_FATESTITCHER) {
                SpendMana(0, 1, 0, 0);
                graveyardFatestitchers--;
                untappedFatestitchers++;
                return 1;
            }
            if (move <= LAND_ETB_TAPPED.Length) {
                // Play a land.
                handQuantities[move]--;
                landPlay = false;
                Card land = (Card)move;
                if (move <= 3) {
                    // Basic land.
                    tappedLandsInPlay[move]++;
                    if (land == Card.Plains) whiteMana++;
                    else if (land == Card.Island) blueMana++;
                    else redMana++;
                } else {
                    // Other nonbasic land.
                    (LAND_ETB_TAPPED[move] ? tappedLandsInPlay : untappedLandsInPlay)[move]++;
                }
                return 1;
            }
            // Cast a spell from hand.
            handQuantities[move]--;
            stack = (Card)move;
            ascendancyTriggers = ascendancies;
            ManaCost cost = MANA_COSTS[stack];
            SpendMana(cost.Item1, cost.Item2, cost.Item3, cost.Item4);
            // Stack resolving.
            if (ascendancyTriggers > 0) {
                return ResolveAscendancyTrigger();
            }
            Card spell = stack;
            stack = Card.None;
            if (spell != Card.JeskaiAscendancy) {
                GoToGraveyard(stack);
            }
            switch (spell) {
                case Card.Brainstorm:
                    choiceTop = 2;
                    return Draw(3);
                case Card.CeruleanWisps:
                    if (tappedFatestitchers > 0) {
                        tappedFatestitchers--;
                        untappedFatestitchers++;
                    } else {
                        UntapLands(1);
                    }
                    return Draw();
                case Card.FranticInventory:
                    return Draw(graveyardInventories);
                case Card.FranticSearch:
                    UntapLands(3);
                    choiceDiscard = 2;
                    return Draw(3);
                case Card.GitaxianProbe:
                    return Draw();
                case Card.JeskaiAscendancy:
                    ascendancies++;
                    return 1;
                case Card.ObsessiveSearch:
                    return Draw();
                case Card.Opt:
                    choiceScry = 1;
                    postScryDraws = 1;
                    return RevealTop(1);
                case Card.Ponder:
                    choicePonder = true;
                    return RevealTop(3);
                case Card.TreasureCruise:
                    return Draw(3);
                default:
                    throw new Exception("Unhandled spell resolving: " + CARD_NAMES[stack]);
            }
        }
        public string MoveToString(int move) {
            // TODO: Move strings.
            return "";
        }

        public bool IsWon() {
            return totalPower >= 20;
        }
        public void SanityCheck() {
            int totalCards = topOfDeck.Count + shuffledLibraryCount + bottomOfDeck.Count + handQuantities.Sum() + untappedLandsInPlay.Sum() + tappedLandsInPlay.Sum() + ascendancies + untappedFatestitchers + tappedFatestitchers + graveyardFatestitchers + graveyardInventories + graveyardOther;
            if (stack != Card.None) totalCards++;
            Debug.Assert(totalCards == 60, string.Format("Total cards in the state is {0}, not 60!\n{1}", totalCards, this));
        }

        float Draw() {
            float probability;
            int i;
            // TODO: Reaching the bottom of the deck.
            if (topOfDeck.Count > 0) {
                i = (int)topOfDeck[0];
                topOfDeck.RemoveAt(0);
                probability = 1;
            } else {
                i = RandomIndexFromDeck();
                probability = shuffledLibraryQuantities[i] / (float)shuffledLibraryCount;
            }
            shuffledLibraryCount--;
            shuffledLibraryQuantities[i]--;
            handQuantities[i]++;
            return probability;
        }
        float Draw(int n) {
            float totalProbability = 1;
            int topDecks = 0;
            for (int i = 0; i < n; i++) {
                float probability = Draw();
                totalProbability *= probability;
                if (probability < 1) {
                    topDecks++;
                }
            }
            // TODO: Is this probability calculation complete?
            return totalProbability * N_FACTORIAL[topDecks];
        }
        float RevealTop(int n) {
            float probability = 1;
            while (topOfDeck.Count < n) {
                int i = RandomIndexFromDeck();
                probability *= shuffledLibraryQuantities[i] / (float)shuffledLibraryCount;
                shuffledLibraryCount--;
                shuffledLibraryQuantities[i]--;
                topOfDeck.Add(i);
            }
            return probability;
        }
        int RandomIndexFromDeck() {
            int selector;
            lock (Program.random) {
                selector = Program.random.Next(shuffledLibraryCount);
            }
            int i = 1;
            for (; selector >= shuffledLibraryQuantities[i]; i++) {
                selector -= shuffledLibraryQuantities[i];
            }
            return i;
        }

        void EndStepAndUntap() {
            whiteMana = tappedLandsInPlay[(int)Card.Plains];
            blueMana = tappedLandsInPlay[(int)Card.Island];
            redMana = tappedLandsInPlay[(int)Card.Mountain];
            totalPower = 0;
            turn++;
            landPlay = true;
            untappedFatestitchers = 0;
            tappedFatestitchers = 0;
            // Untap only lands that produce multiple colors of mana.
            for (int i = 4; i < untappedLandsInPlay.Length; i++) {
                untappedLandsInPlay[i] += tappedLandsInPlay[i];
                tappedLandsInPlay[i] = 0;
            }
        }
        void GoToGraveyard(Card card) {
            if (card == Card.Fatestitcher) {
                graveyardFatestitchers++;
            } else if (card == Card.FranticInventory) {
                graveyardInventories++;
            } else if (card == Card.ObsessiveSearch) {
                choiceObsessive++;
            } else {
                graveyardOther++;
            }
        }
        void Shuffle() {
            foreach (int i in topOfDeck) {
                shuffledLibraryQuantities[i]++;
            }
            foreach (int i in bottomOfDeck) {
                shuffledLibraryQuantities[i]++;
            }
            shuffledLibraryCount += topOfDeck.Count + bottomOfDeck.Count;
            topOfDeck.Clear();
            bottomOfDeck.Clear();
        }
        float ResolveAscendancyTrigger() {
            ascendancyTriggers--;
            untappedFatestitchers += tappedFatestitchers;
            tappedFatestitchers = 0;
            totalPower += untappedFatestitchers;
            choiceDiscard = 1;
            return Draw();
        }

        // TODO: All these. Don't forget the Fatestitchers!
        ManaCost GetMaxWhiteMana() {
            return new ManaCost(0, 0, 0, 0);
        }
        ManaCost GetMaxBlueMana() {
            return new ManaCost(0, 0, 0, 0);
        }
        ManaCost GetMaxRedMana() {
            return new ManaCost(0, 0, 0, 0);
        }
        ManaCost GetMaxMixedMana() {
            return new ManaCost(0, 0, 0, 0);
        }
        void SpendMana(int white, int blue, int red, int generic) {
            // SIMPLIFICATION: Spend mana heuristically instead of including in the search.
            // TODO: Spend mana.
        }
        void UntapLands(int n) {
            // SIMPLIFICATION: Untap lands heuristically instead of including in the search.
            // TODO: Float mana and untap lands.
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            List<string> tokens = new List<string>();
            for (int i = 1; i < handQuantities.Length; i++) {
                if (handQuantities[i] > 0) {
                    tokens.Add(string.Format("{0} {1}", handQuantities[i], CARD_NAMES[(Card)i]));
                }
            }
            sb.Append("Hand: ");
            sb.AppendLine(string.Join(", ", tokens));
            if (topOfDeck.Count > 0) {
                sb.AppendLine("Top of library: " + string.Join(", ", topOfDeck.Select(i => CARD_NAMES[(Card)i])));
            }
            if (shuffledLibraryCount > 0) {
                tokens.Clear();
                for (int i = 1; i < shuffledLibraryQuantities.Length; i++) {
                    if (shuffledLibraryQuantities[i] > 0) {
                        tokens.Add(string.Format("{0} {1}", shuffledLibraryQuantities[i], CARD_NAMES[(Card)i]));
                    }
                }
                sb.Append((topOfDeck.Count > 0 || bottomOfDeck.Count > 0) ? "Library (shuffled portion): " : "Library: ");
                sb.AppendLine(string.Join(", ", tokens));
            }
            if (bottomOfDeck.Count > 0) {
                sb.AppendLine("Bottom of library: " + string.Join(", ", bottomOfDeck.Select(i => CARD_NAMES[(Card)i])));
            }
            return sb.ToString();
        }
    }

    public enum Card {
        None,
        // lands
        Island,
        Mountain,
        Plains,
        MysticMonastery,
        EvolvingWilds,
        // spells
        Brainstorm,
        CeruleanWisps,
        Fatestitcher,
        FranticInventory,
        FranticSearch,
        GitaxianProbe,
        JeskaiAscendancy,
        ObsessiveSearch,
        Opt,
        Ponder,
        TreasureCruise,
    }
}
