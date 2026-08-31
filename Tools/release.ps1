#Requires -Version 5.1
<#
.SYNOPSIS
    Builds/Windows 를 릴리스 zip 으로 묶고 GitHub Release 로 올린다.

.DESCRIPTION
    버전의 단일 출처는 ProjectSettings.asset 의 bundleVersion 이다. 이 스크립트는
    그 값을 읽어 태그 이름과 릴리스 제목을 만들 뿐, 어디에도 버전을 따로 적어 두지 않는다.

.EXAMPLE
    ./Tools/release.ps1 -DryRun
    zip 만 만들어 내용물을 확인한다.

.EXAMPLE
    ./Tools/release.ps1
    태그를 밀고 GitHub Release 를 발행한다.
#>
[CmdletBinding()]
param(
    # zip 까지만 만들고 태그와 릴리스는 건너뛴다.
    [switch]$DryRun,

    # 빌드가 소스보다 오래됐다는 경고를 무시하고 진행한다.
    [switch]$Force,

    # 릴리스 노트. 주지 않으면 gh 가 커밋 목록으로 자동 생성한다.
    [string]$Notes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot 'Builds\Windows'
$exePath  = Join-Path $buildDir 'DOTORI ON.exe'

# 자동 업데이트 확인과 다운로드 페이지가 둘 다 이 파일명에 의존한다. 이름이 고정돼
# 있어야 releases/latest/download/<이름> 이 언제나 최신 릴리스를 가리킨다. 바꾸면
# 이미 배포된 구버전의 업데이트 경로가 끊기므로 함부로 건드리지 않는다.
$assetName = 'DOTORI_ON.zip'
$zipPath   = Join-Path $repoRoot ('Builds\' + $assetName)

function Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Warn([string]$message) { Write-Host "--  $message" -ForegroundColor Yellow }
function Fail([string]$message) { Write-Host "!!  $message" -ForegroundColor Red; exit 1 }

function Invoke-Git {
    $output = & git -C $repoRoot @args
    if ($LASTEXITCODE -ne 0) { Fail ("git " + ($args -join ' ') + " 가 실패했다.") }
    return $output
}

# --- 버전 -------------------------------------------------------------------

Step '버전 확인'
$settingsPath = Join-Path $repoRoot 'ProjectSettings\ProjectSettings.asset'
$match = Select-String -LiteralPath $settingsPath -Pattern '^\s*bundleVersion:\s*(\S+)\s*$' | Select-Object -First 1
if (-not $match) { Fail "ProjectSettings.asset 에서 bundleVersion 을 찾지 못했다." }

$version = $match.Matches[0].Groups[1].Value
# 두 자리로 두면 문자열 비교에서 0.10 이 0.7 보다 작다고 나온다. 앞으로 들어올
# 업데이트 확인이 이 값을 파싱하므로 여기서 세 자리를 강제한다.
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Fail "bundleVersion 이 '$version' 이다. 0.7.0 처럼 세 자리로 적을 것."
}
$tag = "v$version"
Write-Host "    bundleVersion : $version"

# --- 사전 점검 ---------------------------------------------------------------

Step '사전 점검'
if (-not (Test-Path -LiteralPath $exePath)) {
    Fail "빌드가 없다: $exePath`n    Unity 에서 [DOTORI ON > Build Windows x86_64] 를 먼저 실행할 것."
}

# 버전만 올리고 다시 빌드하지 않은 채 올리는 사고가 가장 흔하다. exe 가 마지막
# 소스 변경보다 오래됐으면 멈춘다. Library 는 Unity 가 수시로 건드리므로 보지 않는다.
$exeTime = (Get-Item -LiteralPath $exePath).LastWriteTime
$sourceRoots = @((Join-Path $repoRoot 'Assets'), (Join-Path $repoRoot 'ProjectSettings'))
$newestSource = Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($newestSource -and $newestSource.LastWriteTime -gt $exeTime) {
    $stale = "빌드({0})가 마지막 소스 변경({1}, {2})보다 오래됐다." -f `
        $exeTime.ToString('yyyy-MM-dd HH:mm'),
        $newestSource.LastWriteTime.ToString('yyyy-MM-dd HH:mm'),
        $newestSource.Name
    if (-not $Force) { Fail "$stale`n    다시 빌드하거나, 의도한 것이면 -Force 를 붙일 것." }
    Warn $stale
}

if (-not $DryRun) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Fail "GitHub CLI 가 없다.`n    winget install --id GitHub.cli 로 설치한 뒤 gh auth login 을 한 번 실행할 것."
    }
    $branch = (Invoke-Git rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -ne 'main') { Fail "현재 브랜치가 '$branch' 다. 릴리스는 main 에서 한다." }
    if (Invoke-Git status --porcelain) { Fail "커밋하지 않은 변경이 있다. 릴리스는 깨끗한 트리에서 한다." }
    if (Invoke-Git tag --list $tag) { Fail "태그 $tag 가 이미 있다. bundleVersion 을 올릴 것." }
}

# --- 패키징 -----------------------------------------------------------------

Step '패키징'
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('dotori-on-release-' + [guid]::NewGuid().ToString('N'))
$appDir = Join-Path $stageRoot 'DOTORI ON'
New-Item -ItemType Directory -Path $appDir -Force | Out-Null

try {
    # Builds/Windows 에는 배포하면 안 되는 것이 같이 쌓여 있다. *_DoNotShip 은 Burst
    # 디버그 심볼이고, zip 과 '구 빌드' 는 지난 릴리스의 잔해다. 셋 다 최상위에만
    # 있어서 최상위 한 겹만 걸러도 충분하다.
    Get-ChildItem -LiteralPath $buildDir | Where-Object {
        $_.Name -notlike '*.zip' -and
        $_.Name -notlike '*_DoNotShip' -and
        $_.Name -ne '구 빌드'
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $appDir -Recurse -Force
    }

    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    # ZipArchive / ZipArchiveMode 는 System.IO.Compression 에, ZipFile / ZipFileExtensions 는
    # System.IO.Compression.FileSystem 에 있다. 아래 코드가 넷을 다 쓰므로 둘 다 올린다.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    # ZipFile.CreateFromDirectory 를 쓰지 않고 엔트리를 직접 쓴다. .NET Framework 의
    # CreateFromDirectory 는 엔트리 경로에 '\' 를 넣는데, ZIP 스펙은 '/' 를 요구한다.
    # 탐색기는 넘어가지만 압축 도구에 따라 폴더가 아니라 이름에 '\' 가 든 파일 하나로
    # 읽힌다. 앞으로 붙을 자동 업데이트도 이 zip 을 풀어야 하므로 여기서 바로잡는다.
    #
    # 엔트리 이름은 stageRoot 기준 상대 경로라, zip 최상단이 'DOTORI ON/' 폴더가 된다.
    # 받는 사람이 압축을 풀었을 때 파일이 흩뿌려지지 않는다.
    $archive = [System.IO.Compression.ZipFile]::Open(
        $zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $prefixLength = $stageRoot.TrimEnd('\').Length + 1
        foreach ($file in Get-ChildItem -LiteralPath $stageRoot -Recurse -File) {
            $entryName = $file.FullName.Substring($prefixLength).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$zipMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host "    $assetName ($zipMb MB)"

if ($DryRun) {
    Step 'DryRun — 태그와 릴리스는 건너뛴다'
    Write-Host "    $zipPath"
    return
}

# --- 발행 -------------------------------------------------------------------

Step "태그 $tag"
Invoke-Git tag -a $tag -m "DOTORI ON $tag" | Out-Null
Invoke-Git push origin $tag | Out-Null

Step 'GitHub Release 발행'
$ghArgs = @('release', 'create', $tag, $zipPath, '--title', "DOTORI ON $tag", '--latest')
if ($Notes) { $ghArgs += @('--notes', $Notes) } else { $ghArgs += '--generate-notes' }
& gh @ghArgs
if ($LASTEXITCODE -ne 0) {
    Fail "gh release create 가 실패했다. 태그 $tag 는 이미 올라갔으니, 원인을 고친 뒤 릴리스만 다시 만들 것."
}

$remote = (Invoke-Git remote get-url origin).Trim() -replace '\.git$', ''
Write-Host ''
Write-Host '완료. 항상 최신을 가리키는 고정 다운로드 URL:' -ForegroundColor Green
Write-Host "  $remote/releases/latest/download/$assetName"
