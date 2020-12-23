using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    class Program {
        static Dictionary<Card, int> STARTING_LIST = new Dictionary<Card, int>() {
            { Card.Plains, 1 },
            { Card.Island, 12 },
            { Card.Mountain, 1 },
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
            ParallelTest(1000, 10000);
            //ParallelMulligans(1000, 4);
            //SingleThreadDecklistRun();
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        static void SingleThreadDecklistRun() {
            DecklistMCTS dmcts = new DecklistMCTS(STARTING_LIST, 10000);
            dmcts.Rollout(1000);
            Console.ReadLine();
        }

        static void ParallelTest(int n, int rollouts) {
            Tuple<bool> l = new Tuple<bool>(false);
            float totalReward = 0;
            int winTurnTotal = 0;
            int wins = 0;
            int trials = 0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#if DEBUG
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 1 }, i => {
#else
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
#endif
                int turns = Simulation.RunGame(STARTING_LIST, rollouts);
                lock (l) {
                    trials++;
                    if (turns > 0) {
                        totalReward += Simulation.REWARDS[turns];
                        winTurnTotal += turns;
                        wins++;
                    } else {
                    }
                    Console.WriteLine("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N2"), trials, Simulation.REWARDS.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
                }
            });
            stopwatch.Stop();
            Console.WriteLine("Done in {0} ms. Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", stopwatch.ElapsedMilliseconds, (totalReward / trials).ToString("N2"), trials, Simulation.REWARDS.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
        }

        static void ParallelMulligans(int n, int cardsInHand) {
            Tuple<bool> l = new Tuple<bool>(false);
            float total = 0;
            int trials = 0;
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
                float expected = Simulation.RunMulligan(STARTING_LIST, cardsInHand);
                lock (l) {
                    total += expected;
                    trials++;
                    Console.WriteLine("Average {0} for {1} trials.", total / trials, trials);
                }
            });
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
                state.SanityCheck();
                Console.WriteLine();
                Console.WriteLine(state);
            }
        }
    }
}