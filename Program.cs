using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    class Program {
        static Dictionary<Card, int> STARTING_LIST = new Dictionary<Card, int>() {
            { Card.Plains, 3 },
            { Card.Island, 7 },
            { Card.Mountain, 3 },
            { Card.MysticMonastery, 4 },
            { Card.EvolvingWilds, 1 },
            { Card.Brainstorm, 4 },
            { Card.CeruleanWisps, 3 },
            { Card.Fatestitcher, 4 },
            { Card.FranticSearch, 4 },
            { Card.GitaxianProbe, 4 },
            { Card.JeskaiAscendancy, 4 },
            { Card.ObsessiveSearch, 4 },
            { Card.Opt, 3 },
            { Card.Ponder, 4 },
            { Card.TreasureCruise, 3 },
            { Card.VividCreek, 3 },
            { Card.VisionSkeins, 1 },
            { Card.IdeasUnbound, 1 },
        };
        static TimeSpan DMCTS_ROUND_TIME = TimeSpan.FromHours(8);
        static int[] DMCTS_CHANGES = new int[] { -10, 19, -2, 9, -11, 18, -2, 1, -11, 29, -11, 28, -8, 3, -24, 29, -24, 21, -8, 6, -24, 3 };
        static int RANDOM_SUFFIX = new Random().Next();

        static void Main(string[] args) {
            //RunManualTest(STARTING_LIST);
            //ParallelTest(STARTING_LIST, 20000, 10000, true);
            //ParallelMulligans(1000, 4);
            //SingleThreadDecklistRun();
            //DecklistMCTS();
            //BenchmarkChanges();
            GameToConsole();
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        static void GameToConsole() {
            Simulation.RunGame(STARTING_LIST, 100000, true);
        }

        static void BenchmarkChanges() {
            Dictionary<Card, int> decklist = new Dictionary<Card, int>(STARTING_LIST);
            Console.WriteLine("Running baseline test...");
            float averageReward = ParallelTest(decklist, 20000, 1000);
            for (int i = 0; i < DMCTS_CHANGES.Length; i += 2) {
                Card removal = (Card)(-DMCTS_CHANGES[i]);
                if (decklist[removal] == 1) {
                    decklist.Remove(removal);
                } else {
                    decklist[removal]--;
                }
                Card addition = (Card)DMCTS_CHANGES[i + 1];
                if (decklist.ContainsKey(addition)) {
                    decklist[addition]++;
                } else {
                    decklist[addition] = 1;
                }
                Console.WriteLine("Removed one copy of {0}.", State.CARD_NAMES[removal]);
                Console.WriteLine("Added one copy of {0}.", State.CARD_NAMES[addition]);
                averageReward = ParallelTest(decklist, 20000, 1000);
            }
        }

        static void DecklistMCTS() {
            bool complete = false;
            while (!complete) {
                complete = TreeParallelizedDecklistRun();
            }
        }
        static bool TreeParallelizedDecklistRun() {
# if DEBUG
            int threadCount = 1;
#else
            int threadCount = Environment.ProcessorCount;
#endif
            DMCTSSaveState save = Util.Load();
            if (save != null && save.lastMove == 0) {
                Console.WriteLine("Previous run concluded. Relocate log and try again.");
                return true;
            }
            Console.WriteLine(save == null ? "Starting new run on {0} logical processors." : "Continuing run on {0} logical processors.", threadCount);
            DateTime endTime = DateTime.Now + DMCTS_ROUND_TIME;
            DecklistMCTS dmcts = save == null ? new DecklistMCTS(STARTING_LIST, 1000) : new DecklistMCTS(save, 1000);
            CancellationTokenSource cancel = new CancellationTokenSource(DMCTS_ROUND_TIME);
            ParallelOptions po = new ParallelOptions { MaxDegreeOfParallelism = threadCount, CancellationToken = cancel.Token };
            bool done = false;
            while (!done) {
                Console.WriteLine("Round will conclude: {0:g}", endTime);
                Console.WriteLine("Press any key to pause (might take a few seconds).");
                try {
                    Parallel.For(0, threadCount, po, i => {
                        while (true) {
                            if (Console.KeyAvailable) {
                                break;
                            }
                            dmcts.Rollout();
                            int numRollouts = dmcts.NumRollouts();
                            if (numRollouts % 250 == 0) {
                                Console.WriteLine("Tree reached {0} rollouts.", numRollouts);
                            }
                            po.CancellationToken.ThrowIfCancellationRequested();
                        }
                    });
                } catch (OperationCanceledException) {
                    Console.WriteLine("Round has finished.");
                } finally {
                    cancel.Dispose();
                }
                if (Console.KeyAvailable) {
                    while (Console.KeyAvailable) {
                        Console.ReadKey();
                    }
                    TimeSpan timeLeft = endTime - DateTime.Now;
                    Console.WriteLine("Round paused. Press Enter to resume.");
                    Console.ReadLine();
                    endTime = DateTime.Now + timeLeft;
                    cancel = new CancellationTokenSource(timeLeft);
                    po = new ParallelOptions { MaxDegreeOfParallelism = threadCount, CancellationToken = cancel.Token };
                    Console.WriteLine("Resuming.");
                } else {
                    done = true;
                }
            }
            MCTSVote[] votes = new MCTSVote[] { dmcts.Vote() };
            Dictionary<int, int> voteTally = new Dictionary<int, int>();
            foreach (MCTSVote vote in votes) {
                if (voteTally.ContainsKey(vote.move)) {
                    voteTally[vote.move] += vote.rollouts;
                } else {
                    voteTally[vote.move] = vote.rollouts;
                }
            }
            int bestChange = voteTally.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                Console.WriteLine(dmcts);
            Console.WriteLine("The votes are in: {0}", bestChange);
            Util.Save(dmcts, bestChange);
            if (bestChange == 0) {
                Console.WriteLine("Decklist finalized. The run has concluded.");
                return true;
            }
            return false;
        }

        static void SingleThreadDecklistRun() {
            DecklistMCTS dmcts = new DecklistMCTS(STARTING_LIST, 1000);
            dmcts.Rollout(10000);
        }

        static float ParallelTest(Dictionary<Card, int> decklist, int n, int rollouts, bool print = false) {
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
                int turns = Simulation.RunGame(decklist, rollouts);
                lock (l) {
                    trials++;
                    if (turns > 0) {
                        totalReward += Simulation.REWARDS[turns];
                        winTurnTotal += turns;
                        wins++;
                    } else {
                    }
                    if (print) {
                        string s = string.Format("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N3"), trials, Simulation.REWARDS.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
                        Console.WriteLine(s);
                        File.AppendAllText("log" + RANDOM_SUFFIX + ".txt", "\n" + s);
                    }
                }
            });
            stopwatch.Stop();
            Console.WriteLine("Done in {0} ms. Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", stopwatch.ElapsedMilliseconds, (totalReward / trials).ToString("N3"), trials, Simulation.REWARDS.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
            return totalReward / trials;
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