# GPU Assign (完全ポータブル版)

Windows の「設定 → システム → ディスプレイ → グラフィックス」で設定するアプリごとの GPU 割り当てを、バージョンアップによる EXE パス変更に自動追従させるツールです。

---

## 主な特徴

1. **完全ポータブル仕様（AppData 不使用）**
   - 設定ファイル (`apps.json`)、実行ログ (`sync_log.json`)、バックアップ (`backups/`) はすべて **実行ファイル（`GPUAssign.exe`）と同じフォルダ** に保存されます。
   - フォルダごと USB メモリや好きな場所に配置してそのまま持ち運べます。

2. **起動時 管理者権限（UAC 昇格対応）**
   - レジストリ操作およびシステム設定管理をスムーズに行うため、起動時に自動で管理者権限（`requireAdministrator`）を要求します。

3. **直感的なパス存在確認・エクスプローラー連携**
   - **「📁 フォルダを開く」ボタン**: 登録した検索ディレクトリをエクスプローラーで即座に開いて確認可能。
   - **「🔍 EXEの存在確認・検出テスト」ボタン**: 入力中の検索条件（固定パス/最新バージョン/ワイルドカード/正規表現）で実際にどの EXE がマッチするかをバックグラウンドで即座にテストし、結果を表示。
   - **「📂 検出されたEXEを開く」ボタン**: 検出された最新 EXE をエクスプローラーで選択状態で直接開いて確認可能。

4. **UI と処理の完全分離 & マルチスレッド設定**
   - 重いファイル探索・同期処理はすべてワーカースレッド（`Task.Run` / `SemaphoreSlim`）で実行し、UI の応答性を完全に維持。
   - 設定画面から **並列スレッド数（1 / 2 / 4 [推奨] / 8 / 16）** を自由に変更可能。

5. **初期設定はクリーン（ユーザー手動登録）**
   - 初期状態では勝手なプリセット登録は行われず、空の状態でスタート。
   - プリセットから追加したい場合は「カタログから追加」ボタンから必要なアプリだけを選択して追加可能。

6. **多言語 & ダークモード対応**
   - OS 言語の自動検知 + 日本語 (`ja-JP`) / 英語 (`en-US`) の切り替え対応。
   - システム同期 / ライト / ダークテーマ対応。

7. **ログオン時自動同期 (常駐不要)**
   - ログオン 30 秒後に `/silent` オプションでバックグラウンド実行し、同期後に即終了。常駐プロセス不要。

---

## ポータブルパッケージ（同梱版・非同梱版）

`Publish-Portable.ps1` を実行することで、用途に合わせた 2 種類のポータブルパッケージを出力できます。

| パッケージ | 出力先 | 特徴 | 用途 |
|---|---|---|---|
| **同梱版 (Self-Contained)** | `publish\GPUAssign-SelfContained\` | .NET 9 ランタイムや必要なライブラリをすべてフォルダ内に同梱 | .NET が入っていない他の PC や USB から単体で即座に起動したい場合 |
| **非同梱版 (Framework-Dependent)** | `publish\GPUAssign-FrameworkDependent\` | 軽量構成（.NET 9 Desktop Runtime がインストール済みの環境で動作） | ディスク容量を節約したい場合 |

---

## ビルド & パッケージ作成方法

### 1. 通常ビルド
```cmd
build.cmd          REM Debug ビルド
build.cmd release  REM Release ビルド
```

### 2. ポータブルパッケージの一括作成 (同梱版 & 非同梱版)
```powershell
.\Publish-Portable.ps1
```

---

## 実行方法

- **GUI 起動**:
  `GPUAssign.exe` をダブルクリック（UAC 管理者権限プロンプトが表示されます）
- **サイレント同期 (CLI / タスクスケジューラ用)**:
  `GPUAssign.exe /silent`

---

## ファイル構成

```text
GPUAssign/
├── Assets/
│   ├── default_apps.json       # プリセットアプリカタログ (30種)
│   └── Locales/
│       ├── ja-JP.json          # 日本語リソース
│       └── en-US.json          # 英語リソース
├── Models/
│   ├── AppDefinition.cs        # アプリ定義モデル (SearchMode, INPC)
│   └── AppConfig.cs            # 設定モデル (MaxDegreeOfParallelism, Theme, Language)
├── Services/
│   ├── ExeDiscoveryService.cs  # 固定/最新版/Glob/正規表現 EXE 探索
│   ├── GpuPreferenceService.cs # レジストリ読み書き & .reg バックアップ/復元
│   ├── SyncService.cs          # 並行度制御付きマルチスレッド並列同期
│   ├── ConfigService.cs        # EXE 直下の完全ポータブルデータ管理
│   ├── BackupService.cs        # .reg バックアップ管理
│   ├── StartupService.cs       # ログオン時自動タスク管理
│   └── LocalizationService.cs  # 多言語 JSON 管理 & OS 言語検知
├── Pages/
│   ├── AppsPage.xaml(.cs)      # アプリ管理画面 (フォルダを開く / 同期進捗)
│   ├── SyncLogPage.xaml(.cs)   # 同期ログ画面
│   ├── BackupPage.xaml(.cs)    # バックアップ/復元画面
│   ├── SettingsPage.xaml(.cs)  # 設定画面 (スレッド数設定・テーマ・言語)
│   └── Dialogs/
│       ├── AppEditDialog.cs    # アプリ追加・編集 (存在確認・テスト・フォルダ開く)
│       └── CatalogPickerDialog.cs # カタログ選択ダイアログ
├── app.manifest                # requireAdministrator (起動時管理者権限)
├── build.cmd                   # 通常ビルドスクリプト
└── Publish-Portable.ps1        # 同梱版・非同梱版ポータブル発行スクリプト
```
