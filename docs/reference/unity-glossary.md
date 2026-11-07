# Unity 개발 용어 단어집

> 기준: Unity 6.3 LTS (6000.3.6f1), URP 17.3, Input System 1.18
> 작성일: 2026-11-04
> 공식 문서 링크 모음은 [unity-official-reference.md](unity-official-reference.md)를 본다. Unity 공식 정의는 [Unity Glossary](https://docs.unity3d.com/6000.3/Documentation/Manual/Glossary.html)를 따른다.

## 읽는 법

| 열 | 뜻 |
|---|---|
| **용어** | Unity Editor, 코드, 공식 문서에서 실제로 쓰는 영문 표기 |
| **한국어** | 국내 현업에서 흔히 부르는 말 |
| **뜻** | 짧은 정의 |
| **사용 환경** | 이 용어를 만나는 곳: 창, 메뉴, 컴포넌트, 코드, 파일, 회의 등 |

메뉴 경로는 `Window > Analysis > Profiler`처럼 `>`로 표기한다. 단축키는 Windows 기준이며 macOS는 `Ctrl` 대신 `Cmd`를 쓴다.

---

## 1. 에디터·툴 인터페이스

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Editor | 에디터 | Unity 개발 도구 프로그램 자체 | Unity Hub에서 버전별 설치 |
| Unity Hub | 유니티 허브 | Editor 버전 설치, 프로젝트 생성·열기, 라이선스 관리 런처 | 별도 앱 |
| Hierarchy | 하이어라키 | 현재 씬의 GameObject를 부모-자식 트리로 보여 주는 창 | `Window > General > Hierarchy` |
| Inspector | 인스펙터 | 선택한 오브젝트·에셋의 컴포넌트와 속성을 편집하는 창 | `Window > General > Inspector` |
| Project window | 프로젝트 창 | `Assets/`와 `Packages/` 파일을 탐색하는 창 | `Window > General > Project` |
| Scene view | 씬 뷰 | 월드를 편집하는 3D 작업 화면 | 메인 탭 |
| Game view | 게임 뷰 | 카메라에 찍힌 실제 플레이 화면 | 메인 탭, Play 모드 |
| Console | 콘솔 | 로그, 경고, 오류, 컴파일 에러를 보여 주는 창 | `Window > General > Console` (`Ctrl+Shift+C`) |
| Toolbar | 툴바 | Play/Pause/Step 버튼, 계정, 레이어 등이 있는 상단 바 | Editor 상단 |
| Play Mode | 플레이 모드 | Editor 안에서 게임을 실행한 상태. 이때 바꾼 값은 종료하면 되돌아간다 | `Ctrl+P` 시작/종료, `Ctrl+Shift+P` 일시정지 |
| Edit Mode | 에디트 모드 | 게임이 실행되지 않는 편집 상태 | 기본 상태 |
| Pause / Step | 일시정지 / 한 프레임 진행 | Play 중 멈추고 한 프레임씩 진행 | 툴바 |
| Overlay | 오버레이 | Scene 뷰 위에 뜨는 도구 패널 | Scene 뷰, `` ` `` 키로 메뉴 |
| Gizmo | 기즈모 | Scene 뷰에 그려지는 보조 도형(콜라이더 외곽선, 카메라 범위 등) | Scene 뷰 Gizmos 메뉴, `OnDrawGizmos()` |
| Handle | 핸들 | 오브젝트를 드래그해 조작하는 Scene 뷰 조작 도구 | Move/Rotate/Scale 툴, `Handles` API |
| Move / Rotate / Scale / Rect / Transform Tool | 이동·회전·크기·사각·통합 툴 | 오브젝트 변형 도구 | 단축키 `W` `E` `R` `T` `Y`, 시점 이동은 `Q` |
| Frame Selected | 선택 대상 포커스 | 선택한 오브젝트로 Scene 뷰 카메라를 맞춤 | `F` 키 |
| Pivot / Center | 피벗 / 중심 | 핸들 위치 기준. 오브젝트 원점과 바운드 중심 중 선택 | 툴 설정 오버레이 |
| Global / Local | 글로벌 / 로컬 | 핸들 축 기준. 월드 축과 오브젝트 자체 축 중 선택 | 툴 설정 오버레이 |
| Grid Snapping | 그리드 스냅 | 격자 단위로 위치를 맞춤 | Scene 뷰 툴바, `Ctrl` 드래그 |
| Vertex Snapping | 버텍스 스냅 | 정점끼리 붙여 배치 | `V` 키를 누른 채 드래그 |
| Scene Visibility / Pickability | 씬 가시성 / 선택 가능 여부 | 편집 중에만 숨기거나 선택을 막음(게임 결과에는 영향 없음) | Hierarchy 왼쪽 눈·손 아이콘 |
| Project Settings | 프로젝트 설정 | Physics, Input, Quality, Player 등 프로젝트 전역 설정 | `Edit > Project Settings` |
| Preferences | 환경설정 | 개인 Editor 설정(외부 스크립트 에디터, 색상 등) | `Edit > Preferences` |
| Package Manager | 패키지 매니저 | 패키지 설치, 업데이트, 제거 | Window 메뉴, `Packages/manifest.json` |
| Asset Store | 에셋 스토어 | 공식 에셋 마켓 | 웹, Package Manager의 My Assets |
| Inspector Debug Mode | 디버그 모드 | private 필드까지 보여 주는 Inspector 모드 | Inspector 우측 ⋮ 메뉴 > Debug |
| Lock (Inspector) | 인스펙터 잠금 | 다른 오브젝트를 선택해도 현재 Inspector 내용을 유지 | Inspector 자물쇠 아이콘 |
| Context Menu | 컨텍스트 메뉴 | 우클릭 또는 ⋮ 메뉴. 컴포넌트 Reset, Copy/Paste 값 등 | Inspector, Hierarchy |
| Prefab Mode | 프리팹 모드 | 프리팹 에셋만 단독으로 여는 편집 모드 | 프리팹 더블클릭 |
| Search | 검색 | 에셋, 씬 오브젝트, 메뉴, 설정 통합 검색 | `Ctrl+K` |
| Shortcuts Manager | 단축키 관리자 | 단축키 확인·변경 | `Edit > Shortcuts` |
| Layout | 레이아웃 | 창 배치 프리셋 | 우측 상단 Layout 드롭다운 |
| Animator window | 애니메이터 창 | 상태 머신·전이·파라미터 편집 | `Window > Animation > Animator` |
| Animation window | 애니메이션 창 | 키프레임과 커브로 클립을 편집 | `Window > Animation > Animation` (`Ctrl+6`) |
| Lighting window | 라이팅 창 | 라이트맵 베이크와 환경광 설정 | `Window > Rendering > Lighting` |
| Profiler | 프로파일러 | CPU, GPU, 메모리, 렌더링 비용 측정 | `Window > Analysis > Profiler` (`Ctrl+7`) |
| Frame Debugger | 프레임 디버거 | 한 프레임의 드로우콜과 렌더 패스를 단계별로 확인 | `Window > Analysis > Frame Debugger` |
| Physics Debugger | 물리 디버거 | 콜라이더, 접촉점, 쿼리를 시각화 | `Window > Analysis > Physics Debugger` |
| Rendering Debugger | 렌더링 디버거 | URP 렌더링 상태(라이팅, 머티리얼, 오버드로)를 디버그 | `Window > Analysis > Rendering Debugger` |
| Test Runner | 테스트 러너 | EditMode/PlayMode 테스트 실행 창 | `Window > General > Test Runner` |
| UI Builder | UI 빌더 | UI Toolkit 화면을 시각적으로 편집하는 도구 | `Window > UI Toolkit > UI Builder` |
| Build Profiles | 빌드 프로필 | Unity 6의 플랫폼별 빌드 설정 창(구 Build Settings) | `File > Build Profiles` |

---

## 2. 기본 구조·씬·에셋

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Project | 프로젝트 | `Assets`, `Packages`, `ProjectSettings` 폴더를 가진 Unity 작업 단위 | 파일 시스템 |
| Asset | 에셋 | 프로젝트에 들어 있는 모든 리소스 파일(모델, 텍스처, 스크립트, 씬 등) | Project 창, `Assets/` |
| Scene | 씬 | 한 화면이나 레벨을 구성하는 GameObject 묶음. `.unity` 파일 | Hierarchy, `SceneManager` |
| GameObject | 게임오브젝트 | 씬에 놓이는 모든 개체의 기본 단위. 컴포넌트를 담는 그릇 | Hierarchy, `GameObject` 클래스 |
| Component | 컴포넌트 | GameObject에 붙어 기능을 제공하는 부품 | Inspector, `GetComponent<T>()` |
| Transform | 트랜스폼 | 위치·회전·크기와 부모-자식 관계. 모든 GameObject가 반드시 가짐 | Inspector 첫 컴포넌트, `transform` |
| Parent / Child | 부모 / 자식 | 계층 관계. 자식은 부모의 Transform을 따라간다 | Hierarchy 드래그, `SetParent()` |
| Local / World Space | 로컬 / 월드 좌표 | 부모 기준 좌표와 월드 원점 기준 좌표 | `localPosition` / `position` |
| Prefab | 프리팹 | 재사용 가능한 GameObject 템플릿 에셋. `.prefab` | Project 창, `Instantiate()` |
| Prefab Instance | 프리팹 인스턴스 | 씬에 놓인 프리팹 사본. 원본 변경을 따라감 | Hierarchy 파란 아이콘 |
| Override | 오버라이드 | 인스턴스에서 원본과 다르게 바꾼 값. Apply/Revert로 처리 | Inspector Overrides 드롭다운 |
| Nested Prefab | 중첩 프리팹 | 프리팹 안에 들어 있는 다른 프리팹 | Prefab Mode |
| Prefab Variant | 프리팹 변형 | 원본 프리팹을 상속해 일부만 바꾼 프리팹 | `Create > Prefab Variant` |
| Unpack Prefab | 프리팹 해제 | 인스턴스와 원본의 연결을 끊음 | Hierarchy 우클릭 |
| Tag | 태그 | 오브젝트 식별용 문자열 하나 | Inspector 상단, `CompareTag()` |
| Layer | 레이어 | 충돌, 렌더링, 레이캐스트 필터용 그룹(최대 32개) | Inspector 상단, `LayerMask` |
| Static (flag) | 스태틱 플래그 | 움직이지 않는 오브젝트 표시. 라이트맵, 배칭, 내비게이션 등에 사용 | Inspector 우측 상단 체크박스 |
| `.meta` file | 메타 파일 | 에셋의 GUID와 임포트 설정 저장 파일. 참조가 이것에 의존함 | 에셋 옆 숨김 파일 |
| GUID | GUID | 에셋 고유 식별자. 씬·프리팹이 에셋을 GUID로 참조 | `.meta`, `AssetDatabase` |
| Import Settings | 임포트 설정 | 에셋을 Unity 형식으로 변환하는 옵션 | 에셋 선택 시 Inspector |
| Reimport | 재임포트 | 임포트를 다시 실행 | 에셋 우클릭 |
| AssetDatabase | 에셋 데이터베이스 | Editor에서 에셋을 다루는 API와 시스템 | 에디터 스크립트 |
| Library folder | 라이브러리 폴더 | 임포트 캐시. 지워도 재생성됨(버전 관리 제외) | `Library/` |
| Package | 패키지 | 기능 단위로 배포되는 모듈 | Package Manager, `Packages/` |
| `manifest.json` | 매니페스트 | 프로젝트가 의존하는 패키지와 버전 목록 | `Packages/manifest.json` |
| `packages-lock.json` | 패키지 잠금 파일 | 실제로 해석된 패키지 버전 기록 | `Packages/` |
| Special Folders | 특수 폴더 | `Editor`, `Resources`, `StreamingAssets`, `Plugins` 등 의미가 정해진 폴더 이름 | `Assets/` 하위 |
| Resources | 리소스 폴더 | `Resources.Load()`로 이름으로 불러오는 폴더(전부 빌드에 포함됨) | `Assets/**/Resources/` |
| StreamingAssets | 스트리밍 에셋 | 변환 없이 그대로 빌드에 복사되는 폴더 | `Application.streamingAssetsPath` |
| Addressables | 어드레서블 | 주소 기반 비동기 에셋 로드 시스템(패키지) | `Window > Asset Management` |
| AssetBundle | 에셋번들 | 에셋을 묶어 런타임에 로드하는 아카이브 | 빌드 파이프라인 |
| Preset | 프리셋 | 컴포넌트·임포터 설정값 저장본 | Inspector 슬라이더 아이콘 |
| Dirty | 더티 | 변경되었지만 저장되지 않은 상태. 씬 이름 옆 `*` | 에디터, `EditorUtility.SetDirty()` |

---

## 3. 스크립팅·C# 개발

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| MonoBehaviour | 모노비헤이비어 | GameObject에 붙는 스크립트의 기본 클래스 | `class X : MonoBehaviour` |
| ScriptableObject | 스크립터블 오브젝트 | GameObject 없이 데이터를 담는 에셋 클래스 | `[CreateAssetMenu]`, `.asset` |
| Event Functions | 이벤트 함수 | Unity가 정해진 시점에 호출하는 메서드 | `Awake`, `Start`, `Update` 등 |
| Awake | 어웨이크 | 오브젝트가 로드될 때 한 번 호출. 자기 참조 초기화 | 스크립트 |
| OnEnable / OnDisable | 활성화 / 비활성화 | 컴포넌트가 켜지거나 꺼질 때 호출. 이벤트 구독·해제 | 스크립트 |
| Start | 스타트 | 첫 `Update` 직전 한 번 호출. 다른 오브젝트 참조 초기화 | 스크립트 |
| Update | 업데이트 | 매 프레임 호출. 입력과 일반 로직 | 스크립트 |
| FixedUpdate | 픽스드 업데이트 | 고정 간격(기본 0.02초)으로 호출. 물리 처리 | 스크립트, `Time.fixedDeltaTime` |
| LateUpdate | 레이트 업데이트 | 모든 `Update` 후 호출. 카메라 추적 | 스크립트 |
| OnDestroy | 온디스트로이 | 오브젝트가 파괴될 때 호출 | 스크립트 |
| Execution Order | 실행 순서 | 이벤트 함수와 스크립트 간 호출 순서 | Manual, `[DefaultExecutionOrder]` |
| `Time.deltaTime` | 델타타임 | 이전 프레임부터 걸린 시간(초) | 프레임 독립 이동 |
| `Time.timeScale` | 타임스케일 | 게임 시간 배율. 0이면 일시정지 효과 | 일시정지, 슬로모션 |
| `Time.unscaledDeltaTime` | 비스케일 델타타임 | `timeScale`의 영향을 받지 않는 시간 | 일시정지 중 UI 애니메이션 |
| Fixed Timestep | 고정 시간 간격 | `FixedUpdate`와 물리 스텝 간격 | `Project Settings > Time` |
| Coroutine | 코루틴 | 여러 프레임에 걸쳐 실행되는 함수(`IEnumerator`) | `StartCoroutine()`, `yield return` |
| Awaitable | 어웨이터블 | Unity의 async/await 지원 타입 | `await Awaitable.NextFrameAsync()` |
| Serialization | 직렬화 | 필드 값을 씬·프리팹·에셋 파일에 저장하는 과정 | Inspector 노출 규칙 |
| `[SerializeField]` | 시리얼라이즈 필드 | private 필드를 직렬화하고 Inspector에 노출 | 필드 속성 |
| `[FormerlySerializedAs]` | 이전 이름 유지 | 필드 이름을 바꿔도 기존 저장값을 이어받음 | 필드 속성 |
| `[SerializeReference]` | 참조 직렬화 | 인터페이스·다형 타입을 참조로 직렬화 | 필드 속성 |
| `[Header]` / `[Tooltip]` / `[Range]` | 헤더 / 툴팁 / 범위 | Inspector 표시 꾸밈 속성 | 필드 속성 |
| `[RequireComponent]` | 필수 컴포넌트 | 이 스크립트를 붙일 때 지정 컴포넌트를 자동 추가 | 클래스 속성 |
| `[DisallowMultipleComponent]` | 중복 금지 | 같은 컴포넌트를 두 번 붙이지 못하게 함 | 클래스 속성 |
| `[ExecuteAlways]` | 항상 실행 | Edit 모드에서도 이벤트 함수 실행 | 클래스 속성 |
| `[ContextMenu]` | 컨텍스트 메뉴 항목 | Inspector ⋮ 메뉴에 메서드 실행 항목 추가 | 메서드 속성 |
| UnityEvent | 유니티 이벤트 | Inspector에서 콜백을 연결하는 이벤트 타입 | Button `OnClick` 등 |
| Callback / Delegate / Action | 콜백 / 델리게이트 / 액션 | 함수를 값으로 넘기는 C# 기능 | `event Action` |
| `Instantiate` / `Destroy` | 생성 / 파괴 | 오브젝트 복제와 제거 | 스크립트 |
| Object Pooling | 오브젝트 풀링 | 생성·파괴 대신 비활성 오브젝트를 재사용 | `UnityEngine.Pool.ObjectPool<T>` |
| Singleton | 싱글턴 | 인스턴스가 하나뿐인 클래스 패턴 | 매니저 클래스 |
| Fake null | 페이크 널 | 파괴된 `UnityEngine.Object`가 `== null`에서는 true인 현상. `?.`과 `??`는 이를 인식하지 못함 | null 체크 |
| Domain Reload | 도메인 리로드 | 스크립트 상태(static 포함)를 초기화하는 재로드 | Play 진입, 컴파일 후 |
| Enter Play Mode Options | 플레이 모드 진입 설정 | 도메인·씬 리로드 생략 여부 설정 | `Project Settings > Editor` |
| Compile Error | 컴파일 에러 | 스크립트 문법·타입 오류. 하나라도 있으면 Play 불가 | Console 빨간 메시지 |
| Assembly | 어셈블리 | 컴파일 결과 DLL 단위 | `Assembly-CSharp.dll` 등 |
| Assembly Definition (`.asmdef`) | 어셈블리 정의 | 폴더 단위로 어셈블리를 나눠 컴파일 시간과 의존성 관리 | `Create > Scripting > Assembly Definition` |
| Editor script | 에디터 스크립트 | Editor에서만 동작하는 코드. 빌드에 포함되지 않음 | `Editor` 폴더, `UnityEditor` 네임스페이스 |
| `#if UNITY_EDITOR` | 전처리기 지시문 | 플랫폼·환경별 조건부 컴파일 | 스크립트 |
| Custom Inspector / Editor | 커스텀 인스펙터 | Inspector 표시를 직접 정의 | `[CustomEditor]`, `Editor` 클래스 |
| EditorWindow | 에디터 창 | 직접 만드는 툴 창 | `[MenuItem]` + `EditorWindow` |
| Property Drawer | 프로퍼티 드로어 | 특정 타입·속성의 Inspector 표시 방식 정의 | `[CustomPropertyDrawer]` |
| `Undo.RecordObject` | 실행 취소 기록 | 에디터 툴 변경을 Ctrl+Z로 되돌릴 수 있게 기록 | 에디터 스크립트 |
| Mono / IL2CPP | 모노 / IL2CPP | 스크립팅 백엔드. JIT 기반 Mono와, C++로 변환하는 AOT 방식 IL2CPP | `Player Settings > Other Settings` |
| Code Stripping | 코드 스트리핑 | 빌드 시 사용하지 않는 코드 제거 | `Managed Stripping Level` |
| Job System / Burst | 잡 시스템 / 버스트 | 멀티스레드 작업과 고성능 컴파일러 | `IJob`, `[BurstCompile]` |
| DOTS / ECS | 닷츠 / ECS | 데이터 지향 기술 스택, 엔티티 컴포넌트 시스템 | Entities 패키지 |
| Visual Scripting | 비주얼 스크립팅 | 노드 그래프로 로직을 작성 | Script Graph, State Graph |
| Hot path | 핫 패스 | 매 프레임 실행되는 성능 민감 코드 | `Update` 내부 |

---

## 4. 물리

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| PhysX | 피직스 | Unity 3D 물리 엔진 | 내부 엔진 |
| Rigidbody | 리지드바디 | 물리 시뮬레이션을 받는 컴포넌트 | Inspector, `Rigidbody` |
| Is Kinematic | 키네마틱 | 물리력에 반응하지 않고 스크립트로만 움직이는 Rigidbody | Rigidbody 옵션, 이동 발판 |
| `linearVelocity` | 선속도 | Rigidbody 속도(Unity 6 이름, 구 `velocity`) | 스크립트 |
| `linearDamping` / `angularDamping` | 선형·각 감쇠 | 공기 저항 같은 감속(구 `drag`, `angularDrag`) | Rigidbody |
| Mass | 질량 | 충돌·힘 계산에 쓰이는 무게 | Rigidbody |
| Use Gravity | 중력 사용 | 중력 적용 여부. 전역 중력은 `Physics.gravity` | Rigidbody, `Project Settings > Physics` |
| Constraints (Freeze Position/Rotation) | 축 고정 | 특정 축의 이동·회전 고정. 횡스크롤에서 Z 이동과 회전 잠금에 사용 | Rigidbody Constraints |
| Interpolate / Extrapolate | 보간 / 외삽 | 물리 스텝 사이 렌더 위치를 부드럽게 처리 | Rigidbody Interpolate |
| Collision Detection (Discrete / Continuous) | 충돌 감지 방식 | 빠른 물체 관통 방지를 위한 연속 충돌 감지(CCD) | Rigidbody |
| ForceMode | 포스 모드 | 힘 적용 방식: Force, Impulse, Acceleration, VelocityChange | `AddForce(v, ForceMode.Impulse)` |
| Collider | 콜라이더 | 충돌 형태 컴포넌트: Box, Sphere, Capsule, Mesh 등 | Inspector |
| Trigger | 트리거 | 물리 반응 없이 겹침만 감지하는 콜라이더 | `Is Trigger`, `OnTriggerEnter` |
| `OnCollisionEnter/Stay/Exit` | 충돌 콜백 | 실제 충돌 시 호출 | 스크립트 |
| `OnTriggerEnter/Stay/Exit` | 트리거 콜백 | 트리거 겹침 시 호출 | 스크립트 |
| Contact Point / Normal | 접촉점 / 법선 | 충돌 지점과 면 방향. 바닥·벽·천장 판별에 사용 | `Collision.contacts`, `GetContact()` |
| Physics Material | 물리 머티리얼 | 마찰력과 반발력 설정 에셋(구 `PhysicMaterial`) | Collider의 Material 슬롯 |
| Friction / Bounciness | 마찰 / 반발 | 미끄러짐과 튕김 정도 | Physics Material |
| Layer Collision Matrix | 레이어 충돌 매트릭스 | 레이어끼리의 충돌 여부 표 | `Project Settings > Physics` |
| LayerMask | 레이어마스크 | 쿼리·충돌 대상 레이어 비트마스크 | `[SerializeField] LayerMask` |
| Raycast | 레이캐스트 | 광선을 쏴서 맞은 콜라이더를 찾음 | `Physics.Raycast` |
| SphereCast / BoxCast / CapsuleCast | 형상 캐스트 | 두께 있는 형상으로 쓸어 검사. 접지 판정에 자주 사용 | `Physics.*Cast` |
| Overlap | 오버랩 | 특정 영역 안 콜라이더 검색 | `Physics.OverlapBox/Sphere` |
| `QueryTriggerInteraction` | 트리거 포함 여부 | 쿼리에서 트리거를 포함할지 지정 | 쿼리 인자 |
| Character Controller | 캐릭터 컨트롤러 | Rigidbody 없이 충돌을 처리하는 캐릭터 이동 컴포넌트 | `Move()`, `isGrounded` |
| Skin Width / Step Offset / Slope Limit | 스킨 폭 / 턱 높이 / 경사 한계 | Character Controller의 충돌 여유, 오를 수 있는 턱, 최대 경사 | Inspector |
| Ground Check | 접지 판정 | 캐릭터가 바닥에 있는지 판단하는 로직 | 플랫포머 컨트롤러 |
| Joint | 조인트 | 두 Rigidbody를 연결: Hinge, Spring, Fixed, Configurable | 흔들 발판, 사슬 |
| Physics Debugger | 물리 디버거 | 콜라이더와 쿼리를 시각화 | `Window > Analysis` |

---

## 5. 애니메이션

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Mecanim | 메카님 | Unity의 Animator 기반 애니메이션 시스템 통칭 | 공식 문서 |
| Animation Clip | 애니메이션 클립 | 키프레임 애니메이션 데이터. `.anim` 또는 FBX 내부 | Project 창 |
| Animator | 애니메이터 | Controller를 실행하는 컴포넌트 | Inspector |
| Animator Controller | 애니메이터 컨트롤러 | 상태 머신 에셋. `.controller` | Animator 창 |
| State / State Machine | 상태 / 상태 머신 | 재생할 클립 단위와 그 묶음 | Animator 창 |
| Transition | 전이 | 상태 사이 이동 규칙 | Animator 화살표 |
| Has Exit Time | 종료 시간 사용 | 클립이 특정 지점까지 재생된 뒤 전이. 즉각 반응이 필요하면 끔 | Transition Inspector |
| Transition Duration | 전이 시간 | 두 상태를 섞는 시간 | Transition Inspector |
| Parameter (Float/Int/Bool/Trigger) | 파라미터 | 전이 조건에 쓰는 변수 | Animator 창 Parameters 탭 |
| `Animator.StringToHash` | 해시 변환 | 파라미터 이름을 정수 해시로 미리 변환 | 스크립트 |
| Blend Tree | 블렌드 트리 | 파라미터 값으로 여러 클립을 섞음(대기·걷기·달리기) | Animator 창 |
| Any State | 애니 스테이트 | 어느 상태에서든 전이 가능한 특수 노드 | Animator 창 |
| Entry / Exit | 진입 / 종료 노드 | 상태 머신 시작과 끝 | Animator 창 |
| Animation Layer | 애니메이션 레이어 | 여러 애니메이션을 겹쳐 재생(상체만 공격 등) | Animator Layers 탭 |
| Avatar Mask | 아바타 마스크 | 레이어 적용 부위 제한 | 레이어 설정 |
| Avatar | 아바타 | 모델 뼈대와 휴머노이드 구조 매핑 | 모델 Rig 탭 |
| Humanoid / Generic | 휴머노이드 / 제네릭 | 리그 타입. Humanoid는 리타게팅 가능 | 모델 Rig 탭 |
| Retargeting | 리타게팅 | 다른 휴머노이드 모델의 애니메이션을 재사용 | Humanoid 리그 |
| Root Motion | 루트 모션 | 애니메이션의 루트 이동을 실제 오브젝트 이동에 반영 | `Apply Root Motion` |
| Animation Event | 애니메이션 이벤트 | 클립 특정 프레임에서 함수 호출(발소리 등) | Animation 창, 클립 Events |
| StateMachineBehaviour | 상태 머신 비헤이비어 | 상태 진입·종료 시 실행되는 스크립트 | 상태에 Add Behaviour |
| IK (Inverse Kinematics) | 역운동학 | 손발 끝 위치에서 관절 각도를 계산 | `OnAnimatorIK` |
| Keyframe / Curve | 키프레임 / 커브 | 시간별 값과 보간 곡선 | Animation 창 |
| Dopesheet | 도프시트 | 키프레임을 시간축에 나열한 보기 | Animation 창 |
| Timeline | 타임라인 | 컷신과 연출용 시퀀서 | Timeline 창, Playable Director |
| Rig | 리그 | 뼈대와 스킨 구조 | 모델 임포트, DCC |
| Skinning / Weight | 스키닝 / 가중치 | 정점이 뼈를 따라가는 비율 | DCC 툴 |

---

## 6. 그래픽·렌더링 (URP)

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Render Pipeline | 렌더 파이프라인 | 화면을 그리는 전체 과정과 구성 | Graphics 설정 |
| SRP (Scriptable Render Pipeline) | 스크립터블 렌더 파이프라인 | C#으로 구성 가능한 파이프라인 구조 | URP, HDRP의 기반 |
| URP (Universal Render Pipeline) | 유니버설 렌더 파이프라인 | 다양한 플랫폼용 SRP. 이 프로젝트 사용 | URP Asset |
| HDRP | HD 렌더 파이프라인 | 고사양 PC·콘솔용 SRP | (이 프로젝트 미사용) |
| Built-in Render Pipeline (BiRP) | 빌트인 파이프라인 | 기존 기본 파이프라인. URP와 셰이더가 호환되지 않음 | 레거시 에셋 |
| URP Asset | URP 에셋 | 품질 수준별 렌더링 설정(그림자, HDR, MSAA, Render Scale) | `Project Settings > Graphics/Quality` |
| Renderer Data / Universal Renderer | 렌더러 데이터 | 렌더링 경로와 Renderer Feature 목록 | URP Asset의 Renderer List |
| Rendering Path (Forward / Forward+ / Deferred) | 렌더링 경로 | 조명 계산 방식. Forward+는 오브젝트당 라이트 수 제한이 사실상 없음 | Universal Renderer |
| Renderer Feature | 렌더러 피처 | 렌더링 단계에 커스텀 패스 추가(아웃라인 등) | Universal Renderer Inspector |
| Render Graph | 렌더 그래프 | URP의 렌더 패스 작성 API와 리소스 관리 체계 | 커스텀 패스 코드 |
| Camera Stack (Base / Overlay) | 카메라 스택 | 여러 카메라를 겹쳐 그림 | Camera Render Type |
| Render Scale | 렌더 스케일 | 내부 해상도 배율 | URP Asset |
| Draw Call | 드로우콜 | CPU가 GPU에 그리기 명령을 보내는 단위 | Frame Debugger, Stats |
| SetPass Call | 셋패스 콜 | 셰이더 상태 전환 횟수. 드로우콜보다 비용 지표로 중요 | Game 뷰 Stats |
| Batching | 배칭 | 여러 드로우를 묶어 비용 절감 | Static, Dynamic, SRP Batcher |
| SRP Batcher | SRP 배처 | 같은 셰이더 변형 오브젝트의 CPU 렌더 비용 절감 | URP Asset 옵션 |
| GPU Instancing | GPU 인스턴싱 | 같은 메시·머티리얼을 한 번에 여러 개 그림 | 머티리얼 옵션 |
| GPU Resident Drawer | GPU 상주 드로어 | GPU 기반 대량 렌더링(Forward+ 필요) | URP Asset |
| Culling | 컬링 | 보이지 않는 대상을 그리지 않음 | Frustum, Occlusion |
| Frustum | 절두체 | 카메라 시야 사각뿔 범위 | Camera |
| Occlusion Culling | 오클루전 컬링 | 가려진 오브젝트 제외 | `Window > Rendering > Occlusion Culling` |
| LOD (Level of Detail) | LOD | 거리별로 낮은 품질 메시로 교체 | LOD Group |
| Overdraw | 오버드로 | 같은 픽셀을 여러 번 그리는 현상(투명 오브젝트에서 심함) | Rendering Debugger |
| Post-processing | 후처리 | 렌더 후 화면 효과(Bloom, Color Grading, Vignette, Depth of Field) | Volume |
| Volume / Volume Profile | 볼륨 / 볼륨 프로필 | 후처리 설정과 적용 범위(Global/Local) | GameObject Volume 컴포넌트 |
| Tonemapping | 톤매핑 | HDR 값을 화면 범위로 변환 | Volume Override |
| Bloom | 블룸 | 밝은 부분 번짐 효과 | Volume Override |
| Anti-aliasing (MSAA / FXAA / SMAA / TAA) | 안티에일리어싱 | 계단 현상 완화 방식 | URP Asset, Camera |
| HDR | HDR | 1.0을 넘는 밝기 범위로 렌더링 | URP Asset |
| Color Space (Linear / Gamma) | 색 공간 | 조명 계산 색 공간. URP 권장은 Linear | `Project Settings > Player` |
| Frame Rate / FPS | 프레임레이트 | 초당 프레임 수 | `Application.targetFrameRate`, VSync |
| VSync | 수직동기 | 모니터 주사율에 프레임 맞춤 | `Project Settings > Quality` |
| Quality Level | 품질 단계 | 품질 프리셋별 설정 묶음 | `Project Settings > Quality` |

---

## 7. 셰이더·머티리얼·텍스처·모델

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Shader | 셰이더 | GPU에서 표면 색과 형태를 계산하는 프로그램 | `.shader`, `.shadergraph` |
| Material | 머티리얼 | 셰이더와 그 입력값(텍스처, 색) 조합 에셋. `.mat` | Renderer의 Materials 슬롯 |
| Shader Graph | 셰이더 그래프 | 노드로 셰이더를 만드는 도구 | `Create > Shader Graph` |
| ShaderLab / HLSL | 셰이더랩 / HLSL | Unity 셰이더 파일 문법과 GPU 코드 언어 | `.shader` 코드 |
| Lit / Unlit / Simple Lit | 릿 / 언릿 / 심플 릿 | URP 기본 셰이더. 조명 반영 여부와 품질 차이 | `Universal Render Pipeline/Lit` |
| Toon Shader (UTS) | 툰 셰이더 | 셀 셰이딩 스타일 셰이더 | `com.unity.toonshader` |
| Pink / Magenta material | 분홍 머티리얼 | 셰이더 오류나 파이프라인 불일치 표시 | URP에서 Built-in 셰이더 사용 시 |
| Shader Variant / Keyword | 셰이더 변형 / 키워드 | 기능 조합별로 컴파일된 셰이더 사본과 이를 켜는 스위치 | 빌드 시간·용량에 영향 |
| PBR | 물리 기반 렌더링 | 실제 물리 성질 기반 재질 표현 | Lit 셰이더 |
| Metallic / Smoothness | 금속성 / 매끄러움 | PBR 재질 파라미터 | Lit 머티리얼 |
| Albedo / Base Map | 알베도 / 베이스 맵 | 표면 기본 색 텍스처 | 머티리얼 |
| Normal Map | 노멀 맵 | 표면 요철을 빛 반응으로 표현하는 텍스처 | Texture Type: Normal map |
| Emission | 이미션 | 스스로 빛나는 색. Bloom과 함께 쓰임 | 머티리얼 |
| Occlusion Map (AO) | 앰비언트 오클루전 | 틈새 그늘 정보 | 머티리얼 |
| Render Queue / Surface Type (Opaque / Transparent) | 렌더 큐 / 표면 타입 | 그리는 순서와 불투명·반투명 구분 | 머티리얼 |
| Alpha Clipping | 알파 클리핑 | 알파 기준 이하 픽셀 버림(잎사귀 등) | 머티리얼 |
| Texture / Sprite | 텍스처 / 스프라이트 | 이미지 에셋 / 2D·UI용 이미지 | Texture Type |
| Mipmap | 밉맵 | 거리별 저해상도 텍스처 사본 | Texture Import |
| Texture Compression | 텍스처 압축 | 플랫폼별 GPU 압축 형식(BC, ASTC, ETC) | Import 플랫폼 탭 |
| sRGB (Color Texture) | sRGB | 색 텍스처는 켜고 마스크·노멀 등 데이터 텍스처는 끔 | Texture Import |
| Filter Mode / Wrap Mode | 필터·랩 모드 | 텍스처 확대 보간 방식과 경계 반복 방식 | Texture Import |
| Mesh | 메시 | 정점, 삼각형, UV로 된 3D 형상 데이터 | MeshFilter |
| MeshFilter / MeshRenderer | 메시 필터 / 메시 렌더러 | 형상 보관 / 화면 렌더링 | Inspector |
| SkinnedMeshRenderer | 스킨드 메시 렌더러 | 뼈에 따라 변형되는 메시 렌더러 | 캐릭터 |
| Vertex / Polygon / Triangle | 정점 / 폴리곤 / 삼각형 | 메시 구성 요소. 성능 지표로 쓰임 | Stats, DCC |
| UV | UV | 텍스처 좌표 | DCC, 모델 임포트 |
| Lightmap UV (UV2) | 라이트맵 UV | 라이트맵 전용 겹침 없는 UV | `Generate Lightmap UVs` |
| FBX | FBX | 모델·애니메이션 교환 표준 형식 | Blender 내보내기 |
| Scale Factor / Convert Units | 스케일 팩터 / 단위 변환 | 임포트 크기 보정. Unity 1 unit = 1 m | 모델 Model 탭 |
| Pivot / Origin | 피벗 / 원점 | 모델 회전·배치 기준점 | DCC에서 설정 |
| Normals / Tangents | 노멀 / 탄젠트 | 면 방향과 노멀맵 계산 축 | 모델 Model 탭 |
| Material Slot | 머티리얼 슬롯 | 메시의 서브메시별 머티리얼 칸 | MeshRenderer Materials |
| Texel Density | 텍셀 밀도 | 단위 면적당 텍스처 해상도. 모듈 간 일관성 기준 | 아트 가이드 |
| DCC | DCC 툴 | Blender, Maya 등 외부 콘텐츠 제작 툴 | 리소스 파이프라인 |

---

## 8. 라이팅

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Directional / Point / Spot / Area Light | 방향광 / 점광원 / 스포트 / 영역광 | 라이트 종류 | Light 컴포넌트 |
| Light Mode (Realtime / Mixed / Baked) | 라이트 모드 | 실시간 계산, 혼합, 미리 굽기 | Light 컴포넌트 |
| Lighting Mode (Baked Indirect / Shadowmask / Subtractive) | 라이팅 모드 | Mixed 라이트의 처리 방식 | Lighting 창 |
| Global Illumination (GI) | 전역 조명 | 반사된 간접광 계산 | Lighting 창 |
| Lightmap / Baking | 라이트맵 / 베이크 | 정적 조명을 텍스처로 미리 계산 | Lighting 창 Generate Lighting |
| Light Probe | 라이트 프로브 | 움직이는 오브젝트에 간접광 정보 제공 | Light Probe Group |
| Adaptive Probe Volume (APV) | 적응형 프로브 볼륨 | 자동 배치되는 프로브 기반 간접광(URP 지원) | Lighting 창 |
| Reflection Probe | 반사 프로브 | 주변 반사 이미지 캡처 | Reflection Probe 컴포넌트 |
| Skybox / Environment Lighting | 스카이박스 / 환경광 | 배경 하늘과 그로부터의 주변광 | Lighting 창 Environment |
| Shadow Cascade | 섀도 캐스케이드 | 거리별로 그림자 해상도 분할 | URP Asset |
| Shadow Distance | 그림자 거리 | 그림자를 그리는 최대 거리 | URP Asset |
| Shadow Bias | 그림자 바이어스 | 그림자 얼룩(acne)·떠 보임(peter-panning) 보정 | Light, URP Asset |
| Ambient Occlusion (SSAO) | 앰비언트 오클루전 | 틈새 어두움 효과(URP는 Renderer Feature) | Universal Renderer |

---

## 9. UI (uGUI · UI Toolkit)

### 9.1 uGUI (Unity UI)

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| uGUI / Unity UI | 유니티 UI | GameObject 기반 UI 시스템 | `com.unity.ugui` |
| Canvas | 캔버스 | UI 요소를 그리는 루트 영역 | Hierarchy |
| Render Mode (Screen Space - Overlay / Camera, World Space) | 렌더 모드 | 화면 최상단, 카메라 앞, 월드 공간 배치 방식 | Canvas |
| Canvas Scaler | 캔버스 스케일러 | 해상도별 UI 크기 조정. 보통 Scale With Screen Size | Canvas |
| Reference Resolution | 기준 해상도 | Canvas Scaler 기준 해상도 | Canvas Scaler |
| RectTransform | 렉트 트랜스폼 | UI 위치·크기·앵커를 담는 Transform | 모든 UI 요소 |
| Anchor / Pivot | 앵커 / 피벗 | 부모 기준 고정점 / 자신의 회전·크기 기준점 | RectTransform 앵커 프리셋 |
| Anchor Presets | 앵커 프리셋 | 자주 쓰는 앵커·스트레치 조합 | RectTransform 좌상단 사각형 |
| Graphic Raycaster | 그래픽 레이캐스터 | UI 클릭 감지 컴포넌트 | Canvas |
| Raycast Target | 레이캐스트 대상 | 클릭 감지 여부. 장식 이미지는 꺼서 비용 절감 | Image, Text |
| EventSystem | 이벤트 시스템 | UI 입력 이벤트 분배. 씬에 하나 필요 | Hierarchy |
| Input System UI Input Module | UI 입력 모듈 | Input System과 uGUI를 연결 | EventSystem |
| Image / Raw Image | 이미지 / 로 이미지 | 스프라이트 표시 / 텍스처 직접 표시 | UI 요소 |
| TextMeshPro (TMP) | 텍스트메시프로 | 고품질 텍스트 렌더링(Unity 6부터 uGUI에 포함) | `TextMeshProUGUI` |
| Font Asset (SDF) | 폰트 에셋 | TMP용 거리장 폰트 에셋. 한글은 글리프 범위 포함 필요 | Font Asset Creator |
| Button / Toggle / Slider / Scrollbar / Dropdown / Input Field | 기본 UI 컨트롤 | 상호작용 UI 요소 | `GameObject > UI` |
| Scroll View / Scroll Rect | 스크롤 뷰 | 스크롤 영역 | UI 요소 |
| Mask / Rect Mask 2D | 마스크 | 자식 UI를 영역 밖에서 잘라냄 | 컴포넌트 |
| Layout Group (Horizontal / Vertical / Grid) | 레이아웃 그룹 | 자식 자동 정렬 | 컴포넌트 |
| Content Size Fitter | 콘텐츠 사이즈 피터 | 내용 크기에 맞춰 자동 리사이즈 | 컴포넌트 |
| Layout Element | 레이아웃 엘리먼트 | 레이아웃 계산 시 최소·선호 크기 지정 | 컴포넌트 |
| Canvas Group | 캔버스 그룹 | 하위 UI 전체 알파·상호작용 일괄 제어 | 페이드, 비활성 처리 |
| 9-Slice (Sliced Image) | 9분할 | 모서리를 유지하고 가운데만 늘리는 스프라이트 | Sprite Editor Border |
| Sprite Atlas | 스프라이트 아틀라스 | 여러 스프라이트를 한 텍스처로 묶어 드로우콜 절감 | `Create > 2D > Sprite Atlas` |
| Navigation (UI) | UI 내비게이션 | 키보드·패드로 포커스 이동 순서 | Selectable 컴포넌트 |
| Canvas Rebuild | 캔버스 리빌드 | UI 변경 시 메시 재생성. 잦으면 성능 저하 → 동적·정적 Canvas 분리 | Profiler UI 항목 |

### 9.2 UI Toolkit

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| UI Toolkit | UI 툴킷 | 웹 방식(UXML/USS) UI 시스템. 에디터와 런타임 UI 모두 지원 | `UnityEngine.UIElements` |
| UXML | UXML | UI 구조 정의 파일(HTML과 유사) | `.uxml` |
| USS | USS | 스타일 시트(CSS와 유사) | `.uss` |
| VisualElement | 비주얼 엘리먼트 | UI Toolkit 기본 노드 | C# |
| UIDocument | UI 도큐먼트 | 씬에서 UXML을 표시하는 컴포넌트 | GameObject |
| Panel Settings | 패널 설정 | 런타임 UI 렌더링·스케일 설정 에셋 | UIDocument |
| Selector / Class | 셀렉터 / 클래스 | USS 스타일 대상 지정 | `.uss`, `AddToClassList()` |
| Flexbox / Yoga | 플렉스박스 / 요가 | UI Toolkit 레이아웃 방식과 엔진 | USS `flex-direction` 등 |
| Query (Q / Query) | 쿼리 | 요소 찾기 | `root.Q<Button>("name")` |
| Data Binding | 데이터 바인딩 | 데이터와 UI 값을 자동 연결(Unity 6 런타임 바인딩) | UI Builder, C# |
| IMGUI | IMGUI | 코드 즉시 모드 GUI. 주로 레거시 에디터 툴과 디버그 표시 | `OnGUI()` |

### 9.3 UI·UX 공통 용어

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| HUD | 허드 | 플레이 중 상시 표시 정보(체력, 코인, 시간) | 게임 화면 |
| Safe Area | 세이프 에어리어 | 노치·라운드 모서리를 피한 표시 영역 | `Screen.safeArea` |
| Aspect Ratio | 화면비 | 가로세로 비율 | Game 뷰 해상도 드롭다운 |
| Focus / Selected State | 포커스 / 선택 상태 | 현재 입력 대상 UI | 패드 내비게이션 |
| Modal / Popup | 모달 / 팝업 | 다른 입력을 막는 대화창 | 일시정지 메뉴 |
| Transition (UI) | 전환 효과 | 화면 간 페이드·슬라이드 | Canvas Group, 애니메이션 |
| Localization | 현지화 | 다국어 텍스트·에셋 대응 | Localization 패키지 |

---

## 10. 입력 (Input System)

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Input System | 인풋 시스템 | 액션 기반 새 입력 시스템 패키지 | `com.unity.inputsystem` |
| Input Manager (Legacy) | 구 입력 관리자 | `Input.GetAxis` 기반 이전 시스템 | `Project Settings > Input Manager` |
| Active Input Handling | 활성 입력 처리 | 새/구/둘 다 중 무엇을 쓸지 설정 | `Project Settings > Player` |
| Input Action Asset | 인풋 액션 에셋 | 액션 맵과 바인딩 정의 파일. `.inputactions` | Project 창 |
| Project-wide Actions | 프로젝트 전역 액션 | 프로젝트 기본 액션 에셋(`InputSystem.actions`) | `Project Settings > Input System Package` |
| Action Map | 액션 맵 | 상황별 액션 묶음(Player, UI) | 액션 에셋 |
| Action | 액션 | 의미 단위 입력(Move, Jump) | 액션 에셋 |
| Action Type (Value / Button / Pass Through) | 액션 타입 | 연속 값, 버튼, 필터 없는 통과 | 액션 속성 |
| Binding | 바인딩 | 액션과 실제 키·버튼 연결 | 액션 에셋 |
| Composite Binding (2D Vector 등) | 복합 바인딩 | WASD처럼 여러 키를 하나의 값으로 조합 | 액션 에셋 |
| Control Scheme | 컨트롤 스킴 | 키보드·패드 등 장치 조합 | 액션 에셋 |
| Interaction (Press / Hold / Tap / MultiTap) | 인터랙션 | 입력 패턴 조건 | 바인딩 속성 |
| Processor (Deadzone / Normalize / Invert) | 프로세서 | 입력 값 후처리 | 바인딩 속성 |
| Phase (started / performed / canceled) | 단계 | 액션 콜백 단계 | `action.performed += ...` |
| `WasPressedThisFrame` | 이번 프레임 눌림 | 폴링 방식 버튼 체크 | `InputAction` |
| PlayerInput | 플레이어 인풋 | 액션을 메시지·이벤트로 전달하는 컴포넌트 | GameObject |
| Generated C# Class | 생성 C# 클래스 | 액션 에셋에서 자동 생성한 래퍼 클래스 | 에셋 Inspector 옵션 |
| Rebinding | 리바인딩 | 런타임 키 재설정 | `PerformInteractiveRebinding()` |
| `InputTestFixture` | 입력 테스트 픽스처 | 가상 장치로 입력을 주입하는 테스트 도구 | Test Framework |
| Dead Zone | 데드존 | 스틱 미세 입력 무시 범위 | 프로세서, 설정 |

---

## 11. 오디오

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| AudioClip | 오디오 클립 | 소리 에셋 | Project 창 |
| AudioSource | 오디오 소스 | 소리를 재생하는 컴포넌트 | GameObject |
| AudioListener | 오디오 리스너 | 소리를 듣는 귀. 씬에 하나만 둠(보통 카메라) | Camera |
| Audio Mixer / Group | 오디오 믹서 / 그룹 | 볼륨·효과 버스. BGM, SFX 분리 | `Window > Audio > Audio Mixer` |
| Exposed Parameter | 노출 파라미터 | 스크립트로 조절하는 믹서 값 | `mixer.SetFloat()` |
| Snapshot | 스냅샷 | 믹서 상태 프리셋 전환 | Audio Mixer |
| Spatial Blend | 공간 블렌드 | 2D(0)와 3D(1) 사운드 비율 | AudioSource |
| Load Type (Decompress On Load / Compressed In Memory / Streaming) | 로드 타입 | 메모리와 CPU 사용 방식 | Audio Import |
| `PlayOneShot` | 원샷 재생 | 겹쳐 재생되는 효과음 재생 | AudioSource API |
| BGM / SFX | 배경음 / 효과음 | 음악과 효과음 구분 | 믹서 그룹명 |

---

## 12. 카메라

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Camera | 카메라 | 씬을 화면에 찍는 컴포넌트 | GameObject |
| Projection (Perspective / Orthographic) | 원근 / 직교 투영 | 원근감 유무 | Camera |
| Field of View (FOV) | 시야각 | 원근 카메라 화각 | Camera |
| Near / Far Clip Plane | 근·원 클립 평면 | 그리는 거리 범위 | Camera |
| Culling Mask | 컬링 마스크 | 카메라가 그릴 레이어 | Camera |
| Viewport Rect | 뷰포트 영역 | 화면 일부에 그리기 | Camera |
| Cinemachine | 시네머신 | 절차적 카메라 제어 패키지 | `com.unity.cinemachine` |
| Dead Zone / Soft Zone | 데드존 / 소프트존 | 카메라가 따라가지 않는 영역 / 부드럽게 따라가는 영역 | 카메라 추적 설정 |
| Look-ahead | 룩어헤드 | 진행 방향 앞쪽을 더 보여 주는 오프셋 | 횡스크롤 카메라 |
| Damping | 댐핑 | 추적 지연과 부드러움 | 카메라 스크립트, Cinemachine |
| Camera Bounds / Confiner | 카메라 경계 | 카메라 이동 제한 영역 | 레벨 끝 처리 |
| Screen Shake | 화면 흔들림 | 타격·착지 연출 | Cinemachine Impulse 등 |

---

## 13. 성능·프로파일링·디버깅

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Profiling | 프로파일링 | 성능 측정과 병목 분석 | Profiler |
| CPU / GPU Bound | CPU·GPU 병목 | 어느 쪽이 프레임 시간을 결정하는지 | Profiler |
| Frame Time (ms) | 프레임 시간 | 한 프레임 처리 시간. 60fps = 약 16.67ms | Profiler |
| Main Thread / Render Thread | 메인 스레드 / 렌더 스레드 | 게임 로직 스레드와 렌더 명령 스레드 | Profiler Timeline |
| Hierarchy / Timeline view | 계층 / 타임라인 보기 | Profiler CPU 모듈 표시 방식 | Profiler |
| Deep Profile | 딥 프로파일 | 모든 함수 호출 계측(오버헤드 큼) | Profiler 툴바 |
| `ProfilerMarker` | 프로파일러 마커 | 코드 구간을 직접 계측 | `Unity.Profiling` |
| GC Alloc | GC 할당 | 관리 힙 메모리 할당량. 매 프레임 0이 목표 | Profiler CPU 모듈 |
| Garbage Collection (GC) Spike | GC 스파이크 | 가비지 수거로 인한 순간 프레임 저하 | Profiler |
| Incremental GC | 점진적 GC | GC를 여러 프레임에 나눠 실행 | `Project Settings > Player` |
| Boxing | 박싱 | 값 타입이 object로 변환되며 할당 발생 | C# 코드 |
| Memory Profiler | 메모리 프로파일러 | 메모리 스냅샷 분석 패키지 | Package Manager |
| Stats (Game view) | 통계 | Batches, SetPass, Tris, FPS 요약 | Game 뷰 Stats 버튼 |
| Development Build | 개발 빌드 | 프로파일러 연결과 디버그 정보가 포함된 빌드 | Build Profiles |
| Autoconnect Profiler | 프로파일러 자동 연결 | 빌드 실행 시 Profiler 자동 연결 | Build Profiles |
| Breakpoint / Debugger Attach | 중단점 / 디버거 연결 | IDE로 실행 중인 Editor에 붙어 디버깅 | Visual Studio, Rider |
| Stack Trace | 스택 트레이스 | 오류 발생 호출 경로 | Console 하단 |
| NullReferenceException | 널 참조 예외 | 비어 있는 참조 접근. Inspector 연결 누락이 흔한 원인 | Console |
| MissingReferenceException | 미싱 참조 예외 | 파괴된 오브젝트 접근 | Console |
| Missing Script | 미싱 스크립트 | 스크립트 파일이나 GUID가 사라진 컴포넌트 | Inspector 경고 |
| Editor.log / Player.log | 에디터·플레이어 로그 | 로그 파일 | `%LOCALAPPDATA%\Unity\Editor\Editor.log` 등 |

---

## 14. 빌드·배포

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Build | 빌드 | 실행 파일 생성 | `File > Build Profiles` |
| Build Profile | 빌드 프로필 | 플랫폼·씬 목록·설정 묶음(Unity 6) | Build Profiles 창 |
| Scene List (Scenes In Build) | 빌드 씬 목록 | 빌드에 포함될 씬과 인덱스 | Build Profiles |
| Player Settings | 플레이어 설정 | 앱 이름, 아이콘, 해상도, 백엔드 | `Project Settings > Player` |
| Target Platform | 대상 플랫폼 | Windows, Android, WebGL 등 | Build Profiles |
| Switch Platform | 플랫폼 전환 | 대상 플랫폼 변경. 에셋 재임포트 발생 | Build Profiles |
| Scripting Define Symbols | 스크립팅 정의 심볼 | 조건부 컴파일 심볼 | Player Settings |
| Batch Mode | 배치 모드 | UI 없이 명령줄로 Editor 실행 | `-batchmode` |
| CI/CD | 지속 통합·배포 | 자동 빌드·테스트 파이프라인 | 빌드 서버 |
| Versioning (Bundle Version) | 버전 표기 | 앱 버전 번호 | Player Settings |

---

## 15. 테스트·품질

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Unity Test Framework (UTF) | 유니티 테스트 프레임워크 | NUnit 기반 테스트 도구 | `com.unity.test-framework` |
| EditMode Test | 에디트 모드 테스트 | Play 없이 실행하는 테스트 | Test Runner |
| PlayMode Test | 플레이 모드 테스트 | 실제 게임 루프에서 실행하는 테스트 | Test Runner, `[UnityTest]` |
| `[Test]` / `[UnityTest]` | 테스트 속성 | 일반 테스트 / 코루틴 테스트 | 테스트 코드 |
| `[SetUp]` / `[TearDown]` | 준비 / 정리 | 각 테스트 전후 실행 | 테스트 코드 |
| Test Assembly | 테스트 어셈블리 | 테스트 전용 `.asmdef` | `Tests` 폴더 |
| NUnit XML | 테스트 결과 파일 | 명령줄 테스트 결과 형식 | `-testResults` |
| Regression Test | 회귀 테스트 | 고친 버그가 재발하지 않는지 확인 | QA |
| Smoke Test | 스모크 테스트 | 핵심 경로가 동작하는지 빠르게 확인 | 빌드 직후 |
| Playtest | 플레이테스트 | 사람이 직접 플레이하며 감각·문제 확인 | QA |
| Repro Steps | 재현 절차 | 버그 재현 단계 | 버그 리포트 |
| Deterministic | 결정론적 | 같은 입력이면 같은 결과가 나옴 | 테스트, 리플레이 |

---

## 16. 협업·버전 관리

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Version Control (VCS) | 버전 관리 | Git, Unity Version Control(구 Plastic SCM) 등 | 저장소 |
| Visible Meta Files | 메타 파일 표시 | 버전 관리를 위해 `.meta`를 노출하는 설정(기본값) | `Project Settings > Version Control` |
| Force Text (Asset Serialization) | 텍스트 직렬화 | 씬·프리팹을 YAML 텍스트로 저장해 diff·병합 가능 | `Project Settings > Editor` |
| YAML | YAML | 씬·프리팹 텍스트 저장 형식 | `.unity`, `.prefab` |
| Smart Merge (UnityYAMLMerge) | 스마트 머지 | Unity YAML 전용 병합 도구 | Git mergetool 설정 |
| `.gitignore` | 깃 무시 목록 | `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` 등 제외 | 저장소 루트 |
| Git LFS | 깃 대용량 파일 | 대용량 바이너리(FBX, PSD, WAV) 관리 | `.gitattributes` |
| Merge Conflict | 병합 충돌 | 같은 파일을 동시에 수정해 생긴 충돌. 씬 파일은 작업 분담으로 예방 | Git |

---

## 17. 플랫포머 게임 디자인·게임 필(Game Feel) 용어

이 프로젝트(3D 횡스크롤 플랫포머)에서 자주 쓰는 설계 용어다. Unity 공식 용어가 아니라 **게임 개발 업계 공통 용어**다.

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| Side-scroller | 횡스크롤 | 좌우 진행 시점의 게임 | 장르 |
| 2.5D | 2.5D | 3D 그래픽에 2D 평면 게임플레이 | X·Y 이동, Z 고정 |
| Game Feel | 게임 필 | 조작 반응의 손맛 | 튜닝 회의 |
| Coyote Time | 코요테 타임 | 발판에서 떨어진 직후 짧게 점프를 허용하는 유예 시간 | 점프 로직 |
| Jump Buffer | 점프 입력 버퍼 | 착지 직전 누른 점프를 기억했다가 착지 순간 실행 | 점프 로직 |
| Variable Jump Height | 가변 점프 높이 | 버튼을 누른 시간에 따라 점프 높이가 달라짐 | 점프 컷(jump cut) |
| Apex | 정점 | 점프 최고점. 정점 근처 중력 완화로 체공감 조절 | 점프 튜닝 |
| Fall Multiplier | 낙하 배율 | 하강 시 중력을 키워 빠르게 떨어지게 함 | 점프 튜닝 |
| Terminal Velocity | 종단 속도 | 최대 낙하 속도 제한 | 낙하 튜닝 |
| Air Control | 공중 제어 | 공중에서 좌우 조작 가능 정도 | 이동 튜닝 |
| Acceleration / Deceleration | 가속 / 감속 | 최고 속도까지 걸리는 시간과 멈추는 시간 | 이동 튜닝 |
| Grounded | 접지 상태 | 바닥에 서 있는 상태 | 상태 플래그 |
| Wall Slide / Wall Jump | 벽 미끄러짐 / 벽 점프 | 벽 접촉 중 동작 | 확장 능력 |
| One-way Platform | 일방향 발판 | 아래에서 통과, 위에서 착지 가능한 발판 | 레벨 오브젝트 |
| Moving Platform | 이동 발판 | 움직이는 발판. 플레이어를 함께 운반해야 함 | 키네마틱 Rigidbody |
| Hazard | 위험 요소 | 닿으면 피해를 주는 오브젝트 | 레벨 오브젝트 |
| Kill Zone / Death Plane | 낙사 구역 | 떨어지면 사망 처리되는 영역 | 트리거 |
| Checkpoint / Respawn | 체크포인트 / 리스폰 | 재시작 지점과 부활 | 진행 시스템 |
| Collectible | 수집품 | 코인 등 획득 아이템 | 보상 구조 |
| Power-up | 파워업 | 능력 강화 아이템 | 확장 기능 |
| Goal | 골 지점 | 스테이지 클리어 지점 | 레벨 종료 |
| Stomp | 밟기 공격 | 적을 위에서 밟아 처치 | 전투 판정 |
| Hitbox / Hurtbox | 히트박스 / 허트박스 | 공격 판정 영역 / 피격 판정 영역 | 트리거 콜라이더 |
| Invincibility Frames (i-frames) | 무적 시간 | 피격 직후 일시 무적 | 피격 처리 |
| Knockback | 넉백 | 피격 시 밀려남 | 피격 처리 |
| Hit Stop | 히트 스톱 | 타격 순간 짧은 정지 연출 | `Time.timeScale` 등 |
| Squash & Stretch | 찌그러짐과 늘어남 | 점프·착지 시 형태 과장 연출 | 애니메이션 |
| Telegraph | 예고 동작 | 적 공격 전 신호 | 적 AI, 연출 |
| Difficulty Curve | 난이도 곡선 | 진행에 따른 난이도 변화 | 레벨 디자인 |
| Teach → Test → Twist | 가르치기 → 시험 → 변형 | 메커닉 도입 3단계 레벨 설계 기법 | 레벨 디자인 |
| Gray-box / Block-out | 그레이박스 / 블록아웃 | 단순 도형으로 만든 레벨 초안 | 레벨 제작 초기 |
| Readability | 가독성 | 발판·위험·수집품이 한눈에 구분되는 정도 | 아트·레벨 리뷰 |
| Critical Path | 핵심 경로 | 클리어에 반드시 필요한 경로 | 레벨·QA |
| Vertical Slice | 버티컬 슬라이스 | 최종 품질로 만든 짧은 대표 구간 | 마일스톤 |

---

## 18. 프로젝트 관리·문서 용어

| 용어 | 한국어 | 뜻 | 사용 환경 |
|---|---|---|---|
| GDD | 게임 디자인 문서 | 게임 설계서 | `docs/` |
| ADR | 아키텍처 결정 기록 | 기술 결정과 근거 기록 | `docs/architecture` |
| Prototype | 프로토타입 | 아이디어 검증용 빠른 시제품 | 초기 단계 |
| Milestone | 마일스톤 | 주요 목표 시점 | 일정 관리 |
| Sprint | 스프린트 | 짧은 반복 개발 주기 | 일정 관리 |
| Backlog | 백로그 | 해야 할 작업 목록 | 작업 관리 |
| Tech Debt | 기술 부채 | 미뤄 둔 구조 개선 비용 | 리팩터링 |
| Refactoring | 리팩터링 | 동작은 유지하고 구조를 개선 | 코드 작업 |
| Pipeline | 파이프라인 | 제작 흐름(Blender → FBX → Unity 등) | 아트·빌드 |
| Placeholder | 플레이스홀더 | 최종 에셋 전 임시 자원 | 제작 중 |
| Polish | 폴리싱 | 완성도 마무리 작업 | 후반 단계 |
| LTS | 장기 지원 버전 | Long Term Support 릴리스 | Unity 버전 선택 |
