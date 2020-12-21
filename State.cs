using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace JeskaiAscendancyMCTS {
    using ManaCost = Tuple<int, int, int, int, int>;
    using ChanceEvent = ValueTuple<int, float>;

    public class State {
        static readonly int[] N_FACTORIAL = new int[] { 1, 1, 2, 6, 24 };
        static readonly int[][] PONDER_ORDERS = new int[][] {
            new int[] { 0, 2, 1 },
            new int[] { 1, 0, 2 },
            new int[] { 1, 2, 0 },
            new int[] { 2, 0, 1 },
            new int[] { 2, 1, 0 },
            new int[] { 0, 1, 2 }
        };
        static readonly int CARD_ENUM_LENGTH = Enum.GetNames(typeof(Card)).Length;
        static readonly Dictionary<Card, string> CARD_NAMES = new Dictionary<Card, string>() {
            { Card.None, "NONE" },
            // lands
            { Card.Plains, "Plains" },
            { Card.Island, "Island" },
            { Card.Mountain, "Mountain" },
            { Card.Forest, "Forest" },
            { Card.IzzetBoilerworks, "Izzet Boilerworks" },
            { Card.MeanderingRiver, "Meandering River" },
            { Card.HighlandLake, "Highland Lake" },
            { Card.MysticMonastery, "Mystic Monastery" },
            { Card.VividCreek, "Vivid Creek" },
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
        // TODO: Make both of these into arrays.
        static readonly Dictionary<Card, ManaCost> MANA_COSTS = new Dictionary<Card, ManaCost>() {
            // white, blue, red, green, generic
            { Card.Brainstorm, new ManaCost(0, 1, 0, 0, 0) },
            { Card.CeruleanWisps, new ManaCost(0, 1, 0, 0, 0) },
            { Card.FranticInventory, new ManaCost(0, 1, 0, 0, 1) },
            { Card.FranticSearch, new ManaCost(0, 1, 0, 0, 2) },
            { Card.GitaxianProbe, new ManaCost(0, 0, 0, 0, 0) }, // SIMPLIFICATION: Always pay life for Gitaxian Probe.
            { Card.JeskaiAscendancy, new ManaCost(1, 1, 1, 0, 0) },
            { Card.ObsessiveSearch, new ManaCost(0, 1, 0, 0, 0) },
            { Card.Opt, new ManaCost(0, 1, 0, 0, 0) },
            { Card.Ponder, new ManaCost(0, 1, 0, 0, 0) },
            { Card.TreasureCruise, new ManaCost(0, 1, 0, 0, 7) }
        };
        readonly bool[] LAND_ETB_TAPPED = new bool[] { false, false, false, false, false, true, true, true, true, true, false }; // starting with None, then Plains
        static readonly int SPECIAL_MOVE_END_TURN = 0;
        static readonly int SPECIAL_MOVE_FETCH_PLAINS = -1;
        static readonly int SPECIAL_MOVE_FETCH_ISLAND = -2;
        static readonly int SPECIAL_MOVE_FETCH_MOUNTAIN = -3;
        static readonly int SPECIAL_MOVE_FETCH_FOREST = -4;
        static readonly int SPECIAL_MOVE_FETCH_FAIL_TO_FIND = -5;
        static readonly int SPECIAL_MOVE_UNEARTH_FATESTITCHER = -6;

        public int turn;

        // Library.
        Dictionary<Card, int> decklist;
        List<int> topOfDeck;
        int shuffledLibraryCount;
        int[] shuffledLibraryQuantities;
        Queue<int> bottomOfDeck;
        bool deckedOut;

        // Hand.
        int[] handQuantities;

        // Battlefield.
        bool landPlay;
        int[] untappedLands, tappedLands;
        int whiteMana, blueMana, redMana, greenMana;
        int vividCounters; // SIMPLIFICATION: Count the total number of charge counters on vivid lands.
        int ascendancies;
        int untappedFatestitchers, tappedFatestitchers;
        int totalPower;

        // Graveyard.
        int graveyardFatestitchers, graveyardInventories, graveyardOther;

        // Exile.
        int exiledFatestitchers, exiledOther;

        // Stack.
        Card stack; // Which card is waiting for Jeskai Ascendancy trigger(s) to resolve?

        // Choices.
        bool choiceBounce; // Bounce a land.
        int choiceDiscard; // Discard N cards.
        int choiceScry; // Scry the top N cards.
        int choiceTop; // Top N cards with Brainstorm.
        int choiceBottom; // Bottom N cards after London mulligan.
        bool choicePonder; // Reorder top 3 cards or shuffle.
        int choiceObsessive; // N Obsessive Searches were discarded: pay U to draw a card?
        int ascendancyTriggers; // We have one or more Ascendancy triggers waiting to trigger after we float mana.
        int postScryDraws; // We have one or more draws waiting for a scry.
        bool cleanupDiscard; // The current choiceDiscard is happening in the cleanup step.

        public State(State other) {
            decklist = other.decklist;
            turn = other.turn;
            topOfDeck = other.topOfDeck.ToList();
            shuffledLibraryCount = other.shuffledLibraryCount;
            shuffledLibraryQuantities = other.shuffledLibraryQuantities.Clone() as int[];
            bottomOfDeck = new Queue<int>(other.bottomOfDeck);
            deckedOut = other.deckedOut;
            handQuantities = other.handQuantities.Clone() as int[];
            landPlay = other.landPlay;
            untappedLands = other.untappedLands.Clone() as int[];
            tappedLands = other.tappedLands.Clone() as int[];
            whiteMana = other.whiteMana;
            blueMana = other.blueMana;
            redMana = other.redMana;
            greenMana = other.greenMana;
            vividCounters = other.vividCounters;
            ascendancies = other.ascendancies;
            untappedFatestitchers = other.untappedFatestitchers;
            tappedFatestitchers = other.tappedFatestitchers;
            totalPower = other.totalPower;
            graveyardFatestitchers = other.graveyardFatestitchers;
            graveyardInventories = other.graveyardInventories;
            graveyardOther = other.graveyardOther;
            exiledFatestitchers = other.exiledFatestitchers;
            exiledOther = other.exiledOther;
            stack = other.stack;
            choiceBounce = other.choiceBounce;
            choiceDiscard = other.choiceDiscard;
            choiceScry = other.choiceScry;
            choiceTop = other.choiceTop;
            choiceBottom = other.choiceBottom;
            choicePonder = other.choicePonder;
            choiceObsessive = other.choiceObsessive;
            ascendancyTriggers = other.ascendancyTriggers;
            postScryDraws = other.postScryDraws;
            cleanupDiscard = other.cleanupDiscard; // TODO: This is an awful lot of copying... would it be faster to bundle everything up into one array and block copy?
        }
        public State(Dictionary<Card, int> decklist, int startingHandSize) {
            turn = 1;
            // Library.
            this.decklist = decklist;
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
            untappedLands = new int[LAND_ETB_TAPPED.Length];
            tappedLands = new int[LAND_ETB_TAPPED.Length];

            // Draw opening hand.
            // SIMPLIFICATION: Mulligans aren't part of the game tree, instead performed with meta-analysis.
            for (int i = 0; i < 7; i++) {
                Draw();
            }
            choiceBottom = Math.Max(0, 7 - startingHandSize);
        }

        // TODO: Create readonly static versions of the preset int arrays in here, then benchmark.
        public int[] GetMoves() {
            if (IsWon() || IsLost()) return new int[0];
            List<int> moves;
            if (choiceBounce) {
                moves = new List<int>();
                bool firstTapped = true;
                for (int i = 1; i < untappedLands.Length; i++) {
                    if (firstTapped && LAND_ETB_TAPPED[i]) {
                        // Prioritize bouncing ETB-untapped lands.
                        if (moves.Count > 0) return moves.ToArray();
                        firstTapped = false;
                    }
                    if (i == (int)Card.IzzetBoilerworks) continue;
                    if (untappedLands[i] > 0 || tappedLands[i] > 0) {
                        moves.Add(i);
                    }
                }
                Debug.Assert(moves.Count > 0, "Found no valid lands to bounce.");
                return moves.ToArray();
            }
            if (choiceDiscard > 0) {
                int fatestitchersInHand = handQuantities[(int)Card.Fatestitcher];
                if (choiceDiscard == 1 || choiceDiscard > 2) {
                    // If we're discarding more than two cards, do them one at a time for better tree structure.
                    if (fatestitchersInHand >= 1) return new int[] { (int)Card.Fatestitcher };
                    return handQuantities.Select((n, i) => { return n > 0 ? i : 0; }).Where(n => n != 0).ToArray();
                }
                if (fatestitchersInHand >= 2) return new int[] { (int)Card.Fatestitcher * CARD_ENUM_LENGTH + (int)Card.Fatestitcher };
                moves = new List<int>(4);
                for (int i = 1; i < handQuantities.Length; i++) {
                    if (handQuantities[i] == 0) continue;
                    if (handQuantities[i] >= 2 && (fatestitchersInHand == 0 || i == (int)Card.Fatestitcher)) moves.Add(i * CARD_ENUM_LENGTH + i);
                    for (int j = i + 1; j < handQuantities.Length; j++) {
                        if (handQuantities[j] == 0) continue;
                        if (fatestitchersInHand > 0 && i != (int)Card.Fatestitcher && j != (int)Card.Fatestitcher) continue;
                        moves.Add(i * CARD_ENUM_LENGTH + j);
                    }
                }
                return moves.ToArray();
            }
            if (choiceScry > 0) {
                Debug.Assert(choiceScry == 1, "Scry amounts larger than 1 not supported.");
                return topOfDeck.Count > 0 ? new int[] { 1, -1 } : new int[] { 0 };
            }
            if (choiceTop > 0) {
                return handQuantities.Select((n, i) => { return n > 0 ? i : 0; }).Where(n => n != 0).ToArray();
            }
            if (choiceBottom > 0) {
                if (choiceBottom == 1 || choiceBottom > 2) {
                    // If we're bottoming more than two cards, do them one at a time for better tree structure.
                    return handQuantities.Select((n, i) => { return n > 0 ? i : 0; }).Where(n => n != 0).ToArray();
                }
                moves = new List<int>(4);
                for (int i = 1; i < handQuantities.Length; i++) {
                    if (handQuantities[i] == 0) continue;
                    if (handQuantities[i] >= 2) moves.Add(i * CARD_ENUM_LENGTH + i);
                    // SIMPLIFICATION: Don't care about the order we bottom cards.
                    for (int j = i + 1; j < handQuantities.Length; j++) {
                        if (handQuantities[j] == 0) continue;
                        moves.Add(i * CARD_ENUM_LENGTH + j);
                    }
                }
                return moves.ToArray();
            }
            if (choicePonder) {
                if (topOfDeck.Count < 2) return new int[] { 0 };
                if (topOfDeck.Count == 2) return new int[] { 0, 1 };
                int zero = topOfDeck[0], one = topOfDeck[1], two = topOfDeck[2];
                if (zero == one && one == two) return new int[] { 5, 6 };
                if (zero == one) return new int[] { 0, 3, 5, 6 };
                if (zero == two) return new int[] { 0, 1, 5, 6 };
                if (one == two) return new int[] { 2, 3, 5, 6 };
                return new int[] { 0, 1, 2, 3, 4, 5, 6 };
            }
            int[] manaDAG = CreateManaDAG();
            if (choiceObsessive > 0) {
                return CanPay(manaDAG, new ManaCost(0, 1, 0, 0, 0)) ? new int[] { 0, 1 } : new int[] { 0 };
            }
            // Playing lands and casting spells.
            moves = new List<int>(4);
            for (int i = landPlay ? 1 : LAND_ETB_TAPPED.Length; i < handQuantities.Length; i++) {
                if (handQuantities[i] == 0) {
                    continue;
                }
                Card card = (Card)i;
                if (card == Card.CeruleanWisps && untappedFatestitchers == 0 && tappedFatestitchers == 0) {
                    // No target.
                    continue;
                }
                if (card == Card.EvolvingWilds) {
                    bool canFetch = false;
                    if (shuffledLibraryQuantities[(int)Card.Plains] > 0 || topOfDeck.Contains((int)Card.Plains) || bottomOfDeck.Contains((int)Card.Plains)) {
                        moves.Add(SPECIAL_MOVE_FETCH_PLAINS);
                        canFetch = true;
                    }
                    if (shuffledLibraryQuantities[(int)Card.Island] > 0 || topOfDeck.Contains((int)Card.Island) || bottomOfDeck.Contains((int)Card.Island)) {
                        moves.Add(SPECIAL_MOVE_FETCH_ISLAND);
                        canFetch = true;
                    }
                    if (shuffledLibraryQuantities[(int)Card.Mountain] > 0 || topOfDeck.Contains((int)Card.Mountain) || bottomOfDeck.Contains((int)Card.Mountain)) {
                        moves.Add(SPECIAL_MOVE_FETCH_MOUNTAIN);
                        canFetch = true;
                    }
                    if (shuffledLibraryQuantities[(int)Card.Forest] > 0 || topOfDeck.Contains((int)Card.Forest) || bottomOfDeck.Contains((int)Card.Forest)) {
                        moves.Add(SPECIAL_MOVE_FETCH_FOREST);
                        canFetch = true;
                    }
                    if (!canFetch) {
                        // SIMPLIFICATION: Can only fail to find when no basics remain to fetch.
                        moves.Add(SPECIAL_MOVE_FETCH_FAIL_TO_FIND);
                    }
                    continue;
                }
                if (card == Card.Fatestitcher) {
                    // SIMPLIFICATION: No hardcast Fatestitchers.
                    continue;
                }
                if (card == Card.IzzetBoilerworks) {
                    for (int j = 1; j < untappedLands.Length; j++) {
                        if (j == (int)Card.IzzetBoilerworks) continue;
                        if (untappedLands[j] > 0 || tappedLands[j] > 0) {
                            moves.Add((int)Card.IzzetBoilerworks);
                            break;
                        }
                    }
                    continue;
                }
                if (card == Card.TreasureCruise) {
                    int generic = Math.Max(0, 7 - graveyardFatestitchers - graveyardInventories - graveyardOther);
                    if (CanPay(manaDAG, new ManaCost(0, 1, 0, 0, generic))) {
                        moves.Add(i);
                    }
                    continue;
                }
                // If the card is a spell, make sure we can pay its cost.
                // TODO: Don't call Pay with the exact same cost multiple times in this function.
                if (i >= LAND_ETB_TAPPED.Length && !CanPay(manaDAG, MANA_COSTS[card])) {
                    continue;
                }
                moves.Add(i);
            }
            if (ascendancies > 0 && graveyardFatestitchers > 0 && CanPay(manaDAG, new ManaCost(0, 1, 0, 0, 0))) {
                // SIMPLIFICATION: Only unearth Fatestitchers with at least one Jeskai Ascendancy in play. They can otherwise be used to fix, but it's marginal.
                moves.Add(SPECIAL_MOVE_UNEARTH_FATESTITCHER);
            }
            moves.Add(SPECIAL_MOVE_END_TURN);
            return moves.ToArray();
        }
        // Returns 1 for deterministic moves, and the probability of the resultant state for stochastic moves.
        public ChanceEvent ExecuteMove(int move) {
            ChanceEvent chanceEvent = new ChanceEvent(0, 1);
            // Choices.
            if (choiceBounce) {
                // TODO: Float mana if bouncing untapped land.
                (tappedLands[move] > 0 ? tappedLands : untappedLands)[move]--;
                handQuantities[move]++;
                choiceBounce = false;
                return chanceEvent;
            } else if (choiceDiscard > 0) {
                while (move > 0) {
                    int cardIndex = move % CARD_ENUM_LENGTH;
                    Card card = (Card)cardIndex;
                    GoToGraveyard(card);
                    if (card == Card.ObsessiveSearch) {
                        choiceObsessive++;
                    }
                    handQuantities[cardIndex]--;
                    choiceDiscard--;
                    move /= CARD_ENUM_LENGTH;
                }
                if (choiceDiscard > 0) return chanceEvent; // We still have discards to make.
                if (choiceObsessive > 0) return chanceEvent; // We have Obsessive Search triggers on the stack.
                if (choiceObsessive == 0 && ascendancyTriggers == 0 && stack == Card.None) {
                    if (cleanupDiscard) {
                        cleanupDiscard = false;
                        Untap();
                        return Draw();
                    } else {
                        return chanceEvent;
                    }
                }
                // If a spell or Ascendancy trigger(s), was waiting for a Jeskai Ascendancy discard, we can go ahead and let it resolve now.
            } else if (choiceScry > 0) {
                if (move == 0) {
                    // Library is empty.
                }
                if (move == -1) {
                    bottomOfDeck.Enqueue(topOfDeck[0]);
                    topOfDeck.RemoveAt(0);
                }
                choiceScry = 0;
                if (postScryDraws > 0) {
                    ChanceEvent output = Draw(postScryDraws);
                    postScryDraws = 0;
                    return output;
                }
                return chanceEvent;
            } else if (choiceTop > 0) {
                handQuantities[move]--;
                topOfDeck.Insert(0, move);
                choiceTop--;
                return chanceEvent;
            } else if (choiceBottom > 0) {
                while (move > 0) {
                    int cardIndex = move % CARD_ENUM_LENGTH;
                    handQuantities[cardIndex]--;
                    bottomOfDeck.Enqueue(cardIndex);
                    move /= CARD_ENUM_LENGTH;
                    choiceBottom--;
                }
                return chanceEvent;
            } else if (choicePonder) {
                if (topOfDeck.Count < 2) {
                    // Deck is too small to reorder.
                } else if (topOfDeck.Count == 2) {
                    if (move == 1) {
                        int zero = topOfDeck[0];
                        topOfDeck[0] = topOfDeck[1];
                        topOfDeck[1] = zero;
                    }
                } else if (move == 6) {
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
            } else if (choiceObsessive > 0) {
                // SIMPLIFICATION: Resolve all madnessed Obsessive Searches before all Ascendancy triggers, even though in reality they can be interleaved.
                choiceObsessive--;
                if (move == 1) {
                    Pay(new ManaCost(0, 1, 0, 0, 0));
                    ascendancyTriggers += ascendancies;
                    // SIMPLIFICATION: Since the order of multiple cards drawn doesn't matter, future draws combining with this chance event will underestimate the event's true probability.
                    chanceEvent = Draw();
                }
                if (choiceObsessive == 0) {
                    if (cleanupDiscard) {
                        int cardsInHand = handQuantities.Sum();
                        if (cardsInHand > 7) {
                            choiceDiscard = cardsInHand - 7;
                            return chanceEvent;
                        }
                        // SIMPLIFICATION: Ignore Ascendancy triggers from cleanup-cast Obsessive Searches.
                        cleanupDiscard = false;
                        Untap();
                        return CombineEvents(chanceEvent, Draw());
                    }
                    if (ascendancyTriggers == 0 && stack == Card.None) return chanceEvent;
                } else return chanceEvent;
            } else if (move == SPECIAL_MOVE_END_TURN) {
                EndStep();
                int cardsInHand = handQuantities.Sum();
                if (cardsInHand > 7) {
                    choiceDiscard = cardsInHand - 7;
                    cleanupDiscard = true;
                    return chanceEvent;
                } else {
                    Untap();
                    return Draw();
                }
            } else if (move == SPECIAL_MOVE_FETCH_PLAINS || move == SPECIAL_MOVE_FETCH_ISLAND || move == SPECIAL_MOVE_FETCH_MOUNTAIN || move == SPECIAL_MOVE_FETCH_FOREST || move == SPECIAL_MOVE_FETCH_FAIL_TO_FIND) {
                Shuffle();
                landPlay = false;
                handQuantities[(int)Card.EvolvingWilds]--;
                GoToGraveyard(Card.EvolvingWilds);
                if (move == SPECIAL_MOVE_FETCH_PLAINS) {
                    shuffledLibraryCount--;
                    shuffledLibraryQuantities[(int)Card.Plains]--;
                    tappedLands[(int)Card.Plains]++;
                } else if (move == SPECIAL_MOVE_FETCH_ISLAND) {
                    shuffledLibraryCount--;
                    shuffledLibraryQuantities[(int)Card.Island]--;
                    tappedLands[(int)Card.Island]++;
                } else if (move == SPECIAL_MOVE_FETCH_MOUNTAIN) {
                    shuffledLibraryCount--;
                    shuffledLibraryQuantities[(int)Card.Mountain]--;
                    tappedLands[(int)Card.Mountain]++;
                } else if (move == SPECIAL_MOVE_FETCH_FOREST) {
                    shuffledLibraryCount--;
                    shuffledLibraryQuantities[(int)Card.Forest]--;
                    tappedLands[(int)Card.Forest]++;
                }
                return chanceEvent;
            } else if (move == SPECIAL_MOVE_UNEARTH_FATESTITCHER) {
                Pay(new ManaCost(0, 1, 0, 0, 0));
                graveyardFatestitchers--;
                untappedFatestitchers++;
                totalPower++;
                return chanceEvent;
            } else if (move < LAND_ETB_TAPPED.Length) {
                // Play a land.
                handQuantities[move]--;
                landPlay = false;
                Card land = (Card)move;
                if (move <= 4) {
                    // Basic land.
                    tappedLands[move]++;
                    if (land == Card.Plains) whiteMana++;
                    else if (land == Card.Island) blueMana++;
                    else if (land == Card.Mountain) redMana++;
                    else greenMana++;
                } else {
                    // Nonbasic land.
                    (LAND_ETB_TAPPED[move] ? tappedLands : untappedLands)[move]++;
                    if (land == Card.IzzetBoilerworks) {
                        choiceBounce = true;
                    } else if (land == Card.VividCreek) {
                        vividCounters += 2;
                    }
                }
                return chanceEvent;
            } else {
                // Cast a spell from hand.
                handQuantities[move]--;
                stack = (Card)move;
                ascendancyTriggers = ascendancies;
                if (stack == Card.TreasureCruise) {
                    // Delve.
                    // SIMPLIFICATION: Always delve the max amount, preserving Fatestitchers, then Frantic Inventories.
                    int generic = 7;
                    int min = Math.Min(generic, graveyardOther);
                    graveyardOther -= min;
                    exiledOther += min;
                    generic -= min;
                    min = Math.Min(generic, graveyardInventories);
                    graveyardInventories -= min;
                    exiledOther += min;
                    generic -= min;
                    min = Math.Min(generic, graveyardFatestitchers);
                    graveyardFatestitchers -= min;
                    exiledFatestitchers += min;
                    generic -= min;
                    Pay(new ManaCost(0, 1, 0, 0, generic));
                } else {
                    ManaCost cost = MANA_COSTS[stack];
                    Pay(cost);
                }
            }
            // Stack resolving.
            if (ascendancyTriggers > 0) {
                return CombineEvents(chanceEvent, ResolveAscendancyTrigger());
            }
            Card spell = stack;
            stack = Card.None;
            if (spell != Card.JeskaiAscendancy) {
                // SIMPLIFICATION: Instants and sorceries go to the graveyard when cast instead of when resolving. We don't cast anything relevant at instant speed anyway.
                GoToGraveyard(spell);
            }
            switch (spell) {
                case Card.Brainstorm:
                    choiceTop = 2;
                    return CombineEvents(chanceEvent, Draw(3));
                case Card.CeruleanWisps:
                    if (tappedFatestitchers > 0) {
                        tappedFatestitchers--;
                        untappedFatestitchers++;
                    } else {
                        UntapLands(1);
                    }
                    return CombineEvents(chanceEvent, Draw());
                case Card.FranticInventory:
                    return CombineEvents(chanceEvent, Draw(graveyardInventories));
                case Card.FranticSearch:
                    UntapLands(3);
                    choiceDiscard = 2;
                    return CombineEvents(chanceEvent, Draw(3));
                case Card.GitaxianProbe:
                    return CombineEvents(chanceEvent, Draw());
                case Card.JeskaiAscendancy:
                    ascendancies++;
                    return chanceEvent;
                case Card.ObsessiveSearch:
                    return CombineEvents(chanceEvent, Draw());
                case Card.Opt:
                    choiceScry = 1;
                    postScryDraws = 1;
                    return CombineEvents(chanceEvent, RevealTop(1, false));
                case Card.Ponder:
                    choicePonder = true;
                    return CombineEvents(chanceEvent, RevealTop(3, false));
                case Card.TreasureCruise:
                    return CombineEvents(chanceEvent, Draw(3));
                default:
                    throw new Exception("Unhandled spell resolving: " + CARD_NAMES[stack]);
            }
        }

        public bool IsWon() {
            // SIMPLIFICATION: If we've gone off to the point of having 20 power of Fatestitchers, we can presumably tap down any blockers and win...?
            return totalPower >= 20;
        }
        public bool IsLost() {
            return exiledFatestitchers == decklist[Card.Fatestitcher] || deckedOut;
        }

        ChanceEvent Draw() {
            if (topOfDeck.Count == 0 && shuffledLibraryCount == 0 && bottomOfDeck.Count == 0) {
                deckedOut = true;
                return new ChanceEvent(0, 1);
            }
            float probability;
            int i;
            if (topOfDeck.Count > 0) {
                i = topOfDeck[0];
                topOfDeck.RemoveAt(0);
                probability = 1;
            } else if (shuffledLibraryCount == 0) {
                i = bottomOfDeck.Dequeue();
                probability = 1;
            } else {
                i = RandomIndexFromDeck();
                probability = shuffledLibraryQuantities[i] / (float)shuffledLibraryCount;
                shuffledLibraryCount--;
                shuffledLibraryQuantities[i]--;
            }
            handQuantities[i]++;
            return new ChanceEvent(i, probability);
        }
        ChanceEvent Draw(int n) {
            if (n == 1) return Draw();
            if (topOfDeck.Count + shuffledLibraryCount + bottomOfDeck.Count < n) {
                deckedOut = true;
                return new ChanceEvent(0, 1);
            }

            int[] cardIDs = new int[n];
            float totalProbability = 1;
            int topDecks = 0;
            for (int i = 0; i < n; i++) {
                ChanceEvent drawEvent = Draw();
                cardIDs[i] = drawEvent.Item1;
                totalProbability *= drawEvent.Item2;
                if (drawEvent.Item2 < 1) {
                    topDecks++;
                }
            }
            Array.Sort(cardIDs);
            int eventID = 0;
            for (int i = 0; i < n; i++) {
                eventID = eventID * CARD_ENUM_LENGTH + cardIDs[i];
            }
            return new ChanceEvent(eventID, totalProbability * N_FACTORIAL[topDecks]);
        }
        ChanceEvent RevealTop(int n, bool orderMatters) {
            n = Math.Min(n, topOfDeck.Count + shuffledLibraryCount + bottomOfDeck.Count);
            if (!orderMatters && n > 1) return RevealTopNoOrder(n);
            int eventID = 0;
            float probability = 1;
            while (topOfDeck.Count < n) {
                if (shuffledLibraryCount == 0) {
                    topOfDeck.Add(bottomOfDeck.Dequeue());
                } else {
                    int i = RandomIndexFromDeck();
                    eventID = (eventID * CARD_ENUM_LENGTH) + i;
                    probability *= shuffledLibraryQuantities[i] / (float)shuffledLibraryCount;
                    shuffledLibraryCount--;
                    shuffledLibraryQuantities[i]--;
                    topOfDeck.Add(i);
                }
            }
            return new ChanceEvent(eventID, probability);
        }
        ChanceEvent RevealTopNoOrder(int n) {
            float probability = 1;
            while (topOfDeck.Count < n) {
                if (shuffledLibraryCount == 0) {
                    topOfDeck.Add(bottomOfDeck.Dequeue());
                } else {
                    int i = RandomIndexFromDeck();
                    probability *= shuffledLibraryQuantities[i] / (float)shuffledLibraryCount;
                    shuffledLibraryCount--;
                    shuffledLibraryQuantities[i]--;
                    topOfDeck.Add(i);
                }
            }
            // Sort the top n cards of the deck and calculate the event ID.
            int[] cardIDs = topOfDeck.Take(n).ToArray();
            Array.Sort(cardIDs);
            int eventID = 0;
            for (int i = 0; i < n; i++) {
                eventID = eventID * CARD_ENUM_LENGTH + cardIDs[i];
            }
            return new ChanceEvent(eventID, probability * N_FACTORIAL[n]);
        }
        int RandomIndexFromDeck() {
            int selector = StaticRandom.Next(shuffledLibraryCount);
            int i = 1;
            for (; selector >= shuffledLibraryQuantities[i]; i++) {
                selector -= shuffledLibraryQuantities[i];
            }
            return i;
        }

        void EndStep() {
            whiteMana = tappedLands[(int)Card.Plains];
            blueMana = tappedLands[(int)Card.Island] + tappedLands[(int)Card.IzzetBoilerworks];
            redMana = tappedLands[(int)Card.Mountain] + tappedLands[(int)Card.IzzetBoilerworks];
            greenMana = tappedLands[(int)Card.Forest];
            exiledFatestitchers += untappedFatestitchers;
            exiledFatestitchers += tappedFatestitchers;
            untappedFatestitchers = 0;
            tappedFatestitchers = 0;
            totalPower = 0;
        }
        void Untap() {
            turn++;
            landPlay = true;
            // Untap only lands with a choice of mana to produce.
            for (int i = 6; i < untappedLands.Length; i++) {
                untappedLands[i] += tappedLands[i];
                tappedLands[i] = 0;
            }
            Debug.Assert(untappedLands[(int)Card.Plains] == 0);
            Debug.Assert(untappedLands[(int)Card.Island] == 0);
            Debug.Assert(untappedLands[(int)Card.Mountain] == 0);
            Debug.Assert(untappedLands[(int)Card.Forest] == 0);
        }
        void GoToGraveyard(Card card) {
            if (card == Card.Fatestitcher) {
                graveyardFatestitchers++;
            } else if (card == Card.FranticInventory) {
                graveyardInventories++;
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
        ChanceEvent ResolveAscendancyTrigger() {
            ascendancyTriggers--;
            UntapLands(untappedFatestitchers);
            untappedFatestitchers += tappedFatestitchers;
            tappedFatestitchers = 0;
            totalPower += untappedFatestitchers;
            choiceDiscard = 1;
            return Draw(); // SIMPLIFICATION: Always loot.
        }

        // SIMPLIFICATION: Pay costs heuristically instead of including in the MCTS.
        // SIMPLIFICATION: Including filter lands would require a search of some kind in here, so... let's not for now.
        // Plus, weird situations like paying {U} with Plains and Mystic Gate, leaving a choice of mana to float.
        const int MANADAG_TOTAL = 0, MANADAG_BLUE = 1, MANADAG_WHITE = 2, MANADAG_RED = 3, MANADAG_GREEN = 4, MANADAG_BLUE_TO_WHITE = 5, MANADAG_BLUE_TO_RED = 6, MANADAG_BLUE_TO_GREEN = 7, MANADAG_WHITE_TO_RED = 8, MANADAG_WHITE_TO_GREEN = 9, MANADAG_RED_TO_GREEN = 10;
        const int MANADAG_UWR = 11;
        static readonly int[] MANADAG_LANDS_BLUE = new Card[] { Card.MeanderingRiver, Card.HighlandLake, Card.MysticMonastery, Card.VividCreek }.Cast<int>().ToArray();
        static readonly int[] MANADAG_LANDS_BLUE_TO_WHITE = new Card[] { Card.MeanderingRiver, Card.MysticMonastery, Card.VividCreek }.Cast<int>().ToArray();
        static readonly int[] MANADAG_LANDS_BLUE_TO_RED = new Card[] { Card.HighlandLake, Card.MysticMonastery, Card.VividCreek }.Cast<int>().ToArray();
        static readonly int[] MANADAG_LANDS_BLUE_TO_GREEN = new Card[] { Card.VividCreek }.Select(l => (int)l).ToArray();
        static readonly int[] MANADAG_LANDS_UWR = new Card[] { Card.MysticMonastery, Card.VividCreek }.Select(l => (int)l).ToArray();
        int[] CreateManaDAG() {
            // Construct a directed acyclic graph that represents how much mana of each color is available, dependent on other colors.
            int[] dag = new int[] {
                /* max total mana */ -1, // (to be calculated)
                /* max blue mana  */ blueMana + MANADAG_LANDS_BLUE.Select(i => untappedLands[i]).Sum(),
                /* max white mana */ whiteMana,
                /* max red mana   */ redMana,
                /* max green mana */ greenMana,
                /* blue that can be converted to white  */ MANADAG_LANDS_BLUE_TO_WHITE.Select(i => untappedLands[i]).Sum(),
                /* blue that can be converted to red    */ MANADAG_LANDS_BLUE_TO_RED.Select(i => untappedLands[i]).Sum(),
                /* blue that can be converted to green  */ MANADAG_LANDS_BLUE_TO_GREEN.Select(i => untappedLands[i]).Sum(),
                /* white that can be converted to red   */ 0,
                /* white that can be converted to green */ 0,
                /* red that can be converted to green   */ 0,
                /* blue that can be converted to white or red */ MANADAG_LANDS_UWR.Select(i => untappedLands[i]).Sum(), // edges between edges, yikes. we're not in DAGsas anymore...
            };
            if (untappedFatestitchers > 0) {
                dag[MANADAG_BLUE] += untappedFatestitchers;
                bool fatestitcherWhite = false, fatestitcherRed = false;
                if (dag[MANADAG_WHITE] > 0 || dag[MANADAG_BLUE_TO_WHITE] > 0) {
                    dag[MANADAG_BLUE_TO_WHITE] += untappedFatestitchers;
                    fatestitcherWhite = true;
                }
                if (dag[MANADAG_RED] > 0 || dag[MANADAG_BLUE_TO_RED] > 0 || dag[MANADAG_WHITE_TO_RED] > 0) {
                    dag[MANADAG_BLUE_TO_RED] += untappedFatestitchers;
                    fatestitcherRed = true;
                }
                if (dag[MANADAG_GREEN] > 0 || dag[MANADAG_BLUE_TO_GREEN] > 0 || dag[MANADAG_WHITE_TO_GREEN] > 0 || dag[MANADAG_RED_TO_GREEN] > 0) {
                    dag[MANADAG_BLUE_TO_GREEN] += untappedFatestitchers;
                }
                if (fatestitcherWhite && fatestitcherRed) {
                    dag[MANADAG_UWR] += untappedFatestitchers;
                }
            }
            dag[MANADAG_TOTAL] = dag[MANADAG_BLUE] + dag[MANADAG_WHITE] + dag[MANADAG_RED] + dag[MANADAG_GREEN];
            return dag;
        }
        bool CanPay(int[] dag, ManaCost cost) {
            if (cost.Item1 + cost.Item2 + cost.Item3 + cost.Item4 + cost.Item5 > dag[MANADAG_TOTAL]) return false;
            // Clone the incoming mana DAG since it may be destructively used multiple times in each GetMoves() call.
            dag = dag.Clone() as int[];
            DAGTransfer(dag, MANADAG_RED, MANADAG_GREEN, MANADAG_RED_TO_GREEN, cost.Item4);
            DAGTransfer(dag, MANADAG_WHITE, MANADAG_GREEN, MANADAG_WHITE_TO_GREEN, cost.Item4);
            DAGTransfer(dag, MANADAG_BLUE, MANADAG_GREEN, MANADAG_BLUE_TO_GREEN, cost.Item4);
            if (dag[MANADAG_GREEN] < cost.Item4) return false;
            DAGTransfer(dag, MANADAG_WHITE, MANADAG_RED, MANADAG_WHITE_TO_RED, cost.Item3);
            DAGTransfer(dag, MANADAG_BLUE, MANADAG_RED, MANADAG_BLUE_TO_RED, cost.Item3, MANADAG_BLUE_TO_WHITE, MANADAG_UWR);
            if (dag[MANADAG_RED] < cost.Item3) return false;
            DAGTransfer(dag, MANADAG_BLUE, MANADAG_WHITE, MANADAG_BLUE_TO_WHITE, cost.Item1);
            if (dag[MANADAG_WHITE] < cost.Item1) return false;
            return dag[MANADAG_BLUE] >= cost.Item2;
        }
        static void DAGTransfer(int[] dag, int a, int b, int aToB, int amount, int dependency = -1, int dependencyCount = -1) {
            amount = Math.Min(amount - dag[b], dag[aToB]);
            if (amount <= 0) return;
            dag[a] -= amount;
            dag[b] += amount;
            // TODO: Find a better way to represent trilands (we can have up to two dependencies with green involved).
            if (dependency > -1) {
                dag[dependency] -= Math.Min(amount, dependencyCount);
            }
        }
        void Pay(ManaCost cost) {
            int[] dag = CreateManaDAG();
            int genericCost = cost.Item5;
            // TODO: Paying green, and a more general solution overall with less repeated code.
            // Spend red.
            int redCost = cost.Item3;
            int redFromPool = Math.Min(redMana, redCost);
            redMana -= redFromPool;
            redCost -= redFromPool;
            TapN(MANADAG_LANDS_BLUE_TO_RED, redCost);
            redFromPool = Math.Min(redMana, genericCost);
            redMana -= redFromPool;
            genericCost -= redFromPool;
            // Spend white.
            int whiteCost = cost.Item1;
            int whiteFromPool = Math.Min(whiteMana, whiteCost);
            whiteMana -= whiteFromPool;
            whiteCost -= whiteFromPool;
            TapN(MANADAG_LANDS_BLUE_TO_WHITE, whiteCost);
            whiteFromPool = Math.Min(whiteMana, genericCost);
            whiteMana -= whiteFromPool;
            genericCost -= whiteFromPool;
            // Spend blue for blue and generic.
            int blueAndGeneric = cost.Item2 + genericCost;
            int blueFromPool = Math.Min(blueMana, blueAndGeneric);
            blueMana -= blueFromPool;
            TapN(MANADAG_LANDS_BLUE, blueAndGeneric - blueFromPool);
#if DEBUG
            int[] newDAG = CreateManaDAG();
            int newTotal = newDAG[MANADAG_TOTAL];
            Debug.Assert(newTotal == dag[MANADAG_TOTAL] - cost.Item1 - cost.Item2 - cost.Item3 - cost.Item4 - cost.Item5);
#endif
        }
        void DAGTransferWithTap(int[] landIndices, int[] dag, int a, int b, int aToB, int amount) {
            amount = Math.Min(amount - dag[b], dag[aToB]);
            if (amount == 0) return;
            TapN(landIndices, amount);
            dag[a] -= amount;
            dag[b] += amount;
        }
        void TapN(int[] landIndices, int n) {
            int i = 0;
            while (n > 0) {
                while (i < landIndices.Length && untappedLands[landIndices[i]] == 0) i++;
                if (i == landIndices.Length) break;
                int landIndex = landIndices[i];
                int tapped = Math.Min(n, untappedLands[landIndex]);
                untappedLands[landIndex] -= tapped;
                tappedLands[landIndex] += tapped;
                if (landIndex == (int)Card.VividCreek) vividCounters -= tapped;
                n -= tapped;
            }
            Debug.Assert(n <= untappedFatestitchers, "Can't pay for the rest with Fatestitchers.");
            untappedFatestitchers -= n;
            tappedFatestitchers += n;
        }

        static readonly Card[] UNTAP_BLUE = new Card[] { Card.IzzetBoilerworks, Card.MysticMonastery, Card.VividCreek, Card.MeanderingRiver, Card.HighlandLake }; // TODO: Prioritize Vivid Creek over Mystic Monastery when green is added.
        void UntapLands(int n) {
            n = Math.Min(n, untappedLands.Sum() + tappedLands.Sum());
            // Untap blue lands.
            for (int i = 0; i < UNTAP_BLUE.Length && n > 0; i++) {
                int landIndex = (int)UNTAP_BLUE[i];
                int toUntap = Math.Min(n, tappedLands[landIndex]);
                tappedLands[landIndex] -= toUntap;
                untappedLands[landIndex] += toUntap;
                n -= toUntap;
            }
            if (n == 0) return;
            int extraBlue = Math.Min(n, UNTAP_BLUE.Select(l => untappedLands[(int)l]).Sum() + tappedLands[(int)Card.Island]);
            blueMana += extraBlue;
            n -= extraBlue;
            if (n == 0) return;
            // Untap nonblue basics.
            while (n > 0) {
                if (tappedLands[(int)Card.Plains] > 0) {
                    whiteMana++;
                    n--;
                }
                if (n == 0) return;
                if (tappedLands[(int)Card.Mountain] > 0) {
                    redMana++;
                    n--;
                }
                if (n == 0) return;
                if (tappedLands[(int)Card.Forest] > 0) {
                    greenMana++;
                    n--;
                }
            }
            // TODO: Float/untap for Jeskai Ascendancy.
        }

        static int[] MAX_CARD_ENUM_VALUE_POWERS = new int[] { 1, 1, CARD_ENUM_LENGTH,
                                                                    CARD_ENUM_LENGTH * CARD_ENUM_LENGTH,
                                                                    CARD_ENUM_LENGTH * CARD_ENUM_LENGTH * CARD_ENUM_LENGTH,
                                                                    CARD_ENUM_LENGTH * CARD_ENUM_LENGTH * CARD_ENUM_LENGTH * CARD_ENUM_LENGTH,
                                                                    CARD_ENUM_LENGTH * CARD_ENUM_LENGTH * CARD_ENUM_LENGTH * CARD_ENUM_LENGTH * CARD_ENUM_LENGTH };
        static ChanceEvent CombineEvents(ChanceEvent one, ChanceEvent two) {
            int logTwo = 0;
            while (two.Item1 >= MAX_CARD_ENUM_VALUE_POWERS[logTwo]) logTwo++;
            int combinedID = one.Item1 * MAX_CARD_ENUM_VALUE_POWERS[logTwo + 1] + two.Item1;
            return new ChanceEvent(combinedID, one.Item2 * two.Item2);
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            if (IsWon()) {
                sb.AppendLine(string.Format("TURN {0}: WIN!", turn));
            } else {
                sb.AppendLine(string.Format("TURN {0}", turn));
            }
            List<string> tokens = new List<string>();
            // Hand.
            for (int i = 1; i < handQuantities.Length; i++) {
                if (handQuantities[i] > 0) {
                    tokens.Add(string.Format("{0} {1}", handQuantities[i], CARD_NAMES[(Card)i]));
                }
            }
            sb.Append("Hand: ");
            sb.AppendLine(string.Join(", ", tokens));
            tokens.Clear();
            // Stack.
            if (stack != Card.None) tokens.Add(CARD_NAMES[stack]);
            if (ascendancyTriggers == 1) tokens.Add("1 Jeskai Ascendancy trigger");
            else if (ascendancyTriggers > 1) tokens.Add(ascendancyTriggers + " Jeskai Ascendancy triggers");
            if (tokens.Count > 0) {
                sb.AppendLine("Stack: " + string.Join(", ", tokens));
                tokens.Clear();
            }
            // Battlefield.
            if (ascendancies == 1) tokens.Add("1 Jeskai Ascendancy");
            else if (ascendancies > 1) tokens.Add(ascendancies + " Jeskai Ascendancies");
            if (untappedFatestitchers == 1) tokens.Add("1 untapped Fatestitcher");
            else if (untappedFatestitchers > 1) tokens.Add(untappedFatestitchers + " untapped Fatestitchers");
            if (tappedFatestitchers == 1) tokens.Add("1 tapped Fatestitcher");
            else if (tappedFatestitchers > 1) tokens.Add(untappedFatestitchers + " tapped Fatestitchers");
            if (totalPower > 0) tokens.Add("TOTAL POWER: " + totalPower);
            if (tokens.Count > 0) {
                sb.AppendLine("Battlefield: " + string.Join(", ", tokens));
                tokens.Clear();
            }
            if (whiteMana > 0) tokens.Add(whiteMana + "W");
            if (blueMana > 0) tokens.Add(blueMana + "U");
            if (redMana > 0) tokens.Add(redMana + "R");
            if (greenMana > 0) tokens.Add(greenMana + "G");
            if (tokens.Count > 0) {
                sb.AppendLine("Mana pool: " + string.Join(", ", tokens));
                tokens.Clear();
            }
            for (int i = 1; i < untappedLands.Length; i++) {
                if (untappedLands[i] > 0) {
                    tokens.Add(string.Format("{0} {1}", untappedLands[i], CARD_NAMES[(Card)i]));
                }
            }
            if (tokens.Count > 0) {
                sb.AppendLine("Untapped lands: " + string.Join(", ", tokens));
                tokens.Clear();
            }
            for (int i = 1; i < tappedLands.Length; i++) {
                if (tappedLands[i] > 0) {
                    tokens.Add(string.Format("{0} {1}", tappedLands[i], CARD_NAMES[(Card)i]));
                }
            }
            if (tokens.Count > 0) {
                sb.AppendLine("Tapped lands: " + string.Join(", ", tokens));
                tokens.Clear();
            }
            // Library.
            if (topOfDeck.Count > 0) {
                sb.AppendLine("Top of library: " + string.Join(", ", topOfDeck.Select(i => CARD_NAMES[(Card)i])));
            }
            if (shuffledLibraryCount > 0) {
                for (int i = 1; i < shuffledLibraryQuantities.Length; i++) {
                    if (shuffledLibraryQuantities[i] > 0) {
                        tokens.Add(string.Format("{0} {1}", shuffledLibraryQuantities[i], CARD_NAMES[(Card)i]));
                    }
                }
                sb.Append((topOfDeck.Count > 0 || bottomOfDeck.Count > 0) ? "Library (shuffled portion): " : "Library: ");
                sb.Append(string.Join(", ", tokens));
                sb.AppendLine(string.Format(" ({0} {1})", shuffledLibraryCount, shuffledLibraryCount == 1 ? "card" : "cards"));
                tokens.Clear();
            }
            if (bottomOfDeck.Count > 0) {
                sb.AppendLine("Bottom of library: " + string.Join(", ", bottomOfDeck.Select(i => CARD_NAMES[(Card)i])));
            }
            // Graveyard.
            if (graveyardFatestitchers > 0 || graveyardInventories > 0 || graveyardOther > 0) {
                if (graveyardFatestitchers > 0) tokens.Add(graveyardFatestitchers + " Fatestitcher");
                if (graveyardInventories > 0) tokens.Add(graveyardInventories + " Frantic Inventory");
                if (graveyardOther > 0) tokens.Add(graveyardOther + ((graveyardFatestitchers > 0 || graveyardInventories > 0) ? " other" : graveyardOther == 1 ? " card" : " cards"));
                sb.AppendLine("Graveyard: " + string.Join(", ", tokens));
                tokens.Clear();
            }
            return sb.ToString();
        }
        public string MoveToString(int move) {
            if (choiceBounce) {
                return string.Format("Return {0} to hand.", CARD_NAMES[(Card)move]);
            }
            if (choiceDiscard > 0) {
                List<string> tokens = new List<string>();
                while (move > 0) {
                    tokens.Add(CARD_NAMES[(Card)(move % CARD_ENUM_LENGTH)]);
                    move /= CARD_ENUM_LENGTH;
                }
                return string.Format("Discard {0}.", string.Join(", ", tokens));
            }
            if (choiceScry > 0) {
                if (topOfDeck.Count == 0) return "Scry no-op (no cards in library).";
                return string.Format("Scry {0} to the {1}.", CARD_NAMES[(Card)topOfDeck[0]], move == -1 ? "bottom" : "top");
            }
            if (choiceTop > 0) {
                return string.Format("Brainstorm: {0} on top.", CARD_NAMES[(Card)move]);
            }
            if (choiceBottom > 0) {
                return string.Format("London mulligan: {0} on bottom.", CARD_NAMES[(Card)move]);
            }
            if (choicePonder) {
                if (topOfDeck.Count == 0) return "Ponder: no-op (no cards in library).";
                if (topOfDeck.Count == 1) return "Ponder: no-op (one card in library).";
                if (topOfDeck.Count == 2) return move == 0 ? string.Format("Ponder: {0} on top, then {1}.", CARD_NAMES[(Card)topOfDeck[0]], CARD_NAMES[(Card)topOfDeck[1]]) :
                                                             string.Format("Ponder: {0} on top, then {1}.", CARD_NAMES[(Card)topOfDeck[1]], CARD_NAMES[(Card)topOfDeck[0]]);
                if (move == 6) return "Ponder: shuffle.";
                int[] ponderOrder = PONDER_ORDERS[move];
                Card zero = (Card)topOfDeck[ponderOrder[0]], one = (Card)topOfDeck[ponderOrder[1]], two = (Card)topOfDeck[ponderOrder[2]];
                return string.Format("Ponder: {0} on top, then {1}, then {2}.", CARD_NAMES[zero], CARD_NAMES[one], CARD_NAMES[two]);
            }
            if (choiceObsessive > 0) {
                return move == 1 ? "Cast Obsessive Search with madness." : "Decline to cast Obsessive Search.";
            }
            if (move == SPECIAL_MOVE_FETCH_PLAINS) {
                return "Play Evolving Wilds, crack, fetch a tapped Plains.";
            }
            if (move == SPECIAL_MOVE_FETCH_ISLAND) {
                return "Play Evolving Wilds, crack, fetch a tapped Island.";
            }
            if (move == SPECIAL_MOVE_FETCH_MOUNTAIN) {
                return "Play Evolving Wilds, crack, fetch a tapped Mountain.";
            }
            if (move == SPECIAL_MOVE_FETCH_FOREST) {
                return "Play Evolving Wilds, crack, fetch a tapped Forest.";
            }
            if (move == SPECIAL_MOVE_FETCH_FAIL_TO_FIND) {
                return "Play Evolving Wilds, crack, fail to find.";
            }
            if (move == SPECIAL_MOVE_UNEARTH_FATESTITCHER) {
                return "Unearth a Fatestitcher.";
            }
            if (move == SPECIAL_MOVE_END_TURN) {
                return "End the turn.";
            }
            return string.Format("{0} {1}.", move < LAND_ETB_TAPPED.Length ? "Play" : "Cast", CARD_NAMES[(Card)move]);
        }
        public void SanityCheck() {
            Debug.Assert(shuffledLibraryCount == shuffledLibraryQuantities.Sum(), "Shuffled library count has not been updated correctly.");
            int totalCards = topOfDeck.Count + shuffledLibraryCount + bottomOfDeck.Count + handQuantities.Sum() + untappedLands.Sum() + tappedLands.Sum() + ascendancies + untappedFatestitchers + tappedFatestitchers + graveyardFatestitchers + graveyardInventories + graveyardOther + exiledOther;
            if (stack != Card.None) totalCards++;
            Debug.Assert(totalCards == 60, string.Format("Total cards in the state is {0}, not 60!\n{1}", totalCards, this));
            Debug.Assert(untappedLands.All(n => n >= 0), "Negative untapped land count.");
            Debug.Assert(tappedLands.All(n => n >= 0), "Negative tapped land count.");
            Debug.Assert(handQuantities.All(n => n >= 0), "Negative hand quantity.");
            Debug.Assert(shuffledLibraryQuantities.All(n => n >= 0), "Negative library quantity.");
            Debug.Assert(new int[] { whiteMana, blueMana, redMana, greenMana }.All(n => n >= 0), "Negative mana in pool.");
            Debug.Assert(untappedFatestitchers >= 0, "Negative untapped Fatestitchers.");
            Debug.Assert(tappedFatestitchers >= 0, "Negative tapped Fatestitchers.");
            Debug.Assert(vividCounters >= 0, "Negative vivid counters.");
        }
    }

    public enum Card {
        None,
        // autotap lands
        Plains,
        Island,
        Mountain,
        Forest,
        IzzetBoilerworks,
        // manual tap lands
        MeanderingRiver,
        HighlandLake,
        MysticMonastery,
        VividCreek,
        // fetch lands
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
