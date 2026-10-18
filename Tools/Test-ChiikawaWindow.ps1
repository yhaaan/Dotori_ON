$ErrorActionPreference = 'Stop'
$eventWorkspace = Split-Path -Parent $PSScriptRoot
$eventSources = @(
    (Join-Path $PSScriptRoot 'Tests/ChiikawaWindow/Stubs.cs'),
    (Join-Path $eventWorkspace 'Assets/01. Scripts/08. Events/Subjugation/SubjugationNativeWindow.cs'),
    (Join-Path $PSScriptRoot 'Tests/ChiikawaWindow/Check.cs')
)
# Compile the actual Windows implementation against minimal Unity value-type stubs.
# The check creates two temporary native windows; it never signs in or moves the mouse.
Add-Type -Path $eventSources -CompilerOptions '/define:UNITY_STANDALONE_WIN'
[ChiikawaNativeCheck]::Run()
