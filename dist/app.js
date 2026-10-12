const {RULES,PATTERNS,evolve}=Automata;
const $=id=>document.getElementById(id), W=80,H=52,canvas=$('world'),ctx=canvas.getContext('2d');
let cells=new Uint8Array(W*H),ghosts=new Uint8Array(W*H),initial, generation=0,running=false,rule=RULES.life,wrap=true,painting=false,erase=false,history=[],last=0,accumulator=0,previousPoint=null;
const count=()=>cells.reduce((a,b)=>a+b,0);
function render(){
  const size=canvas.width/W;
  ctx.fillStyle='#101a14';ctx.fillRect(0,0,canvas.width,canvas.height);
  for(let i=0;i<cells.length;i++){
    if(cells[i])ctx.fillStyle='#b9e78c';
    else if(ghosts[i]&&$('trails').checked)ctx.fillStyle=['','#1b2b1b','#263b21','#354f2a','#4a6635'][ghosts[i]];
    else continue;
    ctx.fillRect((i%W)*size+1,Math.floor(i/W)*size+1,size-2,size-2);
  }
  ctx.strokeStyle='#b6d5a009';ctx.lineWidth=1;
  ctx.beginPath();for(let x=0;x<=W;x++){ctx.moveTo(x*size+.5,0);ctx.lineTo(x*size+.5,canvas.height);}for(let y=0;y<=H;y++){ctx.moveTo(0,y*size+.5);ctx.lineTo(canvas.width,y*size+.5);}ctx.stroke();
}
function stats(record=false){
  const pop=count();$('generation').textContent=String(generation).padStart(4,'0');$('population').textContent=pop.toLocaleString();$('density').innerHTML=(pop/(W*H)*100).toFixed(1)+'<small>%</small>';
  if(record){history.push(pop);if(history.length>100)history.shift();}
  const max=Math.max(1,...history);$('chart-path').setAttribute('d',history.map((n,i)=>(i?'L':'M')+(i*160/Math.max(1,history.length-1)).toFixed(2)+','+(32-n/max*29).toFixed(2)).join(' '));
  $('hint').hidden=running||generation>0;
}
function setRunning(value){running=value;accumulator=0;last=performance.now();$('play').textContent=running?'Ⅱ 暂停演化':'▶ 开始演化';$('status').textContent=running?'演化中':'已暂停';$('status-dot').classList.toggle('running',running);stats();}
function advance(){const next=evolve(cells,W,H,rule,wrap);for(let i=0;i<cells.length;i++)ghosts[i]=cells[i]&&!next[i]?4:Math.max(0,ghosts[i]-1);cells=next;generation++;stats(true);render();}
function checkpoint(){initial=cells.slice();generation=0;history=[];ghosts.fill(0);stats(true);render();}
function markPattern(name){document.querySelectorAll('[data-pattern]').forEach(b=>b.classList.toggle('active',b.dataset.pattern===name));}
function loadPattern(name){setRunning(false);cells.fill(0);const rows=PATTERNS[name],ox=Math.floor((W-Math.max(...rows.map(r=>r.length)))/2),oy=Math.floor((H-rows.length)/2);rows.forEach((row,y)=>[...row].forEach((v,x)=>{if(v==='O')cells[(y+oy)*W+x+ox]=1;}));markPattern(name);checkpoint();}
function randomize(){setRunning(false);cells=Uint8Array.from({length:W*H},()=>+(Math.random()<.22));markPattern('');checkpoint();}
$('play').onclick=()=>setRunning(!running);$('step').onclick=()=>{setRunning(false);advance();};$('reset').onclick=()=>{setRunning(false);cells=initial.slice();checkpoint();markPattern('');};$('clear').onclick=()=>{setRunning(false);cells.fill(0);markPattern('');checkpoint();};$('random').onclick=randomize;
document.querySelectorAll('[data-pattern]').forEach(b=>b.onclick=()=>loadPattern(b.dataset.pattern));
$('rules').onchange=()=>{setRunning(false);rule=RULES[$('rules').value];$('rule-code').textContent=rule.code;$('rule-label').textContent=rule.label+' · '+rule.code.replaceAll(' ','');$('rule-description').textContent=rule.description;checkpoint();};
$('wrap').onchange=()=>{wrap=$('wrap').checked;setRunning(false);checkpoint();};$('trails').onchange=render;$('speed').oninput=()=>{$('speed-value').textContent=$('speed').value;accumulator=0;};
function setBrush(value){erase=value;for(const id of ['draw','erase']){const selected=(id==='erase')===erase;$(id).classList.toggle('selected',selected);$(id).setAttribute('aria-pressed',selected);}}
$('draw').onclick=()=>setBrush(false);$('erase').onclick=()=>setBrush(true);
function point(e){const r=canvas.getBoundingClientRect();return [Math.min(W-1,Math.max(0,Math.floor((e.clientX-r.left)/r.width*W))),Math.min(H-1,Math.max(0,Math.floor((e.clientY-r.top)/r.height*H)))];}
function paint(e){const p=point(e),start=previousPoint||p,steps=Math.max(Math.abs(p[0]-start[0]),Math.abs(p[1]-start[1]),1);for(let s=0;s<=steps;s++){const x=Math.round(start[0]+(p[0]-start[0])*s/steps),y=Math.round(start[1]+(p[1]-start[1])*s/steps);cells[y*W+x]=erase?0:1;ghosts[y*W+x]=0;}previousPoint=p;stats();render();}
canvas.onpointerdown=e=>{if(e.button!==0)return;setRunning(false);painting=true;previousPoint=null;canvas.setPointerCapture(e.pointerId);markPattern('');paint(e);};canvas.onpointermove=e=>{if(painting)paint(e);};
function finishPaint(){if(!painting)return;painting=false;previousPoint=null;checkpoint();}
canvas.onpointerup=finishPaint;canvas.onpointercancel=finishPaint;canvas.onlostpointercapture=finishPaint;
document.addEventListener('keydown',e=>{if(e.target.closest('input,select,button,textarea,a')||e.ctrlKey||e.metaKey||e.altKey||e.repeat)return;if(e.code==='Space'){e.preventDefault();setRunning(!running);}if(e.key.toLowerCase()==='n')$('step').click();if(e.key.toLowerCase()==='r')$('reset').click();});
function frame(now){if(running){accumulator+=Math.min(now-last,200);const interval=1000/Number($('speed').value);while(accumulator>=interval){advance();accumulator-=interval;}}last=now;requestAnimationFrame(frame);}
loadPattern('gun');requestAnimationFrame(frame);
