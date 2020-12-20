using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    class Program {
        static float[] rewards = new float[] { 0, 0, 0, 0, 1, .75f, .5f, .25f };
        static int MULLIGAN_ROLLOUTS = 100000;
        // These thresholds are tied to the reward table, the decklist, and the number of mulligan rollouts. If any changes, the thresholds must be updated.
        static float[] mulliganThresholds = new float[] { -1, -1, -1, -1, -1, -1, -1,
                                                          //0.000878707f }; // average 6-card best-child expected reward
                                                          0.0006f }; // something I made up
        static Dictionary<Card, int> STARTING_LIST = new Dictionary<Card, int>() {
            { Card.Plains, 1 },
            { Card.Island, 8 },
            { Card.Mountain, 1 },
            { Card.IzzetBoilerworks, 4 },
            { Card.MysticMonastery, 4 },
            { Card.EvolvingWilds, 4 },
            { Card.Brainstorm, 4 },
            { Card.CeruleanWisps, 2 },
            { Card.Fatestitcher, 4 },
            { Card.FranticInventory, 4 },
            { Card.FranticSearch, 4 },
            { Card.GitaxianProbe, 4 },
            { Card.JeskaiAscendancy, 4 },
            { Card.ObsessiveSearch, 2 },
            { Card.Opt, 4 },
            { Card.Ponder, 4 },
            { Card.TreasureCruise, 2 },
        };

        static void Main(string[] args) {
            //RunManualTest(STARTING_LIST);
            AutoRunParallelTest(1000, 10000);
        }

        static void AutoRunParallelTest(int n, int rollouts) {
            Tuple<bool> l = new Tuple<bool>(false);
            float totalReward = 0;
            int winTurnTotal = 0;
            int wins = 0;
            int trials = 0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 1 }, i => {
                int turns = AutoRunGame(STARTING_LIST, rollouts);
                lock (l) {
                    trials++;
                    if (turns > 0) {
                        totalReward += rewards[turns];
                        winTurnTotal += turns;
                        wins++;
                    } else {
                    }
                    Console.WriteLine("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N2"), trials, rewards.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
                }
            });
            stopwatch.Stop();
            Console.WriteLine("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N2"), trials, rewards.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
            Console.ReadLine();
        }
        static int AutoRunGame(Dictionary<Card, int> decklist, int rollouts) {
            MCTS mcts = null;
            State state = null;
            for (int n = 7; n > 0; n--) {
                state = new State(decklist, n);
                mcts = new MCTS(state, rewards);
                // Mulligan decision.
                float threshold = mulliganThresholds[n];
                if (threshold == -1) {
                    break;
                }
                mcts.Rollout(MULLIGAN_ROLLOUTS);
                if (mcts.ExpectedRewardOfBestChild() >= threshold) {
                    break;
                }
            }
            // Play the game.
            while (!mcts.rootState.IsWon() && !mcts.rootState.IsLost() && mcts.rootState.turn < rewards.Length) {
                mcts.Rollout(rollouts);
                List<string> moveStrings = new List<string>();
                mcts.Advance(moveStrings);
                foreach (string s in moveStrings) Console.WriteLine(s);
            }
            if (state.IsWon()) {
                return mcts.rootState.turn;
            } else {
                return -1;
            }
        }

        static void RunManualTest(Dictionary<Card, int> decklist) {
            State state = null;
            for (int n = 7; n > 0; n--) {
                state = new State(decklist, n);
                Console.WriteLine(state);
                Console.WriteLine("Mulligan to {0}? (y/N)", n - 1);
                string line = Console.ReadLine();
                if (!line.Equals("y", StringComparison.OrdinalIgnoreCase)) break;
            }
            while (!state.IsWon() && !state.IsLost()) {
                int[] moves = state.GetMoves();
                foreach (int move in moves) Console.WriteLine("{0}: {1}", move, state.MoveToString(move));
                Console.WriteLine();
                int chosen = int.MinValue;
                while (!moves.Contains(chosen)) {
                    if (!int.TryParse(Console.ReadLine(), out chosen)) chosen = int.MinValue;
                }
                state.ExecuteMove(chosen);
                Console.WriteLine();
                Console.WriteLine(state);
            }
        }
    }
}