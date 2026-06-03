using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Handles combat modal overlays such as hero death and combat retry prompts.
    /// </summary>
    public sealed class CombatModalHandler
    {
        private sealed class AlertChoice
        {
            public string Text;
            public bool ConfirmAnswer;
            public bool IsSingleButton;
        }

        private string _lastDeathText;
        private string _lastAlertText;
        private int _alertIndex;

        /// <summary>
        /// Updates active combat modal accessibility.
        /// </summary>
        public bool Update()
        {
            if (HandleDeathScreen())
            {
                return true;
            }

            return HandleRetryAlert();
        }

        private bool HandleDeathScreen()
        {
            MatchManager match = MatchManager.Instance;
            UICombatDeath deathScreen = match != null ? match.DeathScreen : null;
            if (deathScreen == null || !deathScreen.IsActive())
            {
                _lastDeathText = null;
                return false;
            }

            AccessStateManager.SetState(AccessState.Combat);
            string text = BuildDeathText(deathScreen);
            if (text != _lastDeathText)
            {
                _lastDeathText = text;
                GameEventBuffer.Add(text);
                ScreenReader.Say(text);
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (deathScreen.button != null && Functions.TransformIsVisible(deathScreen.button))
                {
                    ScreenReader.Say(Loc.Get("activated", ReadTransformText(deathScreen.button, Loc.Get("event_continue"))));
                    deathScreen.TurnOffFromButton();
                }
                else
                {
                    ScreenReader.Say(text);
                }
            }

            return true;
        }

        private static string BuildDeathText(UICombatDeath deathScreen)
        {
            List<string> lines = new List<string>();
            AddLine(lines, Loc.Get("combat_death_screen"));
            AddLine(lines, Clean(deathScreen.textCharDeath != null ? deathScreen.textCharDeath.text : ""));
            AddLine(lines, Clean(deathScreen.textInstructions != null ? deathScreen.textInstructions.text : ""));
            if (deathScreen.button != null && Functions.TransformIsVisible(deathScreen.button))
            {
                AddLine(lines, Loc.Get("combat_modal_button", ReadTransformText(deathScreen.button, Loc.Get("event_continue"))));
            }

            return string.Join(" ", lines.ToArray());
        }

        private bool HandleRetryAlert()
        {
            AlertManager alert = AlertManager.Instance;
            if (alert == null || !alert.IsActive() || !IsCombatRetryAlert(alert))
            {
                _lastAlertText = null;
                _alertIndex = 0;
                return false;
            }

            AccessStateManager.SetState(AccessState.Combat);
            List<AlertChoice> choices = BuildAlertChoices(alert);
            _alertIndex = ClampIndex(_alertIndex, choices.Count);
            string text = BuildAlertText(alert, choices);
            if (text != _lastAlertText)
            {
                _lastAlertText = text;
                GameEventBuffer.Add(text);
                ScreenReader.Say(text);
            }

            ProcessAlertKeys(alert, choices);
            return true;
        }

        private static bool IsCombatRetryAlert(AlertManager alert)
        {
            if (alert.reloadIcon != null && alert.reloadIcon.gameObject.activeInHierarchy)
            {
                return true;
            }

            string alertText = Clean(alert.alertText != null ? alert.alertText.text : "");
            string retryText = Clean(GameText.Get("combatWantToRetry"));
            return !string.IsNullOrWhiteSpace(alertText) && !string.IsNullOrWhiteSpace(retryText) && alertText.Contains(retryText);
        }

        private static List<AlertChoice> BuildAlertChoices(AlertManager alert)
        {
            List<AlertChoice> choices = new List<AlertChoice>();
            if (alert.alertTextLeftButton != null && alert.alertTextLeftButton.transform.parent.gameObject.activeInHierarchy)
            {
                choices.Add(new AlertChoice
                {
                    Text = Clean(alert.alertTextLeftButton.text),
                    ConfirmAnswer = false
                });
            }

            if (alert.alertTextRightButton != null && alert.alertTextRightButton.transform.parent.gameObject.activeInHierarchy)
            {
                choices.Add(new AlertChoice
                {
                    Text = Clean(alert.alertTextRightButton.text),
                    ConfirmAnswer = true
                });
            }

            if (choices.Count == 0 && alert.alertTextSingleButton != null && alert.alertTextSingleButton.transform.parent.gameObject.activeInHierarchy)
            {
                choices.Add(new AlertChoice
                {
                    Text = Clean(alert.alertTextSingleButton.text),
                    IsSingleButton = true
                });
            }

            return choices;
        }

        private string BuildAlertText(AlertManager alert, List<AlertChoice> choices)
        {
            List<string> lines = new List<string>();
            AddLine(lines, Loc.Get("combat_retry_screen"));
            AddLine(lines, Clean(alert.alertText != null ? alert.alertText.text : ""));
            if (choices.Count > 0)
            {
                AddLine(lines, Loc.Get("combat_modal_selected", choices[_alertIndex].Text));
            }
            else
            {
                AddLine(lines, Loc.Get("combat_modal_no_buttons"));
            }

            return string.Join(" ", lines.ToArray());
        }

        private void ProcessAlertKeys(AlertManager alert, List<AlertChoice> choices)
        {
            if (choices.Count == 0)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveAlertChoice(choices, -1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveAlertChoice(choices, 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpAlertChoice(choices, false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpAlertChoice(choices, true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                AlertChoice choice = choices[_alertIndex];
                ScreenReader.Say(Loc.Get("activated", choice.Text));
                if (choice.IsSingleButton)
                {
                    alert.CloseAlert(true);
                }
                else
                {
                    alert.SetConfirmAnswer(choice.ConfirmAnswer);
                }
            }
        }

        private void MoveAlertChoice(List<AlertChoice> choices, int delta)
        {
            if (!NavigationBounds.TryMove(ref _alertIndex, delta, choices.Count))
            {
                return;
            }

            ScreenReader.Say(Loc.Get("combat_modal_selected", choices[_alertIndex].Text));
        }

        private void JumpAlertChoice(List<AlertChoice> choices, bool end)
        {
            if (!NavigationBounds.TryJump(ref _alertIndex, end, choices.Count))
            {
                return;
            }

            ScreenReader.Say(Loc.Get("combat_modal_selected", choices[_alertIndex].Text));
        }

        private static string ReadTransformText(Transform transform, string fallback)
        {
            if (transform == null)
            {
                return fallback;
            }

            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            string text = button != null ? Clean(button.GetText()) : "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            TMP_Text tmp = transform.GetComponentInChildren<TMP_Text>(true);
            text = tmp != null ? Clean(tmp.text) : "";
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
