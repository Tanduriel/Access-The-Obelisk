using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides screen reader access for general game confirmation alerts.
    /// </summary>
    public sealed class AlertHandler
    {
        private sealed class AlertChoice
        {
            public string Text;
            public bool ConfirmAnswer;
            public bool IsSingleButton;
            public Transform Transform;
        }

        private bool _announced;
        private bool _waitForEscapeRelease;
        private int _index;
        private string _lastAlertText;
        private static float _dismissOnlySingleAlertUntil;

        internal static void DismissNextSingleButtonAlertWithoutDelegate()
        {
            _dismissOnlySingleAlertUntil = Time.unscaledTime + 2f;
        }

        /// <summary>
        /// Updates alert focus, choice speech, and keyboard activation.
        /// </summary>
        public bool Update()
        {
            AlertManager alert = AlertManager.Instance;
            if (alert == null || !alert.IsActive())
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Alert);
            if (IsCopyPasteAlert(alert))
            {
                return UpdateCopyPasteAlert(alert);
            }

            if (IsPasteCopyAlert(alert))
            {
                return UpdatePasteCopyAlert(alert);
            }

            if (IsInputAlert(alert))
            {
                return UpdateInputAlert(alert);
            }

            List<AlertChoice> choices = BuildChoices(alert);
            _index = ClampIndex(_index, choices.Count);
            string text = AlertText(alert);
            if (!_announced || text != _lastAlertText)
            {
                _announced = true;
                _index = 0;
                _lastAlertText = text;
                _waitForEscapeRelease = ModInput.GetKey(KeyCode.Escape);
                ScreenReader.Say(Loc.Get("settings_alert", text));
                ScreenReader.SayQueued(FocusText(choices));
                if (IsDismissOnlySingleButtonAlert(alert, choices))
                {
                    ClearFocus();
                }
                else
                {
                    FocusChoice(choices);
                }
            }
            else
            {
                if (IsDismissOnlySingleButtonAlert(alert, choices))
                {
                    ClearFocus();
                }
                else
                {
                    EnsureFocus(choices);
                }
            }

            if (_waitForEscapeRelease)
            {
                if (ModInput.GetKey(KeyCode.Escape))
                {
                    return true;
                }

                _waitForEscapeRelease = false;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftArrow) || ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(choices, -1);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.RightArrow) || ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                Move(choices, 1);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                Jump(choices, false);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                Jump(choices, true);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                if (choices.Count == 0)
                {
                    ScreenReader.Say(Loc.Get("settings_alert_no_button"));
                }
                else if (choices[_index].IsSingleButton)
                {
                    ScreenReader.Say(Loc.Get("activated", choices[_index].Text));
                    if (ShouldDismissSingleButtonAlertWithoutDelegate(alert))
                    {
                        ClearFocus();
                        AlertManager.buttonClickDelegate = null;
                        alert.HideAlert();
                    }
                    else
                    {
                        alert.CloseAlert(force: true);
                    }
                }
                else
                {
                    ScreenReader.Say(Loc.Get("activated", choices[_index].Text));
                    alert.SetConfirmAnswer(choices[_index].ConfirmAnswer);
                }

                ClearFocus();
                InputActivationGuard.BlockSubmitAfterModal();
                Reset();
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                alert.CloseAlert();
                Reset();
                return true;
            }

            return true;
        }

        private bool UpdateCopyPasteAlert(AlertManager alert)
        {
            string title = Clean(alert.alertTextCP != null ? alert.alertTextCP.text : "");
            string code = Clean(alert.alertInputCP != null ? alert.alertInputCP.text : "");
            string text = string.IsNullOrWhiteSpace(code) ? title : Loc.Get("alert_copy_code", title, code);
            if (!_announced || text != _lastAlertText)
            {
                _announced = true;
                _lastAlertText = text;
                _waitForEscapeRelease = ModInput.GetKey(KeyCode.Escape);
                ScreenReader.Say(text);
                ScreenReader.SayQueued(Loc.Get("alert_copy_controls"));
                ClearFocus();
            }
            else
            {
                ClearFocus();
            }

            if (_waitForEscapeRelease)
            {
                if (ModInput.GetKey(KeyCode.Escape))
                {
                    return true;
                }

                _waitForEscapeRelease = false;
            }

            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (ctrl && ModInput.GetKeyDown(KeyCode.C))
            {
                GUIUtility.systemCopyBuffer = alert.alertInputCP != null ? alert.alertInputCP.text : "";
                ScreenReader.Say(Loc.Get("alert_copy_done"));
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space) || ModInput.GetKeyDown(KeyCode.Escape))
            {
                AlertManager.buttonClickDelegate = null;
                alert.HideAlert();
                InputActivationGuard.BlockSubmitAfterModal();
                Reset();
                return true;
            }

            return true;
        }

        private bool UpdatePasteCopyAlert(AlertManager alert)
        {
            string text = Clean(alert.alertTextCP != null ? alert.alertTextCP.text : "");
            if (!_announced || text != _lastAlertText)
            {
                _announced = true;
                _lastAlertText = text;
                _waitForEscapeRelease = ModInput.GetKey(KeyCode.Escape);
                ScreenReader.Say(Loc.Get("settings_alert_input", text));
                ScreenReader.SayQueued(Loc.Get("alert_paste_controls"));
                FocusPasteInput(alert);
            }
            else
            {
                FocusPasteInput(alert);
            }

            if (_waitForEscapeRelease)
            {
                if (ModInput.GetKey(KeyCode.Escape))
                {
                    return true;
                }

                _waitForEscapeRelease = false;
            }

            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (ctrl && ModInput.GetKeyDown(KeyCode.V))
            {
                if (alert.alertInputPC != null)
                {
                    alert.alertInputPC.text = GUIUtility.systemCopyBuffer ?? "";
                    alert.alertInputPC.Select();
                    alert.alertInputPC.ActivateInputField();
                    ScreenReader.Say(Loc.Get("alert_paste_done"));
                }

                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (alert.alertInputPC == null || string.IsNullOrWhiteSpace(alert.alertInputPC.text))
                {
                    ScreenReader.Say(Loc.Get("settings_alert_input_empty"));
                    return true;
                }

                alert.CloseAlert(force: true);
                InputActivationGuard.BlockSubmitAfterModal();
                Reset();
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                AlertManager.buttonClickDelegate = null;
                alert.HideAlert();
                InputActivationGuard.BlockSubmitAfterModal();
                Reset();
                return true;
            }

            return true;
        }

        private bool UpdateInputAlert(AlertManager alert)
        {
            string text = AlertText(alert);
            if (!_announced || text != _lastAlertText)
            {
                _announced = true;
                _lastAlertText = text;
                _waitForEscapeRelease = ModInput.GetKey(KeyCode.Escape);
                ScreenReader.Say(Loc.Get("settings_alert_input", text));
            }

            if (_waitForEscapeRelease)
            {
                if (ModInput.GetKey(KeyCode.Escape))
                {
                    return true;
                }

                _waitForEscapeRelease = false;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (alert.alertInput == null || string.IsNullOrWhiteSpace(alert.alertInput.text))
                {
                    ScreenReader.Say(Loc.Get("settings_alert_input_empty"));
                    return true;
                }

                alert.AlertInputSuccess();
                InputActivationGuard.BlockSubmitAfterModal();
                Reset();
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                alert.CloseAlert();
                Reset();
                return true;
            }

            return true;
        }

        private void Reset()
        {
            _announced = false;
            _waitForEscapeRelease = false;
            _index = 0;
            _lastAlertText = null;
        }

        private void Move(List<AlertChoice> choices, int delta)
        {
            if (choices.Count == 0)
            {
                ScreenReader.Say(Loc.Get("settings_alert_no_button"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, choices.Count))
            {
                return;
            }

            FocusChoice(choices);
            ScreenReader.Say(FocusText(choices));
        }

        private void Jump(List<AlertChoice> choices, bool end)
        {
            if (choices.Count == 0)
            {
                ScreenReader.Say(Loc.Get("settings_alert_no_button"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, choices.Count))
            {
                return;
            }

            FocusChoice(choices);
            ScreenReader.Say(FocusText(choices));
        }

        private static string AlertText(AlertManager alert)
        {
            string text = Clean(alert.alertText != null ? alert.alertText.text : "");
            if (string.IsNullOrWhiteSpace(text) && alert.alertTextCP != null && alert.alertTextCP.gameObject.activeInHierarchy)
            {
                text = Clean(alert.alertTextCP.text);
            }

            return text;
        }

        private static List<AlertChoice> BuildChoices(AlertManager alert)
        {
            List<AlertChoice> choices = new List<AlertChoice>();
            if (IsVisible(alert.alertTextLeftButton))
            {
                choices.Add(new AlertChoice
                {
                    Text = Clean(alert.alertTextLeftButton.text),
                    ConfirmAnswer = false,
                    Transform = alert.alertTextLeftButton.transform.parent
                });
            }

            if (IsVisible(alert.alertTextRightButton))
            {
                choices.Add(new AlertChoice
                {
                    Text = Clean(alert.alertTextRightButton.text),
                    ConfirmAnswer = true,
                    Transform = alert.alertTextRightButton.transform.parent
                });
            }

            if (choices.Count == 0 && IsVisible(alert.alertTextSingleButton))
            {
                choices.Add(new AlertChoice
                {
                    Text = Clean(alert.alertTextSingleButton.text),
                    IsSingleButton = true,
                    Transform = alert.alertTextSingleButton.transform.parent
                });
            }

            return choices;
        }

        private static bool IsInputAlert(AlertManager alert)
        {
            return alert.alertInput != null && alert.alertInput.gameObject.activeInHierarchy;
        }

        private static bool IsCopyPasteAlert(AlertManager alert)
        {
            return alert.alertInputCP != null && alert.alertInputCP.gameObject.activeInHierarchy;
        }

        private static bool IsPasteCopyAlert(AlertManager alert)
        {
            return alert.alertInputPC != null && alert.alertInputPC.gameObject.activeInHierarchy;
        }

        private string FocusText(List<AlertChoice> choices)
        {
            if (choices.Count == 0)
            {
                return Loc.Get("settings_alert_no_button");
            }

            return Loc.Get("settings_alert_choice", choices[_index].Text);
        }

        private void FocusChoice(List<AlertChoice> choices)
        {
            if (choices.Count == 0 || choices[_index].Transform == null)
            {
                return;
            }

            Transform transform = choices[_index].Transform;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(transform.gameObject);
            }

            if (Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(transform.position);
            }
        }

        private static void ClearFocus()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private static void FocusPasteInput(AlertManager alert)
        {
            if (alert == null || alert.alertInputPC == null)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != alert.alertInputPC.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(alert.alertInputPC.gameObject);
            }

            alert.alertInputPC.ActivateInputField();
        }

        private static bool ShouldDismissSingleButtonAlertWithoutDelegate(AlertManager alert)
        {
            if (alert == null)
            {
                return false;
            }

            if (_dismissOnlySingleAlertUntil >= Time.unscaledTime)
            {
                _dismissOnlySingleAlertUntil = 0f;
                return true;
            }

            if (Texts.Instance == null)
            {
                return false;
            }

            string expected = Clean(Texts.Instance.GetText("selectLanguageChanged"));
            return !string.IsNullOrWhiteSpace(expected) && AlertText(alert) == expected;
        }

        private static bool IsDismissOnlySingleButtonAlert(AlertManager alert, List<AlertChoice> choices)
        {
            if (alert == null || choices == null || choices.Count != 1 || !choices[0].IsSingleButton)
            {
                return false;
            }

            if (_dismissOnlySingleAlertUntil >= Time.unscaledTime)
            {
                return true;
            }

            if (Texts.Instance == null)
            {
                return false;
            }

            string expected = Clean(Texts.Instance.GetText("selectLanguageChanged"));
            return !string.IsNullOrWhiteSpace(expected) && AlertText(alert) == expected;
        }

        private void EnsureFocus(List<AlertChoice> choices)
        {
            if (choices.Count == 0 || choices[_index].Transform == null || EventSystem.current == null)
            {
                return;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != choices[_index].Transform.gameObject)
            {
                FocusChoice(choices);
            }
        }

        private static bool IsVisible(TMP_Text text)
        {
            return text != null && text.transform.parent != null && text.transform.parent.gameObject.activeInHierarchy;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }
    }
}
