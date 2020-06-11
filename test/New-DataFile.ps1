[CmdletBinding()]
param (
    [Parameter(Mandatory=$false)]
    $size = 100GB,

    [Parameter(Mandatory=$false)]
    #[System.IO.FileInfo]$dataFilename = (Join-Path $PSScriptRoot "output/data-$size.bin")
    [System.IO.FileInfo]$dataFilename = (Join-Path $PSScriptRoot "output/data-$(((Get-Date).ToUniversalTime()).ToString('yyyy-MM-ddTHHmmss')).bin"),
    
    [Parameter(Mandatory=$false)]
    [long]
    $MAX_CHUNK_SIZE = 100MB
)

#$VerbosePreference = "Continue"
$VerbosePreference = "SilentlyContinue"
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
Set-PSDebug -Strict

function Write-Data() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        $dataSize,

        [Parameter(Mandatory=$false)]
        [System.IO.FileInfo]$dataFilename = (Join-Path $PSScriptRoot "output/data-$size.data")
    )

    $stream = [System.IO.FileStream]::new($dataFilename, [System.IO.FileMode]::Create);
    try {
        [System.Security.Cryptography.RNGCryptoServiceProvider] $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
        try {
            $writer = [System.IO.BinaryWriter]::new($stream);
            try {
                $rndBytes = New-Object byte[] 0
                $activity = "${dataFilename}: Creating data file"
                $size = $dataSize
                while ($size -gt 0) {
                    Write-Progress -Activity $activity -Status "Progress:" -PercentComplete ((1-($size/$dataSize))*100)

                    $chunkSize = [Math]::Min($size, $MAX_CHUNK_SIZE)
                    if ($chunkSize -ne $rndBytes.Count) {
                        $rndBytes = New-Object byte[] $chunkSize
                    }
                    try {
                        $rng.GetBytes($rndbytes)
                        #[System.IO.File]::WriteAllBytes($dataFilename, $rndbytes)
                        $writer.Write($rndBytes);
                    } finally {
                        #$rndBytes = $null
                    }

                    # $rng.GetBytes($rndbytes)
                    # [System.IO.File]::WriteAllBytes($dataFilename, $rndbytes)
                    #$writer.Write('Testing...')

                    $size -= $chunkSize 
                }
                Write-Progress -Activity $activity -Completed
            } finally {
                $rndBytes = $null
                $writer.Flush();
                $writer.Close();
                $writer.Dispose()
            }    
        } finally {
            $rng.Dispose()
        }
    } finally {
        $stream.Dispose();
    }
}

# See:https://stackoverflow.com/questions/57530347/how-to-convert-value-to-kb-mb-or-gb-depending-on-digit-placeholders
function Format-Size() {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [double]$SizeInBytes
    )

    #$f = '{0:N2}'
    $f = '{0:0.##}'
    switch ([math]::Max($SizeInBytes, 0)) {
        {$_ -ge 1PB} {"${f}PB" -f ($SizeInBytes / 1PB); break}
        {$_ -ge 1TB} {"${f}TB" -f ($SizeInBytes / 1TB); break}
        {$_ -ge 1GB} {"${f}GB" -f ($SizeInBytes / 1GB); break}
        {$_ -ge 1MB} {"${f}MB" -f ($SizeInBytes / 1MB); break}
        {$_ -ge 1KB} {"${f}KB" -f ($SizeInBytes / 1KB); break}
        default {"$SizeInBytes Bytes"}
    }
}

Measure-Command {
    #1MB, 10MB, 100MB, 1GB, 10GB, 100GB | ForEach-Object {
    #10GB, 100GB | ForEach-Object {
    200GB, 300GB, 400GB, 500GB | ForEach-Object {
            Write-Data $_ "output\data-$(Format-Size $_).bin"
    }
}