# ElevenLabs Voice Generator for Ciga2026 Cutscenes
# Usage: .\generate_voice.ps1 -ApiKey "your-key-here"
# Output: MP3 files in the same directory

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey
)

$voiceId = "TxGEqnHWrfWFTfGW9XjX"  # Josh - deep male storytelling voice
$outputDir = $PSScriptRoot

$scripts = @{
    "opening.mp3" = @"
They say that those who sink an anchor into uncharted waters... and offer up a creature of the sea... may forge a pact with the God of the Deep. A pact that brings wealth beyond measure.

The Captain was nobody, once. Just another sailor chasing the horizon. Until the storm. Until the anchor. He dragged it up from the ocean floor, and the sea itself whispered to him: Bring me one who belongs to the ocean.

So he did. He caught a mermaid. Wounded. Beautiful. He locked her in the belly of his ship, believing the anchor would bind fortune to him forever.

At midnight, the crew dropped anchor over the sacrificial waters. The ritual was about to begin.
"@

    "phase2.mp3" = @"
She ran. Through crew quarters, past notice boards scrawled with promises of gold. Through cargo holds where treasure lay entombed in coral... and the bodies of sea creatures told silent stories.

But every corridor led back to where she began. The ship had no end.

The anchor pulsed with a faint, cold light. And there, in the shadows, she saw them. Fragments left by others who had been offered before her. Perhaps... they could show her the way.
"@

    "phase3.mp3" = @"
The keys were gathered. The door creaked open.

But something had awakened in the deep. The God was not satisfied with fragments.

A soul rose from the darkness. Bound by the same anchor. Once a sacrifice... now a guardian of the depths. It stood between her and freedom.
"@

    "ending.mp3" = @"
The guardian fell. The anchor fell silent at last.

She found the Captain's quarters. On the wall, written in a trembling hand, his final words:

The sea accepted my sacrifice. But the offering was never the mermaid. I was never the one who chose. The anchor chose. It chose all of us.

The crew were gone. Silent. Still. The anchor had taken every soul aboard.

She dove into the sea.

The anchor remains on the ocean floor. Waiting. Listening.

For the next one who hears the whisper.
"@
}

foreach ($file in $scripts.Keys) {
    $text = $scripts[$file]
    Write-Host "Generating $file ..." -ForegroundColor Cyan

    $body = @{
        text = $text
        model_id = "eleven_multilingual_v2"
        voice_settings = @{
            stability = 0.45
            similarity_boost = 0.75
            style = 0.3
            use_speaker_boost = $true
        }
    } | ConvertTo-Json -Depth 3

    try {
        Invoke-RestMethod -Uri "https://api.elevenlabs.io/v1/text-to-speech/$voiceId" `
            -Method Post `
            -Headers @{
                "xi-api-key" = $ApiKey
                "Content-Type" = "application/json"
            } `
            -Body $body `
            -OutFile "$outputDir\$file"

        Write-Host "  -> $file saved ($((Get-Item "$outputDir\$file").Length) bytes)" -ForegroundColor Green
    } catch {
        Write-Host "  -> FAILED: $_" -ForegroundColor Red
    }
}

Write-Host "`nDone! Copy the .mp3 files to Assets/Audio/Cutscenes/ in Unity."
