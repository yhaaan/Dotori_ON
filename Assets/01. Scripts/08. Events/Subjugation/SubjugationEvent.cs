using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Core;
using DOTORION.Platform.Windows;
using DOTORION.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Events.Subjugation
{
    /// <summary>Server calls the event may use. Any of them can be null: mock backend, or the event schema not applied yet.</summary>
    public sealed class SubjugationRemote
    {
        /// <summary>Teammates' (current, best) rounds.</summary>
        public Func<CancellationToken, Task<Dictionary<string, (int current, int best)>>> ReadRounds;
        /// <summary>Publishes this member's (current, best) rounds.</summary>
        public Func<int, int, CancellationToken, Task> WriteRound;
        /// <summary>Developer reset of the server-side event rows (team-wide for an admin).</summary>
        public Func<CancellationToken, Task> ResetAll;
        public Func<CancellationToken, Task<int[]>> ReadClaims;
        /// <summary>(milestone, highest cleared round) -> points awarded by this call, zero if already paid.</summary>
        public Func<int, int, CancellationToken, Task<int>> ClaimReward;
        /// <summary>Asks the host to re-read the point total after a reward lands.</summary>
        public Action PointsChanged;
    }

    /// <summary>The removable event composition root. Hiding its display never pauses work-time progression.</summary>
    public sealed class SubjugationEvent : MonoBehaviour
    {
        /// <summary>The event opens at 11:10 on 19 October and runs through 30 November (Seoul). A build released
        /// earlier shows nothing until it opens, and it removes itself after, with no app update either way.
        /// supabase chiikawa_event_ends_at() mirrors the end and refuses reward claims from then on.</summary>
        public static readonly DateTimeOffset StartsAt = new DateTimeOffset(2026, 10, 19, 11, 10, 0, TimeSpan.FromHours(9));
        public static readonly DateTimeOffset EndsAt = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(9));
        public static bool IsRunning(DateTimeOffset now) => now >= StartsAt && now < EndsAt;
        /// <summary>"이벤트 기간 2026/10/19 11:10 ~ 2026/11/30 23:59", in Seoul time.</summary>
        public static string PeriodText => "이벤트 기간 " + StartsAt.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            + " ~ " + EndsAt.AddMinutes(-1).ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        private const string VisibleKey = "DOTORION.Chiikawa.Visible";
        private const int ShopHeight = 338;
        private DOTORIONView _view;
        private WindowsOverlayWindow _window;
        private Func<MemberState> _local;
        private Func<bool> _canAdvance;
        private Action _prepareShop;
        private SubjugationGame _game;
        private SubjugationStore _store;
        private SubjugationPixels _pixels;
        private SubjugationArt _art;
        private SubjugationNativeWindow _native;
        private Button _toggle;
        private Sprite _frame, _weapon;
        private GameObject _shop, _stripRoot, _debugCoins, _debugReset;
        private SubjugationBalance _balance;
        private float _resetArmedUntil;
        private bool _resetting;
        private bool _uiAutoHovered;
        private InputField _roundInput;
        private Text _roundHeading, _autoLabel;
        private RawImage _preview;
        private Image _stripBackdrop;
        private Text _heading, _status;
        private readonly Text[] _names = new Text[6];
        private readonly Image[] _faces = new Image[6];
        private readonly Button[] _buy = new Button[6], _party = new Button[6];
        private readonly Dictionary<string, int> _rounds = new Dictionary<string, int>(), _bestRounds = new Dictionary<string, int>();
        private SubjugationRemote _remote;
        private GameObject _rosterPage, _rewardPage;
        // One window, three tabs: roster, rewards, round selection.
        private const int RosterTab = 0, RewardTab = 1, RoundTab = 2;
        private GameObject _roundPage;
        private Button _rosterTab, _rewardTab, _roundTab;
        private int _tab;
        private bool _claimsLoaded, _claimsFailed, _claimsLoading;
        private int _claiming;
        private readonly HashSet<int> _claimed = new HashSet<int>();
        private readonly Text[] _rewardNames = new Text[10];
        private Text _rewardEarned;
        private readonly Button[] _rewardButtons = new Button[10];
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private string _memberId;
        private bool _visible, _dirty, _storageBlocked, _syncing, _disposed, _syncWarning;
        private float _nextFrame, _nextSave, _nextSync, _animationTime;

        public void Initialize(DOTORIONView view, WindowsOverlayWindow window, string memberId,
            Func<MemberState> local, Func<bool> canAdvance, Action prepareShop,
            SubjugationRemote remote = null, string saveDirectory = null)
        {
            _view = view; _window = window; _memberId = memberId; _local = local;
            _canAdvance = canAdvance; _prepareShop = prepareShop; _remote = remote ?? new SubjugationRemote();
            _visible = PlayerPrefs.GetInt(VisibleKey, 1) != 0;
            var config = Resources.Load<TextAsset>("DOTORION/Events/ChiikawaBalance");
            var balance = _balance = config != null ? JsonUtility.FromJson<SubjugationBalance>(config.text) : new SubjugationBalance();
            _store = new SubjugationStore(saveDirectory ?? Path.Combine(Application.persistentDataPath, "Events"), memberId);
            try { _game = _store.Load(balance); }
            catch (IOException e) { _game = new SubjugationGame(null, balance); _storageBlocked = true; view.ShowFeedback(e.Message, true); }
            _game.Changed += MarkDirty;
            _art = new SubjugationArt();
            _pixels = new SubjugationPixels(_art);
            _pixels.SetFont(_view.EventFont);
            _native = new SubjugationNativeWindow();
            BuildToggle();
            BuildShop();
            BuildDebugCoins();
            LoadClaims();
            _nextSave = Time.unscaledTime + 10;
            _dirty = true;
        }

        private void BuildToggle()
        {
            var template = _view.EventButtonTemplate;
            if (template == null) return;
            _toggle = UiFactory.CreateButton("ChiikawaEventToggle", template.transform.parent, _view.EventFont, "", ToggleVisible);
            _toggle.colors = template.colors;
            var t = (RectTransform)template.transform;
            var neighbor = _view.EventButtonNeighbor != null ? (RectTransform)_view.EventButtonNeighbor.transform : t;
            var rect = (RectTransform)_toggle.transform;
            rect.anchorMin = neighbor.anchorMin; rect.anchorMax = neighbor.anchorMax; rect.pivot = neighbor.pivot;
            rect.sizeDelta = t.sizeDelta;
            rect.anchoredPosition = neighbor.anchoredPosition - new Vector2(neighbor.rect.width + 2, 0);
            _frame = CopyButtonFrame(template.image);
            _toggle.image.sprite = _frame;
            _toggle.image.color = Color.white;
            var icon = UiFactory.CreateImage("Sasumata", _toggle.transform, Color.white);
            _weapon = SubjugationPixels.WeaponIcon();
            icon.sprite = _weapon; icon.raycastTarget = false;
            // Exactly one UI pixel per dot, never stretched: crisp at 100% and 200%. Bottom-centred one pixel up
            // so the uncapped shaft meets the frame's 1px border.
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, 0); iconRect.pivot = new Vector2(.5f, 0);
            iconRect.sizeDelta = new Vector2(SubjugationPixels.WeaponIconSize, SubjugationPixels.WeaponIconSize);
            iconRect.anchoredPosition = new Vector2(0, 1);
        }

        private static Sprite CopyButtonFrame(Image image)
        {
            // Retain the exact outer frame from the existing skin, replacing only its baked-in icon.
            if (image == null || image.sprite == null) return null;
            var source = image.sprite;
            var texture = source.texture;
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            var old = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, rt); RenderTexture.active = rt;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); readable.Apply();
                var r = source.rect;
                var w = (int)r.width; var h = (int)r.height;
                var colors = readable.GetPixels((int)r.x, (int)r.y, w, h);
                var fill = colors[Math.Min(h - 1, 3) * w + Math.Min(w - 1, 3)];
                for (var y = 3; y < h - 3; y++) for (var x = 3; x < w - 3; x++) colors[y * w + x] = fill;
                var frame = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                frame.SetPixels(colors); frame.Apply();
                return Sprite.Create(frame, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); SubjugationPixels.Release(readable); }
        }

        private void BuildShop()
        {
            var panel = UiFactory.CreateImage("ChiikawaRoster", _view.EventPanelParent, new Color(.91f, .96f, 1));
            UiFactory.AnchorTop(panel.rectTransform, 0, 220, 480, ShopHeight);
            _shop = panel.gameObject;
            _heading = Label("Heading", panel.transform, "치이카와 토벌 대작전", 12, 8, 330, 24, 14);
            _heading.fontStyle = FontStyle.Bold;
            var close = UiFactory.CreateButton("Close", panel.transform, _view.EventFont, "닫기", () => SetShop(false));
            UiFactory.AnchorTop((RectTransform)close.transform, 426, 8, 42, 24);
            _status = Label("Status", panel.transform, "", 12, 32, 300, 20, 12);
            _status.resizeTextForBestFit = true; _status.resizeTextMinSize = 9; _status.resizeTextMaxSize = 12;
            _rosterTab = UiFactory.CreateButton("RosterTab", panel.transform, _view.EventFont, "대원", () => SetTab(RosterTab));
            UiFactory.AnchorTop((RectTransform)_rosterTab.transform, 318, 31, 48, 22);
            _rewardTab = UiFactory.CreateButton("RewardTab", panel.transform, _view.EventFont, "보상", () => SetTab(RewardTab));
            UiFactory.AnchorTop((RectTransform)_rewardTab.transform, 370, 31, 48, 22);
            _roundTab = UiFactory.CreateButton("RoundTab", panel.transform, _view.EventFont, "라운드", () => SetTab(RoundTab));
            UiFactory.AnchorTop((RectTransform)_roundTab.transform, 422, 31, 48, 22);
            _rosterPage = UiFactory.CreateRect("RosterPage", panel.transform);
            UiFactory.Stretch((RectTransform)_rosterPage.transform);
            _rewardPage = UiFactory.CreateRect("RewardPage", panel.transform);
            UiFactory.Stretch((RectTransform)_rewardPage.transform);
            _roundPage = UiFactory.CreateRect("RoundPage", panel.transform);
            UiFactory.Stretch((RectTransform)_roundPage.transform);
            for (var i = 0; i < 6; i++)
            {
                var index = i;
                var y = 58 + i * 44;
                var faceFrame = UiFactory.CreateImage("FaceFrame" + i, _rosterPage.transform, Color.white);
                UiFactory.AnchorTop(faceFrame.rectTransform, 12, y, 32, 32);
                _faces[i] = UiFactory.CreateImage("Face" + i, faceFrame.transform, Color.white);
                _faces[i].sprite = _art.Portrait(i);
                _faces[i].preserveAspect = true;
                _faces[i].raycastTarget = false;
                UiFactory.Stretch(_faces[i].rectTransform);
                _names[i] = Label("Member" + i, _rosterPage.transform, "", 50, y, 172, 40, 12);
                _party[i] = UiFactory.CreateButton("Party" + i, _rosterPage.transform, _view.EventFont, "", () => Purchase(() => _game.ToggleParty(index)));
                UiFactory.AnchorTop((RectTransform)_party[i].transform, 224, y + 6, 82, 28);
                _buy[i] = UiFactory.CreateButton("Upgrade" + i, _rosterPage.transform, _view.EventFont, "", () => Purchase(() => _game.Save.levels[index] > 0 ? _game.LevelUp(index) : _game.Hire(index)));
                UiFactory.AnchorTop((RectTransform)_buy[i].transform, 314, y + 6, 152, 28);
            }
            // Two columns of five milestones, the same 44px rows as the roster.
            for (var i = 0; i < 10; i++)
            {
                var milestone = SubjugationGame.RewardMilestones[i];
                var x = 12 + i / 5 * 234; var y = 58 + i % 5 * 44;
                _rewardNames[i] = Label("Reward" + milestone, _rewardPage.transform, "R" + milestone + " 클리어\n도토리온 포인트 " + milestone + "P", x, y, 126, 40, 12);
                _rewardButtons[i] = UiFactory.CreateButton("Claim" + milestone, _rewardPage.transform, _view.EventFont, "", () => ClaimReward(milestone));
                UiFactory.AnchorTop((RectTransform)_rewardButtons[i].transform, x + 128, y + 6, 92, 28);
            }
            _rewardEarned = Label("RewardEarned", _rewardPage.transform, "", 12, 296, 456, 24, 12);
            _rewardPage.SetActive(false);
            BuildRoundPage();
            _shop.SetActive(false);

            // Full mode is one Unity window: the strip cannot be detached and moves in the native drag loop.
            var backdrop = UiFactory.CreateImage("ChiikawaIntegratedStrip", _view.EventPanelParent, new Color(1, 0, 1, 1));
            UiFactory.AnchorTop(backdrop.rectTransform, 0, -64, 480, 64);
            _stripRoot = backdrop.gameObject; _stripBackdrop = backdrop;
#if !UNITY_STANDALONE_WIN || UNITY_EDITOR
            backdrop.color = Color.clear;
#endif
            var preview = UiFactory.CreateRect("ChiikawaPixelPreview", backdrop.transform);
            _preview = preview.AddComponent<RawImage>(); _preview.texture = _pixels.Texture;
            UiFactory.Stretch((RectTransform)preview.transform);
            preview.AddComponent<WindowDragHandle>().Initialize(_window.BeginWindowDrag);
            var coin = UiFactory.CreateButton("Coins", preview.transform, _view.EventFont, "", ToggleShop, Color.clear);
            UiFactory.AnchorTop((RectTransform)coin.transform, 348, 43, 129, 18);
            var round = UiFactory.CreateButton("Round", preview.transform, _view.EventFont, "", ToggleRounds, Color.clear);
            UiFactory.AnchorTop((RectTransform)round.transform, 348, 22, 102, 18);
            var repeat = UiFactory.CreateButton("Repeat", preview.transform, _view.EventFont, "", ToggleAuto, Color.clear);
            UiFactory.AnchorTop((RectTransform)repeat.transform, 453, 22, 24, 18);
            repeat.gameObject.AddComponent<SubjugationHover>().Changed = value => { _uiAutoHovered = value; _pixels.AutoHovered = value; };
            RefreshShop();
        }

        private void BuildDebugCoins()
        {
            if (_view.Dashboard == null) return;
            var button = UiFactory.CreateButton("ChiikawaDebugCoins", _view.Dashboard.transform, _view.EventFont, "코인 +9999", () =>
            {
                if (_storageBlocked) return;
                _game.GrantDebugCoins(); Persist();
                _view.Dashboard.ShowFeedback("이벤트 코인 9,999개 지급", false);
            });
            UiFactory.AnchorTop((RectTransform)button.transform, 378, 268, 88, 24);
            button.GetComponentInChildren<Text>().fontSize = 10;
            button.interactable = !_storageBlocked; _debugCoins = button.gameObject;
            var reset = UiFactory.CreateButton("ChiikawaDebugReset", _view.Dashboard.transform, _view.EventFont, "이벤트 초기화", ResetEvent);
            UiFactory.AnchorTop((RectTransform)reset.transform, 286, 268, 88, 24);
            reset.GetComponentInChildren<Text>().fontSize = 10;
            _debugReset = reset.gameObject;
        }

        /// <summary>Second press within four seconds wipes this event: the server rows, then the local save.</summary>
        private async void ResetEvent()
        {
            if (_resetting) return;
            var label = _debugReset.GetComponentInChildren<Text>();
            if (Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = Time.unscaledTime + 4;
                label.text = "다시 눌러 삭제";
                _view.Dashboard.ShowFeedback("이벤트 기록(라운드·코인·대원·보상 포인트)을 모두 지웁니다. 4초 안에 한 번 더 누르세요.", true);
                return;
            }
            _resetting = true; _resetArmedUntil = 0; label.text = "삭제 중";
            try
            {
                if (_remote.ResetAll != null) await _remote.ResetAll(_lifetime.Token);
                if (_disposed) return;
                _store.Delete();
                _game.Changed -= MarkDirty;
                _game = new SubjugationGame(null, _balance);
                _game.Changed += MarkDirty;
                _storageBlocked = false; _dirty = true; Persist();
                _rounds.Clear(); _bestRounds.Clear(); _claimed.Clear(); _claimsLoaded = false;
                LoadClaims(); RefreshShop(); _nextSync = 0;
                _remote.PointsChanged?.Invoke();
                _view.Dashboard.ShowFeedback("치이카와 이벤트 기록을 모두 삭제했습니다.", false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!_disposed) _view.Dashboard.ShowFeedback("이벤트 초기화 실패: " + e.Message, true);
            }
            finally { _resetting = false; if (!_disposed) label.text = "이벤트 초기화"; }
        }

        private void BuildRoundPage()
        {
            var page = _roundPage.transform;
            _roundHeading = Label("Hint", page, "", 12, 62, 456, 20, 12);
            var inputBg = UiFactory.CreateImage("RoundNumber", page, Color.white);
            UiFactory.AnchorTop(inputBg.rectTransform, 92, 92, 150, 28);
            _roundInput = inputBg.gameObject.AddComponent<InputField>();
            var text = Label("Value", inputBg.transform, "", 6, 0, 138, 28, 13);
            _roundInput.textComponent = text; _roundInput.targetGraphic = inputBg;
            _roundInput.contentType = InputField.ContentType.IntegerNumber; _roundInput.characterLimit = 10;
            var previous = UiFactory.CreateButton("Previous", page, _view.EventFont, "−", () => MoveRound(-1));
            UiFactory.AnchorTop((RectTransform)previous.transform, 12, 92, 68, 28);
            var next = UiFactory.CreateButton("Next", page, _view.EventFont, "+", () => MoveRound(1));
            UiFactory.AnchorTop((RectTransform)next.transform, 254, 92, 68, 28);
            var move = UiFactory.CreateButton("Move", page, _view.EventFont, "이동", ApplyRound);
            UiFactory.AnchorTop((RectTransform)move.transform, 334, 92, 134, 28);
            var auto = UiFactory.CreateButton("AutoAdvance", page, _view.EventFont, "", ToggleAuto);
            UiFactory.AnchorTop((RectTransform)auto.transform, 12, 134, 456, 28);
            _autoLabel = auto.GetComponentInChildren<Text>();
            var hover = auto.gameObject.AddComponent<SubjugationHover>(); hover.Changed = value => { _uiAutoHovered = value; _pixels.AutoHovered = value; };
            _roundPage.SetActive(false);
        }
        private void MoveRound(int delta)
        {
            if (!int.TryParse(_roundInput.text, out var round)) round = _game.Save.round;
            _roundInput.text = Math.Max(1L, Math.Min(_game.Save.maxRound, (long)round + delta)).ToString();
        }
        private void ApplyRound()
        {
            if (_storageBlocked) return;
            if (int.TryParse(_roundInput.text, out var round) && _game.SelectRound(round)) { Persist(); SetShop(false); }
            else _view.ShowFeedback("1~" + _game.Save.maxRound + " 라운드 사이에서 선택해 주세요.");
        }
        private void ToggleAuto()
        {
            if (_storageBlocked) return;
            _game.ToggleAutoAdvance(); Persist(); RefreshShop();
        }
        private void RefreshRounds()
        {
            _status.text = "현재 R." + _game.Save.round + "  ·  최고 R." + _game.Save.maxRound;
            _roundHeading.text = "도달한 라운드(1~" + _game.Save.maxRound + ")로 이동할 수 있습니다.";
            _autoLabel.text = "현재 라운드 반복 : " + (_game.RepeatRound ? "ON" : "OFF");
        }
        /// <summary>The strip's round label opens the window on the round tab, or closes it if already there.</summary>
        private void ToggleRounds()
        {
            if (!_visible) return;
            if (_shop.activeSelf && _tab == RoundTab) SetShop(false);
            else { SetTab(RoundTab); SetShop(true); }
        }

        private Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size)
        {
            var label = UiFactory.CreateText(name, parent, _view.EventFont, size, TextAnchor.MiddleLeft, new Color(.18f, .24f, .34f));
            label.text = value; UiFactory.AnchorTop(label.rectTransform, x, y, w, h); return label;
        }

        private void ToggleVisible()
        {
            _visible = !_visible;
            PlayerPrefs.SetInt(VisibleKey, _visible ? 1 : 0); PlayerPrefs.Save();
            if (!_visible) SetShop(false);
        }
        private void ToggleShop() { if (_visible) SetShop(!_shop.activeSelf); }
        private void SetShop(bool open)
        {
            if (open) { _view.ExitMiniModeForEvent(); _prepareShop?.Invoke(); RefreshShop(); if (_tab == RewardTab) LoadClaims(); }
            else { _uiAutoHovered = false; _pixels.AutoHovered = false; }
            _shop.SetActive(open);
            _window.SetEventPanelHeight(open ? ShopHeight : 0);
        }
        private void Purchase(Func<bool> operation)
        {
            if (_storageBlocked) return;
            if (operation()) { Persist(); RefreshShop(); }
        }
        private void MarkDirty() { _dirty = true; RefreshShop(); }

        private void SetTab(int tab)
        {
            _tab = tab;
            if (tab == RewardTab) LoadClaims();
            if (tab == RoundTab) _roundInput.text = _game.Save.round.ToString();
            _uiAutoHovered = false; _pixels.AutoHovered = false;
            RefreshShop();
        }

        private async void LoadClaims()
        {
            if (_remote.ReadClaims == null || _claimsLoading) return;
            _claimsLoading = true;
            try
            {
                var claimed = await _remote.ReadClaims(_lifetime.Token);
                if (_disposed) return;
                _claimed.Clear();
                foreach (var milestone in claimed) _claimed.Add(milestone);
                _claimsLoaded = true; _claimsFailed = false;
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!_disposed) _claimsFailed = true; }
            finally { _claimsLoading = false; if (!_disposed) RefreshShop(); }
        }

        private async void ClaimReward(int milestone)
        {
            if (_remote.ClaimReward == null || !_claimsLoaded || _claiming != 0 || _claimed.Contains(milestone) || !_game.HasCleared(milestone)) return;
            if (!IsRunning(DateTimeOffset.UtcNow)) { _view.ShowFeedback("이벤트가 종료되어 보상을 받을 수 없습니다."); return; }
            _claiming = milestone; RefreshShop();
            try
            {
                var awarded = await _remote.ClaimReward(milestone, _game.Save.bestClear, _lifetime.Token);
                if (_disposed) return;
                _claimed.Add(milestone);
                _view.ShowFeedback(awarded > 0 ? "R" + milestone + " 클리어 보상: 도토리온 포인트 +" + awarded + "P" : "이미 받은 보상입니다.");
                _remote.PointsChanged?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!_disposed) _view.ShowFeedback(e.Message != null && e.Message.Contains("event_ended")
                    ? "이벤트가 종료되어 보상을 받을 수 없습니다." : "보상을 받지 못했습니다. 잠시 후 다시 시도해 주세요.", true);
            }
            finally { _claiming = 0; if (!_disposed) RefreshShop(); }
        }

        private int ClaimableRewards
        {
            get { var n = 0; if (_claimsLoaded) foreach (var m in SubjugationGame.RewardMilestones) if (_game.HasCleared(m) && !_claimed.Contains(m)) n++; return n; }
        }

        private void RefreshRewards()
        {
            var offline = _remote.ClaimReward == null || _remote.ReadClaims == null;
            var earned = 0; foreach (var m in _claimed) earned += m;
            _status.text = PeriodText;
            _rewardEarned.text = offline ? "포인트 서버에 연결된 계정에서만 받을 수 있습니다."
                : _claimsFailed ? "보상 정보를 불러오지 못했습니다. 잠시 후 다시 열어 주세요."
                : !_claimsLoaded ? "보상 정보를 불러오는 중입니다."
                : "받은 도토리온 포인트 " + earned + " / 550P";
            for (var i = 0; i < 10; i++)
            {
                var milestone = SubjugationGame.RewardMilestones[i];
                var cleared = _game.HasCleared(milestone);
                var claimed = _claimed.Contains(milestone);
                _rewardButtons[i].GetComponentInChildren<Text>().text = claimed ? "받음" : _claiming == milestone ? "받는 중" : cleared ? "보상받기" : "미달성";
                _rewardButtons[i].interactable = !offline && _claimsLoaded && _claiming == 0 && cleared && !claimed;
                _rewardNames[i].color = cleared || claimed ? new Color(.18f, .24f, .34f) : new Color(.5f, .55f, .62f);
            }
        }

        private void RefreshShop()
        {
            if (_heading == null) return;
            _heading.text = "토벌대 관리  ·  " + _game.Save.coins.ToString("N0") + " 코인";
            var claimable = ClaimableRewards;
            _rewardTab.GetComponentInChildren<Text>().text = claimable > 0 ? "보상 " + claimable : "보상";
            _rosterTab.interactable = _tab != RosterTab; _rewardTab.interactable = _tab != RewardTab; _roundTab.interactable = _tab != RoundTab;
            _rosterPage.SetActive(_tab == RosterTab); _rewardPage.SetActive(_tab == RewardTab); _roundPage.SetActive(_tab == RoundTab);
            if (_tab == RewardTab) { RefreshRewards(); return; }
            if (_tab == RoundTab) { RefreshRounds(); return; }
            _status.text = _storageBlocked ? "저장 오류: 진행이 일시 중지되었습니다." : "출전 " + _game.PartyCount + "/3";
            for (var i = 0; i < 6; i++)
            {
                var hired = _game.Save.levels[i] > 0;
                var next = i == _game.NextHire;
                _faces[i].color = hired || next ? Color.white : new Color(.72f, .77f, .85f, .85f);
                _names[i].text = SubjugationGame.Names[i] + "  Lv." + _game.Level(i)
                    + "\n최대체력 " + _game.MaxHealth(i).ToString("0") + (i == 4 ? "  회복량 " : "  공격력 ") + _game.Power(i).ToString("0.#");
                _party[i].gameObject.SetActive(hired);
                _party[i].GetComponentInChildren<Text>().text = _game.Save.party[i] ? "출전중" : "출전";
                _party[i].interactable = !_storageBlocked && (_game.Save.party[i] ? _game.PartyCount > 1 : _game.PartyCount < 3);
                var cost = hired ? _game.LevelCost(i) : _game.Balance.hireCosts[i];
                var maxed = hired && _game.IsMaxLevel(i);
                _buy[i].GetComponentInChildren<Text>().text = maxed ? "최대 레벨" : hired ? "레벨업  " + cost.ToString("N0") + " 코인" : next ? "고용  " + cost.ToString("N0") + " 코인" : "이전 멤버 고용 필요";
                _buy[i].interactable = !_storageBlocked && (hired || next) && !maxed && _game.Save.coins >= cost;
            }
        }

        private void Update()
        {
            if (_game == null || _disposed) return;
            if (Application.isPlaying && !IsRunning(DateTimeOffset.UtcNow))
            {
                // Past the deadline the whole event leaves; OnDestroy restores the window and cards.
                // Only in the running app: the editor preview drives Update by hand on any date.
                _view.ShowFeedback("치이카와 토벌 대작전 이벤트가 종료되었습니다.");
                Destroy(this);
                return;
            }
            var local = _local();
            var online = local != null && local.IsClockedIn;
            var activity = !online ? 3 : local.ActivityStatus == ActivityStatus.Meal ? 2 : local.ActivityStatus == ActivityStatus.Break ? 1 : 0;
            var working = CanAdvance(local, DateTimeOffset.UtcNow) && _canAdvance() && !_storageBlocked;
            _game.Tick(Time.unscaledDeltaTime, working);
            if (working && _game.RetryRemaining == 0) _animationTime += Mathf.Min(Time.unscaledDeltaTime, 1);
            if (_toggle != null)
            {
                _toggle.gameObject.SetActive(online);
                _toggle.image.color = _visible ? Color.white : new Color(.6f, .65f, .75f);
            }
            if (_shop.activeSelf && (!online || _view.IsMiniModeVisible || _view.IsSettingsVisible || _view.IsStatisticsVisible || _view.IsAvatarPickerVisible || _view.IsDashboardVisible)) SetShop(false);
            var mini = _view.IsMiniModeVisible;
            var show = _visible;
            _window.SetEventStripHeight(show && !mini ? 64 : 0);
            _view.SetEventStripInset(show && !mini ? 64 : 0);
            _stripRoot.SetActive(show && !mini);
            if (Time.unscaledTime >= _nextFrame)
            {
                _nextFrame = Time.unscaledTime + .08f;
                var canvas = _view.GetComponent<Canvas>();
                _pixels.SetScale(Mathf.Max(_window.UiScale, canvas != null ? canvas.scaleFactor : 1));
                _pixels.AutoHovered = _uiAutoHovered || _native.AutoHovered;
                var renderActivity = activity == 0 && !working ? 3 : activity;
                // Windows results use a per-pixel-alpha native surface over the transparent keyed strip.
                var stripBackground = Color.clear;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                stripBackground = new Color(1, 0, 1, 1);
#endif
                _stripBackdrop.color = stripBackground;
                _pixels.Draw(_game, renderActivity, _animationTime);
                var nativeResult = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                nativeResult = !mini && renderActivity == 0 && _game.Outcome != 0;
#endif
                _native.Present(_window, _pixels.Pixels, show && (mini || nativeResult),
                    _pixels.RenderWidth, _pixels.RenderHeight, nativeResult);
                _preview.enabled = !nativeResult;
                if (_preview != null) _preview.texture = _pixels.Texture;
                _rounds[_memberId] = _game.Save.round;
                _bestRounds[_memberId] = _game.Save.maxRound;
                _view.BindEventRounds(_rounds, _bestRounds, _visible);
                if (_debugReset != null && !_resetting && _resetArmedUntil > 0 && Time.unscaledTime > _resetArmedUntil)
                { _resetArmedUntil = 0; _debugReset.GetComponentInChildren<Text>().text = "이벤트 초기화"; }
            }
            if (_native.ConsumeShopRequest()) ToggleShop();
            if (_native.ConsumeRoundRequest()) ToggleRounds();
            if (_native.ConsumeAutoRequest()) ToggleAuto();
            if (_dirty && Time.unscaledTime >= _nextSave) { Persist(); _nextSave = Time.unscaledTime + 10; }
            if (_remote.ReadRounds != null && !_syncing && Time.unscaledTime >= _nextSync) SyncRounds();
        }

        public static bool CanAdvance(MemberState member, DateTimeOffset now)
        {
            return member != null && member.IsClockedIn && member.ActivityStatus == ActivityStatus.Working
                && member.ConnectionStatus == ConnectionStatus.Connected && member.LastHeartbeatAtUtc.HasValue
                && now - member.LastHeartbeatAtUtc.Value < TimeSpan.FromMinutes(3);
        }

        private async void SyncRounds()
        {
            _syncing = true; _nextSync = Time.unscaledTime + 30;
            try
            {
                if (_remote.WriteRound != null) await _remote.WriteRound(_game.Save.round, _game.Save.maxRound, _lifetime.Token);
                var rounds = await _remote.ReadRounds(_lifetime.Token);
                if (_disposed) return;
                _rounds.Clear(); _bestRounds.Clear();
                foreach (var item in rounds) { _rounds[item.Key] = item.Value.current; _bestRounds[item.Key] = item.Value.best; }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!_disposed && !_syncWarning) { _syncWarning = true; _view.ShowFeedback("이벤트 팀 라운드 동기화 대기 중입니다. 개인 진행은 저장됩니다."); }
            }
            finally { _syncing = false; }
        }

        private void Persist()
        {
            if (!_dirty || _storageBlocked || _store == null) return;
            try { _store.Write(_game.Save); _dirty = false; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _storageBlocked = true;
                _view.ShowFeedback("이벤트 저장 실패: 진행을 일시 중지했습니다. " + e.Message, true);
                RefreshShop();
            }
        }
        private void OnApplicationPause(bool paused) { if (paused) Persist(); }
        private void OnApplicationQuit() => Persist();
        private void OnDisable() { if (_native != null) _native.Dispose(); Persist(); }
        private void OnDestroy()
        {
            _disposed = true; _lifetime.Cancel(); Persist();
            if (_game != null) _game.Changed -= MarkDirty;
            _native?.Dispose(); _pixels?.Dispose(); _art?.Dispose(); _lifetime.Dispose();
            if (_window != null) { _window.SetEventPanelHeight(0); _window.SetEventStripHeight(0); }
            if (_view != null) _view.SetEventStripInset(0);
            if (_frame != null) { SubjugationPixels.Release(_frame.texture); SubjugationPixels.Release(_frame); }
            if (_weapon != null) { SubjugationPixels.Release(_weapon.texture); SubjugationPixels.Release(_weapon); }
            if (_toggle != null) SubjugationPixels.Release(_toggle.gameObject);
            if (_shop != null) SubjugationPixels.Release(_shop);
            if (_stripRoot != null) SubjugationPixels.Release(_stripRoot);
            if (_debugCoins != null) SubjugationPixels.Release(_debugCoins);
            if (_debugReset != null) SubjugationPixels.Release(_debugReset);
            if (_view != null) _view.BindEventRounds(_rounds, _bestRounds, false);
        }
    }
}
