using System;
using System.IO;
using System.Text;
using Volleyball.Domain.Replay;

namespace Volleyball.Presentation
{
    public static class MatchReplayArtifactWriter
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static void Write(string outputDirectory, MatchReplayV1 replay)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Replay output directory is required.", nameof(outputDirectory));
            }

            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            var json = MatchReplayJson.Serialize(replay);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "replay.json"), json, Utf8WithoutBom);
            File.WriteAllText(
                Path.Combine(outputDirectory, "index.html"),
                BuildHtml(json),
                Utf8WithoutBom);
        }

        private static string BuildHtml(string replayJson)
        {
            var embeddedJson = replayJson.Replace("<", "\\u003c");
            return @"<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>MatchReplayV1 Viewer</title>
<style>
:root { color-scheme: dark; --bg:#08111d; --panel:#111f30; --line:#29415c; --ink:#edf5ff; --muted:#9eb0c5; --accent:#59d9ff; --orange:#ff9d45; --blue:#55a7ff; --danger:#ff6b7a; }
* { box-sizing:border-box; }
body { margin:0; min-height:100vh; background:radial-gradient(circle at top,#14283f 0,#08111d 52%); color:var(--ink); font:14px/1.45 Inter,ui-sans-serif,system-ui,sans-serif; }
button,select,input { font:inherit; }
.shell { max-width:1320px; margin:0 auto; padding:24px; }
.topbar { display:flex; align-items:flex-start; justify-content:space-between; gap:20px; margin-bottom:18px; }
.eyebrow { color:var(--accent); font-size:11px; font-weight:800; letter-spacing:.18em; text-transform:uppercase; }
h1 { margin:4px 0 0; font-size:25px; }
#load-status { color:var(--muted); text-align:right; }
.scoreboard { display:grid; grid-template-columns:repeat(6,minmax(100px,1fr)); gap:1px; overflow:hidden; margin-bottom:16px; border:1px solid var(--line); border-radius:12px; background:var(--line); }
.metric { min-height:64px; padding:10px 13px; background:rgba(17,31,48,.94); }
.metric span { display:block; color:var(--muted); font-size:10px; letter-spacing:.12em; text-transform:uppercase; }
.metric strong { display:block; margin-top:4px; font-size:17px; }
.workspace { display:grid; grid-template-columns:minmax(360px,640px) minmax(380px,1fr); gap:16px; align-items:start; }
.card { border:1px solid var(--line); border-radius:14px; background:rgba(12,25,40,.94); box-shadow:0 20px 60px rgba(0,0,0,.24); }
.court-card { padding:14px; }
#court { display:block; width:100%; max-height:72vh; border-radius:8px; background:#153858; }
.side { display:grid; gap:16px; }
.controls { padding:14px; }
.button-row { display:flex; flex-wrap:wrap; align-items:center; gap:8px; }
button,select { border:1px solid #365777; border-radius:8px; background:#162b42; color:var(--ink); padding:8px 11px; cursor:pointer; }
button:hover { border-color:var(--accent); }
#play { min-width:82px; background:#16617b; border-color:#2f9cb8; font-weight:800; }
.timeline-wrap { position:relative; margin-top:16px; padding-top:13px; }
#timeline { width:100%; accent-color:var(--accent); }
#event-markers { position:absolute; left:7px; right:7px; top:0; height:13px; pointer-events:none; }
.event-marker { position:absolute; width:2px; height:10px; border-radius:2px; background:var(--orange); transform:translateX(-1px); }
#event-caption { margin-top:8px; min-height:21px; color:var(--muted); }
#score-panel { overflow:hidden; }
.panel-head { display:flex; justify-content:space-between; gap:12px; padding:13px 15px; border-bottom:1px solid var(--line); }
.panel-head h2 { margin:0; font-size:15px; }
#decision-summary { color:var(--muted); font-size:12px; text-align:right; }
.table-scroll { overflow:auto; max-height:43vh; }
table { width:100%; border-collapse:collapse; font-variant-numeric:tabular-nums; }
th,td { padding:8px 10px; border-bottom:1px solid #20364d; text-align:right; white-space:nowrap; }
th { position:sticky; top:0; z-index:1; background:#13263b; color:var(--muted); font-size:10px; letter-spacing:.06em; text-transform:uppercase; }
th:first-child,td:first-child,th:nth-child(2),td:nth-child(2) { text-align:left; }
tr.selected { background:rgba(89,217,255,.12); }
.status-selected { color:var(--accent); font-weight:800; }
.status-unreachable,.status-consecutive { color:var(--danger); }
.empty { padding:28px 15px; color:var(--muted); text-align:center; }
.set-quality-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:8px; padding:13px 15px; }
.set-quality-grid div { padding:8px; border:1px solid #20364d; border-radius:8px; }
.set-quality-grid span { display:block; color:var(--muted); font-size:10px; text-transform:uppercase; }
.set-quality-reason { grid-column:1/-1; }
.player-label { fill:#fff; font-size:13px; font-weight:800; paint-order:stroke; stroke:#08111d; stroke-width:3px; stroke-linejoin:round; }
.court-note { fill:#a9c7df; font-size:12px; letter-spacing:.12em; }
@media (max-width:900px) { .workspace { grid-template-columns:1fr; } .scoreboard { grid-template-columns:repeat(3,1fr); } #court { max-height:none; } }
</style>
</head>
<body>
<main class='shell'>
  <header class='topbar'>
    <div><div class='eyebrow'>Diagnostic replay</div><h1>MatchReplayV1</h1></div>
    <div id='load-status'>Loading replay.json…</div>
  </header>
  <section class='scoreboard' aria-label='Replay state'>
    <div class='metric'><span>Score</span><strong id='score'>–</strong></div>
    <div class='metric'><span>Server</span><strong id='server'>–</strong></div>
    <div class='metric'><span>Rotations</span><strong id='rotations'>–</strong></div>
    <div class='metric'><span>Phase</span><strong id='phase'>–</strong></div>
    <div class='metric'><span>Possession</span><strong id='possession'>–</strong></div>
    <div class='metric'><span>Time</span><strong id='time'>0.000 s</strong></div>
  </section>
  <section class='workspace'>
    <div class='card court-card'><svg id='court' viewBox='0 0 450 900' role='img' aria-label='Top-down volleyball court'></svg></div>
    <div class='side'>
      <div class='card controls'>
        <div class='button-row'>
          <button id='previous-event' type='button'>← Event</button>
          <button id='play' type='button'>Play</button>
          <button id='next-event' type='button'>Event →</button>
          <label>Speed <select id='speed'><option value='0.5'>0.5×</option><option value='1' selected>1×</option><option value='2'>2×</option></select></label>
        </div>
        <div class='timeline-wrap'><div id='event-markers'></div><input id='timeline' type='range' min='0' max='1' step='0.001' value='0'></div>
        <div id='event-caption'>Ready</div>
      </div>
      <div id='score-panel' class='card'>
        <div class='panel-head'><h2>Decision candidates</h2><div id='decision-summary'></div></div>
        <div id='candidate-content' class='empty'>Move to a Decision event to inspect its ranking.</div>
      </div>
      <div id='set-quality' class='card'>
        <div class='panel-head'><h2>Set quality</h2><div id='set-quality-summary'></div></div>
        <div id='set-quality-content' class='empty'>Move to a set-contact event to inspect its attack chain.</div>
      </div>
    </div>
  </section>
</main>
<script id='embedded-replay' type='application/json'>" + embeddedJson + @"</script>
<script>
'use strict';
const svgNs='http://www.w3.org/2000/svg';
const ui={
  court:document.querySelector('#court'), status:document.querySelector('#load-status'),
  score:document.querySelector('#score'), server:document.querySelector('#server'), rotations:document.querySelector('#rotations'),
  phase:document.querySelector('#phase'), possession:document.querySelector('#possession'), time:document.querySelector('#time'),
  play:document.querySelector('#play'), speed:document.querySelector('#speed'), timeline:document.querySelector('#timeline'),
  markers:document.querySelector('#event-markers'), caption:document.querySelector('#event-caption'),
  previous:document.querySelector('#previous-event'), next:document.querySelector('#next-event'),
  summary:document.querySelector('#decision-summary'), candidates:document.querySelector('#candidate-content'),
  setQualitySummary:document.querySelector('#set-quality-summary'), setQuality:document.querySelector('#set-quality-content')
};
let replay=null;
let playing=false;
let currentTime=0;
let lastFrame=0;
let activeEventIndex=-1;
let lastAutoPausedDecision=-1;
let eventSnapshotIndexes=new Set();
const playerMetadata=new Map();

function validateReplay(value){
  if(!value || value.formatVersion!==1) throw new Error(`Unsupported replay formatVersion: ${value && value.formatVersion}`);
  if(!Array.isArray(value.players) || value.players.length!==12) throw new Error('MatchReplayV1 requires twelve players.');
  if(!Array.isArray(value.snapshots) || value.snapshots.length===0 || !Array.isArray(value.events)) throw new Error('Replay snapshots and events are required.');
  return value;
}

async function loadReplay(){
  try {
    const response=await fetch('replay.json',{cache:'no-store'});
    if(!response.ok) throw new Error(`HTTP ${response.status}`);
    ui.status.textContent='Loaded replay.json';
    return validateReplay(await response.json());
  } catch(error) {
    ui.status.textContent='Local-file mode · embedded replay fallback';
    return validateReplay(JSON.parse(document.querySelector('#embedded-replay').textContent));
  }
}

function svgElement(name,attributes={}){
  const element=document.createElementNS(svgNs,name);
  for(const [key,value] of Object.entries(attributes)) element.setAttribute(key,String(value));
  return element;
}

function initializeCourt(){
  ui.court.innerHTML='';
  ui.court.append(svgElement('rect',{x:0,y:0,width:450,height:900,fill:'#143b5c'}));
  ui.court.append(svgElement('rect',{x:18,y:18,width:414,height:864,fill:'#2f7391',stroke:'#e7f5ff','stroke-width':4}));
  ui.court.append(svgElement('line',{x1:18,y1:450,x2:432,y2:450,stroke:'#ffffff','stroke-width':6}));
  ui.court.append(svgElement('line',{x1:18,y1:300,x2:432,y2:300,stroke:'#d8edf8','stroke-width':2}));
  ui.court.append(svgElement('line',{x1:18,y1:600,x2:432,y2:600,stroke:'#d8edf8','stroke-width':2}));
  const blue=svgElement('text',{x:28,y:42,class:'court-note'}); blue.textContent='BLUE'; ui.court.append(blue);
  const orange=svgElement('text',{x:28,y:876,class:'court-note'}); orange.textContent='ORANGE'; ui.court.append(orange);
}

function courtPoint(position){
  return {x:225+(position.x/9)*414,y:450-(position.z/18)*864};
}

function interpolateNumber(a,b,t){ return a+((b-a)*t); }
function interpolateVector(a,b,t){ return {x:interpolateNumber(a.x,b.x,t),y:interpolateNumber(a.y,b.y,t),z:interpolateNumber(a.z,b.z,t)}; }
function interpolateYaw(a,b,t){ let delta=((b-a+540)%360)-180; return a+(delta*t); }

function interpolatedSnapshot(before,after,t){
  const afterPlayers=new Map(after.players.map(player=>[player.playerId,player]));
  return {...before,
    simulationTimeSeconds:interpolateNumber(before.simulationTimeSeconds,after.simulationTimeSeconds,t),
    ball:{position:interpolateVector(before.ball.position,after.ball.position,t),velocity:interpolateVector(before.ball.velocity,after.ball.velocity,t)},
    players:before.players.map(player=>{
      const target=afterPlayers.get(player.playerId);
      return target ? {...player,position:interpolateVector(player.position,target.position,t),yawDegrees:interpolateYaw(player.yawDegrees,target.yawDegrees,t),movementTarget:interpolateVector(player.movementTarget,target.movementTarget,t)} : player;
    })
  };
}

function snapshotAt(time,preferredIndex=null){
  if(preferredIndex!==null) return replay.snapshots[preferredIndex];
  let beforeIndex=0;
  let afterIndex=replay.snapshots.length-1;
  for(let index=0;index<replay.snapshots.length;index++){
    if(replay.snapshots[index].simulationTimeSeconds<=time+1e-7) beforeIndex=index;
    if(replay.snapshots[index].simulationTimeSeconds>time+1e-7){ afterIndex=index; break; }
  }
  const before=replay.snapshots[beforeIndex];
  const after=replay.snapshots[afterIndex];
  if(beforeIndex===afterIndex || eventSnapshotIndexes.has(beforeIndex) || eventSnapshotIndexes.has(afterIndex)) return before;
  const duration=after.simulationTimeSeconds-before.simulationTimeSeconds;
  if(duration<=0) return before;
  return interpolatedSnapshot(before,after,Math.max(0,Math.min(1,(time-before.simulationTimeSeconds)/duration)));
}

function activeEventAt(time){
  let found=-1;
  for(let index=0;index<replay.events.length;index++){
    if(Math.abs(replay.events[index].simulationTimeSeconds-time)<0.0005) found=index;
  }
  return found;
}

function renderCourt(snapshot,decision){
  initializeCourt();
  const selected=decision ? decision.selectedPlayerId : null;
  for(const player of snapshot.players){
    const metadata=playerMetadata.get(player.playerId);
    const point=courtPoint(player.position);
    const color=metadata.team==='Blue' ? '#55a7ff' : '#ff9d45';
    const group=svgElement('g');
    group.append(svgElement('circle',{cx:point.x,cy:point.y,r:selected===player.playerId?15:11,fill:color,stroke:selected===player.playerId?'#59d9ff':'#07111d','stroke-width':selected===player.playerId?5:3}));
    const angle=player.yawDegrees*Math.PI/180;
    group.append(svgElement('line',{x1:point.x,y1:point.y,x2:point.x+(Math.sin(angle)*25),y2:point.y-(Math.cos(angle)*25),stroke:'#ffffff','stroke-width':3,'stroke-linecap':'round'}));
    const label=svgElement('text',{x:point.x+16,y:point.y-14,class:'player-label'});
    label.textContent=`${metadata.team.toUpperCase()} P${metadata.rosterSlot} ${metadata.role.toUpperCase()}`;
    group.append(label);
    ui.court.append(group);
  }
  const ball=courtPoint(snapshot.ball.position);
  ui.court.append(svgElement('circle',{cx:ball.x,cy:ball.y,r:9,fill:'#fff7b2',stroke:'#1a2530','stroke-width':3}));
}

function candidateStatus(candidate,decision){
  if(candidate.playerId===decision.selectedPlayerId) return ['SELECTED','status-selected'];
  if(candidate.exclusionReason==='ConsecutiveTouch') return ['CONSECUTIVE TOUCH','status-consecutive'];
  if(candidate.exclusionReason==='Unreachable') return ['UNREACHABLE','status-unreachable'];
  return [candidate.isFeasible?'ELIGIBLE':'EXCLUDED',''];
}

function escapeHtml(value){
  const element=document.createElement('span');
  element.textContent=String(value);
  return element.innerHTML;
}

function renderDecision(event){
  if(!event || !event.decision){
    ui.summary.textContent='';
    ui.candidates.className='empty';
    ui.candidates.textContent='Move to a Decision event to inspect its ranking.';
    return;
  }
  const decision=event.decision;
  ui.summary.textContent=`${decision.team} · ${decision.stage} · ${decision.action} · ${decision.availableSeconds.toFixed(3)} s available`;
  const rows=decision.candidates.map(candidate=>{
    const metadata=playerMetadata.get(candidate.playerId);
    const status=candidateStatus(candidate,decision);
    const playerLabel=escapeHtml(`${metadata.team.toUpperCase()} P${metadata.rosterSlot} ${metadata.role}`);
    return `<tr class='${candidate.playerId===decision.selectedPlayerId?'selected':''}'><td>${playerLabel}</td><td class='${status[1]}'>${status[0]}</td><td>${candidate.reachability.toFixed(3)}</td><td>${candidate.nominalRole.toFixed(3)}</td><td>${candidate.approach.toFixed(3)}</td><td>${candidate.angle.toFixed(3)}</td><td>${candidate.technique.toFixed(3)}</td><td>${candidate.total.toFixed(3)}</td></tr>`;
  }).join('');
  ui.candidates.className='table-scroll';
  ui.candidates.innerHTML=`<table><thead><tr><th>Player</th><th>Status</th><th>Reach</th><th>Role</th><th>Approach</th><th>Angle</th><th>Technique</th><th>Total</th></tr></thead><tbody>${rows}</tbody></table>`;
}

function renderSetQuality(event){
  if(!event || !event.setChain){
    ui.setQualitySummary.textContent='';
    ui.setQuality.className='empty';
    ui.setQuality.textContent='Move to a set-contact event to inspect its attack chain.';
    return;
  }
  const chain=event.setChain;
  const vector=value=>`(${value.x.toFixed(2)}, ${value.y.toFixed(2)}, ${value.z.toFixed(2)})`;
  ui.setQualitySummary.textContent=`Grade ${chain.qualityGrade}`;
  ui.setQuality.className='set-quality-grid';
  ui.setQuality.innerHTML=`<div><span>Planned contact</span>${vector(chain.plannedAttackContactCenter)}</div><div><span>Actual contact</span>${vector(chain.actualAttackContactCenter)}</div><div><span>Replan</span>${escapeHtml(chain.replanOutcome)}</div><div><span>Responsibility</span>${escapeHtml(chain.primaryResponsibility)}</div><div class='set-quality-reason'><span>Reason</span>${escapeHtml(chain.reason)}</div>`;
}

function render(time,preferredSnapshotIndex=null,eventIndex=null){
  currentTime=Math.max(Number(ui.timeline.min),Math.min(Number(ui.timeline.max),time));
  activeEventIndex=eventIndex===null ? activeEventAt(currentTime) : eventIndex;
  const event=activeEventIndex>=0 ? replay.events[activeEventIndex] : null;
  const snapshot=snapshotAt(currentTime,preferredSnapshotIndex);
  const decision=event && event.kind==='Decision' ? event.decision : null;
  ui.timeline.value=currentTime;
  ui.score.textContent=`${snapshot.homeScore} : ${snapshot.awayScore}`;
  ui.server.textContent=snapshot.servingTeam.toUpperCase();
  ui.rotations.textContent=`H${snapshot.homeRotationOffset} · A${snapshot.awayRotationOffset}`;
  ui.phase.textContent=decision ? decision.stage : snapshot.rallyPhase;
  ui.possession.textContent=snapshot.possessionTeam || '–';
  ui.time.textContent=`${currentTime.toFixed(3)} s`;
  ui.caption.textContent=event ? `${event.kind} · ${event.team}${event.playerId?' · '+event.playerId:''}` : 'Between recorded events';
  renderCourt(snapshot,decision);
  renderDecision(event);
  renderSetQuality(event);
}

function setPlaying(value){ playing=value; ui.play.textContent=playing?'Pause':'Play'; lastFrame=performance.now(); }

function showEvent(index){
  if(index<0 || index>=replay.events.length) return;
  const event=replay.events[index];
  setPlaying(false);
  render(event.simulationTimeSeconds,event.snapshotIndex,index);
}

function eventAfter(time){ return replay.events.findIndex(event=>event.simulationTimeSeconds>time+0.0005); }
function eventBefore(time){
  let found=-1;
  for(let index=0;index<replay.events.length;index++) if(replay.events[index].simulationTimeSeconds<time-0.0005) found=index;
  return found;
}

function animate(now){
  if(playing){
    const previous=currentTime;
    const delta=((now-lastFrame)/1000)*Number(ui.speed.value);
    const next=Math.min(Number(ui.timeline.max),currentTime+delta);
    const decisionIndex=replay.events.findIndex((event,index)=>event.kind==='Decision' && index!==lastAutoPausedDecision && event.simulationTimeSeconds>previous+0.00001 && event.simulationTimeSeconds<=next+0.00001);
    if(decisionIndex>=0){ lastAutoPausedDecision=decisionIndex; showEvent(decisionIndex); }
    else { render(next); if(next>=Number(ui.timeline.max)) setPlaying(false); }
  }
  lastFrame=now;
  requestAnimationFrame(animate);
}

function initialize(value){
  replay=value;
  replay.players.forEach(player=>playerMetadata.set(player.playerId,player));
  replay.events.forEach(event=>eventSnapshotIndexes.add(event.snapshotIndex));
  const start=replay.snapshots[0].simulationTimeSeconds;
  const end=replay.snapshots[replay.snapshots.length-1].simulationTimeSeconds;
  ui.timeline.min=start; ui.timeline.max=end; ui.timeline.value=start;
  ui.markers.innerHTML='';
  for(const event of replay.events){
    const marker=document.createElement('span');
    marker.className='event-marker';
    marker.style.left=`${end===start?0:((event.simulationTimeSeconds-start)/(end-start))*100}%`;
    marker.title=`${event.kind} @ ${event.simulationTimeSeconds.toFixed(3)} s`;
    ui.markers.append(marker);
  }
  ui.play.addEventListener('click',()=>setPlaying(!playing));
  ui.timeline.addEventListener('input',()=>{ setPlaying(false); lastAutoPausedDecision=-1; render(Number(ui.timeline.value)); });
  ui.previous.addEventListener('click',()=>{ const index=activeEventIndex>0?activeEventIndex-1:eventBefore(currentTime); if(index>=0) showEvent(index); });
  ui.next.addEventListener('click',()=>{ const index=activeEventIndex>=0&&activeEventIndex<replay.events.length-1?activeEventIndex+1:eventAfter(currentTime); if(index>=0) showEvent(index); });
  render(start);
  requestAnimationFrame(animate);
}

loadReplay().then(initialize).catch(error=>{ ui.status.textContent=`Replay rejected · ${error.message}`; ui.status.style.color='#ff6b7a'; });
</script>
</body>
</html>
";
        }
    }
}
