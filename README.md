# Daily Goal ☀️

> A治愈系 daily task清单 that lives on the top-right corner of your Windows screen.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows_10%2B-brightgreen)](#)
[![Language: C#](https://img.shields.io/badge/language-C%23-239120?logo=c-sharp)](#)

<div align="center">

### 🌐 Language / 语言选择

| 🇨🇳 中文 | 🇬🇧 English | 🇯🇵 日本語 | 🇰🇷 한국어 |
|:-:|:-:|:-:|:-:|
| [简体中文使用说明](#-简体中文---daily-goal) | [English Documentation](#-english---daily-goal) | [日本語ドキュメント](#-日本語---daily-goal) | [한국어 설명서](#-한국어---daily-goal) |

</div>

---

## ✨ Features Overview / 功能概览 / 機能概要 / 기능 개요

```
┌──────────────────────────────────────┐
│  □ Today's Tasks          ○ 85%  ──  │
│                                      │
│  ✅ Morning run      ──── ⏱ undo     │
│  ☐ Read 30 pages   + ───────────────  │
│  ☐ Write blog post       ──────────── │
│  ☐ Practice design             ────── │
│                                      │
│  ┌─ tomorrow ───────────────────────┐ │
│  │ ☑ Plan weekly review              │ │
│  └──────────────────────────────────┘ │
└──────────────────────────────────────┘
```

| Feature / 功能 | Description / 说明 |
|---|---|
| 🔖 **Pin-to-edge** — 贴边隐藏 | Smoothly sticks to screen edge, expands on hover |
| 🎯 **Drag anywhere** — 拖动定位 | Drag title bar or rail; auto-snaps to nearest edge |
| 📅 **Smart scheduling** — 智能排程 | Unfinished tasks roll over; recurring tasks auto-generate |
| 🔥 **Streak & heatmaps** — 连续打卡 | Visual completion tracking with streak indicators |
| 📋 **Templates** — 常用模板 | Save frequent tasks as templates for instant reuse |
| ↩️ **Undo & restore** — 撤销恢复 | Particle animation on completion; 6-second undo window |
| 🎨 **Three themes** — 三套主题 | Dark · Light · 「玄翎」XuanLing character palette |
| 💾 **Auto-save** — 自动保存 | Atomic writes, daily snapshots, export/import JSON |
| 🔒 **Single instance** — 单实例启动 | Double-click launches after first launch focuses existing window |

---

## 🇨🇳 简体中文 — Daily Goal

### 简介

Daily Goal 是一个贴在 Windows 屏幕右上角的治愈系日常清单。提供深色、浅色和角色「玄翎」三套主题，支持贴边收展、拖动定位、智能排程等功能。

### 下载安装

| 方式 | 说明 |
|------|------|
| **安装包** | `dist/DailyGoal-Setup-2.x.x-x64.exe` — 自包含发布，无需额外安装 .NET 运行时 |
| **便携版** | 解压 `app/DailyGoal.exe` 后直接运行 |
| **Web 预览** | 浏览器打开 `index.html` — 纯静态页面，数据存储在 localStorage |

### 系统要求

- Windows 10 / Windows 11（64 位）
- 最低分辨率 800×600（程序会自动缩放至 68%）
- 推荐 1920×1080 以获得最佳体验

### 数据存储

任务数据保存在 `%APPDATA%\DailyGoal\tasks.json`。每日快照位于 `%APPDATA%\DailyGoal\backups\`（保留 30 份）。损坏文件自动另存为 `tasks.corrupt-*.json`。旧版 v2–v4 状态会在加载时自动迁移到最新 v5 格式。

### 从源码构建

```powershell
# 构建桌面版（Release 配置）
~/.dotnet/dotnet.exe build src/DailyGoal -c Release

# 生成自包含 win-x64 安装器
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1

# 运行自检
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --self-test           # 核心逻辑
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --particle-self-test  # 粒子系统
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --ui-self-test        # UI 渲染
```

---

## 🇬🇧 English — Daily Goal

### Overview

Daily Goal is a治愈系 daily task清单 application that lives on the top-right corner of your Windows screen. It features three themes (Dark, Light, and 「玄翎」), smooth edge-docking, drag positioning, and smart task scheduling.

### Download & Install

| Method | Details |
|--------|---------|
| **Installer** | `dist/DailyGoal-Setup-2.x.x-x64.exe` — Self-contained, no .NET runtime required |
| **Portable** | Extract `app/DailyGoal.exe` and run directly |
| **Web Preview** | Open `index.html` in browser — static HTML/CSS/JS, data stored in localStorage |

### System Requirements

- Windows 10 / Windows 11 (64-bit)
- Minimum resolution: 800×600 (auto-scales down to 68%)
- Recommended: 1920×1080 for the best experience

### Data Storage

Task data is stored at `%APPDATA%\DailyGoal\tasks.json`. Daily snapshots are saved to `%APPDATA%\DailyGoal\backups\` (30-day retention). Corrupted files are automatically renamed to `tasks.corrupt-*.json`. Legacy v2–v4 state formats are migrated to the latest v5 format on load.

### Building from Source

```powershell
# Build desktop version (Release configuration)
~/.dotnet/dotnet.exe build src/DailyGoal -c Release

# Create self-contained win-x64 installer
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1

# Run self-tests
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --self-test           # Core logic
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --particle-self-test  # Particle system
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --ui-self-test        # UI rendering
```

---

## 🇯🇵 日本語 — Daily Goal

### 概要

Daily Goal は Windows 画面の右上隅に貼る癒やし系デイリータスククリップです。ダーク、ライト、キャラクター「玄翎」の3つのテーマを提供し、エッジへの貼り付け・ドラッグ位置決め・スマートなタスクスケジューリングなどの機能を備えています。

### ダウンロードとインストール

| 方法 | 説明 |
|------|------|
| **インストーラー** | `dist/DailyGoal-Setup-2.x.x-x64.exe` — 自己完結型、.NET ランタイム不要 |
| **ポータブル版** | `app/DailyGoal.exe` を解凍してそのまま実行 |
| **Web プレビュー** | ブラウザで `index.html` を開く — 純粋な静的ページ、データは localStorage に保存 |

### システム要件

- Windows 10 / Windows 11（64ビット）
- 最小解像度 800×600（プログラムは自動的に68%まで縮小）
- 推奨 1920×1080 で最高の体験

### データストレージ

タスクデータは `%APPDATA%\DailyGoal\tasks.json` に保存されます。毎日のスナップショットは `%APPDATA%\DailyGoal\backups\` に保存（30日分保持）。破損したファイルは自動的に `tasks.corrupt-*.json` にリネームされます。

### ソースからのビルド

```powershell
# デスクトップ版をビルド（Release構成）
~/.dotnet/dotnet.exe build src/DailyGoal -c Release

# 自己完結型 win-x64 インストーラーを生成
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1

# 自己テストを実行
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --self-test           # コアロジック
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --particle-self-test  # パーティクルシステム
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --ui-self-test        # UIレンダリング
```

---

## 🇰🇷 한국어 — Daily Goal

### 개요

Daily Goal 은 Windows 화면의 오른쪽 상단에 붙이는 치유형 데일리 작업 목록 앱입니다. 다크, 라이트, 캐릭터 「현령」의 3가지 테마를 제공하며,Edge 도킹, 드래그 위치 조정, 스마트 일정 관리 등의 기능을 지원합니다.

### 다운로드 및 설치

| 방법 | 설명 |
|------|------|
| **인스톨러** | `dist/DailyGoal-Setup-2.x.x-x64.exe` — 자체 완결형, .NET 런타임 불필요 |
| **포터블 버전** | `app/DailyGoal.exe` 압축 해제 후 바로 실행 |
| **웹 미리보기** | 브라우저에서 `index.html` 열기 — 정적 페이지, 데이터는 localStorage 에 저장 |

### 시스템 요구사항

- Windows 10 / Windows 11 (64비트)
- 최소 해상도 800×600 (프로그램이 자동으로 68%까지 축소)
- 권장 1920×1080 최적의 경험

### 데이터 저장소

작업 데이터는 `%APPDATA%\DailyGoal\tasks.json` 에 저장됩니다. 일일 스냅샷은 `%APPDATA%\DailyGoal\backups\` 에 보관 (30일분 유지). 손상된 파일은 자동으로 `tasks.corrupt-*.json` 으로 이름 변경됩니다.

### 소스에서 빌드하기

```powershell
# 데스크탑 버전 빌드 (Release 구성)
~/.dotnet/dotnet.exe build src/DailyGoal -c Release

# 자체 완결형 win-x64 인스톨러 생성
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1

# 자동 테스트 실행
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --self-test           # 핵심 로직
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --particle-self-test  # 파티클 시스템
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --ui-self-test        # UI 렌더링
```

---

## 🏗 Architecture

```
Daily Goal/
├── src/DailyGoal/            # C# WinForms desktop app (.NET 10)
│   ├── DailyGoalForm.cs      # Main form lifecycle
│   ├── DailyGoalForm.Tasks.cs       # CRUD, reorder, completion
│   ├── DailyGoalForm.Planning.cs    # Today/tomorrow/history pages
│   ├── DailyGoalForm.Animation.cs   # Slide animations, hover delay
│   ├── DailyGoalForm.Drag.cs        # Drag-to-move, edge snapping
│   ├── AppState.cs               # v5 state format, migrations
│   ├── StateStore.cs             # Load/save, atomic writes, backups
│   ├── Scheduling.cs             # Rollover & recurrence logic
│   ├── Theme.cs                  # 3 palettes, accents, scaling
│   └── assets/                   # XuanLing character artwork
├── index.html                  # Web prototype (static HTML/CSS/JS)
├── styles.css                  # Web prototype styles
├── app.js                      # Web prototype logic
├── installer/                  # Inno Setup installer script
└── scripts/                    # Build automation
```

---

## 🤝 Contributing

This is a personal project. Ideas, issues, and PRs are welcome! Please open an issue first for larger changes.

这是一个个人项目。欢迎提交想法、问题和建议 PR！对于较大的改动，请先创建一个 issue。

これは個人のプロジェクトです。アイデア、イシュー、PRは大歓迎です！大きな変更を行う場合は、事前にイシューを作成してください.

이것은 개인 프로젝트입니다. 아이디어, 이슈 및 PR을 환영합니다! 큰 변경 사항의 경우 먼저 이슈를 만들어 주세요.

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

---

<p align="center">Made with ❤️ by <a href="https://github.com/xiaoxiao6">xiaoxiao6</a></p>
