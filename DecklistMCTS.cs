using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    public class DecklistMCTS {
        public static Dictionary<Card, Tuple<int, int>> CARD_QUANTITY_LIMITS = new Dictionary<Card, Tuple<int, int>>() {
            { Card.Plains,           new Tuple<int, int>(0, 6)  },
            { Card.Island,           new Tuple<int, int>(0, 20) },
            { Card.Mountain,         new Tuple<int, int>(0, 6)  },
            { Card.Forest,           new Tuple<int, int>(0, 0)  },
            { Card.IzzetBoilerworks, new Tuple<int, int>(0, 4)  },
            { Card.MeanderingRiver,  new Tuple<int, int>(0, 12)  }, // could go up to 16: Azorius Guildgate, Sejiri Refuge, Tranquil Cove
            { Card.HighlandLake,     new Tuple<int, int>(0, 12)  }, // could go up to 16: Izzet Guildgate, Swiftwater Cliffs, Wandering Fumarole
            { Card.MysticMonastery,  new Tuple<int, int>(0, 4)  },
            { Card.VividCreek,       new Tuple<int, int>(0, 4)  },
            { Card.EvolvingWilds,    new Tuple<int, int>(0, 8)  }, // Terramorphic Expanse
            { Card.Brainstorm,       new Tuple<int, int>(0, 4)  },
            { Card.CeruleanWisps,    new Tuple<int, int>(0, 4)  },
            { Card.DesperateRavings, new Tuple<int, int>(0, 4)  },
            { Card.Fatestitcher,     new Tuple<int, int>(4, 4)  },
            { Card.FranticInventory, new Tuple<int, int>(0, 4)  },
            { Card.FranticSearch,    new Tuple<int, int>(0, 4)  },
            { Card.GitaxianProbe,    new Tuple<int, int>(0, 4)  },
            { Card.IdeasUnbound,     new Tuple<int, int>(0, 4)  },
            { Card.IzzetCharm,       new Tuple<int, int>(0, 4)  },
            { Card.JeskaiAscendancy, new Tuple<int, int>(4, 4)  },
            { Card.MagmaticInsight,  new Tuple<int, int>(0, 4)  },
            { Card.ObsessiveSearch,  new Tuple<int, int>(0, 4)  },
            { Card.OmenOfTheSea,     new Tuple<int, int>(0, 4)  },
            { Card.Opt,              new Tuple<int, int>(0, 4)  },
            { Card.Ponder,           new Tuple<int, int>(0, 4)  },
            { Card.ThinkTwice,       new Tuple<int, int>(0, 4)  },
            { Card.ToArms,           new Tuple<int, int>(0, 4)  },
            { Card.TreasureCruise,   new Tuple<int, int>(0, 4)  },
            { Card.VisionSkeins,     new Tuple<int, int>(0, 4)  },
            { Card.WitchingWell,     new Tuple<int, int>(0, 4)  },
        };

        public Dictionary<Card, int> startingDecklist;
        int rollouts;
        public HashSet<int> startingAdditions, startingDeletions;
        DecklistMCTSNode root;

        public DecklistMCTS(Dictionary<Card, int> startingDecklist, int rollouts) {
            this.startingDecklist = startingDecklist;
            this.rollouts = rollouts;
            startingAdditions = new HashSet<int>();
            startingDeletions = new HashSet<int>();
            foreach (var kvp in CARD_QUANTITY_LIMITS) {
                Card card = kvp.Key;
                if (!startingDecklist.ContainsKey(card)) {
                    if (kvp.Value.Item2 > 0) {
                        startingAdditions.Add((int)card);
                    }
                } else {
                    if (startingDecklist[card] > kvp.Value.Item1) {
                        startingDeletions.Add(-(int)card);
                    }
                    if (startingDecklist[card] < kvp.Value.Item2) {
                        startingAdditions.Add((int)card);
                    }
                }
            }
            root = new DecklistMCTSNode(null, 0);
        }
        public DecklistMCTS(DMCTSSaveState save, int rollouts) {
            startingDecklist = save.decklist;
            this.rollouts = rollouts;
            startingAdditions = new HashSet<int>(save.additions);
            startingDeletions = new HashSet<int>(save.deletions);
            root = new DecklistMCTSNode(null, save.lastMove);
        }

        public void Rollout(int n) {
            while (root.rollouts < n) {
                Rollout();
            }
        }
        public void Rollout() {
            Dictionary<Card, int> decklist = new Dictionary<Card, int>(startingDecklist);
            HashSet<int> additions = new HashSet<int>(startingAdditions);
            HashSet<int> deletions = new HashSet<int>(startingDeletions);
            // Selection + expansion.
            DecklistMCTSNode current = root;
            DecklistMCTSNode next = null;
            while (true) {
                if (current.parent != null && current.change == 0) {
                    // Terminal node.
                    break;
                }
                lock (current) {
                    current.rollouts++;
                    // First expansion.
                    if (current.children == null) {
                        if (current.change >= 0) {
                            current.children = new DecklistMCTSNode[deletions.Count + 1]; // The extra child is the first, "null change" child.
                            current.children[0] = new DecklistMCTSNode(current, 0);
                            next = current.children[0];
                            break;
                        }
                        current.children = new DecklistMCTSNode[additions.Count];
                        int addition = additions.ElementAt(StaticRandom.Next(additions.Count));
                        current.children[0] = new DecklistMCTSNode(current, addition);
                        next = current.children[0];
                        ChangeDecklist(decklist, next.change, additions, deletions);
                    } else if (current.children[current.children.Length - 1] == null) {
                        // Expansion.
                        HashSet<int> changes = current.change < 0 ? additions : deletions;
                        int i = 0;
                        for (; current.children[i] != null; i++) {
                            changes.Remove(current.children[i].change);
                        }
                        int change = changes.ElementAt(StaticRandom.Next(changes.Count));
                        current.children[i] = new DecklistMCTSNode(current, change);
                        next = current.children[i];
                        ChangeDecklist(decklist, next.change, additions, deletions);
                        // SIMPLIFICATION: Once a copy of a card has been added in a subtree, copies of that card cannot be removed within the same subtree.
                        // This can get us caught in local maxima but reduces branching and transpositions.
                        HashSet<int> reverseChanges = next.change < 0 ? deletions : additions;
                        reverseChanges.Remove(-next.change);
                    } else {
                        // Selection.
                        next = current.Select();
                        ChangeDecklist(decklist, next.change, additions, deletions);
                        HashSet<int> reverseChanges = next.change < 0 ? deletions : additions;
                        reverseChanges.Remove(-next.change);
                    }
                }
                current = next;
                current.rollouts++;
            }
            current = next;
            current.rollouts++;
            // Simulation.
            int turns = Simulation.RunGame(decklist, rollouts);
            float reward = turns == -1 ? 0 : Simulation.REWARDS[turns];
            // Backpropagation.
            while (current != null) {
                lock (current) {
                    current.totalReward += reward;
                }
                current = current.parent;
            }
        }
        static void ChangeDecklist(Dictionary<Card, int> decklist, int change, HashSet<int> additions, HashSet<int> deletions) {
            if (change == 0) return;
            Card card = (Card)(change < 0 ? -change : change);
            if (change > 0) {
                if (decklist.ContainsKey(card)) {
                    decklist[card]++;
                } else {
                    decklist[card] = 1;
                }
                if (decklist[card] == CARD_QUANTITY_LIMITS[card].Item2) {
                    additions.Remove(change);
                }
            } else {
                if (decklist[card] == CARD_QUANTITY_LIMITS[card].Item1 + 1) {
                    deletions.Remove(change);
                }
                if (decklist[card] == 1) {
                    decklist.Remove(card);
                } else {
                    decklist[card]--;
                }
            }
        }

        public int NumRollouts() {
            return root.rollouts;
        }
        public MCTSVote Vote() {
            int highestRollouts = -1;
            int highestIndex = -1;
            for (int i = 0; i < root.children.Length && root.children[i] != null; i++) {
                if (root.children[i].rollouts > highestRollouts) {
                    highestRollouts = root.children[i].rollouts;
                    highestIndex = i;
                }
            }
            DecklistMCTSNode child = root.children[highestIndex];
            return new MCTSVote(child.change, child.rollouts);
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("root: {0} total reward / {1} rollouts\n", root.totalReward, root.rollouts);
            for (int i = 0; i < root.children.Length && root.children[i] != null; i++) {
                sb.AppendFormat("\t{0,4:#,##0}: {1} total reward / {2} rollouts\n", root.children[i].change, root.children[i].totalReward, root.children[i].rollouts);
            }
            return sb.ToString();
        }
    }

    class DecklistMCTSNode {
        public readonly static double EXPLORATION = Math.Sqrt(2);

        public DecklistMCTSNode parent;
        public int change;
        public float totalReward;
        public int rollouts;
        public DecklistMCTSNode[] children;

        public DecklistMCTSNode(DecklistMCTSNode parent, int change) {
            this.parent = parent;
            this.change = change;
        }

        public DecklistMCTSNode Select() {
            double highestUCT = double.MinValue;
            int highestIndex = -1;
            double lnSimulations = Math.Log(rollouts);
            for (int i = 0; i < children.Length; i++) {
                DecklistMCTSNode child = children[i];
                double uct = child.totalReward / (double)child.rollouts + EXPLORATION * Math.Sqrt(lnSimulations / child.rollouts);
                if (uct > highestUCT) {
                    highestUCT = uct;
                    highestIndex = i;
                }
            }
            return children[highestIndex];
        }
    }

    public struct MCTSVote {
        public int move, rollouts;
        
        public MCTSVote(int move, int rollouts) {
            this.move = move;
            this.rollouts = rollouts;
        }
    }
}
