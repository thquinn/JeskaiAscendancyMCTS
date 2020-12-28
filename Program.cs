using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
            { Card.FranticInventory, 2 },
            { Card.FranticSearch, 4 },
            { Card.GitaxianProbe, 4 },
            { Card.JeskaiAscendancy, 4 },
            { Card.ObsessiveSearch, 2 },
            { Card.OmenOfTheSea, 2 },
            { Card.Opt, 4 },
            { Card.Ponder, 4 },
            { Card.TreasureCruise, 2 },
        };

        static void Main(string[] args) {
            //RunManualTest(STARTING_LIST);
            //ParallelTest(10000, 1000);
            //ParallelMulligans(1000, 4);
            //SingleThreadDecklistRun();
            //RootParallelizedDecklistRun();
            OptimizeDecklist();
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        static void OptimizeDecklist() {
            bool complete = false;
            while (!complete) {
                complete = RootParallelizedDecklistRun();
            }
        }
        static bool RootParallelizedDecklistRun() {
            DecklistMCTS[] decklistRuns = new DecklistMCTS[Environment.ProcessorCount];
            DMCTSSaveState save = Util.Load();
            if (save != null && save.lastMove == 0) {
                Console.WriteLine("Previous run concluded. Relocate log and try again.");
                return true;
            }
            Console.WriteLine(save == null ? "Starting new run on {0} logical processors." : "Continuing run on {0} logical processors.", decklistRuns.Length);
            Console.WriteLine("Round will conclude: {0:g}", DateTime.Now + TimeSpan.FromHours(6));
            for (int i = 0; i < decklistRuns.Length; i++) {
                decklistRuns[i] = save == null ? new DecklistMCTS(STARTING_LIST, 1000) : new DecklistMCTS(save, 1000);
            }
            CancellationTokenSource cancel = new CancellationTokenSource(TimeSpan.FromHours(6));
            ParallelOptions po = new ParallelOptions { MaxDegreeOfParallelism = decklistRuns.Length, CancellationToken = cancel.Token };
            try {
                Parallel.For(0, decklistRuns.Length, po, i => {
                    DecklistMCTS dmcts = decklistRuns[i];
                    while (true) {
                        dmcts.Rollout();
                        int numRollouts = dmcts.NumRollouts();
                        if (numRollouts % 250 == 0) {
                            Console.WriteLine("Tree {0} reached {1} rollouts.", i, numRollouts);
                        }
                        po.CancellationToken.ThrowIfCancellationRequested();
                    }
                });
            } catch (OperationCanceledException) {
                Console.WriteLine("Round has finished.");
            } finally {
                cancel.Dispose();
            }
            MCTSVote[] votes = decklistRuns.Select(m => m.Vote()).ToArray();
            Dictionary<int, int> voteTally = new Dictionary<int, int>();
            foreach (MCTSVote vote in votes) {
                if (voteTally.ContainsKey(vote.move)) {
                    voteTally[vote.move] += vote.rollouts;
                } else {
                    voteTally[vote.move] = vote.rollouts;
                }
            }
            int bestChange = voteTally.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            foreach (DecklistMCTS dmcts in decklistRuns) {
                Console.WriteLine(dmcts);
            }
            Console.WriteLine("The votes are in: {0}", bestChange);
            Util.Save(decklistRuns, bestChange);
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

        static void ParallelTest(int n, int rollouts) {
            Tuple<bool> l = new Tuple<bool>(false);
            float totalReward = 0;
            int winTurnTotal = 0;
            int wins = 0;
            int trials = 0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#if DEBUG
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
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
                    Console.WriteLine("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N3"), trials, Simulation.REWARDS.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
                }
            });
            stopwatch.Stop();
            Console.WriteLine("Done in {0} ms. Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", stopwatch.ElapsedMilliseconds, (totalReward / trials).ToString("N3"), trials, Simulation.REWARDS.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
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