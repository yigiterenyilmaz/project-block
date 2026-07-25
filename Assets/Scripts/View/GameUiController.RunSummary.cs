// PURPOSE: GameUiController's end-of-run screen - what the player gets instead of the
// one-line "GAME OVER" the HUD used to print.
//
// It reads ONLY public Core state (score, rounds, inventories, bosses met), so ending a run
// needed no rules change at all. Both terminal phases land here: GamePhase.RunWon is the
// win and GamePhase.GameOver is the loss - anything waiting on a run ending must accept both.
//
// TIMING: the summary is not opened from the phase-changed event, which fires in the middle
// of resolving a turn. A flag is raised there and Update opens the screen once the placement's
// explosion and water animations have finished playing - so the run ends on screen, not
// mid-blast.

using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private const int SummaryPlayAgain = 0;
        private const int SummaryChangeDeck = 1;
        private const int SummaryTitle = 2;

        /// <summary>Set when the session reports a terminal phase; consumed by Update once
        /// nothing is animating. See the TIMING note above.</summary>
        private bool runOverPending;

        /// <summary>Subscribed to GameSession.PhaseChanged for the whole life of a session.</summary>
        private void OnSessionPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.RunWon || phase == GamePhase.GameOver)
            {
                runOverPending = true;
            }
        }

        private void OpenRunSummary()
        {
            CancelDrag();
            HideTooltip();
            screen = AppScreen.RunOver;
            ShowRunSummary();
        }

        private void ShowRunSummary()
        {
            bool won = session != null && session.Phase == GamePhase.RunWon;
            var entries = new List<MenuEntry>
            {
                MenuEntry.Of(Loc.Pick("PLAY AGAIN", "TEKRAR OYNA")),
                MenuEntry.Of(Loc.Pick("CHANGE DECK", "DESTE DEĞİŞTİR")),
                MenuEntry.Of(Loc.Pick("TITLE", "ANA MENÜ"))
            };
            menu.ShowText(
                won ? Loc.Pick("RUN COMPLETE", "OYUN TAMAMLANDI") : Loc.Pick("RUN OVER", "OYUN BİTTİ"),
                RunSummaryBody(), entries, MenuSkin.Backdrop);
        }

        private string RunSummaryBody()
        {
            if (session == null)
            {
                return string.Empty;
            }
            var sb = new StringBuilder();
            if (session.Phase == GamePhase.RunWon)
            {
                sb.Append(Loc.Pick("All ", "")).Append(session.Config.TotalRounds)
                    .Append(Loc.Pick(" rounds survived.", " rauntun hepsi geçildi."));
            }
            else
            {
                RoundEngine round = session.CurrentRound;
                sb.Append(Loc.Pick("Lost on round ", "Kaybedilen raunt: "))
                    .Append(session.RoundNumber).Append(" - ")
                    .Append(DescribeLoss(round != null ? round.Loss : null));
            }
            sb.Append("\n\n");
            sb.Append(Loc.Pick("Final score: ", "Son puan: ")).Append(session.TotalScore).Append('\n');
            sb.Append(Loc.Pick("Rounds: ", "Raunt: ")).Append(session.RoundNumber)
                .Append(" / ").Append(session.Config.TotalRounds).Append('\n');
            sb.Append(Loc.Pick("Deck: ", "Deste: ")).Append(currentDeck.Name)
                .Append(" (").Append(session.OwnedCards.Count)
                .Append(Loc.Pick(" cards)", " kart)")).Append('\n');
            sb.Append(Loc.Pick("Seed: ", "Tohum: ")).Append(lastSeedUsed).Append("\n\n");

            sb.Append(Loc.Pick("Jokers: ", "Jokerler: ")).Append(JokerNames()).Append('\n');
            sb.Append(Loc.Pick("Powers: ", "Güçler: ")).Append(PowerNames()).Append('\n');
            sb.Append(Loc.Pick("Bosses fought: ", "Karşılaşılan patronlar: ")).Append(BossNames());
            return sb.ToString();
        }

        private string JokerNames()
        {
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            if (jokers.Count == 0)
            {
                return Loc.Pick("none", "yok");
            }
            var names = new List<string>();
            for (int i = 0; i < jokers.Count; i++)
            {
                names.Add(jokers[i].DisplayName);
            }
            return string.Join(", ", names);
        }

        private string PowerNames()
        {
            IReadOnlyList<Power> powers = session.Powers.Powers;
            if (powers.Count == 0)
            {
                return Loc.Pick("none", "yok");
            }
            var names = new List<string>();
            for (int i = 0; i < powers.Count; i++)
            {
                names.Add(powers[i].DisplayName);
            }
            return string.Join(", ", names);
        }

        /// <summary>The run's bosses by display name. GameSession records them as DefIds, so
        /// they are looked back up in the registry; an unknown id falls back to the id itself
        /// rather than dropping the boss from the list.</summary>
        private string BossNames()
        {
            IReadOnlyList<string> fought = session.BossesFought;
            if (fought.Count == 0)
            {
                return Loc.Pick("none", "yok");
            }
            var names = new List<string>();
            for (int i = 0; i < fought.Count; i++)
            {
                BossDefinition definition = BossRegistry.Get(fought[i]);
                names.Add(definition != null ? definition.DisplayName : fought[i]);
            }
            return string.Join(", ", names);
        }

        private void ActivateSummaryEntry(int index)
        {
            switch (index)
            {
                case SummaryPlayAgain:
                    menu.Hide();
                    screen = AppScreen.Playing;
                    SetRunPresentationVisible(true);
                    NewGame();
                    break;
                case SummaryChangeDeck:
                    menu.Hide();
                    screen = AppScreen.DeckSelect;
                    deckSelect.Show(DeckLibrary.All, currentDeck);
                    break;
                case SummaryTitle:
                    session = null;
                    GoToTitle();
                    break;
            }
        }
    }
}
