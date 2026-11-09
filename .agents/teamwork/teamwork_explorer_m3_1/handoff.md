# Milestone 3 — Test Assembly Infrastructure & Environment Setup Report

**Executive Summary**: Milestone 3 requires establishing the EditMode test infrastructure at `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`. This report specifies the exact assembly configuration, compilation pipeline (`dotnet build`), execution engine (`unity test . --mode EditMode`), and Roslyn analyzer constraints required for reliable unit and integration testing.

---

## 1. Observation

### 1.1 Existing Assembly Definitions
- **`Assets/_Project/Editor/Game.Editor.asmdef`** (Lines 1–33):
  ```json
  {
      "name": "Game.Editor",
      "rootNamespace": "Game.Editor",
      "references": [
          "Game.Core",
          "Game.Network",
          "Game.Gameplay",
          "PurrNet.Runtime",
          "PurrNet.Editor",
          "VContainer",
          "Unity.InputSystem",
          "TriInspector",
          "TriInspector.Editor",
          "Unity.Cinemachine",
          "Unity.Cinemachine.Editor",
          "Unity.TextMeshPro",
          "UnityEngine.UI",
          "Unity.Animation.Rigging",
          "Unity.Animation.Rigging.Editor"
      ],
      "includePlatforms": [
          "Editor"
      ],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": false,
      "precompiledReferences": [],
      "autoReferenced": false,
      "defineConstraints": [],
      "versionDefines": [],
      "noEngineReferences": false
  }
  ```
  *Observed*: `Game.Editor` is editor-only (`includePlatforms: ["Editor"]`), `autoReferenced: false`, and houses all Milestone 1 and 2 lifecycle services (`Assets/_Project/Editor/Core/Lifecycle/`).

- **`Assets/_Project/Core/Game.Core.asmdef`** (Lines 1–18):
  ```json
  {
      "name": "Game.Core",
      "rootNamespace": "Game.Core",
      "references": [
          "UniTask",
          "PurrNet.Runtime"
      ],
      "includePlatforms": [],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": false,
      "precompiledReferences": [],
      "autoReferenced": true,
      "defineConstraints": [],
      "versionDefines": [],
      "noEngineReferences": false
  }
  ```

- **`Assets/PurrNet/Addons/WebRTC/Tests/PurrNet.WebRTC.Tests.asmdef`** (Lines 1–16):
  ```json
  {
      "name": "PurrNet.WebRTC.Tests",
      "references": ["PurrNet.Runtime", "PurrNet.WebRTC.Runtime", "Unity.WebRTC", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
      "includePlatforms": ["Android", "Editor", "iOS", "LinuxStandalone64", "macOSStandalone", "WindowsStandalone64"],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": true,
      "precompiledReferences": ["nunit.framework.dll"],
      "autoReferenced": false,
      "defineConstraints": ["UNITY_INCLUDE_TESTS", "PURRNET_UNITY_WEBRTC"],
      "versionDefines": [
          { "name": "com.unity.webrtc", "expression": "[3.0.0,4.0.0)", "define": "PURRNET_UNITY_WEBRTC" }
      ],
      "noEngineReferences": false
  }
  ```
  *Observed*: Existing test assemblies in the repository use `overrideReferences: true`, link `nunit.framework.dll` via `precompiledReferences`, set `autoReferenced: false`, and guard compilation with `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.

### 1.2 Unity Test Framework Package & Tooling Environment
- **`Packages/manifest.json`** (Line 21):
  `"com.unity.test-framework": "1.6.0"` is registered as an explicit project dependency.
- **Unity Editor Version & Binary**:
  Unity 6000.3.7f1 is installed at `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe`.
- **Unity CLI Runner**:
  `unity.exe` CLI version 1.0.0.0 is present at `C:\Users\SaidR\AppData\Local\Unity\bin\unity.exe`.
  Tool execution verification via `unity test --help`:
  ```text
  Usage: unity test [options] [project]
  Run a project's EditMode/PlayMode tests in the editor and write a results report
  Options:
    --mode <mode>        Test platform to run: EditMode or PlayMode.
    --filter <pattern>   Run only tests whose names match this filter
    --output <path>      Path to write results report (default: "test-results.xml")
    --non-interactive    Disable interactive prompts
  ```
- **.NET SDK & Build Toolchain**:
  `dotnet --version` outputs `10.0.400`.
  Direct execution of `dotnet build Game.Editor.csproj -c Debug` yielded:
  `Compilación correcta. 0 Advertencia(s), 0 Errores` (Elapsed time: 11.01s).
  Direct execution of `dotnet build PurrNet.WebRTC.Tests.csproj -c Debug` yielded:
  `Compilación correcta. 0 Advertencia(s), 0 Errores` (Elapsed time: 1.35s).

### 1.3 Roslyn Analyzers and `.editorconfig` Scope
- **`Game.Editor.csproj`** (Lines 44–51):
  Analyzers are loaded into editor assemblies:
  - `Assets\Analyzers\Microsoft.Unity.Analyzers.dll`
  - `Assets\Analyzers\Antigravity.Unity.Analyzers.dll`
  - `Assets\PurrNet\Analyzers\PurrNet.Analyzers.dll`
- **`.editorconfig`** (Lines 7–100):
  - Scope `[*.cs]` enforces all `error` rules globally across the project.
  - Exclusions in Lines 79–100 apply only to `[Assets/{PurrNet,Plugins,TextMesh Pro,TutorialInfo}/**.cs]`.
  - `Assets/_Project/Tests/**.cs` is **not excluded**, meaning all test code will be evaluated against:
    - `UNT0002`: CompareTag required for tag checks.
    - `UNT0007`, `UNT0008`, `UNT0023`: Null-propagation (`?.`) and null-coalescing (`??`, `??=`) prohibited on `UnityEngine.Object`.
    - `AGY0001`: Shallow inheritance depth (max 1 level from base domain classes).
    - `AGY0002`: Encapsulate serialized fields (`[SerializeField] private`).
    - `AGY0003`: Read-only property external access.
    - `AGY0011`–`AGY0016`: Hot path rules (LINQ, GetComponent, new, string manipulation, boxing, closures). Scoped to hot-path methods (Update/LateUpdate/FixedUpdate/HotPath attributes).
    - `AGY0021`: Named event handlers required in `+=` subscriptions (no inline lambdas).

### 1.4 Project Directory Hierarchy
- `Assets/_Project/` contains `Art`, `Core`, `Editor`, `Gameplay`, `Network`, `Prefabs`, `Scenes`.
- `Assets/_Project/Tests/` does not currently exist. Milestone 3 is the initial creator of this directory tree.

---

## 2. Logic Chain

1. **Assembly Separation (SRP & Build Isolation)**:
   - Milestone 3 tests must exercise editor services (`InspectorSanitizerService`, `SelectionMementoService`, `SessionStateSelectionPersistence`, `PlayModeTransitionObserver`, `HierarchyPathResolver`).
   - These services reside in `Game.Editor.asmdef`.
   - Creating an isolated test assembly (`Game.Editor.Tests.asmdef`) located in `Assets/_Project/Tests/Editor/` ensures test code is never shipped or compiled into production assemblies.
   - Referencing `Game.Editor` and `Game.Core` grants compile-time access to all target interfaces, models, and services without circular references.

2. **Test Framework Linking & Precompiled NUnit**:
   - Unity Test Framework 1.6.0 requires `UnityEngine.TestRunner` and `UnityEditor.TestRunner` for EditMode runner execution and attributes (`[Test]`, `[UnityTest]`).
   - In accordance with Unity asmdef specifications and the precedent set by `PurrNet.WebRTC.Tests.asmdef` and `Unity.Addressables.Editor.Tests.asmdef`, referencing NUnit requires `overrideReferences: true` paired with `precompiledReferences: ["nunit.framework.dll"]`.
   - Without `overrideReferences: true`, NUnit types (`Assert`, `TestAttribute`) fail resolution during standalone compilation.

3. **Compilation Constraints & Boundaries**:
   - `autoReferenced: false` ensures that other runtime or editor assemblies do not automatically pull in test dependencies.
   - `includePlatforms: ["Editor"]` guarantees that these tests are strictly treated as EditMode tests by Unity Test Framework.
   - `defineConstraints: ["UNITY_INCLUDE_TESTS"]` ensures that if tests are excluded from a build pipeline, the test assembly is stripped cleanly without compilation overhead.

4. **Analyzer Rule Gating in Test Suites**:
   - Because `Assets/_Project/Tests/` is not ignored in `.editorconfig`, test authors must adhere to critical constraints:
     - **Null Checks (`UNT0007`/`UNT0008`)**: Test code must use standard equality `if (obj != null)` or NUnit `Assert.That(obj, Is.Not.Null)`. Attempting `obj?.name` will fail compilation.
     - **Unity Object Equality**: In NUnit, `Assert.IsTrue(unityObject == null)` or `Assert.IsFalse(unityObject != null)` must be used when checking destroyed objects, because NUnit's `Assert.IsNull` can bypass Unity's custom C++ lifetime operator overloads.
     - **Event Subscriptions (`AGY0021`)**: In `PlayModeObserverIntegrationTests`, testing subscriber registration must bind to named member methods (e.g. `observer.OnTransition += HandleTransition;`). Inline lambdas (`+= () => { ... }`) trigger compile errors under `AGY0021`.
     - **Method Naming**: Test fixtures and helper methods must never be named `Update`, `FixedUpdate`, or `LateUpdate` to prevent triggering hot-path zero-GC analyzer checks (`AGY0011`–`AGY0016`).

5. **Tooling & CLI Execution**:
   - Local validation can be executed via two mechanisms:
     - Fast compilation check: `dotnet build Game.Editor.csproj -c Debug` (and `dotnet build Game.Editor.Tests.csproj -c Debug` once generated).
     - Test execution: `unity test . --mode EditMode --filter "Game.Editor.Tests" --non-interactive` or native batchmode `"C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "test-results.xml" -logFile "test.log"`.

---

## 3. Caveats

1. **Unity Solution Sync for `.csproj`**:
   - `dotnet build` relies on `.csproj` files existing. Unity generates `Game.Editor.Tests.csproj` when it reloads the domain or when opening the project in Unity. If running purely in headless CLI without launching Unity first, the `.csproj` file will not exist until Unity runs batchmode or imports the asset.
   - *Mitigation*: The test runner CLI (`unity test . --mode EditMode`) automatically triggers asset import and compiles the assembly internally before executing tests.
2. **Internal Access / Visibility**:
   - All interfaces and classes created in Milestone 1 and 2 (`IInspectorSanitizerService`, `ISelectionMementoService`, `SelectionMemento`, `HierarchyPathResolver`, `InspectorSanitizerService`, `PlayModeTransitionObserver`, `SessionStateSelectionPersistence`) are `public`.
   - If future internal helpers need testing, `[assembly: InternalsVisibleTo("Game.Editor.Tests")]` will need to be added to `Game.Editor`. Currently, no `internal` access is required.
3. **Precompiled Assemblies Override**:
   - When `overrideReferences: true` is set, only precompiled DLLs explicitly listed in `precompiledReferences` are included. Precompiled references should only be `["nunit.framework.dll"]` as Unity engine and package assemblies are handled via the `references` list.

---

## 4. Conclusion

The test infrastructure for Milestone 3 is cleanly specified and fully aligned with Unity 6000.3.7f1, UTF 1.6.0, and the repository's architectural and analyzer rules (`AGENTS.md`).

### Production-Ready Specification: `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`
```json
{
    "name": "Game.Editor.Tests",
    "rootNamespace": "Game.Editor.Tests",
    "references": [
        "Game.Editor",
        "Game.Core",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Companion Meta File: `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef.meta`
```yaml
fileFormatVersion: 2
guid: 5d83b4c194e945cbb6e921e1069df91e
AssemblyDefinitionImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

### Folder Structure Required:
- `Assets/_Project/Tests/` (Folder)
- `Assets/_Project/Tests/Editor/` (Folder)
- `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`
- Test suites to be created in Milestone 3:
  - `Assets/_Project/Tests/Editor/SelectionMementoUnitTests.cs`
  - `Assets/_Project/Tests/Editor/HierarchyPathResolverTests.cs`
  - `Assets/_Project/Tests/Editor/InspectorSanitizerTests.cs`
  - `Assets/_Project/Tests/Editor/PlayModeObserverIntegrationTests.cs`

---

## 5. Verification Method

To independently verify the test infrastructure and test suites:

1. **Verify asmdef JSON Schema & Valid GUIDs**:
   - Inspect `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef` using `view_file` to confirm valid JSON syntax, correct assembly names, and platform restrictions.
2. **Compilation Verification (C# Diagnostics & Roslyn Analyzers)**:
   - Run compilation via Unity batchmode:
     ```powershell
     unity run . -- -batchmode -quit
     ```
   - Alternatively, build using `dotnet build` once `.csproj` is generated:
     ```powershell
     dotnet build Game.Editor.Tests.csproj -c Debug
     ```
   - Ensure zero errors from Roslyn analyzers (`UNT*` and `AGY*`).
3. **Test Execution Verification**:
   - Run EditMode tests non-interactively:
     ```powershell
     unity test . --mode EditMode --filter "Game.Editor.Tests" --non-interactive
     ```
   - Native Unity Editor batchmode execution:
     ```powershell
     & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "test-results.xml" -logFile "unity-test.log"
     ```
4. **Invalidation Conditions**:
   - If `UnityEngine.TestRunner` or `UnityEditor.TestRunner` cannot be resolved, verify UTF 1.6.0 in `Packages/manifest.json`.
   - If `nunit.framework` types (`Test`, `Assert`) cannot be resolved, check `overrideReferences: true` and `precompiledReferences: ["nunit.framework.dll"]`.
   - If compiler errors appear regarding `AGY0021`, verify that all event subscriptions in test files use named methods instead of lambdas.
