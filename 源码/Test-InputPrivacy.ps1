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
    $null = $apply.Invoke($pet, [object[]]@($state, $time, $pet.Handle))
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
    $chooseBuild=$formType.GetMethod('SetBuild',$instance)
    $chooseRole=$formType.GetMethod('SelectCat',$instance)
    $null=$chooseBuild.Invoke($pet,[object[]]@([int]3))
    $null=$chooseRole.Invoke($pet,[object[]]@($config.Roles[7]))
    $null=$chooseBuild.Invoke($pet,[object[]]@([int]2))
    $saved=$config.GetType().GetMethod('Load').Invoke($null,[object[]]@([string]$settingsPath))
    Assert-True ($saved.Cat.Id -eq 'black-cat' -and $saved.Cat.BuildIndex -eq 2 -and $saved.Roles[1].BuildIndex -eq 3 -and $saved.Transparency -eq 35 -and $saved.ScaleIndex -eq 1) 'Builds must round-trip independently for each role and preserve desktop settings'
    $null=$chooseRole.Invoke($pet,[object[]]@($config.Roles[1]))
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
        Assert-True ($motion.MatchedWord.Label -eq '鸡你太美' -and $motion.Tricks.Current($time).Kind.ToString() -eq 'Basketball') 'Both basketball aliases must trigger their own dance in chat'
    }
    $decode=$assembly.GetType('KeyboardCat.Native').GetMethod('KeyCode')
    foreach($fixture in @(@(0x31,78),@(0x39,32),@(0x02,49))) {
        $raw=[Activator]::CreateInstance($dataType)
        $dataType.GetField('VKey').SetValue($raw,[uint16]0xe5)
        $dataType.GetField('MakeCode').SetValue($raw,[uint16]$fixture[0])
        Assert-True ($decode.Invoke($null,[object[]]@($raw.PSObject.BaseObject)) -eq $fixture[1]) 'IME process keys must preserve the underlying letter or candidate key'
    }
    $rules=@(
        @('NIHAO','你好','Wave'), @('AIXIN','爱心','Love'), @('XIAOYU','小鱼','Fish'),
        @('WANAN','晚安','Sleep'), @('TIAOWU','跳舞','Dance'), @('LAICAI','来财','Fortune'),
        @('JINGZIJIYIBEI','敬自己一杯','Toast'), @('YINGKONG','硬控','Freeze'),
        @('JINITAIMEI','鸡你太美','Basketball'), @('JNTM','鸡你太美','Basketball'),
        @('MIXUEBINGCHENGTIANMIMI','蜜雪冰城甜蜜蜜','SweetCone'), @('MXBCTMM','蜜雪冰城甜蜜蜜','SweetCone'),
        @('GANFANSHIJIAN','干饭时间','RiceBowl'), @('YIJIANSANLIAN','一键三连','TripleTap')
    )
    foreach($context in 'Ordinary','WeChatMain') {
        Set-TestFocus $context 3
        foreach($rule in $rules) {
            $motion.Reset()
            $time=3.0
            $letters=$rule[0].ToCharArray()
            for($i=0;$i -lt $letters.Count;$i++) {
                $null=Send-TestKey $letters[$i] $false $time
                if($i -lt $letters.Count-1) {
                    Assert-True ($null -eq $motion.Tricks.Current($time)) 'Partial pinyin must not trigger'
                } else {
                    Assert-True ($motion.MatchedWord.Label -eq $rule[1] -and $motion.Tricks.Current($time).Kind.ToString() -eq $rule[2]) "Match $($rule[0]) on its final keydown in $context"
                    $duration=$motion.Tricks.Current($time).Duration
                    $null=Send-TestKey $letters[$i] $false ($time+.01)
                }
                $null=Send-TestKey $letters[$i] $true ($time+.02)
                $time+=.07
            }
            $started=$motion.MatchedWord.Start
            foreach($key in [char]32,[char]49,[char]13) {
                $null=Send-TestKey $key $false $time
                $null=Send-TestKey $key $true ($time+.01)
                $time+=.04
            }
            $motion.MouseButtons(1,$time)
            $motion.MouseButtons(2,$time+.01)
            Assert-True ($motion.MatchedWord.Start -eq $started -and $null -eq $motion.Tricks.Current($started+$duration+.01)) 'Repeats and candidate selection must not replay or queue the trick'
        }
    }
    $catalog=$assembly.GetType('KeyboardCat.ChineseWord').GetField('All').GetValue($null)
    Assert-True ($catalog.Count -eq 12) 'All twelve public tricks have fixed Chinese words'
    $fish=$catalog | Where-Object {$_.Simplified -eq '小鱼'}
    $motion.Reset()
    $time=2.0
    foreach($c in 'XIAOYU'.ToCharArray()) {
        $motion.KeyEvent([int]$c,$false,$time,$true)
        $motion.KeyEvent([int]$c,$true,$time+.01,$true)
        $time+=.05
    }
    $original=$motion.MatchedWord.Start
    $motion.ExactWord($fish,'小魚',$motion.Tricks.Entry,$time,$true)
    Assert-True ($motion.MatchedWord.Start -eq $original -and $null -eq $motion.Tricks.Current(6)) 'Selecting the Han word for a pinyin hit must not replay'
    $motion.Reset()
    $motion.ExactWord($fish,'小魚',$motion.Tricks.Entry,1,$true)
    Assert-True ($motion.MatchedWord.Label -eq '小魚') 'A literal word without pinyin can trigger'
    $time=1.001
    foreach($c in 'XIAOYU'.ToCharArray()) {
        $motion.KeyEvent([int]$c,$false,$time,$true,$false,$false)
        $motion.KeyEvent([int]$c,$true,$time+.0001,$true,$false,$false)
        $time+=.001
    }
    Assert-True ($motion.MatchedWord.Start -eq 1 -and $null -eq $motion.Tricks.Current(4)) 'Late spelling for a literal hit is consumed'
    $motion.ExactWord($fish,'小鱼',$motion.Tricks.Entry,1.01,$true)
    Assert-True ($motion.MatchedWord.Start -eq 1.01) 'A second occurrence of the same literal can trigger immediately'
    # Test retained syllables independently of which input languages are installed on a CI runner.
    $motion.Reset()
    $time=1.0
    foreach($key in 'JING ZIJI1YIBEI'.ToCharArray()) {
        $split=$key -eq ' ' -or $key -eq '1'
        $motion.KeyEvent([int]$key,$false,$time,$true,$false,$split)
        $motion.KeyEvent([int]$key,$true,$time+.01,$true,$false,$split)
        $time+=.08
    }
    Assert-True ($motion.MatchedWord.Label -eq '敬自己一杯') 'Segmented pinyin must fire on the final letter'
    $motion.Reset()
    $null=$motion.Tricks.Observe([int][char]'L',1,$false,$false)
    $null=$motion.Tricks.Observe(32,1.1,$false,$true)
    foreach($c in 'OVE'.ToCharArray()) {$null=$motion.Tricks.Observe([int]$c,1.3,$false,$false)}
    Assert-True ($null -eq $motion.Tricks.Current(1.3)) 'English aliases must not span candidate keys'
    $motion.Reset()
    foreach($c in 'XIAO'.ToCharArray()) {$null=$motion.Tricks.Observe([int]$c,0,$false,$false)}
    foreach($c in 'YU'.ToCharArray()) {$null=$motion.Tricks.Observe([int]$c,12,$false,$false)}
    Assert-True ($null -eq $motion.Tricks.Current(12)) 'Stale syllables must expire'

    $motion.Reset()
    foreach($c in 'XIAO  YU'.ToCharArray()) {$null=$motion.Tricks.Observe([int]$c,1,$false,($c -eq ' '))}
    Assert-True ($null -eq $motion.Tricks.Current(1.1)) 'Two separators must break a partial spelling'
    $motion.Reset()
    foreach($c in 'XIAOYUXIAOYU'.ToCharArray()) {$null=$motion.Tricks.Observe([int]$c,1,$false,$false)}
    Assert-True ($motion.Tricks.Current(3.65).Kind.ToString() -eq 'Fish') 'Typing the complete spelling twice must play twice'
    $motion.Reset();$motion.Tricks.Enabled=$false
    foreach($c in 'XIAOYU'.ToCharArray()) {$null=Send-TestKey $c $false 1; $null=Send-TestKey $c $true 1.01}
    Assert-True ($null -eq $motion.Tricks.Current(1.1)) 'Disabling word tricks must disable pinyin'
    $motion.Tricks.Enabled=$true
    foreach($c in 'JNTM'.ToCharArray()) {$motion.KeyEvent([int]$c,$false,1.7,$false);$motion.KeyEvent([int]$c,$true,1.71,$false)}
    Assert-True ($null -eq $motion.MatchedWord -and $motion.Tricks.Current(1.8).Kind.ToString() -eq 'Basketball') 'Hiding labels must keep pinyin tricks enabled'
    Set-TestFocus Password 2
    foreach($c in 'AIXIN'.ToCharArray()) {$null=Send-TestKey $c $false 2; $null=Send-TestKey $c $true 2.01}
    Assert-True ($null -eq $motion.MatchedWord -and $null -eq $motion.Tricks.Current(2.1)) 'Passwords must clear and suppress all pinyin tricks'
    Assert-True ($motion.Private -and $motion.Cover(2.3) -gt 0.99) 'Password fields must cover the eyes'
    Set-TestFocus ActivityOnly 2.4
    Assert-True (!(Send-TestKey 'Q' $false 2.5) -and $motion.Private) 'Losing metadata in the same password window must preserve privacy'
    Set-TestFocus WeChatMain 2.6
    Assert-True (!(Send-TestKey 'F' $false 2.7) -and $motion.Private) 'An opaque main window must not unlock a known password context'
    Write-Output 'Passed all twelve word tricks on final keydown, selection and repeat deduplication, WeChat, and protected input.'
} finally {
    if ($pet) { $pet.Dispose() }
    if (Test-Path -LiteralPath $settingsPath) { Remove-Item -LiteralPath $settingsPath }
}

$fixture=Join-Path ([IO.Path]::GetTempPath()) ('PawPaw-caret-'+[guid]::NewGuid().ToString('N')+'.exe')
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path $csc)) {$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
$wpf=Join-Path (Split-Path -Parent $csc) 'WPF'
try {
    & $csc /nologo /codepage:65001 "/lib:$wpf" /r:WindowsBase.dll,PresentationFramework.dll,PresentationCore.dll,UIAutomationClient.dll,UIAutomationTypes.dll,System.Xaml.dll "/out:$fixture" (Join-Path $PSScriptRoot '..\scripts\test-chinese-caret.cs')
    if($LASTEXITCODE){throw 'Compiling the Chinese caret fixture failed'}
    & $fixture $exe
    if($LASTEXITCODE){throw 'Native Chinese input regression'}
} finally {if(Test-Path -LiteralPath $fixture){Remove-Item -LiteralPath $fixture}}

$stageFixture=Join-Path ([IO.Path]::GetTempPath()) ('PawPaw-poses-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    $files=@(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.cs' | ForEach-Object FullName)
    & $csc /nologo /unsafe /optimize+ /codepage:65001 /main:KeyboardCat.Painter "/lib:$wpf" /r:UIAutomationClient.dll,UIAutomationTypes.dll,WindowsBase.dll,System.Drawing.dll,System.Windows.Forms.dll "/resource:$PSScriptRoot\ChineseWords.tsv,ChineseWords.tsv" "/out:$stageFixture" @files (Join-Path $PSScriptRoot '..\scripts\test-choreography.cs')
    if($LASTEXITCODE){throw 'Compiling the choreography fixture failed'}
    & $stageFixture
    if($LASTEXITCODE){throw 'Choreography regression'}
} finally {if(Test-Path -LiteralPath $stageFixture){Remove-Item -LiteralPath $stageFixture}}
