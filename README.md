## StickBrain (MVP)

간단한 포스트잇 스타일 메모 앱 StickBrain MVP입니다.

- **플랫폼**: Windows 10/11
- **런타임**: .NET 8
- **UI**: WPF (MVVM)
- **DB**: SQLite (Microsoft.Data.Sqlite)

### 1. 요구 사항

- **.NET SDK 8.0 이상** 설치
- Windows 10 / 11

### 2. 빌드 방법

```bash
cd d:\16.memo
dotnet build StickBrain.sln
```

### 3. 실행 방법

```bash
cd d:\16.memo\StickBrain.App
dotnet run
```

### 4. 현재 구현 상태 (MVP 진행 중)

- **전역 단축키**: `Ctrl + Shift + Space`  
  - 어디서든 누르면 작은 캡처 창(`CaptureWindow`)이 뜹니다.
  - 입력 후 `Enter`: 메모 저장 후 창 닫기
  - `Esc`: 저장하지 않고 닫기
- **자동 메타데이터**: 저장 시 활성 앱 이름 / 윈도우 타이틀을 Win32 API로 자동 수집
- **Sticky Wall**:
  - 앱 실행 시 `StickyWallWindow`가 기본 화면으로 열리며, 저장된 메모가 포스트잇 카드로 표시됩니다.
  - 카드 드래그로 위치 이동 가능, 300ms 디바운스로 SQLite에 위치 저장
  - (편집/색상/핀/삭제/검색/스택 등은 다음 단계에서 추가 예정)

DB는 로컬 사용자 AppData 폴더 하위에 `stickbrain.db`로 생성되며, `notes` + `notes_fts(FTS5)` 구조를 사용합니다.

