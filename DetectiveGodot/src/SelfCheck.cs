using System.Collections.Generic;
using Detective.Case;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using Detective.NPC;
using DetectiveGodot.Platform;
using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 실제 데이터로 무결성·추리 가능성을 검사하고 종료 코드로 알린다.
    ///
    /// Unity 쪽에는 이 역할이 둘로 나뉘어 있다 — 에디터 메뉴 `Tools/Detective/Validate Game Data`와
    /// EditMode의 <c>GameDataTests</c>. 둘 다 Unity의 Resources 로더를 타므로 엔진 밖에서는 돌지 않는다.
    /// (그래서 <c>SharedLogicTests</c>는 그 파일 하나만 빼고 255개를 돌린다.)
    /// 여기가 Godot 쪽의 같은 게이트다: <see cref="GodotDataLoader"/>로 읽고, **공유 검사기**에 물린다.
    ///
    /// 검사 내용은 새로 쓰지 않았다. Unity 판 씬 빌더가 쓰는 것과 같은 함수들이다 —
    /// <see cref="GameDataValidator"/>(참조 무결성) · <see cref="CaseSolvabilityChecker"/>(추리 가능성) ·
    /// <see cref="ScriptValidator"/>(대본 무결성) · <see cref="SliceSolvability"/>(한 회차로는 못 듣는가) ·
    /// <see cref="ArtManifestValidator"/>(연출 참조) · <see cref="MovementTrackValidator"/>(이동 트랙).
    ///
    ///   godot --headless --path DetectiveGodot -- --selfcheck
    ///
    /// 종료 코드 0 = 전부 통과.
    /// </summary>
    public static class SelfCheck
    {
        public static bool Requested()
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--selfcheck") return true;
            return false;
        }

        /// <summary>문제 건수를 돌려준다. 0이면 통과.</summary>
        public static int Run()
        {
            int failures = 0;
            GD.Print("=== 데이터 자기검사 (Godot / 공유 검사기) ===");

            // ── 맵 ────────────────────────────────────────────
            RoomTable table = GodotDataLoader.LoadRoomTable();
            RoomLayout layout = RoomLayout.FromTable(table);
            failures += Report("rooms.json 무결성", RoomLayoutValidator.Validate(table));
            GD.Print($"  방 {layout.RoomCount}개 · 문 {layout.Doors.Count}개 · "
                     + $"벽 조각 {layout.BuildAllWallSegments().Count}개");

            // ── 저택 사건(case_01) ────────────────────────────
            var roster = new NpcRoster(GodotDataLoader.LoadNpcs());
            EvidenceTable evidence = GodotDataLoader.LoadEvidenceTable();
            List<DialogueFile> dialogues = GodotDataLoader.LoadDialogues();
            CaseDefinition case01 = GodotDataLoader.LoadCase(GodotDataLoader.DefaultCaseId);
            var database = new CaseDatabase(layout, roster, evidence, dialogues, case01);

            GD.Print($"  인물 {roster.All.Count}명 · 단서 {evidence.evidence.Length}개 · 대사 파일 {dialogues.Count}개");
            failures += Report("참조 무결성", GameDataValidator.Validate(database));
            failures += Report("추리 가능성 (case_01)", CaseSolvabilityChecker.Check(database));

            ArtManifest art = GodotDataLoader.LoadArtManifest();
            if (art != null) failures += Report("연출 참조 (art.json)", ArtManifestValidator.Validate(art, database));
            else GD.Print("  art.json 없음 — 넘어감");

            // ── 엿듣기 사건(case_02) ──────────────────────────
            ScriptDefinition script = GodotDataLoader.LoadScript(GodotDataLoader.EavesdropCaseId);
            if (script == null)
            {
                GD.PushError("  case_02 대본을 읽지 못했다");
                failures++;
            }
            else
            {
                GD.Print($"  대본 발화 {script.utterances.Length}개 · 목소리 {script.speakers.Length}개 · "
                         + $"회차 {SonarText.Clock(script.durationMs)}");
                failures += Report("대본 무결성", ScriptValidator.Validate(script, layout, roster));

                // 이 게임의 전제: 한 회차로는 전부 들을 수 없다.
                SliceSolvabilityReport solvability = SliceSolvability.Check(script, layout);
                failures += Report("회차 성립 조건", solvability.Problems);
                failures += Expect("필요한 사실이 어딘가에서는 들린다", solvability.EveryFactAudibleSomewhere);
                failures += Expect("한 방에 고정되면 전부는 못 듣는다", solvability.NoSingleRoomSuffices);
                failures += Expect("한 회차로 전부 모을 수는 없다", !solvability.SinglePassPossible);

                MovementTrackTable tracks = GodotDataLoader.LoadTracks(GodotDataLoader.EavesdropCaseId);
                if (tracks != null)
                    failures += Report("이동 트랙",
                        MovementTrackValidator.Validate(tracks, layout, roster, script.durationMs));
                else
                    GD.Print("  tracks.json 없음 — 넘어감");
            }

            GD.Print(failures == 0 ? "=== 전부 통과 ===" : $"=== 문제 {failures}건 ===");
            return failures;
        }

        private static int Report(string label, List<string> problems)
        {
            if (problems == null || problems.Count == 0)
            {
                GD.Print($"  [통과] {label}");
                return 0;
            }
            GD.PushError($"  [실패] {label} — {problems.Count}건");
            foreach (string problem in problems) GD.PushError("        " + problem);
            return problems.Count;
        }

        private static int Expect(string label, bool condition)
        {
            if (condition) { GD.Print($"  [통과] {label}"); return 0; }
            GD.PushError($"  [실패] {label}");
            return 1;
        }
    }
}
