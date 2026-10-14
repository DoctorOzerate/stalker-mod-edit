// Pure import/export helpers. Browser passes DOMParser; tests may pass @xmldom/xmldom.
export const NODE_TYPES=new Set(['START','PHRASE','CONDITION','QUEST_CHECK','ITEM_CHECK','RANDOM','ACTION','END']);
export function normalizePath(value){let p=String(value||'').replace(/\\/g,'/').replace(/\/+/g,'/');if(/^(?:[a-z]:|\/\/)/i.test(p))return '';const a=[];for(const x of p.split('/')){if(!x||x==='.')continue;if(x==='..'){if(a.length)a.pop();else return '';}else a.push(x)}return a.join('/')}
export function escapeText(v){return String(v??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')}
export function escapeAttribute(v){return escapeText(v).replace(/"/g,'&quot;').replace(/'/g,'&apos;')}
function children(el,name){return Array.from(el.childNodes||[]).filter(x=>x.nodeType===1&&x.tagName===name)}
export function parseLtx(text){const lines=String(text??'').split(/\r?\n/),sections=[],includes=[];let cur;for(let i=0;i<lines.length;i++){let raw=lines[i], code=raw.replace(/;.*$/,'').trim(),m=code.match(/^\[([^\]]+)\]\s*(?::\s*(.*))?$/);if(m){cur={name:m[1].trim(),parents:(m[2]||'').split(',').map(x=>x.trim()).filter(Boolean),startLine:i+1,entries:[],rawLines:[raw]};sections.push(cur);continue}let inc=code.match(/^#include\s+"([^"]+)"/i);if(inc){includes.push({path:inc[1],line:i+1,raw});continue}let eq=code.indexOf('=');if(eq>0&&cur){cur.entries.push({key:code.slice(0,eq).trim(),value:code.slice(eq+1).trim(),line:i+1,raw});cur.rawLines.push(raw)}else if(cur)cur.rawLines.push(raw)}return {lines,sections,includes}}
export function parseCharactersXml(text,Parser=globalThis.DOMParser){
 if(!Parser)throw Error('DOMParser недоступен');
 const d=new Parser().parseFromString(String(text).replace(/^\s*#include\b[^\n]*$/gm,''),'application/xml');
 if(d.getElementsByTagName('parsererror').length)return [];
 const pick=(el,tag,attr)=>{const a=el.getAttribute(attr);if(a)return a;const cs=children(el,tag);if(!cs.length)return '';const ts=children(cs[0],'text');return (ts.length?ts[0].textContent:cs[0].textContent||'').trim()};
 return Array.from(d.getElementsByTagName('specific_character')).map(x=>({
   id:x.getAttribute('id')||'',
   name:pick(x,'name','name')||x.getAttribute('id')||'',
   community:pick(x,'community','community')||x.getAttribute('team')||'',
   rank:pick(x,'rank','rank'),
   reputation:pick(x,'reputation','reputation'),
   class:pick(x,'class','class'),
   terrain_sect:pick(x,'terrain_sect','terrain_sect'),
   snd_config:pick(x,'snd_config','snd_config'),
   map_icon:(function(){const mi=children(x,'map_icon')[0];return mi?{x:mi.getAttribute('x')||'',y:mi.getAttribute('y')||''}:null;})(),
   bio:pick(x,'bio','bio'),
   icon:pick(x,'icon','icon'),
   visual:pick(x,'visual','visual'),
   supplies:(function(){const s=children(x,'supplies')[0];return s?(s.textContent||'').trim():'';})(),
   start_dialog:children(x,'start_dialog').map(e=>e.textContent.trim()),
   actor_dialog:children(x,'actor_dialog').map(e=>e.textContent.trim()),
   source:'gamedata'
 })).filter(x=>x.id)
}
export function parseDialogueXml(text,Parser=globalThis.DOMParser){
 if(!Parser)throw Error('DOMParser недоступен');const d=new Parser().parseFromString(String(text),'application/xml');if(d.getElementsByTagName('parsererror').length)throw Error('Некорректный XML диалогов');
 const root=d.documentElement, dialogs=root&&root.tagName==='game_dialogs'?children(root,'dialog'):root&&root.tagName==='dialog'?[root]:[];
 const result=[];
 for(let di=0;di<dialogs.length;di++){const dialog=dialogs[di], name=dialog.getAttribute('id')||`dialogue_${di}`, list=children(dialog,'phrase_list')[0], warnings=[];
  if(!list){warnings.push(`${name}: отсутствует точный тег phrase_list`);result.push({id:name,name,nodes:[],edges:[],warnings,imported:true});continue}
  const phrases=children(list,'phrase'), nodes=[{id:`${name}__start`,type:'START',x:0,y:0,data:{}}], edges=[], occurrences=new Map(), raws=[];
  phrases.forEach((p,index)=>{const phraseId=p.getAttribute('id')||String(index), count=occurrences.get(phraseId)||0;occurrences.set(phraseId,count+1);if(count)warnings.push(`${name}: дублирующийся phrase id "${phraseId}" (связи ведут к первому)`);const id=`${name}__phrase_${phraseId}__${count}`;
   const meta={};for(const tag of ['has_info','dont_has_info','precondition','give_info','disable_info','action'])meta[tag]=children(p,tag).map(e=>e.textContent??'');
   const te=children(p,'text')[0], text=te?(te.textContent||''):'', actor=(p.getAttribute('actor')||'').toLowerCase(), role=/actor|player/.test(actor)?'player':'unknown';
   const snd=children(p,'sound')[0]||children(p,'voice')[0], voice=snd?(snd.textContent||'').trim():'';
   nodes.push({id,type:'PHRASE',x:0,y:0,data:{phraseId,role,roleSource:actor?'xml_actor':'unknown',text_id:text,use_id:!!text,text:'',voice_id:voice,expression:'',action_type:'',params:'',metadata:meta,xray:{metadata:meta}}});
   children(p,'next').forEach(n=>raws.push({from:id,to:n.textContent.trim()}));
  });
  const resolve=(pid)=>{const n=nodes.find(x=>x.data&&x.data.phraseId===pid);return n&&n.id};
  const first=nodes.find(n=>n.type==='PHRASE');if(first)edges.push({id:`e_${name}_start`,source:`${name}__start`,target:resolve('0')||first.id});
  raws.forEach((r,i)=>{const target=resolve(r.to);edges.push({id:`e_${name}_${i}`,source:r.from,target:target||`${name}__missing_${r.to}`,broken:!target,originalNext:r.to})});
  nodes.filter(n=>n.type==='PHRASE'&&!edges.some(e=>e.source===n.id&&!e.broken)).forEach(n=>{const end=`${name}__end_${n.id}`;nodes.push({id:end,type:'END',x:0,y:0,data:{}});edges.push({id:`e_${end}`,source:n.id,target:end})});
  result.push({id:name,name,nodes,edges,warnings,imported:true});
 } return result;
}
export function validateGraph(nodes,edges){const m=new Map(),out=new Map(),issues=[];(nodes||[]).forEach(n=>{if(!n||!NODE_TYPES.has(n.type))issues.push({severity:'error',code:'type'});else if(m.has(n.id))issues.push({severity:'error',code:'dup_id'});else m.set(n.id,n)});(edges||[]).forEach(e=>{if(e?.broken||!m.has(e?.source)||!m.has(e?.target)){issues.push({severity:'error',code:'broken_edge'});return}(out.get(e.source)||out.set(e.source,[]).get(e.source)).push(e.target)});
 const color=new Map();function visit(v){color.set(v,1);for(const w of out.get(v)||[]){if(color.get(w)===1)issues.push({severity:'warning',code:'imported_cycle',msg:`Игровой цикл: ${v} → ${w}`});else if(color.get(w)!==2)visit(w)}color.set(v,2)}for(const id of m.keys())if(!color.get(id))visit(id);return issues}
export function migrateProject(p){if(!p||typeof p!=='object'||Array.isArray(p))throw Error('Проект должен быть объектом');const q={version:4,projectName:'new_dialogue',series:'soc',nodes:[],edges:[],characters:[],languages:[],scripts:[],dialogues:[],ltxFiles:{},textures:[],itemCategories:{},settings:{importEncoding:'auto',exportEncoding:'windows-1251'},...p};for(const k of ['nodes','edges','characters','languages','scripts','dialogues','textures'])if(!Array.isArray(q[k]))q[k]=[];q.settings={importEncoding:'auto',exportEncoding:'windows-1251',...(q.settings||{})};q.languages=q.languages.filter(x=>x&&typeof x==='object').map(x=>({...x,entries:Array.isArray(x.entries)?x.entries:[]}));q.nodes=q.nodes.filter(Boolean).map(n=>{let type=n.type;if(type==='NPC_REPLY'||type==='PLAYER_REPLY')type='PHRASE';let d={role:type==='PHRASE'?(n.type==='NPC_REPLY'?'npc':n.type==='PLAYER_REPLY'?'player':(n.data?.role||'unknown')):'unknown',roleSource:n.data?.roleSource||'migration',phraseId:n.data?.phraseId||n.id,text_id:'',use_id:false,text:'',voice_id:'',expression:'',action_type:'',params:'',metadata:{},...(n.data||{})};delete d.speaker;return {...n,type,data:d}});return q}
export function decodeGameBytes(buf,encoding='auto'){const a=new Uint8Array(buf), ascii=new TextDecoder('latin1').decode(a.slice(0,300));let enc=encoding;if(a[0]===0xef&&a[1]===0xbb&&a[2]===0xbf)enc='utf-8';else if(a[0]===0xff&&a[1]===0xfe)enc='utf-16le';else {const x=ascii.match(/<\?xml[^>]*encoding\s*=\s*["']([^"']+)/i);if(x)enc=x[1].toLowerCase();else if(enc==='auto'){try{return new TextDecoder('utf-8',{fatal:true}).decode(a)}catch{enc='windows-1251'}}}try{return new TextDecoder(enc==='cp1251'?'windows-1251':enc).decode(a)}catch(e){if(enc==='windows-1251')throw Error('Браузер не поддерживает TextDecoder("windows-1251"); выберите UTF-8 или обновите браузер');throw e}}
export function cp1251Encode(s){const dec=new TextDecoder('windows-1251'),rev=new Map;for(let i=0;i<256;i++)rev.set(dec.decode(Uint8Array.of(i)),i);return Uint8Array.from(Array.from(String(s),c=>c.charCodeAt(0)<128?c.charCodeAt(0):rev.has(c)?rev.get(c):63))}
