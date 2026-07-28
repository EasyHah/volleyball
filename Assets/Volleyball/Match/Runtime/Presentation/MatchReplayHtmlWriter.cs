using System;
using System.IO;
using System.Text;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    public static class MatchReplayArtifactWriter
    {
        public static void Write(
            string outputDirectory,
            MatchReplayV4 replay)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException(
                    "Replay output directory is required.",
                    nameof(outputDirectory));
            }

            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            var json = ContractJson.SerializeV4(replay);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "replay.json"),
                json,
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(outputDirectory, "index.html"),
                Render(replay),
                new UTF8Encoding(false));
        }

        public static string Render(MatchReplayV4 replay)
        {
            if (replay == null)
                throw new ArgumentNullException(nameof(replay));
            return Html(EscapeEmbeddedJson(
                ContractJson.SerializeV4(replay)));
        }

        private static string EscapeEmbeddedJson(string json)
        {
            return json.Replace("</", "<\\/");
        }

        private static string Html(string embeddedJson)
        {
            return @"<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>MatchReplayV4 diagnostics</title>
<style>
:root{color-scheme:dark;font-family:Inter,system-ui,sans-serif;background:#07111d;color:#eaf5ff}
body{margin:0;padding:28px;background:linear-gradient(145deg,#07111d,#102b43)}
main{max-width:1180px;margin:auto}.top{display:flex;justify-content:space-between;gap:16px;align-items:end}
h1{margin:.15rem 0;font-size:2rem}.versions{display:flex;gap:10px;flex-wrap:wrap}.tag{padding:8px 12px;border:1px solid #3f769b;border-radius:999px;background:#102d47}
.hash{font:12px ui-monospace,monospace;color:#97c5df;overflow-wrap:anywhere}.events{display:grid;gap:16px;margin-top:24px}
.event{border:1px solid #285270;border-radius:14px;background:#0b2032;padding:18px;box-shadow:0 18px 40px #0005}
.event h2{margin:0 0 12px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:12px}
.panel{border-radius:10px;background:#102b43;padding:12px}.panel h3{margin:0 0 8px;color:#7ed9ff;font-size:.95rem}
.perspectives{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:12px}.home{border-left:4px solid #57a9ff}.away{border-left:4px solid #ff9a57}
dl{margin:0;display:grid;grid-template-columns:max-content 1fr;gap:5px 10px}dt{color:#9bb8ca}dd{margin:0;overflow-wrap:anywhere}
table{border-collapse:collapse;width:100%;font-size:.85rem}th,td{text-align:left;padding:6px;border-bottom:1px solid #285270}
</style>
</head>
<body>
<main>
  <header class='top'>
    <div><div>Canonical diagnostic replay</div><h1>MatchReplayV4</h1><div id='replay-hash' class='hash'></div></div>
    <div class='versions'><span class='tag'>Contract version 4</span><span class='tag'>Rules version 3</span></div>
  </header>
  <section id='events' class='events'></section>
</main>
<script id='embedded-replay' type='application/json'>" + embeddedJson + @"</script>
<script>
'use strict';
const replay=JSON.parse(document.querySelector('#embedded-replay').textContent);
if(!replay || replay.formatVersion!==4) throw new Error('Only MatchReplayV4 is supported.');
if(!replay.context || replay.context.contractVersion!==4 || replay.context.rulesVersion!==3) throw new Error('Native V4 context with V3 rules is required.');
document.querySelector('#replay-hash').textContent=`Replay SHA-256 ${replay.replayHash}`;
const text=value=>value===null||value===undefined?'—':String(value);
const vector=value=>value?`(${value.x}, ${value.y}, ${value.z})`:'—';
const items=value=>Array.isArray(value)?value.join(', '):'—';
const perspective=(side,event)=>{
  const view=event.perceptionAuthority?.observingSide===side?event.perceptionAuthority:null;
  if(!view) return `<div class='empty'>No event-owned view</div>`;
  return `<dl>
    <dt>View</dt><dd>${view.viewIdentity}</dd>
    <dt>Artifact</dt><dd>${view.authoritativeArtifactIdentity}</dd>
    <dt>Confidence</dt><dd>${view.confidence}</dd>
    <dt>Recognition delay</dt><dd>${view.recognitionDelaySeconds}</dd>
    <dt>Position uncertainty</dt><dd>${view.positionUncertaintyMeters}</dd>
    <dt>Visible threats</dt><dd>${view.visibleThreats.map(item=>`${item.zone} (${item.confidence})`).join(', ')||'—'}</dd>
    <dt>Support</dt><dd>${view.selectedSupportPlayerId} · ${view.selectedSupportZone}${view.conservativeFallback?' · conservative':''}</dd>
  </dl>`;
};
document.querySelector('#events').innerHTML=replay.events.map(event=>`
<article class='event'>
  <h2>#${event.sequenceNumber} ${event.eventKind} · ${event.actorPlayerId}</h2>
  <h3>AUTHORITATIVE / ACTUAL</h3>
  <div class='grid'>
    <section class='panel'><h3>Execution envelopes</h3><dl>
      <dt>Tested identity</dt><dd>${event.testedEnvelope.identity}</dd>
      <dt>Executable identity</dt><dd>${event.executableEnvelope.identity}</dd>
      <dt>Expansion</dt><dd>${event.testedEnvelope.currentExpansionCount} → ${event.executableEnvelope.currentExpansionCount}</dd>
      <dt>Policy</dt><dd>${event.testedEnvelope.policyIdentity}</dd>
      <dt>Source intent</dt><dd>${event.testedEnvelope.sourceIntentIdentity}</dd>
      <dt>Target bounds</dt><dd>${vector(event.executableEnvelope.targetError.minimum)} → ${vector(event.executableEnvelope.targetError.maximum)}</dd>
      <dt>Velocity bounds</dt><dd>${vector(event.executableEnvelope.velocityError.minimum)} → ${vector(event.executableEnvelope.velocityError.maximum)}</dd>
      <dt>Effort</dt><dd>${event.executableEnvelope.requestedEffort} / ${event.executableEnvelope.maximumEffort}</dd>
    </dl></section>
    <section class='panel'><h3>Trajectory artifact</h3><dl>
      <dt>Identity</dt><dd>${event.trajectory.artifactIdentity}</dd>
      <dt>Provider</dt><dd>${event.trajectory.predictorSource} v${event.trajectory.predictorVersion}</dd>
      <dt>Configuration</dt><dd>${event.trajectory.predictorConfigurationHash}</dd>
      <dt>Full cache key</dt><dd>${event.trajectory.cacheKey.identity}</dd>
      <dt>Degradation</dt><dd>${event.trajectory.cacheKey.degradationStep}</dd>
    </dl></section>
    <section class='panel'><h3>Actual sample classification</h3><dl>
      <dt>Kind</dt><dd>${event.classification.kind}</dd>
      <dt>Target</dt><dd>${vector(event.classification.actualSample.target)}</dd>
      <dt>Velocity</dt><dd>${vector(event.classification.actualSample.velocity)}</dd>
      <dt>Dimensions</dt><dd>${items(event.classification.offendingDimensions)}</dd>
    </dl></section>
    <section class='panel'><h3>Observed P6 geometry</h3><dl>
      <dt>Takeoff</dt><dd>${vector(event.observedP6Geometry?.takeoffPoint)}</dd>
      <dt>Contact</dt><dd>${vector(event.observedP6Geometry?.contactPoint)}</dd>
      <dt>Front zone</dt><dd>${text(event.observedP6Geometry?.isTakeoffInFrontZone)}</dd>
      <dt>Above net</dt><dd>${text(event.observedP6Geometry?.isContactAboveNet)}</dd>
      <dt>V3 decision</dt><dd>${event.ruleDecision.accepted?'Accepted':'Rejected'} · ${event.ruleDecision.reasonCode}</dd>
    </dl></section>
    <section class='panel'><h3>Runtime-consumed V4 fields</h3><table>
      <thead><tr><th>Player</th><th>Derived field</th><th>Value</th><th>Evidence</th></tr></thead>
      <tbody>${event.abilityConsumptions.map(item=>`<tr><td>${item.playerId}</td><td>${item.attributeName}</td><td>${item.value}</td><td>${item.evidenceKind}</td></tr>`).join('')}</tbody>
    </table></section>
    <section class='panel'><h3>Deterministic work budget</h3><dl>
      <dt>Configuration</dt><dd>${text(event.workBudget?.configurationIdentity)}</dd>
      <dt>Candidates</dt><dd>${text(event.workBudget?.candidateCount)}</dd>
      <dt>Samples</dt><dd>${text(event.workBudget?.sampleCount)}</dd>
      <dt>Expansions</dt><dd>${text(event.workBudget?.expansionCount)}</dd>
      <dt>Deterministic work units</dt><dd>${text(event.workBudget?.deterministicWorkUnits)}</dd>
      <dt>Outcome</dt><dd>${text(event.workBudget?.budgetOutcome)}</dd>
    </dl></section>
  </div>
  <div class='perspectives'>
    <section class='panel home'><h3>HOME PERCEIVED</h3>${perspective('Home',event)}</section>
    <section class='panel away'><h3>AWAY PERCEIVED</h3>${perspective('Away',event)}</section>
  </div>
</article>`).join('');
</script>
</body>
</html>";
        }
    }
}
