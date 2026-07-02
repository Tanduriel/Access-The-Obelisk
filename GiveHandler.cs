using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for the multiplayer resource transfer window.
    /// </summary>
    public sealed class GiveHandler
    {
        private enum GiveItemKind
        {
            Resource,
            Target,
            Quantity,
            Button
        }

        private sealed class GiveItem
        {
            public GiveItemKind Kind;
            public string Key;
            public string Label;
            public BotonGeneric Button;
            public bool Send;
            public bool Close;
        }

        private readonly List<GiveItem> _items = new List<GiveItem>();
        private int _index;
        private bool _announced;
        private string _lastFocus;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates resource transfer navigation and activation.
        /// </summary>
        public bool Update()
        {
            GiveManager give = GiveManager.Instance;
            if (give == null || !give.IsActive())
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Give);
            if (Time.unscaledTime - _lastRefreshTime > 0.1f)
            {
                Refresh(give);
                _lastRefreshTime = Time.unscaledTime;
            }

            if (!_announced)
            {
                _announced = true;
                ScreenReader.Say(Loc.Get("give_screen"));
                AnnounceFocused(true);
            }

            ProcessKeys(give);
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _announced = false;
            _lastFocus = null;
            _lastRefreshTime = 0f;
        }

        private void Refresh(GiveManager give)
        {
            string currentKey = CurrentItem() != null ? CurrentItem().Key : null;
            _items.Clear();

            AddItem(GiveItemKind.Resource, "resource", ResourceText(give));
            AddItem(GiveItemKind.Target, "target", Loc.Get("give_target", Clean(give.target != null ? give.target.text : "")));
            AddItem(GiveItemKind.Quantity, "quantity", QuantityText(give));
            AddVisibleButtons(give);

            _index = ClampIndex(_index);
            if (!string.IsNullOrWhiteSpace(currentKey))
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Key == currentKey)
                    {
                        _index = i;
                        break;
                    }
                }
            }
        }

        private void AddVisibleButtons(GiveManager give)
        {
            if (give.buttonsController != null)
            {
                for (int i = 0; i < give.buttonsController.Count; i++)
                {
                    Transform transform = give.buttonsController[i];
                    if (transform == null || !Functions.TransformIsVisible(transform))
                    {
                        continue;
                    }

                    BotonGeneric button = transform.GetComponent<BotonGeneric>() ?? transform.GetComponentInParent<BotonGeneric>();
                    if (button == null || button == give.botonGold || button == give.botonDust)
                    {
                        continue;
                    }

                    string label = ReadButton(button);
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    GiveItem item = new GiveItem();
                    item.Kind = GiveItemKind.Button;
                    item.Key = "button:" + transform.name + ":" + i;
                    item.Label = label;
                    item.Button = button;
                    item.Send = transform == give.botonGive || transform.IsChildOf(give.botonGive);
                    item.Close = LooksLikeClose(button);
                    _items.Add(item);
                }
            }

            if (give.botonGive != null && Functions.TransformIsVisible(give.botonGive) && !ContainsTransform(give.botonGive))
            {
                GiveItem send = new GiveItem();
                send.Kind = GiveItemKind.Button;
                send.Key = "send";
                send.Label = Loc.Get("give_send");
                send.Send = true;
                _items.Add(send);
            }

            if (!HasCloseItem())
            {
                GiveItem close = new GiveItem();
                close.Kind = GiveItemKind.Button;
                close.Key = "close";
                close.Label = Loc.Get("give_close");
                close.Close = true;
                _items.Add(close);
            }
        }

        private void AddItem(GiveItemKind kind, string key, string label)
        {
            GiveItem item = new GiveItem();
            item.Kind = kind;
            item.Key = key;
            item.Label = label;
            _items.Add(item);
        }

        private void ProcessKeys(GiveManager give)
        {
            if (ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                Jump(false);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                Adjust(give, -1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                Adjust(give, 1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                Activate(give);
            }
        }

        private void Move(int delta)
        {
            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            AnnounceFocused(true);
        }

        private void Jump(bool end)
        {
            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            AnnounceFocused(true);
        }

        private void Adjust(GiveManager give, int delta)
        {
            GiveItem item = CurrentItem();
            if (item == null)
            {
                return;
            }

            switch (item.Kind)
            {
            case GiveItemKind.Resource:
                give.ShowGive(true, IsGold(give) ? Enums.CurrencyType.GOLD : Enums.CurrencyType.DUST);
                break;
            case GiveItemKind.Target:
                if (delta < 0)
                {
                    give.PrevTarget();
                }
                else
                {
                    give.NextTarget();
                }
                break;
            case GiveItemKind.Quantity:
                give.Give(delta * QuantityStep());
                break;
            default:
                return;
            }

            Refresh(give);
            _lastFocus = null;
            AnnounceFocused(true);
        }

        private void Activate(GiveManager give)
        {
            GiveItem item = CurrentItem();
            if (item == null)
            {
                return;
            }

            if (item.Kind == GiveItemKind.Resource || item.Kind == GiveItemKind.Target || item.Kind == GiveItemKind.Quantity)
            {
                ScreenReader.Say(item.Label);
                return;
            }

            if (item.Close)
            {
                give.ShowGive(false);
                ScreenReader.Say(Loc.Get("give_closed"));
                return;
            }

            if (item.Send)
            {
                if (give.quantity <= 0)
                {
                    ScreenReader.Say(Loc.Get("give_quantity_zero"));
                    return;
                }

                ScreenReader.Say(Loc.Get("give_sent", give.quantity, Clean(give.target != null ? give.target.text : "")));
                give.GiveAction();
                return;
            }

            if (item.Button != null)
            {
                item.Button.Clicked();
                Refresh(give);
                _lastFocus = null;
                AnnounceFocused(true);
            }
        }

        private void AnnounceFocused(bool force)
        {
            GiveItem item = CurrentItem();
            if (item == null)
            {
                return;
            }

            string focus = item.Key + ":" + item.Label;
            if (!force && focus == _lastFocus)
            {
                return;
            }

            _lastFocus = focus;
            ScreenReader.Say(item.Label);
        }

        private GiveItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index);
            return _items[_index];
        }

        private int ClampIndex(int index)
        {
            if (_items.Count == 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            return index >= _items.Count ? _items.Count - 1 : index;
        }

        private static int QuantityStep()
        {
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            bool shift = ModInput.GetKey(KeyCode.LeftShift) || ModInput.GetKey(KeyCode.RightShift);
            if (ctrl)
            {
                return 100;
            }

            return shift ? 20 : 1;
        }

        private static bool IsGold(GiveManager give)
        {
            return give.bgGold != null && give.bgGold.gameObject.activeInHierarchy;
        }

        private static string ResourceText(GiveManager give)
        {
            return Loc.Get(IsGold(give) ? "give_resource_gold" : "give_resource_shards");
        }

        private static string QuantityText(GiveManager give)
        {
            int available = IsGold(give) ? AtOManager.Instance.CurrencyManager.GetPlayerGold() : AtOManager.Instance.CurrencyManager.GetPlayerDust();
            return Loc.Get("give_quantity", give.quantity, available);
        }

        private static string ReadButton(BotonGeneric button)
        {
            string text = Clean(button != null ? button.GetText() : "");
            if (string.IsNullOrWhiteSpace(text) && button != null)
            {
                text = Clean(GameText.Get(button.idTranslate));
            }

            return text;
        }

        private static bool LooksLikeClose(BotonGeneric button)
        {
            if (button == null)
            {
                return false;
            }

            string probe = ((button.name ?? "") + " " + (button.idTranslate ?? "") + " " + ReadButton(button)).ToLowerInvariant();
            return probe.Contains("close") || probe.Contains("exit") || probe.Contains("back") || probe.Contains("закры") || probe.Contains("назад");
        }

        private bool ContainsTransform(Transform transform)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                BotonGeneric button = _items[i].Button;
                if (button != null && (button.transform == transform || button.transform.IsChildOf(transform) || transform.IsChildOf(button.transform)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasCloseItem()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Close)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
