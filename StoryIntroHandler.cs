using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides speech and keyboard continue support for story intro text screens.
    /// </summary>
    public sealed class StoryIntroHandler
    {
        private readonly List<string> _lines = new List<string>();
        private string _lastText;
        private bool _announced;
        private int _lineIndex;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates story intro announcements and activation.
        /// </summary>
        public bool Update()
        {
            IntroNewGameManager intro = IntroNewGameManager.Instance;
            if (intro == null || !intro.gameObject.activeInHierarchy)
            {
                Reset();
                return false;
            }

            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(intro);
                AnnounceOnce();
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(intro);
            return true;
        }

        private void Reset()
        {
            _lines.Clear();
            _lastText = null;
            _announced = false;
            _lineIndex = 0;
        }

        private void Refresh(IntroNewGameManager intro)
        {
            string title = Clean(intro.title != null ? intro.title.text : "");
            string body = Clean(intro.body != null ? intro.body.text : "");
            string button = ReadTransformText(intro.buttonContinue, Loc.Get("story_continue"));
            string text = title + "\n" + body + "\n" + button;
            if (text == _lastText)
            {
                return;
            }

            _lastText = text;
            _lines.Clear();
            AddLine(title);
            AddBodyLines(body);
            AddLine(button);
            _lineIndex = ClampIndex(_lineIndex, _lines.Count);
            _announced = false;
        }

        private void AnnounceOnce()
        {
            if (_announced || _lines.Count == 0)
            {
                return;
            }

            _announced = true;
            AccessStateManager.SetState(AccessState.Event);
            string message = string.Join(" ", _lines.ToArray());
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
        }

        private void ProcessKeys(IntroNewGameManager intro)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                ReadLine(-1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                ReadLine(1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Home))
            {
                JumpLine(false);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.End))
            {
                JumpLine(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(intro);
            }
        }

        private void ReadLine(int delta)
        {
            if (_lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("story_no_text"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, _lines.Count))
            {
                return;
            }

            ScreenReader.Say(_lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            if (_lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("story_no_text"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, _lines.Count))
            {
                return;
            }

            ScreenReader.Say(_lines[_lineIndex]);
        }

        private static void Activate(IntroNewGameManager intro)
        {
            string button = ReadTransformText(intro.buttonContinue, Loc.Get("story_continue"));
            ScreenReader.Say(Loc.Get("activated_loading", button));
            BotonGeneric boton = intro.buttonContinue != null ? intro.buttonContinue.GetComponent<BotonGeneric>() : null;
            if (boton != null)
            {
                boton.Clicked();
                return;
            }

            intro.SkipIntro();
        }

        private void AddBodyLines(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            string[] lines = body.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                AddLine(lines[i]);
            }
        }

        private void AddLine(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _lines.Add(line.Trim());
            }
        }

        private static string ReadTransformText(Transform transform, string fallback)
        {
            TMP_Text text = transform != null ? transform.GetComponentInChildren<TMP_Text>(true) : null;
            string value = text != null ? Clean(text.text) : "";
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static int ClampIndex(int index, int count)
        {
            if (count == 0 || index < 0)
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
