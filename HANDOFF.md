# One Weapon (가제) — 작업 인수인계 (2026-10-21 기준)

새 대화(Claude Code, Unity MCP 연결 예정)에서 이어서 작업하기 위한 정리입니다. 먼저 이 문서를 읽고, 필요하면 `C:\Users\KGA01\.claude\projects\C--Users-KGA01-UnityProject-One-Project\memory\MEMORY.md`(자동 메모리)도 읽으세요.

## 1. 프로젝트와 일정
- 위치: `C:\Users\KGA01\UnityProject\One-Project` (Unity 6000.3.21f1, URP 17.3, Input System, IMGUI HUD). 약 1개월짜리 개인 게임 "One Weapon (가제)".
- 장르: 2D 횡스크롤 로그라이트. **던질 수 있는 무기 하나**를 던지고 직접 주워야 한다(Dungreed 영감). 3D 후드 캐릭터 + 2D 도트 몬스터를 2D 씬에 섞어 쓴다(정사영 카메라).
- 일정: MVP 10/8(완료, `main`의 tag `mvp`), CBT 10/9~10/16, OBT 10/17~10/24(현재), 릴리스 10/25~10/28. **발표는 10/27.** 오늘은 10/21.
- 사용자는 한국어로 대화합니다. 답변도 한국어로, 짧고 정직하게(되지 않은 것은 되지 않았다고).

## 2. 사용자 규칙 / 작업 방식 (꼭 지킬 것)
- **구글 드라이브는 `김슬빈` 폴더(id `1UeDQhfjHGRUVEkp5OylEaZ7ob2_EcXcT`)만** 사용. 개인 드라이브나 다른 폴더는 보지 않는다.
- **파일 다운로드는 매번 허락**(파일명, 출처, 크기를 말하고). VARCO 결과물도 마찬가지.
- 커밋/푸시는 사용자가 요청할 때(관행상 검증된 작업은 로컬 커밋까지 해 두고 푸시는 물어본다).
- **밸런스 조정은 콘텐츠를 다 만든 뒤에**(사용자 결정). 지금 숫자는 임시값.
- 크레딧이 드는 VARCO 작업은 비용을 먼저 말한다(이미지 1장 20, 3D 200).
- 별도 요청 전에는 `Packages/manifest.json`의 Unity MCP 패키지 변경(사용자가 직접 설치)을 커밋하지 않는다.

## 3. Git 상태
- 브랜치 `cbt`가 현재 작업 라인. `main` = MVP(tag `mvp`). `feature/monster-sprites`, `feature/stage-maps`, `feature/player-3d`는 과거 작업 브랜치.
- **`origin/cbt`보다 로컬이 5커밋 앞서 있음(푸시 안 됨)**: a8139c2, 7592ef8, 2dcb814, dbc00e6, b8f3478. 사용자에게 푸시를 물어볼 것.
- 미커밋: `Packages/manifest.json`, `Packages/packages-lock.json`(Unity MCP 패키지, 사용자 작업), `Assets/Art/Monsters/*.png.meta` 2개(가져오기 설정 자동 변경), 이 `HANDOFF.md`, `Tools/MapDesign/*`.
- 줄바꿈 경고(LF→CRLF)는 무시해도 된다.

## 4. 게임 구조 (현재)
한 판: **스테이지 1 → 스테이지 2 → 룬의 방 → 보스 → 게임 클리어**. 반복 라운드 없음. 보상 카드 없음(코드는 남아 있고 `GameManager.rewardAfterEveryStage`=false, 씬 값도 false).
- 스테이지 1(72칸)/2(92칸): 고정 맵. 몬스터를 모두 잡으면 출구(금색)가 열림(잠긴 동안 어두운 붉은색, "잠김 · 남은 적 N" 표시).
- 룬의 방(20칸): 일자 복도, 몬스터 없음. **출구는 처음부터 열려 있고**, 바닥 위에 떠 있는 "회수의 룬"(`RuneItem`)을 먹으면 Q 회수 스킬이 해금된다(먹지 않아도 통과 가능).
- 보스 스테이지: 단일 화면 아레나. 보스를 잡으면 `GameState.Won`(클리어 시간 표시, R 재시작). 보스 보상/회복 없음.
- 플레이어: HP 50, 공격 10, 방어 0, 대시 충전 2(최대 3). 피해 = max(1, 공격 − 방어). 이동 7, 점프 13(점프 높이 약 2.8), 중력 3.
- 몬스터(HP/공격 10 기준): 졸병 10, 정예 20, 비행 10, 슈터 20(**탄 1발씩 조준 사격**), 보스 150. 몬스터끼리, 플레이어와도 충돌하지 않고 겹친다. 걷는 몬스터는 막히면 약 2.4칸까지 뛰어오른다(`Enemy.ShouldStepUp`). 12칸 안에 들어오면 반응(aggro), 보스는 항상.
- 보스 패턴(`Boss.cs`): 돌진 / 돌 낙하(+골렘 소환) / 점프 내려찍기(+양쪽 충격파). 같은 패턴 연속 금지. 체력 50% 이하 2페이즈(붉은 틴트, 더 빠름, 돌 2발, 골렘 한도 2→4). 골렘은 보스 사망 시 같이 사라짐.
- 맵 지형: 바닥에 붙은 **단단한 블록**(`blocks`)과 위에서 서는 **통과 발판**(`platforms`). **구덩이·가시 없음**(무기를 잃어버리면 진행 불가이므로 의도적). 모든 면은 점프로 올라갈 수 있어야 하고 벽은 2.3 이하.

## 5. 구현 완료 목록
- 3D 후드 캐릭터(달리기/점프/대시/던지기 애니, 조준 방향 회전), **3D 단검**(손과 던진 무기 동일 모델, `DaggerSetup`).
- 도트 몬스터 5종+보스 3자세(대기/전조/돌진), **작은 골렘(VARCO 도트)**, **총알 도트**.
- 스테이지 3종 + 보스 아레나, 스테이지별 배경/지형 색, 지형 블록, 출구 잠금, 룬 아이템, 클리어 화면.
- 보스 패턴 3종 + 2페이즈. 돌/충격파/먼지는 코드로 그린 임시 그림(`BossHazards.cs`).
- 타이틀/일시정지 메뉴, 조작 안내, HUD(HP, 무기, 대시, 남은 적, 스테이지 이름, 공격/방어, 회수 쿨타임).

## 6. 코드 지도 (`Assets/Scripts`, `Assets/Editor`)
- `GameManager.cs`: 상태(Title/Playing/Reward/Lost/Paused/Won), 스테이지 진행(`BeginStage`, `NextStage`, `MapFor`), 맵 적용(`ApplyMap`: 블록/발판/배경 색/룬/출구), 스폰, HUD(OnGUI), 룬(`OnRuneCollected`), 클리어(`OnBossDefeated`). 상수 `StageGround=0, StageAir=1, StageRune=2, StageBoss=3`.
- `StageMaps.cs`: 맵 데이터와 `Validate`(도달 가능성/벽/출구/몬스터 위치 검사). **이 파일은 `Tools/MapDesign`의 스크립트로 생성된 파일입니다(아래 8 참고) — 직접 고치면 다음 생성에서 사라집니다.**
- `Player.cs`, `Player3DVisual.cs`, `Weapon.cs`, `CameraFollow.cs`, `StageExit.cs`, `RuneItem.cs`, `Enemy.cs`(+`Flyer`, `Shooter`, `Boss`), `EnemyBullet.cs`, `BossHazards.cs`(낙하 돌, 충격파, 먼지, 코드 생성 스프라이트), `Stats.cs`.
- 에디터 도구(`Assets/Editor`): `Player3DSetup`, `DaggerSetup`, `GolemSetup`, `MonsterSpriteApplier`, `HoodedTestBuilder`, `MvpBuild`(빌드), `MvpSceneBuilder`, `*TestLauncher`.
- 자동 점검 하네스(플레이 모드에서 스크린샷/로그): `StageAutoShot`(전체 흐름), `BossAutoShot`(보스 패턴 강제 순서), `Player3DAutoShot`, `MonsterAutoShot`, `CharacterAutoShot`. 결과 이미지는 `Library/HoodedDiag/*.png`.

## 7. 점검 방법과 함정 (Unity MCP가 있으면 더 쉽게 대체 가능)
지금까지는 Unity MCP 없이 이렇게 했습니다.
- `[InitializeOnLoad]` 런처가 `EditorPrefs` 키로 **한 번만** 자동 실행. 다시 돌리려면 런처의 키 버전을 올린다(`StageTestLauncher.Run.vNN`, `BossTestLauncher.Run.vNN`, `DaggerSetup.Auto.vN`, `GolemSetup.Auto.vN`). 현재 `StageTestLauncher`는 v17, `BossTestLauncher`는 v6.
- 결과는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`의 `[Stages]`, `[Boss]`, `[Dagger]`, `[Golem]` 접두 로그로 확인. 끝 표시는 `shots done`.
- **유니티 창이 앞에 있어야 컴파일/실행이 진행**된다(백그라운드면 멈춤).
- **플레이 모드 중에 스크립트를 고치면** 재컴파일로 `GameManager.Instance`가 사라져 `Enemy.FixedUpdate` NullReference가 수천 번 나온다(게임 버그 아님). 테스트 중에는 코드를 고치지 말 것.
- 보스 스테이지는 적이 0명이면 즉시 클리어 처리되므로, 테스트에서 `BeginStage(3,false)` 직후 **같은 프레임에** `SpawnBoss`를 호출해야 한다.
- 이미지 생성 시 자산을 지웠다 다시 만들면 프리팹 참조가 끊겨(마젠타/보이지 않음) 문제가 생긴다. 에셋은 그 자리에서 갱신할 것. 유니티의 앞면은 시계 방향.
- 새 스크립트/파일 추가 직후 "type not found"가 잠깐 나올 수 있다(재컴파일 후 사라짐).
- Windows 파이썬: `-I` 옵션을 쓰면 PIL이 안 보인다. Bash heredoc에서 `\n`이 실제 줄바꿈으로 바뀌는 경우가 있어, 코드 생성 스크립트에는 `chr(10)`을 썼다.

## 8. 맵 설계 도구 (`Tools/MapDesign`)
- `gen_maps.py`: 맵 3종(`s1`, `s2`, `rr`)을 파이썬으로 정의하고 도달 가능성/벽/헤드룸/몬스터 위치를 검증(`python gen_maps.py`). 도우미 `stairs()`, `pyramid()`.
- `gen_cs.py`: 위 정의로 `Assets/Scripts/StageMaps.cs`를 **통째로 생성**(절대 경로 하드코딩). 맵을 바꿀 때는 `gen_maps.py`를 고치고 `python gen_maps.py && python gen_cs.py`.
- `draw2.py`: 세 맵의 개요 그림을 `Library/HoodedDiag/maps_overview2.png`로 저장.
- 맵 규칙: 점프 2.5 이하 오르기, 간격 3.5 이하, 벽 2.3 이하, 출구/시작 주변은 평지, 몬스터는 블록 가장자리 금지.
- 사용자 피드백: 튀어나온 작은 둔덕/기둥/상자는 "의미 없어 보여서 불쾌" → 큰 구조만(계단 고지대, 탑, 성곽 길, 공중 발판길). 지금 맵이 비슷한 계단식 언덕 위주라 단조롭게 보일 수 있고, 사용자의 평가를 기다리는 중.

## 9. 에셋 파이프라인 (VARCO)
- VARCO 3D MCP는 **브라우저의 VARCO 편집기 탭이 열려 있어야** 동작(`list_flow_sessions`로 확인). 편집기에 이미 다른 프로젝트의 노드(던전 타일 등)가 있으니 건드리지 말고, 새 노드는 y≥40000 아래쪽에 만들고 `run_flow`를 **scope 지정**으로 실행.
- 2D 도트: `GenerateImage`(nano-banana-1, 1:1, 20크레딧) → 허락받고 다운로드 → 흰 배경 제거/격자 추정/다운샘플(예전 `process.py` 방식) → `Assets/Art/Monsters/`, 가져오기는 Point 필터, PPU=캔버스 너비. 참조 이미지 업로드는 base64를 정확히 넘겨야 해서(손으로 옮기면 깨짐) 텍스트 프롬프트를 권장.
- 도트 프롬프트 요령/주의는 메모리 `project_art_direction.md`(예: "chunky/blocky" 금지, 링크 닮음 회피).
- 3D 캐릭터는 VARCO → Blender 5.2(`C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`)로 FBX 재내보내기(리프 본 유지 `ignore_leaf_bones=False`, 회전 보정) → Unity. 자세한 함정은 메모리 `project_3d_character_pipeline.md`.

## 10. 빌드와 배포
- 빌드: 유니티 에디터를 **닫고** `"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\KGA01\UnityProject\One-Project" -executeMethod MvpBuild.BuildWindows -logFile <로그>` → `Builds/OneWeapon` → zip(`*BurstDebugInformation*` 제외).
- 마지막 zip은 `Builds/OneWeapon_CBT_20261013.zip`(**오래됨**: 단검, 보스 패턴, 골렘, 새 맵, 룬, 클리어가 없음). **발표용 새 빌드가 필요**.
- 기획서: Claude Docs 문서 id `6367a635-9960-410e-af2b-47143e8e169c`, PDF는 `Docs/One Weapon 게임 기획서.pdf`. **기획서는 옛 내용**(도트 캐릭터, 라운드 반복, 보상 카드, 보스 회수 카드 등)이라 현재 구조에 맞게 고쳐야 한다. 드라이브 재업로드는 사용자가 직접.

## 11. 남은 일 (발표 10/27 기준 우선순위)
1. **푸시(사용자 확인) + 발표용 새 빌드(zip)** 만들기, 처음부터 끝까지 한 판 플레이 점검(막히는 곳, 너무 어려운지).
2. **효과음/배경음**: 지금 소리가 전혀 없음(던지기, 적중, 피격, 대시, 회수, 보스 전조, 돌 낙하, 룬 획득 등). 무료 효과음 파일 사용 vs 코드로 합성 중 사용자 결정 필요.
3. **타격감**: 적중 이펙트, 화면 흔들림, 몬스터 사망 연출, 단검 튕김 효과.
4. 맵/구성 피드백 반영(사용자가 직접 걸어 본 뒤). 필요 시 동굴/지붕 같은 떠 있는 단단한 구조(지금은 미지원, 엔진 확장 필요).
5. **밸런스**: 보상 카드가 없어져 한 판 동안 성장이 없음. 몬스터 수(스테이지 2는 24마리)가 많아 어려울 수 있음. 메모리 `project_balance.md` 참고.
6. 3D 캐릭터 다듬기(달릴 때 앞으로 기울어짐, 위아래 조준 반영), 몬스터 걷기/피격 애니, 배경/발판 그림, 보스 2페이즈 진입 연출, 돌/충격파 그림 개선, UI 다듬기.
7. 정리: `cbt` → `main` 합치기와 태그, 기획서 갱신(제목, 아트 섹션, 현재 구조), AI 에셋 라이선스 확인(발표 질문 대비), 시연 영상 녹화, 발표 자료. 드라이브 PDF 재업로드/설문은 사용자가 직접.

## 12. 미결정 / 확인할 것
- 효과음 방식(무료 파일 vs 합성), 게임 제목, 발표 형식.
- 룬의 방 위치 해석: "보스전 앞에 룬 스테이지"로 이해해 구현함(사용자 확인 필요할 수 있음).
- Unity MCP 패키지(`com.coplaydev.unity-mcp`)를 프로젝트에 남길지.
