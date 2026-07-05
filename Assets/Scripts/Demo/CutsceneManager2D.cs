using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Demo
{
    /// <summary>
    /// Cutscene system — black screen + Chinese subtitles with RPG tick + tutorial bubbles + BGM.
    /// </summary>
    public sealed class CutsceneManager2D : MonoBehaviour
    {
        [Header("── Timing ──")]
        [SerializeField] private float fadeInDuration = 1.2f;
        [SerializeField] private float fadeOutDuration = 0.8f;
        [SerializeField] private float charDelay = 0.04f;
        [SerializeField] private float linePause = 0.35f;
        [SerializeField] private float blankLinePause = 0.7f;
        [SerializeField] private float endPause = 1.5f;
        [SerializeField] private float chapterShowDuration = 3.5f;
        [SerializeField] private float chapterFadeDuration = 1f;

        [Header("── Fonts ──")]
        [SerializeField] private int bodyFontSize = 28;
        [SerializeField] private int chapterFontSize = 44;
        [SerializeField] private int bubbleFontSize = 24;

        [Header("── BGM (drag .mp3 from Audio/Music/) ──")]
        [SerializeField] private AudioClip openingBGM;
        [SerializeField] private AudioClip phase1BGM;
        [SerializeField] private AudioClip phase2BGM;
        [SerializeField] private AudioClip phase3BGM;
        [SerializeField] private AudioClip endingBGM;
        [SerializeField] private AudioClip endingCreditsBGM;

        [Header("── SFX ──")]
        [SerializeField] private AudioClip typingTick;

        // Singleton
        public static CutsceneManager2D Instance { get; private set; }

        // State
        private static bool _openingHasPlayed;
        private static int _highestChapter = 1; // checkpoint: 1, 2, or 3
        public int CurrentPhase { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool PlayerHasWrapped { get; set; }
        public bool Phase2Played { get; private set; }
        public bool Phase3Played { get; private set; }
        public bool EndingPlayed { get; private set; }
        public bool TutorialDone { get; private set; }
        public event Action<int> OnPhaseChanged;

        // Internal
        private Canvas canvas;
        private Image blackPanel;
        private Text bodyText;
        private Text chapterText;
        private Text bubbleText;
        private GameObject bubbleObj;
        private CanvasGroup panelGroup;
        private bool skipFlag;
        private AudioSource bgmSource;
        private AudioSource tickSource;

        // ── Chinese Subtitles ───────────────────────────────────────────

        private static readonly string[] OpeningLines =
        {
            "传说中……",
            "凡是将锚沉入无人抵达的海域，",
            "并献上一位独特的海洋生命，",
            "便能与深海之神缔结契约，",
            "获得取之不尽的财富。",
            "",
            "船长年轻时，只是一个默默无闻的航海者。",
            "一次暴风雨中，他在海底发现了一枚古老的锚。",
            "当他将锚拖上海面时，海上传来低语：",
            "\"带来属于海洋之人……\"",
            "",
            "船长相信，只要完成献祭，",
            "这枚锚就会为他锁住财富，也锁住幸运。",
            "",
            "于是，他捕获了一条受伤的美人鱼，",
            "将她锁在船底，驶向传说中的献祭海域。",
            "",
            "午夜。",
            "船长命令船员抛下锚，",
            "准备开始献祭仪式。",
        };

        // Chapter-end monologues (speech bubbles before phase transition)
        private static readonly string[] Chapter1EndBubbles =
        {
            "这艘船……它在循环。",
            "无论我怎么跑，都会回到原点。",
            "",
            "但那些碎片……",
            "那些在我之前被献祭者的遗物……",
            "它们散落在船上各处。",
            "也许收集它们，就能打开某扇门。",
            "",
            "我开始明白锚的用法了。",
            "继续前进吧。",
        };

        private static readonly string[] Chapter2EndBubbles =
        {
            "钥匙……全部集齐了。",
            "",
            "我可以离开了？",
            "我能感觉到……",
            "锚在震动。",
            "",
            "有什么东西在等着我。",
            "但我已经没有退路了。",
            "来吧。",
        };

        private const string Chapter1Title = "第一章：逃出生天";

        private static readonly string[] TutorialBubbles =
        {
            "我……一定要逃出去。",
            "这里离海很近，",
            "我要找到离开这艘船的办法。",
            "",
            "我的腿没有力气……",
            "也许……我可以使用那个锚。",
            "",
            "E键 —— 攻击",
            "空格键 —— 跳跃 / 蹬墙",
            "",
            "锚的用法还有很多......",
            "但现在我只知道这个",
            "",
            "准备好了就开始吧。",
        };

        private static readonly string[] Phase2Lines =
        {
            "她拼命地跑。",
            "",
            "穿过船员的房间，墙上钉着便条，",
            "潦草地写满了关于黄金的许诺。",
            "",
            "穿过货舱，堆积的宝物被珊瑚覆盖，",
            "海洋生物的尸体诉说着无声的故事。",
            "",
            "但每一条走廊，都把她带回原点。",
            "这艘船……没有尽头。",
            "",
            "锚在黑暗中发出微弱的寒光。",
            "阴影中，她看到了什么——",
            "那些在她之前被献祭者的遗物。",
            "",
            "也许……它们能帮她找到出路。",
        };

        private const string Chapter2Title = "第二章：锚与钥匙";

        private static readonly string[] Phase3Lines =
        {
            "钥匙集齐了。",
            "我可以回家了",
            "",
            "但好像有什么东西来了。",
            "深海之神并不满足于这些碎片。",
            "",
            "一个灵魂从黑暗中升起。",
            "被同一枚锚所束缚。",
            "曾经也是祭品……",
            "如今成了深渊的守卫。",
            "",
            "它挡在她与自由之间。",
        };

        private const string Chapter3Title = "第三章：深渊的意志";

        private static readonly string[] EndingLines =
        {
            "守卫倒下了。",
            "锚终于安静了。",
            "",
            "她找到了船长的房间。",
            "墙上，颤抖的字迹写着最后的遗言：",
            "",
            "\"大海接受了我的献祭。\"",
            "\"但祭品从来不是美人鱼。\"",
            "\"我从不是做出选择的人。\"",
            "\"是锚选择了。\"",
            "\"它选择了我们所有人。\"",
            "",
            "船员们都已安静地死去。",
            "锚带走了船上的每一个灵魂。",
            "",
            "她跃入海中。",
            "",
            "锚依然沉在海底。",
            "等待着。",
            "聆听着。",
            "下一个听到低语的人。",
        };

        private const string FinTitle = "—— 终 ——";

        // ── Unity Lifecycle ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = 0.3f;
            bgmSource.spatialBlend = 0f;
            bgmSource.playOnAwake = false;
            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.loop = false;
            tickSource.volume = 0.35f;
            tickSource.spatialBlend = 0f;
            tickSource.playOnAwake = false;
        }

        private IEnumerator Start()
        {
            yield return null;
            CurrentPhase = 0;
            IsPlaying = false;
            Phase2Played = false;
            Phase3Played = false;
            EndingPlayed = false;
            TutorialDone = false;
            PlayerHasWrapped = false;

            var player = FindFirstObjectByType<PlayerController2D>();
            if (player != null) player.OnPlayerWrapped += () => PlayerHasWrapped = true;
            var collector = CollectionManager2D.Instance;
            if (collector != null) collector.OnAllCollected.AddListener(OnAllKeysCollected);

            if (!_openingHasPlayed)
            {
                // First time ever — full opening cutscene + tutorial
                _highestChapter = 1;
                yield return StartCoroutine(OpeningSequence());
                _openingHasPlayed = true;
            }
            else
            {
                // Respawn — jump to highest chapter reached
                yield return StartCoroutine(RespawnAtChapter(_highestChapter));
            }
        }

        private void Update()
        {
            if (IsPlaying && !skipFlag && Input.anyKeyDown) skipFlag = true;
            if (PlayerHasWrapped && !Phase2Played && !IsPlaying && TutorialDone) TriggerPhase2();
        }

        private void OnAllKeysCollected()
        {
            if (!Phase3Played && !IsPlaying) TriggerPhase3();
        }

        // ── UI ──────────────────────────────────────────────────────────

        private void BuildUI()
        {
            var cGo = new GameObject("CutsceneCanvas");
            cGo.transform.SetParent(transform);
            canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            var cs = cGo.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cGo.AddComponent<GraphicRaycaster>();

            var pGo = new GameObject("Panel");
            pGo.transform.SetParent(cGo.transform, false);
            blackPanel = pGo.AddComponent<Image>();
            blackPanel.color = Color.black;
            var pr = blackPanel.rectTransform;
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

            chapterText = MakeText("Chapter", pGo.transform, chapterFontSize,
                new Color(0.95f, 0.8f, 0.25f, 0f), TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));

            bodyText = MakeText("Body", pGo.transform, bodyFontSize,
                new Color(0.92f, 0.91f, 0.88f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.85f));

            // Speech bubble
            bubbleObj = new GameObject("Bubble");
            bubbleObj.transform.SetParent(cGo.transform, false);
            var bubbleBg = bubbleObj.AddComponent<Image>();
            bubbleBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
            var br = bubbleObj.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.15f, 0.55f); br.anchorMax = new Vector2(0.85f, 0.9f);
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;

            bubbleText = MakeText("BubbleText", bubbleObj.transform, bubbleFontSize,
                new Color(0.95f, 0.93f, 0.85f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            bubbleObj.SetActive(false);

            panelGroup = pGo.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            blackPanel.gameObject.SetActive(false);
        }

        private static Text MakeText(string n, Transform p, int sz, Color c, TextAnchor a,
            Vector2 amin, Vector2 amax)
        {
            var go = new GameObject(n); go.transform.SetParent(p, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = sz; t.color = c; t.alignment = a;
            var r = t.rectTransform; r.anchorMin = amin; r.anchorMax = amax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return t;
        }

        // ── Audio ───────────────────────────────────────────────────────

        private void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        private void PlayTick()
        {
            if (typingTick == null) return;
            tickSource.PlayOneShot(typingTick, 0.3f);
        }

        private IEnumerator CrossfadeBGM(AudioClip newClip, float duration = 1f)
        {
            if (newClip == null) yield break;
            float start = bgmSource.volume;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            bgmSource.Stop(); bgmSource.volume = 0f;
            PlayBGM(newClip);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, 0.3f, t / duration);
                yield return null;
            }
            bgmSource.volume = 0.3f;
        }

        private void DuckBGM(float to) { bgmSource.volume = to; }

        private void SilenceGameplay()
        {
            Time.timeScale = 0f;
            var p = FindFirstObjectByType<PlayerController2D>();
            if (p != null) p.StopAllAudioLoops();
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Show a gameplay bubble tutorial (called by TutorialTrigger2D). Pauses game, shows text, waits for key, resumes.</summary>
        public IEnumerator ShowGameplayBubble(string[] lines)
        {
            IsPlaying = true;
            blackPanel.color = new Color(0f, 0f, 0f, 0.7f);
            blackPanel.gameObject.SetActive(true);
            panelGroup.alpha = 1f;
            bodyText.text = "";
            chapterText.text = "";
            bubbleObj.SetActive(true);
            bool oldSkip = skipFlag;
            skipFlag = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    yield return new WaitForSecondsRealtime(0.4f);
                    continue;
                }

                bubbleText.text = "";
                for (int c = 0; c < line.Length; c++)
                {
                    if (skipFlag) { bubbleText.text = line; skipFlag = false; break; }
                    bubbleText.text = line.Substring(0, c + 1);
                    PlayTick();
                    yield return new WaitForSecondsRealtime(charDelay);
                }

                if (i < lines.Length - 1)
                {
                    string cur = bubbleText.text;
                    bubbleText.text = cur + "\n<color=#888888>▼</color>";
                    yield return new WaitForSecondsRealtime(0.3f);
                    while (!Input.anyKeyDown) yield return null;
                    skipFlag = false;
                }
            }

            yield return new WaitForSecondsRealtime(0.5f);
            while (!Input.anyKeyDown) yield return null;

            bubbleText.text = "";
            bubbleObj.SetActive(false);
            blackPanel.gameObject.SetActive(false);
            skipFlag = oldSkip;
            IsPlaying = false;
        }

        public void TriggerPhase2()
        {
            if (!PlayerHasWrapped || Phase2Played || IsPlaying || !TutorialDone) return;
            Phase2Played = true;
            StartCoroutine(ChapterEndSequence(Chapter1EndBubbles, Phase2Lines, Chapter2Title, 2, phase2BGM));
        }

        public void TriggerPhase3()
        {
            if (Phase3Played || IsPlaying) return;
            Phase3Played = true;
            StartCoroutine(ChapterEndSequence(Chapter2EndBubbles, Phase3Lines, Chapter3Title, 3, phase3BGM));
        }

        public void TriggerEnding()
        {
            if (EndingPlayed || IsPlaying) return;
            EndingPlayed = true;
            StartCoroutine(EndingSequence());
        }

        // ── Opening (story → tutorial → start game) ─────────────────────

        private IEnumerator OpeningSequence()
        {
            IsPlaying = true;
            SilenceGameplay();
            PlayBGM(openingBGM);
            yield return StartCoroutine(FadeTo(1f));
            yield return StartCoroutine(TypeLines(OpeningLines));
            bodyText.text = "";
            yield return new WaitForSecondsRealtime(0.6f);
            yield return StartCoroutine(ShowChapter(Chapter1Title));
            // Don't fade out yet — go into tutorial bubbles
            yield return StartCoroutine(TutorialSequence());
            yield return StartCoroutine(FadeTo(0f));
            yield return StartCoroutine(CrossfadeBGM(phase1BGM));
            Time.timeScale = 1f;
            TutorialDone = true;
            SetPhase(1);
            IsPlaying = false;
        }

        private IEnumerator TutorialSequence()
        {
            // Semi-transparent background
            blackPanel.color = new Color(0f, 0f, 0f, 0.7f);
            chapterText.text = "";
            bodyText.text = "";
            bubbleObj.SetActive(true);
            skipFlag = false;

            for (int i = 0; i < TutorialBubbles.Length; i++)
            {
                string line = TutorialBubbles[i];

                if (string.IsNullOrEmpty(line))
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }

                // Type each character with tick
                bubbleText.text = "";
                for (int c = 0; c < line.Length; c++)
                {
                    if (skipFlag) { bubbleText.text = line; skipFlag = false; break; }
                    bubbleText.text = line.Substring(0, c + 1);
                    PlayTick();
                    yield return new WaitForSecondsRealtime(charDelay);
                }

                // Wait for player to press any key to advance (except last message)
                if (i < TutorialBubbles.Length - 1)
                {
                    // Show continue hint
                    string currentText = bubbleText.text;
                    bubbleText.text = currentText + "\n<color=#888888>▼</color>";
                    yield return new WaitForSecondsRealtime(0.3f);
                    while (!Input.anyKeyDown) yield return null;
                    skipFlag = false; // consume the key press
                }
            }

            // Last message: "准备好了就开始吧" — wait for key then start
            yield return new WaitForSecondsRealtime(0.5f);
            bubbleText.text = "准备好了就开始吧。\n\n<color=#ffcc44>按任意键开始游戏</color>";
            while (!Input.anyKeyDown) yield return null;

            bubbleObj.SetActive(false);
            blackPanel.color = Color.black;
        }

        // ── Chapter-end monologue → cutscene ────────────────────────────

        private IEnumerator ChapterEndSequence(string[] bubbles, string[] cutsceneLines,
            string chapterTitle, int phase, AudioClip nextBGM)
        {
            IsPlaying = true;
            SilenceGameplay();
            DuckBGM(0.08f);

            // Step 1: Show character monologue bubbles
            yield return StartCoroutine(ShowBubbles(bubbles));

            // Step 2: Full cutscene (black screen + story text + chapter title)
            yield return StartCoroutine(FadeTo(1f));
            yield return StartCoroutine(TypeLines(cutsceneLines));
            bodyText.text = "";
            yield return new WaitForSecondsRealtime(0.6f);
            yield return StartCoroutine(ShowChapter(chapterTitle));
            yield return StartCoroutine(FadeTo(0f));
            yield return StartCoroutine(CrossfadeBGM(nextBGM));

            _highestChapter = phase;
            Time.timeScale = 1f;
            SetPhase(phase);
            IsPlaying = false;
        }

        private IEnumerator ShowBubbles(string[] lines)
        {
            blackPanel.color = new Color(0f, 0f, 0f, 0.7f);
            bodyText.text = "";
            chapterText.text = "";
            bubbleObj.SetActive(true);
            skipFlag = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    yield return new WaitForSecondsRealtime(0.4f);
                    continue;
                }

                bubbleText.text = "";
                for (int c = 0; c < line.Length; c++)
                {
                    if (skipFlag) { bubbleText.text = line; skipFlag = false; break; }
                    bubbleText.text = line.Substring(0, c + 1);
                    PlayTick();
                    yield return new WaitForSecondsRealtime(charDelay);
                }

                if (i < lines.Length - 1)
                {
                    string cur = bubbleText.text;
                    bubbleText.text = cur + "\n<color=#888888>▼</color>";
                    yield return new WaitForSecondsRealtime(0.3f);
                    while (!Input.anyKeyDown) yield return null;
                    skipFlag = false;
                }
            }

            yield return new WaitForSecondsRealtime(0.5f);
            bubbleText.text = "";
            bubbleObj.SetActive(false);
            blackPanel.color = Color.black;
        }

        // ── Respawn ─────────────────────────────────────────────────────

        private IEnumerator RespawnAtChapter(int chapter)
        {
            TutorialDone = true;
            Time.timeScale = 1f;

            if (chapter >= 3)
            {
                // Chapter 3: boss fight — spawn boss directly, no keys needed
                PlayerHasWrapped = true;
                Phase2Played = true;
                Phase3Played = true;
                PlayBGM(phase3BGM);
                SetPhase(3);
                yield return new WaitForSeconds(0.3f);
                var cm = CollectionManager2D.Instance;
                if (cm != null) cm.ForceSpawnBoss();
            }
            else if (chapter >= 2)
            {
                // Chapter 2: skip discovery, go straight to key collection
                PlayerHasWrapped = true;
                Phase2Played = true;
                PlayBGM(phase2BGM);
                SetPhase(2);
            }
            else
            {
                // Chapter 1: skip tutorial, start fresh Phase 1
                PlayBGM(phase1BGM);
                SetPhase(1);
            }

            yield return null;
        }

        // ── Phase transitions ───────────────────────────────────────────

        private IEnumerator RunPhaseTransition(string[] lines, string chapter, int phase, AudioClip nextBGM)
        {
            IsPlaying = true;
            SilenceGameplay();
            DuckBGM(0.08f);
            yield return StartCoroutine(FadeTo(1f));
            yield return StartCoroutine(TypeLines(lines));
            bodyText.text = "";
            yield return new WaitForSecondsRealtime(0.6f);
            yield return StartCoroutine(ShowChapter(chapter));
            yield return StartCoroutine(FadeTo(0f));
            yield return StartCoroutine(CrossfadeBGM(nextBGM));
            Time.timeScale = 1f;
            SetPhase(phase);
            IsPlaying = false;
        }

        private IEnumerator EndingSequence()
        {
            IsPlaying = true;
            SilenceGameplay();
            DuckBGM(0f); bgmSource.Stop();
            yield return new WaitForSecondsRealtime(1f);
            PlayBGM(endingBGM);
            yield return StartCoroutine(FadeTo(1f));
            yield return StartCoroutine(TypeLines(EndingLines));
            bodyText.text = "";
            yield return new WaitForSecondsRealtime(0.6f);
            yield return StartCoroutine(ShowChapter(FinTitle));
            yield return StartCoroutine(CrossfadeBGM(endingCreditsBGM));
            yield return new WaitForSecondsRealtime(6f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Visual ──────────────────────────────────────────────────────

        private IEnumerator FadeTo(float target)
        {
            blackPanel.gameObject.SetActive(true);
            float start = panelGroup.alpha;
            float dur = target > 0.5f ? fadeInDuration : fadeOutDuration;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                if (skipFlag) { panelGroup.alpha = target; skipFlag = false; yield break; }
                panelGroup.alpha = Mathf.Lerp(start, target, t / dur);
                yield return null;
            }
            panelGroup.alpha = target;
            if (target < 0.01f) { blackPanel.gameObject.SetActive(false); bodyText.text = ""; }
        }

        private IEnumerator TypeLines(string[] lines)
        {
            bodyText.text = ""; chapterText.text = "";
            chapterText.color = new Color(0.95f, 0.8f, 0.25f, 0f);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    bodyText.text += "\n";
                    yield return new WaitForSecondsRealtime(blankLinePause);
                    continue;
                }
                string before = bodyText.text;
                for (int c = 0; c < line.Length; c++)
                {
                    if (skipFlag)
                    {
                        bodyText.text = before + line + "\n";
                        for (int j = i + 1; j < lines.Length; j++)
                            bodyText.text += lines[j] + "\n";
                        skipFlag = false;
                        yield break;
                    }
                    bodyText.text = before + line.Substring(0, c + 1);
                    PlayTick();
                    yield return new WaitForSecondsRealtime(charDelay);
                }
                bodyText.text = before + line + "\n";
                yield return new WaitForSecondsRealtime(linePause);
            }
            yield return new WaitForSecondsRealtime(endPause);
        }

        private IEnumerator ShowChapter(string title)
        {
            chapterText.text = title;
            Color c = chapterText.color;
            for (float t = 0; t < chapterFadeDuration; t += Time.unscaledDeltaTime)
            {
                chapterText.color = new Color(c.r, c.g, c.b, t / chapterFadeDuration);
                yield return null;
            }
            chapterText.color = new Color(c.r, c.g, c.b, 1f);
            yield return new WaitForSecondsRealtime(chapterShowDuration);
            for (float t = 0; t < chapterFadeDuration; t += Time.unscaledDeltaTime)
            {
                chapterText.color = new Color(c.r, c.g, c.b, 1f - t / chapterFadeDuration);
                yield return null;
            }
            chapterText.color = new Color(c.r, c.g, c.b, 0f);
            chapterText.text = "";
        }

        // ── Boss Watch ──────────────────────────────────────────────────

        private IEnumerator WatchForBossDefeat()
        {
            BossController2D boss = null; float w = 0f;
            while (boss == null && w < 30f)
            {
                boss = FindFirstObjectByType<BossController2D>();
                if (boss == null) { yield return new WaitForSecondsRealtime(0.3f); w += 0.3f; }
            }
            if (boss != null) boss.OnBossDefeated += TriggerEnding;
        }

        private void SetPhase(int phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
            if (phase == 3) StartCoroutine(WatchForBossDefeat());
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
