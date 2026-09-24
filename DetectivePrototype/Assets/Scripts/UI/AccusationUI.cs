using Detective.Case;
using Detective.Core;
using Detective.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 고발(F) → 확인 → 결과. 6개 항목을 키보드로 고른다(↑↓ 항목, ←→ 선택지).
    /// 채점은 CaseGrader(순수 함수)가 하고, 화면에는 CASE SOLVED / CASE FAILED 두 가지만 보여 준다(§16).
    /// </summary>
    public class AccusationUI : MonoBehaviour
    {
        public KeyCode openKey = KeyCode.F;

        private enum Stage { Closed, Editing, Confirming, Result }

        private Stage _stage = Stage.Closed;
        private AccusationForm _form;
        private int _row;

        private RectTransform _formRoot;
        private Text _formLabel;
        private RectTransform _resultRoot;
        private Text _resultLabel;

        private void Awake()
        {
            _formRoot = UIFactory.CreateRect("Accusation", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-700f, -400f), new Vector2(700f, 400f));
            UIFactory.AddImage(_formRoot, UIFactory.PanelColor);
            _formLabel = UIFactory.AddText(UIFactory.CreateStretch("Text", _formRoot, 40f), 32, TextAnchor.UpperLeft, Color.white);
            _formLabel.verticalOverflow = VerticalWrapMode.Overflow;

            _resultRoot = UIFactory.CreateStretch("Result", transform, 0f);
            UIFactory.AddImage(_resultRoot, new Color(0.02f, 0.02f, 0.04f, 0.97f));
            RectTransform body = UIFactory.CreateRect("Body", _resultRoot,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-720f, -420f), new Vector2(720f, 420f));
            _resultLabel = UIFactory.AddText(body, 32, TextAnchor.MiddleCenter, Color.white);
            _resultLabel.verticalOverflow = VerticalWrapMode.Overflow;

            _formRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            int frame = Time.frameCount;
            GameManager manager = GameManager.Instance;
            if (manager == null) return;

            if (ModalState.IsExploring)
            {
                if (Input.GetKeyDown(openKey) && ModalState.TryEnter(GameMode.Accusation, frame))
                {
                    _form = new AccusationForm(manager.State);
                    _row = 0;
                    _stage = Stage.Editing;
                    _formRoot.gameObject.SetActive(true);
                    RefreshForm();
                }
                return;
            }

            if (_stage == Stage.Result)
            {
                if (ModalState.AcceptsInput(GameMode.Result, frame) && Input.GetKeyDown(KeyCode.R))
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                return;
            }

            if (!ModalState.AcceptsInput(GameMode.Accusation, frame)) return;

            if (_stage == Stage.Confirming)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit(manager, frame);
                else if (Input.GetKeyDown(KeyCode.Escape)) { _stage = Stage.Editing; RefreshForm(); }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(openKey))
            {
                Close(frame);
                return;
            }

            bool changed = false;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { _row = (_row + 1) % CaseGradeResult.FieldCount; changed = true; }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { _row = (_row + CaseGradeResult.FieldCount - 1) % CaseGradeResult.FieldCount; changed = true; }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { _form.Cycle((AccusationField)_row, +1); changed = true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { _form.Cycle((AccusationField)_row, -1); changed = true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { _stage = Stage.Confirming; changed = true; }

            if (changed) RefreshForm();
        }

        private void RefreshForm()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=44><b>고발</b></size>\n");
            sb.Append(UIFactory.Colorize("<size=26>여섯 항목을 모두 맞혀야 사건이 해결된다. 고발은 한 번뿐이다.</size>", UIFactory.MutedColor)).Append("\n\n");

            for (int i = 0; i < CaseGradeResult.FieldCount; i++)
            {
                var field = (AccusationField)i;
                string value = "◀  " + _form.Selected(field).label + "  ▶";
                string line = AccusationForm.FieldLabels[i] + "        " + value;
                sb.Append(i == _row ? UIFactory.Colorize("▶ <b>" + line + "</b>", UIFactory.AccentColor) : "   " + line);
                sb.Append("\n\n");
            }

            if (_stage == Stage.Confirming)
            {
                sb.Append(UIFactory.Colorize("<b>이대로 고발하시겠습니까?</b>   [Enter] 확정   [Esc] 다시 고르기", new Color(1f, 0.55f, 0.5f)));
            }
            else
            {
                sb.Append(UIFactory.Colorize("<size=26>[↑ ↓] 항목   [← →] 선택   [Enter] 고발   [F / Esc] 수사로 돌아가기</size>", UIFactory.MutedColor));
            }
            _formLabel.text = sb.ToString();
        }

        private void Submit(GameManager manager, int frame)
        {
            CaseDefinition definition = manager.Database.Case;
            CaseGradeResult result = CaseGrader.Grade(definition.answer, _form.ToSubmission());
            Debug.Log("[AccusationUI] 고발 결과: " + result.CorrectCount + "/" + CaseGradeResult.FieldCount);

            _stage = Stage.Result;
            _formRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(true);
            _resultRoot.SetAsLastSibling();
            ModalState.Force(GameMode.Result, frame);

            string title = result.Solved
                ? UIFactory.Colorize("<size=84><b>CASE SOLVED</b></size>", UIFactory.AccentColor)
                : UIFactory.Colorize("<size=84><b>CASE FAILED</b></size>", new Color(1f, 0.45f, 0.45f));
            _resultLabel.text = title + "\n\n" + (result.Solved ? definition.solvedText : definition.failedText)
                + "\n\n" + UIFactory.Colorize("[R] 처음부터 다시", UIFactory.MutedColor);
        }

        private void Close(int frame)
        {
            _stage = Stage.Closed;
            _formRoot.gameObject.SetActive(false);
            ModalState.Exit(GameMode.Accusation, frame);
        }
    }
}
