import json,pathlib,re
p=pathlib.Path('releases/age64-save-backup-2026-10-06/save_51.es3'); t=p.read_text(encoding='utf-8-sig'); dec=json.JSONDecoder()
for m in re.finditer(r'"[^"\n]*JSON"\s*:\s*',t):
 try: ns=json.loads(dec.raw_decode(t,m.end())[0]).get('saveItems',[])
 except: continue
 by={n.get('uuid'):n for n in ns}
 for n in ns:
  if n.get('identifier')!='be_liunian_ager': continue
  selected=[n]+[by[u] for u in n.get('childItems',[]) if u in by]
  out=[]
  for c in selected:
   row={k:c.get(k) for k in ('identifier','uuid','uniqueId','unitValue','itemShape','itemTypes','childItems','childItemInventoryNode')}
   row['enabledTags']=[k for k,v in zip(c.get('_keys',[]),c.get('_values',[])) if v.get('valueEnabled')]
   out.append(row)
  pathlib.Path('BrewingExpansion/tests/LiunianInventoryCheck/local-save-fixture.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
  print('Exported compact local-save fixture:',len(out),'items')
