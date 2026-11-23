# CLAUDE.md — 수학 로그라이크 (Unity 6.2 / C#)

이 파일은 Claude Code가 이 저장소에서 작업할 때마다 자동으로 읽는 규칙이다. 항상 한국어로 답한다.

## 프로젝트
- 수학 공식과 기호가 공격 수단이 되는 턴제 로그라이크 (Slay the Spire식 카드 전투 + Into the Breach식 적 행동 예고)
- 1인 초보 개발자와 함께 개발한다. Claude = C# 구현, ChatGPT = 기획, Gemini = QA
- 확정된 규칙의 기준: `Docs/01_결정로그.md`. 코드와 충돌하면 임의로 판단하지 말고 알린다.
- 구조 요약과 회귀 테스트 기준값: `Docs/02_코드현황.md` / 로드맵·미결사항: `Docs/05_로드맵과_미결사항.md`
- 각 단계가 끝나고 내가 테스트 통과를 알려 주면 Docs의 01, 02, 05 문서를 갱신한다. 01_결정로그에는 내가 준 ChatGPT 확정안과 내가 승인한 내용만 적는다. 03, 04는 수정하지 않는다.

## 폴더
- 스크립트: `Assets/_Project/Scripts/Battle/`
- 카드 데이터: `Assets/_Project/Data/Cards/` / 적 데이터: `Assets/_Project/Data/Enemies/`
- 씬: `Assets/Scenes/SampleScene.unity`

## 코드 작성 원칙
- 복잡한 디자인 패턴 금지. 유니티 컴포넌트에 바로 붙일 수 있는 단순한 C# 스크립트
- 핵심 로직마다 초보자도 이해할 수 있는 한국어 주석
- 요청한 기능(MVP) 외의 UI 연출·에셋 생성 코드는 넣지 않는다. 데이터와 로직 위주
- 수치는 하드코딩하지 않고 인스펙터 / ScriptableObject로 둔다
- 로직은 UI를 모른다. UI → 로직 방향의 호출만 허용
- 새 카드·공식이 추가돼도 기존 코드를 크게 고치지 않는 구조
- 파일 이름 = 클래스 이름 (Unity 규칙)

## 작업 방식
- 입력은 `[현재 개발 목표]` + `[ChatGPT가 확정한 기획 명세]` 형태로 온다.
- 한 번에 전부 만들지 않고 단계별로 구현한다. 코드를 쓰기 전에 계획을 먼저 보여 주고 승인을 받는다.
- 기획을 임의로 바꾸거나 기획에 없는 중요한 기능을 추가하지 않는다. 제안과 구현을 구분한다.
- 기존 코드를 크게 바꿔야 하면 변경 이유와 영향 범위를 먼저 설명하고 승인을 받는다.
- 각 단계가 끝나면 보고한다: 변경된 파일 목록 / 테스트 방법 / 기대 결과 / 남은 문제
- 기존 통과 테스트를 깨지 않는다. 큰 변경 후에는 회귀 테스트 기대 PASS 수를 알려 준다.

## 하지 말 것
- `.meta` 파일을 직접 만들거나 지우거나 수정하지 않는다. (새 스크립트의 .meta는 Unity가 만든다)
- `.unity`, `.asset`, `.prefab` 파일을 직접 편집하지 않는다. 인스펙터 설정이 필요하면 사용자에게 방법을 안내한다.
- `Library/`, `Temp/`, `ProjectSettings/`, `Packages/` 는 건드리지 않는다.
- 사용자 요청 없이 git commit / push 하지 않는다.

## 테스트
- Claude는 Unity를 직접 실행할 수 없다. 테스트는 사용자가 Unity에서 컴포넌트 우클릭 → `Run Auto Test` 로 실행한다.
- 결과·에러는 Unity 콘솔 로그로 확인한다. 에디터 로그 파일: `Logs/Editor.log` (프로젝트 폴더 안)
  (Unity 6.2는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`에 시작 부분만 쓰고 이후 로그는 이 파일에 쓴다)
  (사용자가 "로그 확인해"라고 하면 이 파일의 끝부분을 읽어서 PASS/FAIL 수와 에러를 정리한다)
- 현재 회귀 기준값 (FAIL 0 기준): CombatantTest 15 / DataTest 12 / CardManagerTest 40 / PlayerControllerTest 37 / EnemyControllerTest 23 / CombatManagerTest 39 / CardUpgradeTest 40 / TargetSelectionTest 56 / StatusEffectTest 78
