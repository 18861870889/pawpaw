# Run with Windows PowerShell after build.cmd. Test packets go only to the desktop pet.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -File $PSCommandPath
    exit $LASTEXITCODE
}

$exe = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\PawPaw-Windows.exe')).Path
$assembly = [Reflection.Assembly]::LoadFrom($exe)
$formType = $assembly.GetType('KeyboardCat.PetForm', $true)
$fieldType = $assembly.GetType('KeyboardCat.FieldPrivacy', $true)
$dataType = $assembly.GetType('KeyboardCat.Native+RawPayload', $true)
$instance = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$static = [Reflection.BindingFlags]'Static,NonPublic'
$apply = $formType.GetMethod('ApplyPrivacy', $instance)
$route = $formType.GetMethod('HandleKey', $instance)
$inspect = $assembly.GetType('KeyboardCat.PasswordFocus').GetMethod('ElementPrivacy', $static)

function Assert-True([bool]$condition, [string]$description) {
    if (!$condition) { throw $description }
}
function Set-TestFocus([string]$kind, [double]$time) {
    $state = [Enum]::Parse($fieldType, $kind)
    $null = $apply.Invoke($pet, [object[]]@($state, $time, [IntPtr]42))
}
function Send-TestKey([char]$letter, [bool]$released, [double]$time) {
    $packet = [Activator]::CreateInstance($dataType)
    $dataType.GetField('VKey').SetValue($packet, [uint16]$letter)
    $dataType.GetField('KeyFlags').SetValue($packet, [uint16]$released)
    return $route.Invoke($pet, [object[]]@($packet.PSObject.BaseObject, $time))
}

$unreported = $inspect.Invoke($null, [object[]]@($null, [IntPtr]::Zero, [Enum]::Parse($fieldType, 'ActivityOnly')))
Assert-True ($unreported.ToString() -eq 'ActivityOnly') 'Unreported controls must use activity only'
$settingsPath = Join-Path ([IO.Path]::GetTempPath()) ('KeyboardCat-focus-' + [guid]::NewGuid().ToString('N') + '.ini')
$constructor = $formType.GetConstructor($instance, $null, [type[]]@([string]), $null)
# Load an older profile through the normal application constructor.
[IO.File]::WriteAllText($settingsPath, "cat=ragdoll`ntransparency=35`nlabels=1`nwordTriggers=1`n")
$pet = $null
try {
    $pet = $constructor.Invoke([object[]]@([string]$settingsPath))
    $config = $formType.GetField('settings', $instance).GetValue($pet)
    Assert-True ($config.Cat.Id -eq 'ragdoll' -and $config.Transparency -eq 35) 'Upgrades must load the selected role and opacity'
    $retry = $formType.GetField('retryInputAt', $instance)
    $retry.SetValue($pet, [double]10)
    $formType.GetField('pause', $instance).GetValue($pet).PerformClick()
    Assert-True ($config.Paused -and [double]::IsPositiveInfinity($retry.GetValue($pet))) 'Pausing while disconnected must cancel a pending reconnect'
    $motion = $formType.GetField('motion', $instance).GetValue($pet)
    Set-TestFocus Ordinary 0
    $null = Send-TestKey 'Q' $false 0.1
    Assert-True ($motion.Bubbles.Count -eq 1 -and $motion.Bubbles[0].Label -eq 'Q') 'Ordinary fields must retain named keys'

    Set-TestFocus ActivityOnly 0.2
    $time = 0.3
    foreach ($letter in 'FISH'.ToCharArray()) {
        Assert-True (Send-TestKey $letter $false $time) 'An unreported field must react to typing'
        Assert-True (!(Send-TestKey $letter $true ($time + 0.02))) 'Key release must not start another tap'
        Set-TestFocus ActivityOnly ($time + 0.04)
        $time += 0.1
    }
    Assert-True ($motion.IsTypingActivity($time)) 'Polling the same window must not interrupt the animation'
    Assert-True ($motion.Bubbles.Count -eq 0 -and $null -eq $motion.Left.Key -and $null -eq $motion.Right.Key) 'Activity-only typing must not identify a key'
    Assert-True ($null -eq $motion.Tricks.Current($time)) 'Activity-only typing must not trigger a word'
    Assert-True (!$motion.IsTypingActivity($time + 1)) 'The typing burst must finish on its own'

    Set-TestFocus WeChatMain 1.5
    $time = 1.51
    foreach ($letter in 'FISH'.ToCharArray()) {
        $null = Send-TestKey $letter $false $time
        if ($letter -eq 'F') {
            Assert-True ($motion.Bubbles.Count -eq 1 -and $motion.Bubbles[0].Label -eq 'F') 'WeChat must show a named key before a match'
        }
        $null = Send-TestKey $letter $true ($time + 0.02)
        $time += 0.08
    }
    Assert-True ($motion.MatchedWord.Label -eq 'fish' -and $motion.Tricks.Current($time).Kind.ToString() -eq 'Fish') 'The WeChat route must play the matching trick and display the typed alias'

    foreach ($alias in 'JINITAIMEI','JNTM') {
        $motion.Reset()
        $time = 3.0
        foreach ($letter in $alias.ToCharArray()) {
            $null = Send-TestKey $letter $false $time
            $null = Send-TestKey $letter $true ($time + .02)
            $time += .07
        }
        Assert-True ($motion.MatchedWord.Label -eq $alias.ToLowerInvariant() -and $motion.Tricks.Current($time).Kind.ToString() -eq 'Basketball') 'Both basketball aliases must trigger their own dance in chat'
    }
    Set-TestFocus Password 2
    Assert-True ($null -eq $motion.MatchedWord -and $null -eq $motion.Tricks.Current(2)) 'Password entry must clear the word bubble and active trick'
    Assert-True ($motion.Private -and $motion.Cover(2.3) -gt 0.99) 'Password fields must cover the eyes'
    Set-TestFocus ActivityOnly 2.4
    Assert-True (!(Send-TestKey 'Q' $false 2.5) -and $motion.Private) 'Losing metadata in the same password window must preserve privacy'
    Set-TestFocus WeChatMain 2.6
    Assert-True (!(Send-TestKey 'F' $false 2.7) -and $motion.Private) 'An opaque main window must not unlock a known password context'
    Write-Output 'Passed ordinary, WeChat, unreported and protected input regressions.'
} finally {
    if ($pet) { $pet.Dispose() }
    if (Test-Path -LiteralPath $settingsPath) { Remove-Item -LiteralPath $settingsPath }
}
