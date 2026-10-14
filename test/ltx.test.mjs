import test from 'node:test';
import assert from 'node:assert/strict';
import { validateLtxRefs, findDuplicateSections, findInheritanceCycles, renameSection, replaceFieldValues, itemFieldProfile, ITEM_FIELD_PROFILES, classifyItem, COMMUNITY_ICONS, parseGameRelations, parseRatingScales, parseSquadDescr, parseSecrets, parseSpawnSections, parseMonsters, parseActor, parseSurgeManager } from '../src/lib/ltx.js';

test('validateLtxRefs flags missing ammo_class but not known parent', () => {
  const text = '[wpn_ak74]:wpn_base\nammo_class = ammo_9x18_fmj, ammo_missing\n';
  const known = new Set(['wpn_base', 'ammo_9x18_fmj']);
  const issues = validateLtxRefs(text, known);
  assert.ok(issues.some((i) => i.code === 'ltx_ref_value' && i.msg.indexOf('ammo_missing') !== -1));
  assert.equal(issues.filter((i) => i.code === 'ltx_ref_parent').length, 0);
});

test('validateLtxRefs flags missing parent', () => {
  const issues = validateLtxRefs('[child]:ghost_parent\n', new Set());
  assert.ok(issues.some((i) => i.code === 'ltx_ref_parent' && i.msg.indexOf('ghost_parent') !== -1));
});

test('findDuplicateSections lists multi-file sections', () => {
  const map = { shared: ['a.ltx', 'b.ltx'], single: ['a.ltx'] };
  const dups = findDuplicateSections(map);
  assert.equal(dups.length, 1);
  assert.equal(dups[0].name, 'shared');
});

test('findInheritanceCycles detects a→b→a', () => {
  const issues = findInheritanceCycles('[a]:b\n[b]:a\n');
  assert.ok(issues.length >= 1);
  assert.equal(issues[0].code, 'ltx_inherit_cycle');
});

test('renameSection keeps parents', () => {
  assert.equal(renameSection('[old]:base\nx = 1\n', 'old', 'new_sec'), '[new_sec]:base\nx = 1\n');
});

test('replaceFieldValues mass-replaces value substring', () => {
  assert.equal(replaceFieldValues('[a]\ncost = 100\n[b]\ncost = 200\n', 'cost', '00', 'X'), '[a]\ncost = 1X\n[b]\ncost = 2X\n');
});

test('itemFieldProfile maps category to fields', () => {
  assert.ok(itemFieldProfile({ name: 'wpn_ak74' }).indexOf('ammo_class') !== -1);
  assert.ok(itemFieldProfile({ name: 'wpn_ak74' }).indexOf('hit_power') !== -1);
  assert.ok(itemFieldProfile({ name: 'wpn_ak74' }).indexOf('snd_shoot') !== -1);
  assert.ok(itemFieldProfile({ name: 'wpn_ak74' }).indexOf('animation_slot') !== -1);
  assert.ok(itemFieldProfile({ name: 'ammo_9x18_fmj' }).indexOf('k_dist') !== -1);
  assert.ok(itemFieldProfile({ name: 'novice_outfit' }).indexOf('burn_protection') !== -1);
  assert.ok(itemFieldProfile({ name: 'novice_outfit' }).indexOf('actor_visual') !== -1);
  assert.ok(itemFieldProfile({ name: 'trade_barman' }).indexOf('buy_condition') !== -1);
  assert.equal(itemFieldProfile({ name: 'misc_item' }).indexOf('ammo_class'), -1); // только общие поля
  assert.ok(Object.keys(ITEM_FIELD_PROFILES).length >= 6);
  assert.ok(itemFieldProfile({ name: 'bread', entries: [{ key: 'class', value: 'II_FOOD' }] }).indexOf('eat_satiety') !== -1);
  assert.ok(itemFieldProfile({ name: 'jup_a9_conservation_info', entries: [{ key: 'quest_item', value: 'true' }] }).indexOf('quest_item') !== -1);
});

test('classifyItem uses class field and fallback', () => {
  const sec = (name, cls) => ({ name: name, parents: [], entries: cls ? [{ key: 'class', value: cls }] : [] });
  assert.equal(classifyItem(sec('wpn_ak74', 'WP_AK74')), 'Оружие');
  assert.equal(classifyItem(sec('bread', 'II_FOOD')), 'Юзабельные');
  assert.equal(classifyItem(sec('ammo_9x18_fmj', 'AMMO')), 'Боеприпасы');
  assert.equal(classifyItem(sec('af_medusa', 'ARTEFACT')), 'Артефакты');
  assert.equal(classifyItem(sec('wpn_addon_scope', 'WP_SCOPE')), 'Апгрейды');
  assert.equal(classifyItem(sec('zone_radio', 'Z_RADIO')), 'Аномалии');
  assert.equal(classifyItem(sec('device_pda', 'D_PDA')), 'Детекторы');
  assert.equal(classifyItem(sec('novice_outfit', 'E_STLK')), 'Броня');
  assert.equal(classifyItem(sec('wpn_binoc', 'WP_BINOC')), 'Детекторы');
  assert.equal(classifyItem(sec('wpn_abakan', '')), 'Оружие');
});

test('classifyItem separates immunities and base sections', () => {
  const sec = (name, cls, parents, entries) => ({ name, parents: parents || [], entries: entries || (cls ? [{ key: 'class', value: cls }] : []) });
  assert.equal(classifyItem(sec('sect_novice_outfit_immunities', '', [], [{ key: 'burn_immunity', value: '0.04' }])), 'Иммунитеты');
  assert.equal(classifyItem(sec('outfit_base', '', [], [{ key: 'additional_inventory_weight', value: '0' }])), 'Базовые');
  assert.equal(classifyItem(sec('identity_immunities', '', [], [])), 'Базовые');
  assert.equal(classifyItem(sec('ammo_9x18_fmj', 'AMMO')), 'Боеприпасы');
  assert.equal(classifyItem(sec('wpn_ak74', 'WP_AK74')), 'Оружие');
});

test('COMMUNITY_ICONS covers real communities', () => {
  ['actor', 'actor_dolg', 'actor_freedom', 'stalker', 'monolith', 'military', 'killer', 'ecolog', 'dolg', 'freedom', 'bandit', 'zombied', 'stranger', 'trader', 'arena_enemy'].forEach((c) => assert.ok(COMMUNITY_ICONS[c], 'missing ' + c));
});

test('parseGameRelations reads communities and relation matrix', () => {
  const text = '[communities]\ncommunities = actor, 0, stalker, 5, bandit, 12\n[communities_relations]\nactor = 0, -5000\nstalker = -5000, 5000\n';
  const r = parseGameRelations(text);
  assert.deepEqual(r.communities, ['actor', 'stalker', 'bandit']);
  assert.equal(r.relations.actor.length, 2);
  assert.equal(r.relations.stalker[1], '5000');
});

test('parseRatingScales reads rating/reputation scales', () => {
  const text = '[game_relations]\nrating = novice, 300, experienced, 600\nreputation = neutral, 50, good, 150\ngoodwill_names = enemy, -400, friendly\n';
  const r = parseRatingScales(text);
  assert.equal(r.rating.length, 2);
  assert.equal(r.rating[0].name, 'novice');
  assert.equal(r.rating[0].value, '300');
  assert.equal(r.reputation[0].name, 'neutral');
});

test('parseSquadDescr reads squads', () => {
  const text = '[zat_b38_bloodsucker_1_lair]:online_offline_group\nfaction = monster_predatory_night\nnpc = zat_b38_bloodsucker_1\ntarget_smart = zat_b38\nstory_id = zat_b38_bloodsucker_1_lair\n';
  const s = parseSquadDescr(text);
  assert.equal(s.length, 1);
  assert.equal(s[0].faction, 'monster_predatory_night');
  assert.equal(s[0].npc, 'zat_b38_bloodsucker_1');
});

test('itemFieldProfile includes common fields', () => {
  const prof = itemFieldProfile({ name: 'wpn_ak74' });
  assert.ok(prof.indexOf('$spawn') !== -1);
  assert.ok(prof.indexOf('inv_grid_x') !== -1);
  assert.ok(prof.indexOf('ammo_class') !== -1);
});

test('classifyItem detects quest_item flag', () => {
  assert.equal(classifyItem({ name: 'jup_a9_conservation_info', parents: ['device_pda'], entries: [{ key: 'quest_item', value: 'true' }] }), 'Квестовое');
  assert.equal(classifyItem({ name: 'detector_simple', entries: [{ key: 'quest_item', value: 'false' }] }), 'Детекторы');
});

test('parseSecrets reads lists and treasures', () => {
  const text = '[list]:list_zaton, list_pripyat\n[pri_b36_tower_treasure]\nmedkit_army = 3, 1\nammo_gauss = 1, 1\n';
  const r = parseSecrets(text);
  assert.deepEqual(r.lists, ['list_zaton', 'list_pripyat']);
  assert.equal(r.treasures.length, 1);
  assert.equal(r.treasures[0].items[0].item, 'medkit_army');
});

test('parseSpawnSections reads spawn sections', () => {
  const text = '[zat_b14_stalker_1]:stalker\n$spawn = "respawn\\zat_b14_stalker_1"\ncharacter_profile = zat_b14_stalker_1\nspec_rank = regular\ncommunity = stalker\nstory_id = zat_b14_stalker_1\n';
  const r = parseSpawnSections(text);
  assert.equal(r.length, 1);
  assert.equal(r[0].id, 'zat_b14_stalker_1');
  assert.equal(r[0].base, 'stalker');
  assert.equal(r[0].character_profile, 'zat_b14_stalker_1');
  assert.equal(r[0].spec_rank, 'regular');
});

test('parseMonsters reads SM_ class sections', () => {
  const text = '[m_bloodsucker_e]:monster_base\nclass = SM_BLOOD\nvisual = monsters\\krovosos\\krovosos\nscript_binding = bind_monster.bind\n[some_item]\nclass = II_FOOD\n';
  const r = parseMonsters(text);
  assert.equal(r.length, 1);
  assert.equal(r[0].id, 'm_bloodsucker_e');
  assert.equal(r[0].class, 'SM_BLOOD');
  assert.equal(r[0].script_binding, 'bind_monster.bind');
});

test('parseActor reads actor section', () => {
  const text = '[actor]:common_ph\nclass = S_ACTOR\nmax_item_mass = 35\njump_speed = 6.\nmoney = 15000\n';
  const r = parseActor(text);
  assert.equal(r.class, 'S_ACTOR');
  assert.equal(r.max_item_mass, '35');
  assert.equal(r.money, '15000');
});

test('parseSurgeManager reads settings and list', () => {
  const text = '[settings]\ncondlist = {-pri_a16} false, true\nsurvive = {+actor_marked} true, false\n[list]\nzat_surge_hide_a2\nzat_surge_hide_b42\n';
  const r = parseSurgeManager(text);
  assert.equal(r.settings.condlist.indexOf('pri_a16') !== -1, true);
  assert.deepEqual(r.list, ['zat_surge_hide_a2', 'zat_surge_hide_b42']);
});
