# BossKey
[![Windows 실행 파일 다운로드](https://img.shields.io/badge/Windows-다운로드-2878FF?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/Rozashas/BossKey/raw/refs/heads/main/App/BossKey.exe)

버튼을 누르면 설치 없이 사용할 수 있는 Windows x64 실행 파일을 다운로드합니다.
내가 쓰려고 만든 보스키 프로그램

Code → Download ZIP 후 압축을 풀어 `App/BossKey.exe`를 실행해도 됩니다.

Windows 10/11 x64용 보스키. 설치 및 별도 .NET 설치 없이 `App/BossKey.exe` 실행.

1. 숨길 프로그램을 여러 개 체크.
2. **좌 Alt + `** 입력 시 선택한 프로그램의 현재 창 전체 숨김.
3. 같은 단축키 입력 시 원래 위치와 최대화·최소화 상태 복원.

`단축키 변경` 클릭 후 Ctrl / Alt / Shift / Win 중 하나 이상과 일반 키 조합 입력. 좌우 키 구분. Esc 취소. `기본키 복원`으로 좌 Alt + ` 복원.

선택과 단축키는 `%LOCALAPPDATA%\BossKey\settings.json`에 자동 저장. 프로그램 이름 기준 선택하므로 같은 프로그램의 여러 창과 여러 실행 인스턴스에 함께 적용. 종료된 프로그램의 선택도 유지.

`트레이로 보내기`로 설정 창 숨김. 트레이 B 아이콘 더블클릭 시 설정 열기. 트레이 우클릭으로 창 복원 및 종료 가능. 설정 창 X 또는 트레이 종료 시 숨긴 창 복원 후 종료.

검은 바탕 가운데 붉은 B 아이콘 적용.

## 동작 범위

- 숨기는 순간 존재하는 창에 적용. 숨긴 뒤 새로 열린 창은 다음 숨기기에 적용.
- 소리·재생·프로세스는 계속 실행. 음소거 또는 프로그램 종료 기능 없음.
- 관리자 권한으로 실행된 대상은 BossKey도 관리자 권한으로 실행해야 할 수 있음.
- 일반 최상위 데스크톱 창 대상. 탐색기·Windows 셸 제외. 일부 UWP 앱 및 독점 전체 화면 게임은 개별 확인 필요.
- 정상 종료 및 UI 예외 시 복원 수행. 작업 관리자 강제 종료·프로세스 충돌 시 자동 복원은 보장하지 않음. 이 경우 대상 프로그램을 재시작.
- 시스템 예약 단축키 및 다른 프로그램과 중복되는 키는 피할 것.

## 빌드 및 검증

검증: 실제 테스트 창 3개의 숨김·복원, 원래 위치 및 최대화·최소화 상태 보존, 좌우 Alt 구분, 기본 단축키의 키 반복 방지, 사용자 단축키 등록 및 충돌 처리 통과. 실제 개별 게임 호환성과 단축키 변경 화면의 직접 키 입력 조작은 별도 확인 필요.

.NET 10 SDK가 있는 Windows에서 `powershell -ExecutionPolicy Bypass -File .\build.ps1` 실행.

`App\BossKey.exe --self-test`는 전용 테스트 프로세스의 창 3개를 만들고 숨김·복원 및 창 상태 보존, 사용자 단축키 Windows 등록, 충돌 처리 검증. 자동 입력이 전달되는 환경에서는 좌우 Alt 구분과 키 반복도 검증. 결과는 실행 파일 옆 `self-test.json`, UI 렌더링은 `ui-preview.png`에 기록. `keyboardInputValidated`가 `false`이면 실제 단축키 입력 및 변경 UI 조작은 별도 수동 확인 필요.

단축키와 창 표시 제어는 Windows API 사용: [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), [ShowWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow).

**********************************************

임의적인 재빌드 및 재배포는 허용하지 않으며 해당 프로그램은 상업적이용의 목적이 아닙니다.
누구나 어디서든 무료로 사용 가능합니다.
