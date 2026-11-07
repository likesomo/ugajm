# Unity 공식 문서·가이드라인 레퍼런스

> 대상 프로젝트: `D:\AIStudy\PlatformerGame`
> 기준 버전: **Unity 6000.3.6f1 (Unity 6.3 LTS)**, URP 17.3.0, Input System 1.18.0
> 작성일: 2026-11-04 · 모든 링크는 작성일 기준 응답(HTTP 200)을 확인했다.
> 용어 풀이는 [unity-glossary.md](unity-glossary.md)를 본다.

---

## 0. 이 문서 사용법

1. **버전이 박힌 URL을 쓴다.** `docs.unity3d.com/Manual/...`(버전 없음)은 항상 최신 버전을 가리키므로, 이 프로젝트에서는 `docs.unity3d.com/6000.3/...` 형식을 쓴다.
2. **패키지 문서는 패키지 버전으로 찾는다.** 엔진 매뉴얼과 패키지 매뉴얼은 분리되어 있다. URL 형식은 `https://docs.unity3d.com/Packages/<패키지명>@<메이저.마이너>/manual/index.html`이다.
3. **정보 우선순위**: 버전별 Manual/Scripting API > 패키지 매뉴얼 > Unity 공식 e-book·Learn > Unity Discussions > 그 밖의 블로그.
4. 버전을 바꿨다면 6장의 업그레이드 가이드부터 확인한다.

---

## 1. 버전·지원 정책

| 항목 | 내용 | 출처 |
|---|---|---|
| 프로젝트 Editor | `6000.3.6f1` (`ProjectSettings/ProjectVersion.txt`) | 프로젝트 파일 |
| Unity 6.3 LTS 지원 기간 | **2027년 12월까지** 지원(Enterprise/Industry는 1년 추가) | [Unity 6 Releases & Support](https://unity.com/releases/unity-6/support) |
| LTS 성격 | 1년에 한 번 출시, 2년 지원, 버그 수정·플랫폼 패치 위주 | 같은 페이지 |
| Update 릴리스 | LTS 사이에 나오는 프로덕션용 릴리스. 다음 릴리스가 나올 때까지만 지원 | 같은 페이지 |
| 6000.3.6f1 릴리스 노트 | 수정 사항·알려진 문제 | [Release notes 6000.3.6f1](https://unity.com/releases/editor/whats-new/6000.3.6f1) |
| 과거 버전 다운로드 | Editor 아카이브 | [Unity Editor Archive](https://unity.com/releases/editor/archive) |
| LTS와 Tech Stream 비교 | 릴리스 체계 설명 | [LTS vs Tech Stream](https://unity.com/releases/lts-vs-tech-stream) |
| 시스템 요구사항 | Editor·플레이어 OS와 GPU 요구사항 | [System requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html) |

버전 표기 규칙: `6000.3.6f1` = 메이저 6000(Unity 6), 마이너 3, 패치 6, `f`는 정식(final) 빌드다. `a`는 알파, `b`는 베타를 뜻한다.

---

## 2. 핵심 진입점

| 문서 | 용도 | 링크 |
|---|---|---|
| Unity Manual (6000.3) | 엔진 기능 설명서 | https://docs.unity3d.com/6000.3/Documentation/Manual/index.html |
| Scripting API (6000.3) | C# 클래스와 메서드 레퍼런스 | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/index.html |
| 공식 용어집 | Unity 공식 Glossary | https://docs.unity3d.com/6000.3/Documentation/Manual/Glossary.html |
| Unity Docs 허브 | 서비스(UGS) 문서 등 통합 진입점 | https://docs.unity.com/ |
| Unity Learn | 공식 튜토리얼·코스 | https://learn.unity.com/ |
| How-to 허브 | 공식 모범 사례 문서와 e-book 모음 | https://unity.com/how-to |
| Issue Tracker | 공식 버그 검색(버전별 수정 여부) | https://issuetracker.unity3d.com/ |
| Roadmap | 기능 로드맵 | https://unity.com/roadmap |
| Discussions | 공식 포럼 (브라우저 접속 필요) | https://discussions.unity.com/ |
| Asset Store | 공식 에셋 마켓 | https://assetstore.unity.com/ |

---

## 3. 프로젝트 설치 패키지별 공식 문서

`Packages/manifest.json` 기준이다. 버전을 바꾸면 URL의 `@x.y`도 함께 바꾼다.

| 패키지 | 설치 버전 | 공식 매뉴얼 |
|---|---|---|
| Universal RP | `com.unity.render-pipelines.universal` 17.3.0 | [URP 17.3](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/manual/index.html) · [Manual 내 URP 섹션](https://docs.unity3d.com/6000.3/Documentation/Manual/universal-render-pipeline.html) |
| Shader Graph | URP 의존 (17.3) | [Shader Graph 17.3](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/index.html) |
| Input System | `com.unity.inputsystem` 1.18.0 | [Input System 1.18](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/index.html) |
| Test Framework | `com.unity.test-framework` 1.6.0 | [Test Framework 1.6](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html) |
| Timeline | `com.unity.timeline` 1.8.10 | [Timeline 1.8](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html) |
| AI Navigation | `com.unity.ai.navigation` 2.0.9 | [AI Navigation 2.0](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html) |
| Behavior | `com.unity.behavior` 1.0.16 | [Behavior 1.0](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/index.html) |
| uGUI (TextMeshPro 포함) | `com.unity.ugui` 2.0.0 | [uGUI 2.0](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html) |
| Unity Toon Shader | `com.unity.toonshader` 0.13.0-preview | [Toon Shader 0.13](https://docs.unity3d.com/Packages/com.unity.toonshader@0.13/manual/index.html) ※ preview 패키지 |
| Visual Scripting | `com.unity.visualscripting` 1.9.9 | [Visual Scripting 1.9](https://docs.unity3d.com/Packages/com.unity.visualscripting@1.9/manual/index.html) |

미설치지만 플랫포머에서 자주 검토하는 패키지는 아래와 같다. 도입 전 호환 버전을 Package Manager에서 확인한다.

| 패키지 | 용도 | 매뉴얼 |
|---|---|---|
| Cinemachine 3.1 | 추적 카메라, 데드존, 카메라 전환 | [Cinemachine 3.1](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html) |
| ProBuilder 6.0 | 에디터 안 그레이박스 레벨 제작 | [ProBuilder 6.0](https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/index.html) |
| Splines 2.8 | 경로 이동 발판, 레일 | [Splines 2.8](https://docs.unity3d.com/Packages/com.unity.splines@2.8/manual/index.html) |
| Addressables 2.7 | 에셋 비동기 로드와 메모리 관리 | [Addressables 2.7](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/index.html) |

---

## 4. 주제별 공식 문서 (Unity 6.3 Manual)

`M:`은 `https://docs.unity3d.com/6000.3/Documentation/Manual/`, `S:`는 `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/`의 약어다. 실제 링크는 전체 URL로 걸어 두었다.

### 4.1 에디터 인터페이스

| 주제 | 링크 |
|---|---|
| Hierarchy 창 | [Hierarchy](https://docs.unity3d.com/6000.3/Documentation/Manual/Hierarchy.html) |
| Inspector 창 | [Inspector](https://docs.unity3d.com/6000.3/Documentation/Manual/UsingTheInspector.html) |
| Project 창 | [Project window](https://docs.unity3d.com/6000.3/Documentation/Manual/ProjectView.html) |
| Scene 뷰 | [Scene view](https://docs.unity3d.com/6000.3/Documentation/Manual/UsingTheSceneView.html) · [탐색](https://docs.unity3d.com/6000.3/Documentation/Manual/SceneViewNavigation.html) · [오브젝트 배치](https://docs.unity3d.com/6000.3/Documentation/Manual/PositioningGameObjects.html) · [Grid snapping](https://docs.unity3d.com/6000.3/Documentation/Manual/GridSnapping.html) · [Scene visibility](https://docs.unity3d.com/6000.3/Documentation/Manual/SceneVisibility.html) |
| Game 뷰 | [Game view](https://docs.unity3d.com/6000.3/Documentation/Manual/GameView.html) |
| Console 창 | [Console](https://docs.unity3d.com/6000.3/Documentation/Manual/Console.html) |
| Overlays / Gizmos 메뉴 | [Overlays](https://docs.unity3d.com/6000.3/Documentation/Manual/Overlays.html) · [Gizmos menu](https://docs.unity3d.com/6000.3/Documentation/Manual/GizmosMenu.html) |
| 단축키 관리자 | [Shortcuts Manager](https://docs.unity3d.com/6000.3/Documentation/Manual/ShortcutsManager.html) |
| Search | [Search overview](https://docs.unity3d.com/6000.3/Documentation/Manual/search-overview.html) |
| Presets | [Presets](https://docs.unity3d.com/6000.3/Documentation/Manual/Presets.html) |
| Project Settings 전체 | [Project Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/comp-ManagerGroup.html) · [Player](https://docs.unity3d.com/6000.3/Documentation/Manual/class-PlayerSettings.html) · [Quality](https://docs.unity3d.com/6000.3/Documentation/Manual/class-QualitySettings.html) · [Graphics](https://docs.unity3d.com/6000.3/Documentation/Manual/class-GraphicsSettings.html) · [Tags and Layers](https://docs.unity3d.com/6000.3/Documentation/Manual/class-TagManager.html) · [Time](https://docs.unity3d.com/6000.3/Documentation/Manual/class-TimeManager.html) |
| 로그 파일 위치 | [Log files](https://docs.unity3d.com/6000.3/Documentation/Manual/log-files.html) |

### 4.2 기본 개념: GameObject·Component·Scene·Prefab

| 주제 | 링크 |
|---|---|
| GameObject / Component / Transform | [GameObjects](https://docs.unity3d.com/6000.3/Documentation/Manual/GameObjects.html) · [Components](https://docs.unity3d.com/6000.3/Documentation/Manual/Components.html) · [Transform](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Transform.html) |
| Tags / Layers | [Tags](https://docs.unity3d.com/6000.3/Documentation/Manual/Tags.html) · [Layers](https://docs.unity3d.com/6000.3/Documentation/Manual/Layers.html) |
| Scene / 멀티 씬 편집 | [Scenes](https://docs.unity3d.com/6000.3/Documentation/Manual/CreatingScenes.html) · [Multi-Scene editing](https://docs.unity3d.com/6000.3/Documentation/Manual/MultiSceneEditing.html) |
| Prefab | [Prefabs](https://docs.unity3d.com/6000.3/Documentation/Manual/Prefabs.html) · [Prefab Mode](https://docs.unity3d.com/6000.3/Documentation/Manual/EditingInPrefabMode.html) · [Overrides](https://docs.unity3d.com/6000.3/Documentation/Manual/PrefabInstanceOverrides.html) · [Nested](https://docs.unity3d.com/6000.3/Documentation/Manual/NestedPrefabs.html) · [Variants](https://docs.unity3d.com/6000.3/Documentation/Manual/PrefabVariants.html) |

### 4.3 에셋 관리·임포트

| 주제 | 링크 |
|---|---|
| 에셋 임포트 / AssetDatabase | [Importing assets](https://docs.unity3d.com/6000.3/Documentation/Manual/ImportingAssets.html) · [AssetDatabase](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetDatabase.html) |
| `.meta` 파일 (GUID 보존) | [Asset metadata](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html) |
| 특수 폴더 (`Editor`, `Resources`, `StreamingAssets` 등) | [Special folder names](https://docs.unity3d.com/6000.3/Documentation/Manual/SpecialFolders.html) |
| `Resources` 런타임 로드 | [Loading resources at runtime](https://docs.unity3d.com/6000.3/Documentation/Manual/LoadingResourcesatRuntime.html) |
| 모델(FBX) 임포트 | [Importing models](https://docs.unity3d.com/6000.3/Documentation/Manual/ImportingModelFiles.html) · [Model tab](https://docs.unity3d.com/6000.3/Documentation/Manual/FBXImporter-Model.html) |
| 텍스처 임포트 | [Importing textures](https://docs.unity3d.com/6000.3/Documentation/Manual/ImportingTextures.html) · [Texture Import Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/class-TextureImporter.html) · [Compression formats](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-compression-formats.html) · [Mipmaps](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-mipmaps-introduction.html) |
| 오디오 파일 | [Audio files](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioFiles.html) |
| Package Manager / manifest | [Packages](https://docs.unity3d.com/6000.3/Documentation/Manual/Packages.html) · [Package Manager window](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-ui.html) · [Project manifest](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-manifestPrj.html) |
| AssetBundle | [AssetBundles](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetBundlesIntro.html) |

### 4.4 스크립팅

| 주제 | 링크 |
|---|---|
| MonoBehaviour | [Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/class-MonoBehaviour.html) · [API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.html) |
| **이벤트 함수 실행 순서** | [Order of execution](https://docs.unity3d.com/6000.3/Documentation/Manual/execution-order.html) · [Event functions](https://docs.unity3d.com/6000.3/Documentation/Manual/event-functions.html) |
| 고정 업데이트(FixedUpdate) | [Fixed updates](https://docs.unity3d.com/6000.3/Documentation/Manual/fixed-updates.html) |
| 직렬화 규칙 | [Script serialization](https://docs.unity3d.com/6000.3/Documentation/Manual/script-serialization.html) |
| ScriptableObject | [Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html) · [API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ScriptableObject.html) |
| 코루틴 / async-await / Awaitable | [Coroutines](https://docs.unity3d.com/6000.3/Documentation/Manual/Coroutines.html) · [Async/await support](https://docs.unity3d.com/6000.3/Documentation/Manual/async-await-support.html) · [Awaitable API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html) |
| UnityEvent | [Unity events](https://docs.unity3d.com/6000.3/Documentation/Manual/unity-events.html) |
| Assembly Definition (`.asmdef`) | [Assembly definitions](https://docs.unity3d.com/6000.3/Documentation/Manual/assembly-definition-files.html) |
| Enter Play Mode 설정 / 도메인 리로드 | [Configurable Enter Play Mode](https://docs.unity3d.com/6000.3/Documentation/Manual/configurable-enter-play-mode.html) · [Domain reloading](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html) · [Scene reloading](https://docs.unity3d.com/6000.3/Documentation/Manual/scene-reloading.html) |
| 스크립팅 백엔드 | [IL2CPP](https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-backends-il2cpp.html) · [Managed code stripping](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping.html) · [Scripting restrictions](https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-restrictions.html) |
| Job System | [Job system](https://docs.unity3d.com/6000.3/Documentation/Manual/job-system.html) |
| 에디터 확장 | [Extending the Editor](https://docs.unity3d.com/6000.3/Documentation/Manual/ExtendingTheEditor.html) · [EditorWindow](https://docs.unity3d.com/6000.3/Documentation/Manual/editor-EditorWindows.html) · [Custom Inspector (UI Toolkit)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateCustomInspector.html) |

자주 찾는 Scripting API는 다음과 같다.

| 분류 | API |
|---|---|
| 속성(Attribute) | [`SerializeField`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SerializeField.html) · [`RequireComponent`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RequireComponent.html) · [`Header`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HeaderAttribute.html) · [`Tooltip`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/TooltipAttribute.html) · [`Range`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RangeAttribute.html) · [`CreateAssetMenu`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/CreateAssetMenuAttribute.html) · [`DefaultExecutionOrder`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html) · [`ExecuteAlways`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ExecuteAlways.html) · [`ContextMenu`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ContextMenu.html) · [`RuntimeInitializeOnLoadMethod`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html) |
| 에디터 전용 | [`MenuItem`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MenuItem.html) · [`InitializeOnLoad`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/InitializeOnLoadAttribute.html) · [`Undo.RecordObject`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Undo.RecordObject.html) · [`EditorUtility.SetDirty`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorUtility.SetDirty.html) |
| 오브젝트 | [`Instantiate`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.Instantiate.html) · [`Destroy`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.Destroy.html) · [`GetComponent`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GameObject.GetComponent.html) · [`TryGetComponent`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Component.TryGetComponent.html) · [`FindAnyObjectByType`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindAnyObjectByType.html) |
| 시간 | [`Time.deltaTime`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-deltaTime.html) · [`Time.fixedDeltaTime`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-fixedDeltaTime.html) · [`Time.timeScale`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-timeScale.html) · [`Application.targetFrameRate`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html) |
| 수학 | [`Mathf.MoveTowards`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mathf.MoveTowards.html) · [`Vector3.SmoothDamp`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Vector3.SmoothDamp.html) |
| 씬 / 풀링 / 로그 | [`SceneManager.LoadScene`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadScene.html) · [`ObjectPool<T>`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Pool.ObjectPool_1.html) · [`Debug.Log`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Debug.Log.html) |

### 4.5 물리 (3D, PhysX)

| 주제 | 링크 |
|---|---|
| 물리 섹션 개요 | [Physics](https://docs.unity3d.com/6000.3/Documentation/Manual/PhysicsSection.html) · [Physics settings](https://docs.unity3d.com/6000.3/Documentation/Manual/class-PhysicsManager.html) |
| Rigidbody | [Rigidbodies overview](https://docs.unity3d.com/6000.3/Documentation/Manual/RigidbodiesOverview.html) · [Rigidbody component](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Rigidbody.html) · [Interpolation](https://docs.unity3d.com/6000.3/Documentation/Manual/rigidbody-interpolation.html) · [CCD](https://docs.unity3d.com/6000.3/Documentation/Manual/ContinuousCollisionDetection.html) |
| Character Controller | [Character Controller](https://docs.unity3d.com/6000.3/Documentation/Manual/class-CharacterController.html) |
| Collider | [Colliders](https://docs.unity3d.com/6000.3/Documentation/Manual/CollidersOverview.html) · [Collider types](https://docs.unity3d.com/6000.3/Documentation/Manual/collider-types-introduction.html) · [Collider interactions](https://docs.unity3d.com/6000.3/Documentation/Manual/collider-interactions.html) |
| 레이어 기반 충돌 | [Layer-based collision](https://docs.unity3d.com/6000.3/Documentation/Manual/LayerBasedCollision.html) |
| Physics Material | [Physics Material](https://docs.unity3d.com/6000.3/Documentation/Manual/class-PhysicsMaterial.html) |
| 물리 디버그 시각화 | [Physics Debugger](https://docs.unity3d.com/6000.3/Documentation/Manual/PhysicsDebugVisualization.html) |
| 쿼리 API | [`Physics.Raycast`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.Raycast.html) · [`SphereCast`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.SphereCast.html) · [`OverlapBox`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.OverlapBox.html) |

> **Unity 6 API 이름 변경 주의**: `Rigidbody.velocity` → [`linearVelocity`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-linearVelocity.html), `drag` → [`linearDamping`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-linearDamping.html), `angularDrag` → `angularDamping`, `PhysicMaterial` → [`PhysicsMaterial`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsMaterial.html). 과거 튜토리얼 코드를 그대로 쓰면 경고나 오류가 난다.

### 4.6 애니메이션 (Mecanim)

| 주제 | 링크 |
|---|---|
| 개요 | [Animation overview](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationOverview.html) |
| Animator 컴포넌트 / 창 | [Animator](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Animator.html) · [Animator window](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimatorWindow.html) |
| Animator Controller | [Controller](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AnimatorController.html) · [Parameters](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationParameters.html) · [Transitions](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Transition.html) · [Blend Trees](https://docs.unity3d.com/6000.3/Documentation/Manual/class-BlendTree.html) · [Layers](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationLayers.html) · [StateMachineBehaviour](https://docs.unity3d.com/6000.3/Documentation/Manual/StateMachineBehaviours.html) |
| Avatar / Avatar Mask / IK | [Avatar setup](https://docs.unity3d.com/6000.3/Documentation/Manual/AvatarCreationandSetup.html) · [Avatar Mask](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AvatarMask.html) · [IK](https://docs.unity3d.com/6000.3/Documentation/Manual/InverseKinematics.html) |
| 루트 모션 | [Root Motion](https://docs.unity3d.com/6000.3/Documentation/Manual/RootMotion.html) |
| 애니메이션 이벤트 | [Animation Events](https://docs.unity3d.com/6000.3/Documentation/Manual/script-AnimationWindowEvent.html) |
| 파라미터 해시 | [`Animator.StringToHash`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animator.StringToHash.html) · [`SetTrigger`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animator.SetTrigger.html) |
| Timeline | [Timeline 1.8](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html) |

### 4.7 그래픽: URP·셰이더·라이팅

| 주제 | 링크 |
|---|---|
| URP 소개 | [URP introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-introduction.html) |
| URP Asset / 글로벌 설정 | [URP Asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html) · [Global settings](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-global-settings.html) |
| Universal Renderer / 렌더링 경로 | [Universal Renderer](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-universal-renderer.html) · [Rendering paths](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/rendering-paths-introduction-urp.html) |
| Render Graph | [Render Graph](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph.html) |
| Renderer Feature | [Renderer Feature](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-renderer-feature.html) |
| URP 셰이더 / Lit | [Shaders in URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shaders-in-universalrp.html) · [Lit shader](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lit-shader.html) · [Custom shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/writing-custom-shaders-urp.html) |
| Built-in에서 이전 | [Upgrading from BiRP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/upgrading-from-birp.html) · [Upgrading shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/upgrading-your-shaders.html) |
| 후처리 / Volume | [Post-processing](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/integration-with-post-processing.html) · [Volumes](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Volumes.html) |
| 그림자 | [Shadows in URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shadows-in-urp.html) |
| Rendering Debugger | [Rendering Debugger](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/features/rendering-debugger.html) |
| Shader Graph / ShaderLab | [Shader Graph](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-graph.html) · [ShaderLab](https://docs.unity3d.com/6000.3/Documentation/Manual/SL-Shader.html) · [Shader variants](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-variants.html) · [Keywords](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-keywords.html) |
| 셰이더·머티리얼 기본 | [Shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/Shaders.html) · [Materials](https://docs.unity3d.com/6000.3/Documentation/Manual/Materials.html) |
| Mesh / Renderer | [Mesh](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Mesh.html) · [MeshRenderer](https://docs.unity3d.com/6000.3/Documentation/Manual/class-MeshRenderer.html) · [SkinnedMeshRenderer](https://docs.unity3d.com/6000.3/Documentation/Manual/class-SkinnedMeshRenderer.html) |
| 카메라 | [Cameras](https://docs.unity3d.com/6000.3/Documentation/Manual/Cameras.html) · [Camera component](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Camera.html) |
| 라이팅 | [Lighting](https://docs.unity3d.com/6000.3/Documentation/Manual/Lighting.html) · [Lighting overview](https://docs.unity3d.com/6000.3/Documentation/Manual/LightingOverview.html) · [Lighting window](https://docs.unity3d.com/6000.3/Documentation/Manual/lighting-window.html) · [Lighting Settings asset](https://docs.unity3d.com/6000.3/Documentation/Manual/class-LightingSettings.html) · [Lighting Mode](https://docs.unity3d.com/6000.3/Documentation/Manual/lighting-mode.html) |
| 라이트맵 / 프로브 | [Lightmapping](https://docs.unity3d.com/6000.3/Documentation/Manual/Lightmapping.html) · [Baking](https://docs.unity3d.com/6000.3/Documentation/Manual/Lightmapping-baking-before-runtime.html) · [Light Probes](https://docs.unity3d.com/6000.3/Documentation/Manual/LightProbes.html) · [Reflection Probes](https://docs.unity3d.com/6000.3/Documentation/Manual/ReflectionProbes.html) |
| 색 공간 / HDR | [Linear color space](https://docs.unity3d.com/6000.3/Documentation/Manual/linear-color-space.html) · [Linear lighting](https://docs.unity3d.com/6000.3/Documentation/Manual/LinearLighting.html) · [HDR](https://docs.unity3d.com/6000.3/Documentation/Manual/HDR.html) |
| 이펙트 | [Particle System](https://docs.unity3d.com/6000.3/Documentation/Manual/ParticleSystems.html) · [VFX Graph](https://docs.unity3d.com/6000.3/Documentation/Manual/VFXGraph.html) |
| 툰 셰이딩 | [Unity Toon Shader 0.13](https://docs.unity3d.com/Packages/com.unity.toonshader@0.13/manual/index.html) |

### 4.8 UI

| 주제 | 링크 |
|---|---|
| UI 시스템 비교 (UI Toolkit / uGUI / IMGUI) | [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html) |
| UI Toolkit | [Introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/introduction-ui-toolkit.html) · [UI Toolkit 섹션](https://docs.unity3d.com/6000.3/Documentation/Manual/UIElements.html) · [UXML](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-UXML.html) · [USS](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS.html) · [Selectors](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-Selectors.html) · [UI Builder](https://docs.unity3d.com/6000.3/Documentation/Manual/UIBuilder.html) · [Layout engine](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-LayoutEngine.html) · [Events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Events.html) · [Controls](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Controls.html) · [Data binding](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-data-binding.html) · [Runtime binding](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-runtime-binding.html) |
| uGUI | [uGUI 2.0 매뉴얼](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html) · [Canvas](https://docs.unity3d.com/6000.3/Documentation/Manual/UICanvas.html) · [Basic layout](https://docs.unity3d.com/6000.3/Documentation/Manual/UIBasicLayout.html) · [RectTransform](https://docs.unity3d.com/6000.3/Documentation/Manual/class-RectTransform.html) · [Multi-resolution](https://docs.unity3d.com/6000.3/Documentation/Manual/HOWTO-UIMultiResolution.html) · [Button](https://docs.unity3d.com/6000.3/Documentation/Manual/script-Button.html) · [Event System](https://docs.unity3d.com/6000.3/Documentation/Manual/EventSystem.html) |
| 스프라이트 | [Sprites](https://docs.unity3d.com/6000.3/Documentation/Manual/Sprites.html) |
| 공식 e-book | [User interface design and implementation in Unity](https://unity.com/resources/user-interface-design-and-implementation-in-unity) |

> Unity 6부터 TextMeshPro는 uGUI 2.0 패키지에 합쳐졌다. 별도 `com.unity.textmeshpro` 패키지를 설치하지 않는다.

### 4.9 입력 (Input System 1.18)

| 주제 | 링크 |
|---|---|
| 매뉴얼 홈 | [Input System 1.18](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/index.html) |
| Actions / Bindings / Interactions | [Actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/Actions.html) · [Bindings](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/ActionBindings.html) · [Interactions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/Interactions.html) |
| PlayerInput 컴포넌트 | [PlayerInput](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/PlayerInput.html) |
| 입력 테스트 (`InputTestFixture`) | [Testing](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/Testing.html) |
| 구 Input Manager (레거시) | [Input Manager](https://docs.unity3d.com/6000.3/Documentation/Manual/class-InputManager.html) |

### 4.10 오디오

| 주제 | 링크 |
|---|---|
| 개요 / AudioSource / Mixer | [Audio overview](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioOverview.html) · [Audio Source](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioSource.html) · [Audio Mixer](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioMixer.html) |

### 4.11 AI·내비게이션

| 주제 | 링크 |
|---|---|
| AI Navigation (NavMesh) | [Manual 페이지](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.ai.navigation.html) · [패키지 매뉴얼 2.0](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html) |
| Behavior (행동 트리) | [Behavior 1.0](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/index.html) |

### 4.12 성능·프로파일링

| 주제 | 링크 |
|---|---|
| 분석 도구 섹션 | [Analysis](https://docs.unity3d.com/6000.3/Documentation/Manual/analysis.html) |
| Profiler | [Profiler](https://docs.unity3d.com/6000.3/Documentation/Manual/Profiler.html) · [Introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/profiler-introduction.html) · [Profiler window](https://docs.unity3d.com/6000.3/Documentation/Manual/ProfilerWindow.html) · [Memory](https://docs.unity3d.com/6000.3/Documentation/Manual/ProfilerMemory.html) |
| Frame Debugger | [Frame Debugger](https://docs.unity3d.com/6000.3/Documentation/Manual/FrameDebugger.html) |
| GC / 참조형 | [Garbage collector](https://docs.unity3d.com/6000.3/Documentation/Manual/performance-garbage-collector.html) · [Reference types](https://docs.unity3d.com/6000.3/Documentation/Manual/performance-reference-types.html) |
| 드로우콜 최적화 | [SRP Batcher](https://docs.unity3d.com/6000.3/Documentation/Manual/SRPBatcher.html) · [Batching](https://docs.unity3d.com/6000.3/Documentation/Manual/DrawCallBatching.html) · [GPU instancing](https://docs.unity3d.com/6000.3/Documentation/Manual/GPUInstancing.html) · [GPU Resident Drawer](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html) |
| 컬링 / LOD | [Occlusion culling](https://docs.unity3d.com/6000.3/Documentation/Manual/OcclusionCulling.html) · [LOD](https://docs.unity3d.com/6000.3/Documentation/Manual/LevelOfDetail.html) |
| 모범 사례 가이드 | [Best practice guides](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides.html) |

### 4.13 빌드·명령줄·테스트

| 주제 | 링크 |
|---|---|
| Build Profiles (Unity 6의 빌드 설정) | [Build Profiles](https://docs.unity3d.com/6000.3/Documentation/Manual/build-profiles.html) · [Build settings](https://docs.unity3d.com/6000.3/Documentation/Manual/BuildSettings.html) |
| 명령줄 인자 (batchmode 등) | [Command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html) |
| Test Framework | [Manual 소개](https://docs.unity3d.com/6000.3/Documentation/Manual/test-framework/test-framework-introduction.html) · [Test Framework 1.6](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html) |

### 4.14 버전 관리

| 주제 | 링크 |
|---|---|
| 버전 관리 개요 | [Version control](https://docs.unity3d.com/6000.3/Documentation/Manual/VersionControl.html) |
| Smart Merge (UnityYAMLMerge) | [Smart Merge](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html) |

---

## 5. Unity 공식 가이드라인 (e-book·모범 사례)

공식 문서 외에 Unity가 직접 발행하는 가이드라인이다. 대부분 무료 PDF이며 Unity 6 기준 개정판이 있다.

| 가이드 | 다루는 내용 | 링크 |
|---|---|---|
| C# 코드 스타일 가이드 | 네이밍, 서식, 클래스 설계 규칙 | [Create a C# style guide](https://unity.com/resources/create-code-c-sharp-style-guide-e-book) |
| 게임 프로그래밍 패턴 | SOLID, 상태, 옵저버, 커맨드, 오브젝트 풀, MVP 패턴 | [Level up your code with design patterns](https://unity.com/resources/level-up-your-code-with-game-programming-patterns) |
| 프로파일링 완전 가이드 (Unity 6) | Profiler, Memory Profiler, Frame Debugger 활용법 | [Ultimate guide to profiling](https://unity.com/resources/ultimate-guide-to-profiling-unity-games-unity-6) |
| URP 가이드 (Unity 6) | URP 설정, 라이팅, 셰이더, 후처리 | [Introduction to URP for advanced creators](https://unity.com/resources/introduction-to-urp-advanced-creators-unity-6) |
| UI 설계·구현 | uGUI와 UI Toolkit 설계 원칙 | [UI design and implementation](https://unity.com/resources/user-interface-design-and-implementation-in-unity) |
| How-to 허브 | 위 e-book과 기타 기술 문서 전체 목록 | [unity.com/how-to](https://unity.com/how-to) |
| Manual 모범 사례 | 엔진 매뉴얼 내 공식 모범 사례 | [Best practice guides](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides.html) |

### 5.1 공식 문서에서 확인되는 핵심 규칙 (요약)

아래는 위 문서들의 공식 설명을 요약한 것이다. 세부 조건은 링크한 원문을 따른다.

**실행 순서·시간**
- 물리 시뮬레이션은 `FixedUpdate` 주기(기본 `Fixed Timestep` 0.02초 = 50Hz)로 돈다. Rigidbody에 힘이나 속도를 줄 때는 `FixedUpdate`를 쓴다. → [Fixed updates](https://docs.unity3d.com/6000.3/Documentation/Manual/fixed-updates.html)
- 입력 감지와 일반 로직은 `Update`, 카메라 추적은 모든 이동이 끝난 뒤인 `LateUpdate`에서 처리한다. → [Order of execution](https://docs.unity3d.com/6000.3/Documentation/Manual/execution-order.html)
- 프레임 독립적인 이동에는 `Time.deltaTime`을 곱한다. `FixedUpdate` 안에서 `Time.deltaTime`을 읽으면 `fixedDeltaTime` 값이 나온다.
- Rigidbody가 움직이는 화면이 떨리면 `Interpolate`를 켠다. → [Rigidbody interpolation](https://docs.unity3d.com/6000.3/Documentation/Manual/rigidbody-interpolation.html)

**직렬화**
- `public` 필드나 `[SerializeField]`가 붙은 `private` 필드가 직렬화된다. `static`, `const`, `readonly` 필드와 프로퍼티는 직렬화되지 않는다. → [Script serialization](https://docs.unity3d.com/6000.3/Documentation/Manual/script-serialization.html)
- Inspector에서 조정할 값은 `[SerializeField] private`로 노출하는 방식이 일반적이다.
- 필드 이름을 바꾸면 저장된 값이 사라진다. `[FormerlySerializedAs("oldName")]`로 이전 값을 이어받는다.

**에셋·참조**
- 에셋 참조는 `.meta` 파일의 GUID로 연결된다. `.meta`를 지우거나 Unity 밖에서 에셋만 옮기면 참조가 끊긴다. 이동과 이름 변경은 Unity Editor 안에서 한다. → [Asset metadata](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html)
- 버전 관리 시 `.meta` 파일을 반드시 함께 커밋하고 `Library/`, `Temp/`, `Logs/`, `obj/`는 제외한다. → [Version control](https://docs.unity3d.com/6000.3/Documentation/Manual/VersionControl.html)

**Enter Play Mode**
- 도메인 리로드를 끄면 Play 진입이 빨라진다. 대신 `static` 필드가 이전 플레이 값을 유지하므로 `[RuntimeInitializeOnLoadMethod]` 등으로 직접 초기화해야 한다. → [Domain reloading](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)

**렌더링 (URP)**
- URP 프로젝트에서는 Built-in 전용 셰이더(Standard 등)가 분홍색(magenta)으로 보인다. URP용 셰이더로 변환한다. → [Upgrading shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/upgrading-your-shaders.html)
- 커스텀 렌더 패스는 Render Graph API 기준으로 작성한다. → [Render Graph](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph.html)
- SRP Batcher 호환 셰이더를 쓰면 같은 셰이더 변형을 쓰는 오브젝트의 CPU 렌더링 비용이 줄어든다. → [SRP Batcher](https://docs.unity3d.com/6000.3/Documentation/Manual/SRPBatcher.html)
- GPU Resident Drawer는 Forward+ 렌더링 경로에서 동작한다. → [GPU Resident Drawer](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html)

**애니메이션**
- Animator 파라미터는 문자열 대신 `Animator.StringToHash`로 미리 만든 해시를 쓴다.
- 스크립트로 이동을 제어하면 `Apply Root Motion`을 끄거나, 루트 모션을 쓰는 경우 `OnAnimatorMove`에서 직접 처리한다. → [Root Motion](https://docs.unity3d.com/6000.3/Documentation/Manual/RootMotion.html)

**성능**
- `Update` 안에서 `GetComponent`, `Find`, `new`로 매 프레임 할당하는 코드를 피한다. 참조는 `Awake`에서 캐시한다.
- 자주 생성·파괴하는 오브젝트는 `UnityEngine.Pool.ObjectPool<T>`로 재사용한다. → [ObjectPool](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Pool.ObjectPool_1.html)
- 최적화 전에 Profiler로 측정한다. Editor 측정값은 실제 빌드와 다르므로 Development Build에서 확인한다. → [Profiler introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/profiler-introduction.html)

---

## 6. 업그레이드·마이그레이션

| 문서 | 링크 |
|---|---|
| Unity 6.3 업그레이드 가이드 | [Upgrade to Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html) |
| 전체 업그레이드 가이드 목록 | [Upgrade guides](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuides.html) |
| Built-in → URP 전환 | [Upgrading from BiRP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/upgrading-from-birp.html) |

---

## 7. 명령줄(배치 모드) 빠른 참고

출처: [Command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html), [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html)

| 인자 | 의미 |
|---|---|
| `-batchmode` | UI 없이 실행한다. 대화상자가 뜨지 않는다. |
| `-nographics` | GPU 장치를 초기화하지 않는다. 렌더링이 필요한 테스트에는 쓰지 않는다. |
| `-projectPath <경로>` | 열 프로젝트 경로 |
| `-executeMethod <Class.Method>` | 지정한 static 메서드를 실행한다. |
| `-logFile <경로>` | 로그 파일 위치. `-`를 주면 표준 출력으로 내보낸다. |
| `-quit` | 명령을 끝낸 뒤 Editor를 종료한다. **`-runTests`와 함께 쓰지 않는다**(테스트가 끝나면 자동 종료됨). |
| `-runTests` | Test Runner를 실행한다. |
| `-testPlatform EditMode\|PlayMode` | 테스트 종류 |
| `-testResults <경로>` | NUnit XML 결과 파일 위치 |
| `-testFilter <패턴>` | 실행할 테스트 이름 필터 |

---

## 8. 라이선스·서드파티

| 항목 | 링크 |
|---|---|
| Unity 서비스 약관 | [Terms of Service](https://unity.com/legal/terms-of-service) |
| 유니티짱 라이선스 (Unity-Chan License) | [unity-chan.com license](https://unity-chan.com/contents/license_en/) · 프로젝트 정리본: [docs/third-party/unity-chan-license.md](../third-party/unity-chan-license.md) |

---

## 9. 링크 관리 규칙

- 프로젝트 Editor 버전을 바꾸면 이 문서의 `6000.3`을 일괄 교체하고 링크를 다시 확인한다.
- 패키지를 업데이트하면 3장의 버전과 URL `@x.y`를 함께 바꾼다.
- Unity는 매뉴얼 페이지 파일명을 버전 사이에 바꾸기도 한다. 링크가 404이면 Manual 검색창에서 페이지 제목으로 다시 찾는다.
