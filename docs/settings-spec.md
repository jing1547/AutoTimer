# AutoTimer 설정 데이터 명세서

## 설정 JSON 구조 (settings.json)

```json
{
  "general": {
    "runOnStartup": true,
    "timeSource": "server",
    "serverUrl": "https://google.com",
    "syncIntervalMinutes": 60,
    "persistSettings": true
  },
  "display": {
    "targetMonitor": "\\\\.\\.DISPLAY2",
    "fadeOutEnabled": true,
    "fadeOutDurationMs": 500
  },
  "playback": {
    "defaultVideoPath": "C:\\Videos\\default.mp4",
    "usePerScheduleVideo": false,
    "mouseClickThrough": false,
    "lockWindow": true
  },
  "schedules": [
    {
      "id": "uuid-1",
      "enabled": true,
      "dayOfWeek": "Thursday",
      "time": "19:40",
      "videoPath": null,
      "label": "목요일 예배"
    },
    {
      "id": "uuid-2",
      "enabled": true,
      "dayOfWeek": "Sunday",
      "time": "10:50",
      "videoPath": "C:\\Videos\\sunday.mp4",
      "label": "주일 예배"
    }
  ],
  "oneTimeSchedules": [
    {
      "id": "uuid-3",
      "date": "2026-04-07",
      "time": "19:00",
      "videoPath": null,
      "label": "화요일 순방",
      "autoDelete": true
    }
  ]
}
```

---

## 필드 상세 설명

### general (일반 설정)

| 필드 | 타입 | UI 컨트롤 | 설명 |
|------|------|-----------|------|
| `runOnStartup` | bool | 체크박스 | Windows 시작 시 자동 실행 등록/해제 |
| `timeSource` | enum | 라디오/토글 | `"local"` 또는 `"server"` — 시간 기준 선택 |
| `serverUrl` | string | 텍스트 입력 | 서버 시간 가져올 URL (timeSource가 server일 때) |
| `syncIntervalMinutes` | int | 숫자 입력 | 서버 동기화 주기 (분) |
| `persistSettings` | bool | 체크박스 ("유지") | 프로그램 재시작 후에도 설정 유지 |

### display (화면 설정)

| 필드 | 타입 | UI 컨트롤 | 설명 |
|------|------|-----------|------|
| `targetMonitor` | string | 드롭다운 | 영상을 표시할 모니터 선택 |
| `fadeOutEnabled` | bool | 체크박스 | 영상 종료 시 페이드 아웃 효과 |
| `fadeOutDurationMs` | int | 슬라이더/숫자 | 페이드 아웃 지속 시간 (ms) |

### playback (재생 설정)

| 필드 | 타입 | UI 컨트롤 | 설명 |
|------|------|-----------|------|
| `defaultVideoPath` | string | 파일 선택 버튼 | 기본 동영상 파일 경로 |
| `usePerScheduleVideo` | bool | 체크박스 ("개별동영상") | true: 스케줄별 개별 영상 / false: 기본 영상 사용 |
| `mouseClickThrough` | bool | 체크박스 ("마우스 클릭무시") | true: 영상 위 마우스 클릭 무시 (WS_EX_TRANSPARENT) |
| `lockWindow` | bool | 체크박스 ("창 고정") | true: TopMost + Alt+F4/닫기 차단, 트레이에서만 종료 |

### schedules (주간 반복 스케줄)

리스트형 레이아웃. 요일+시간 조합으로 매주 반복.

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | string (UUID) | 고유 식별자 |
| `enabled` | bool | 활성/비활성 토글 |
| `dayOfWeek` | enum | Monday~Sunday |
| `time` | string (HH:mm) | 재생 시작 시각 |
| `videoPath` | string? | 개별 영상 경로 (null이면 기본 영상 사용) |
| `label` | string | 스케줄 이름/메모 |

### oneTimeSchedules (일회성 타이머)

특별 모임, 순방 등 1회성 스케줄. 실행 후 자동 삭제 옵션.

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | string (UUID) | 고유 식별자 |
| `date` | string (yyyy-MM-dd) | 실행 날짜 |
| `time` | string (HH:mm) | 재생 시작 시각 |
| `videoPath` | string? | 개별 영상 경로 (null이면 기본 영상 사용) |
| `label` | string | 스케줄 이름 (예: "화요일 순방") |
| `autoDelete` | bool | 실행 후 자동 삭제 여부 |

---

## UI 레이아웃 구성 (설정 창)

```
┌─────────────────────────────────────────────────┐
│  ⚙ AutoTimer Settings                     [—][X]│
├─────────────────────────────────────────────────┤
│                                                 │
│  ── 일반 ──────────────────────────────────────  │
│  [✓] 시작 프로그램 등록                          │
│  시간 소스: (●) 서버  (○) 로컬                   │
│  현재 시각: 2026-03-30 19:40:03  (Offset: +0.2s)│
│                                                 │
│  ── 화면 ──────────────────────────────────────  │
│  모니터: [▼ DISPLAY2 - 1920x1080 ▼]             │
│  [✓] 페이드 아웃                                 │
│                                                 │
│  ── 재생 ──────────────────────────────────────  │
│  기본 영상: [C:\Videos\default.mp4] [찾아보기]   │
│  [✓] 개별 동영상 (스케줄별 다른 영상)             │
│  [✓] 마우스 클릭 무시                            │
│  [✓] 창 고정 (TopMost + 닫기 차단)               │
│  [✓] 유지 (설정 기억)                            │
│                                                 │
│  ── 주간 스케줄 ───────────────────────────────  │
│  ┌───┬────┬───────┬──────────────────┬────┐     │
│  │ ✓ │ 목 │ 19:40 │ 목요일 예배       │ ✕  │     │
│  │ ✓ │ 일 │ 10:50 │ 주일 예배 [개별▼] │ ✕  │     │
│  │   │ 수 │ 19:00 │ 수요 예배         │ ✕  │     │
│  └───┴────┴───────┴──────────────────┴────┘     │
│                              [+ 스케줄 추가]     │
│                                                 │
│  ── 일회성 타이머 ─────────────────────────────  │
│  ┌───────────┬───────┬──────────────────┬────┐  │
│  │ 2026-04-07│ 19:00 │ 화요일 순방       │ ✕  │  │
│  └───────────┴───────┴──────────────────┴────┘  │
│                         [+ 일회성 타이머 추가]   │
│                                                 │
│              [테스트 재생]    [서버 동기화]       │
│                                                 │
└─────────────────────────────────────────────────┘
```

## 트레이 아이콘 우클릭 메뉴

```
┌──────────────────────┐
│  ⚙ 설정              │
│  ▶ 테스트 재생        │
│  🔄 서버 동기화       │
│  ───────────────────  │
│  ❌ 종료              │
└──────────────────────┘
```

## 동영상 재생 창 (VideoWindow)

- 완전 보더리스 (WindowStyle=None)
- 선택된 모니터 전체화면
- lockWindow=true 시: TopMost + Alt+F4 차단 + 닫기 버튼 없음
- mouseClickThrough=true 시: 마우스 이벤트 통과 (WS_EX_TRANSPARENT)
- 영상 종료 시: fadeOutEnabled면 페이드 아웃 후 닫기, 아니면 즉시 닫기
- UI 요소 일절 없음 — 순수 영상만 표시
