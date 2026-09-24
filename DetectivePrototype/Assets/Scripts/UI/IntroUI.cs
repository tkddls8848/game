using Detective.Core;
using Detective.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>시작 화면. 사건 개요(case_XX.json의 intro)와 조작법을 보여 주고 Enter로 수사를 시작한다.</summary>
    public class IntroUI : MonoBehaviour
    {
        private RectTransform _root;
        private Text _body;

        private void Awake()
        {
            _root = UIFactory.CreateStretch("Intro", transform, 0f);
            UIFactory.AddImage(_root, UIFactory.Night);

            RectTransform body = UIFactory.CreateRect("Body", _root,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-720f, -430f), new Vector2(720f, 430f));
            _body = UIFactory.AddText(body, 30, TextAnchor.MiddleCenter, UIFactory.Cream);
            _body.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void Start()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            CaseDefinition definition = manager.Database.Case;
            _body.text = UIFactory.Colorize("<size=24>사 건 기 록  제 1 호</size>", UIFactory.CreamMuted) + "\n"
                + "<size=64><b>" + definition.title + "</b></size>\n"
                + UIFactory.Colorize("<size=22>━━━━━━━━━━━━━━━━━━━━━━━━━━━━</size>", UIFactory.CreamMuted) + "\n\n"
                + definition.intro + "\n\n"
                + UIFactory.Colorize("<size=24>[WASD] 이동   [E] 조사·대화   [T] 타임라인 관찰   [N] 수사 노트   [F] 고발</size>", UIFactory.CreamMuted)
                + "\n\n" + UIFactory.Colorize("[Enter] 수사 시작", new Color(0.85f, 0.70f, 0.40f));

            // 컴포넌트 Awake 순서에 기대지 않고 확실히 맨 위에 그린다.
            _root.SetAsLastSibling();
            ModalState.Force(GameMode.Intro, Time.frameCount);
        }

        private void Update()
        {
            if (!ModalState.AcceptsInput(GameMode.Intro, Time.frameCount)) return;
            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter) && !Input.GetKeyDown(KeyCode.Space)) return;

            _root.gameObject.SetActive(false);
            ModalState.Exit(GameMode.Intro, Time.frameCount);
        }
    }
}
