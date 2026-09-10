"""Assign explicit sound designs and code references to the project's audio catalog."""
import hashlib
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'KLTN/Assets/Audio'
S = 'KLTN/Assets/Scripts/'
N = 'KLTN/Assets/_Project/Scripts/Networking/'
GROUPS = {
    'stalker': (S+'AI/Stalker/Networking/StalkerFusionRuntime.cs', 'GetReplicatedPresentationState: compare SemanticState, AttackEpisodeId, AttackPhase, AttackOutcome; suppress initial snapshot'),
    'player': (N+'Player/NetworkPlayerMovement.cs', 'Rendered movement / grounded foot contacts; health and revive from NetworkPlayerLifeState.StateChanged'),
    'door': (N+'Interaction/NetworkSlidingDoor.cs', 'StateChanged: compare previous/current state, suppress initial state and duplicate host notifications'),
    'energy_core': (N+'Interaction/NetworkPickupItem.cs', 'StateChanged: compare NetworkItemState and Holder; no pickup cue on initial snapshot'),
    'sector_box_power_hub': (N+'Interaction/NetworkSectorBox.cs', 'ObjectiveStateChanged: compare deposited count / completion; required count is configurable, default 2'),
    'power_puzzle': (N+'Interaction/NetworkPowerPuzzle.cs', 'StateChanged / legacy PowerPuzzleController StepAdvanced, PuzzleFailed, PuzzleCompleted; play only on transitions'),
    'security_terminal': (S+'Security/SecurityTerminalDownload.cs', 'DownloadStarted / DownloadPaused / DownloadResumed / DownloadCompleted; loop only while IsDownloading'),
    'locker_hiding': (S+'Hiding/PlayerHidingController.cs', 'EnterHiding / ExitHiding presentation; no AudioClip hook exists yet'),
    'noise_maker': (S+'Inventory/NoiseMakerBeacon.cs', 'BeaconRoutine: delay 1 s; pulses at 1, 2.5, 4, 5.5 s; destruction at 6 s'),
    'first_aid': (N+'Player/NetworkPlayerLifeState.cs', 'IsReviveInProgress / ReviveProgress01 / StateChanged; cancel loop when revive interrupted'),
    'map_ambience': (S+'MatchFlow/MatchFlowController.cs', 'Proposed room-local ambience; no ambience controller exists'),
    'horror_ambience': (S+'AI/Stalker/StalkerController.cs', 'Proposed ambience layer; never emit AI RuntimeNoise solely because an ambience WAV is played'),
    'escape_endgame': (N+'Match/NetworkMatchState.cs', 'StateChanged / PhaseOrdinal / EndOrdinal; EscapeRemainingSeconds for ticks; Win/Lose for result'),
    'ui': (S+'UI/HUD/GameplayHUDManager.cs', 'Local UI feedback only; wire actual button/selection/objective changes, not every HUD refresh'),
}

# Explicit profile and duration, in seconds. Loops below include 0.2 s overlap.
DESIGNS = '''
idle_breathing_growl_loop growl 8.2
footstep_cham step .48
footstep_chay_chase step .32
detect_cue stinger .65
chase_start stinger 1.1
chase_loop_vocal_loop growl 6.2
attack_swing whoosh .75
attack_hit flesh .32
miss_attack whoosh .38
search_vocal growl 1.6
recover_stun growl .8
door_hit metal .65
door_break debris 1.7
door_jammer_break debris .9
metal_footsteps step .28
concrete_footsteps concrete .24
sprint_footsteps step .22
breathing_loop breath 8.2
low_stamina_breathing_loop breath 6.2
damage_grunt breath .45
death_downed breath 1.4
sliding_open slide .85
sliding_close slide .85
locked_attempt error .25
unlock confirm .3
door_machinery_loop_loop hum 4.2
door_jammer_deploy switch .4
jammer_armed confirm .25
ambient_hum_loop hum 8.2
pickup confirm .3
drop metal .5
deposit_into_sector_box metal .55
core_accepted confirm .55
idle_machinery_loop hum 8.2
core_1_3_2_3 confirm .65
fully_powered confirm 2.4
terminal_interaction click .09
breaker_toggle switch .18
rotary_switch switch .12
correct_input confirm .3
wrong_input error .4
puzzle_complete confirm 1.4
electrical_sparks spark .6
terminal_boot confirm .9
access_granted confirm .5
access_denied error .35
locker_open hinge .6
locker_close metal .45
enter_hiding hinge .55
player_breathing_inside_loop breath 8.2
monster_outside_locker step .45
monster_hits_locker metal .65
activate click .18
beacon_loop_loop alarm 3.2
throw_drop metal .35
end_deactivate down .2
use_kit cloth .8
heal_confirm confirm .5
facility_room_tone_loop fan 12.2
hvac_loop fan 12.2
fluorescent_buzz_loop hum 8.2
server_room_loop fan 12.2
electrical_room_loop hum 8.2
generator_loop hum 8.2
distant_metal_bang metal 1.6
pipe_creak creak 1.5
steam_hiss steam 1.8
electrical_flicker spark .4
alarm_ambience_loop alarm 4.2
distant_monster_sound growl 2.6
random_impact metal 1.3
tension_drone_loop drone 12.2
low_frequency_rumble_loop rumble 12.2
escape_unlocked confirm .8
countdown_start stinger .7
bay_door_opening slide 3.5
mission_success confirm 2.2
hover click .045
click click .065
confirm confirm .18
error error .22
inventory_pickup click .16
objective_update confirm .55
lobby_ready confirm .35
match_start stinger 1.2
'''


def main():
    items = json.loads((OUT/'audio_manifest.json').read_text(encoding='utf-8'))
    designs = {r[0]: (r[1], float(r[2])) for r in (line.split() for line in DESIGNS.splitlines() if line.strip())}
    for item in items:
        stem = Path(item['file']).stem
        if stem not in designs:
            continue
        item['profile'], item['duration'] = designs[stem]
        folder = item['file'].split('/')[0]
        item['source'], item['trigger'] = GROUPS[folder]
        item['status'] = 'needs_audio_binding'
        item['peak_dbfs'] = -18 if item['loop'] else -9 if folder == 'ui' else -6
        item['breath_period'] = 1.5 if 'chase' in stem or 'stamina' in stem else 4
        item['frequency'] = {'hum': 60, 'fan': 92, 'drone': 44, 'rumble': 32, 'slide': 85}.get(item['profile'], 58)
        if folder == 'stalker':
            item['frequency'] = 42
        if 'footstep' in stem:
            item['ring'] = .3 if folder == 'player' else .65
        if folder in ('map_ambience', 'horror_ambience'):
            item['status'] = 'ambience_design'
        if stem in ('door_hit','door_break','door_jammer_break','door_jammer_deploy','jammer_armed','monster_hits_locker','recover_stun'):
            item['status'] = 'reserved_no_matching_mechanic'
        if stem == 'recover_stun':
            item['trigger'] = 'Legacy filename: RECOVER is post-attack recovery, not a stun state. Prefer recover_exhale.wav.'
        if stem == 'core_1_3_2_3':
            item['trigger'] = 'Legacy 3-core name; offline requiredCoreCount defaults to 3, network SectorBox defaults to 2. Prefer core_insert_partial.wav.'
        if stem == 'beacon_loop_loop':
            item['status'] = 'legacy_not_for_beacon_routine'
            item['trigger'] = 'Generic alarm only. Prefer beacon_pulse.wav for each accepted pulse, or beacon_sequence_6s.wav once at spawn.'
        if stem in ('access_denied', 'access_granted', 'terminal_boot'):
            item['trigger'] += '; download component has no separate authentication/grant/deny event; use download-specific clips'
        if stem == 'bay_door_opening':
            item['status'] = 'reserved_no_matching_mechanic'
            item['trigger'] = 'No timed blast-door animation found; do not use this 3.5 s clip for the 0.85 s sliding-door prefab.'

    def add(folder, name, profile, seconds, source=None, trigger=None, loop=False, peak=-9):
        filename = f'{folder}/{name}.wav'
        if any(x['file'] == filename for x in items):
            return
        default_source, default_trigger = GROUPS[folder]
        items.append(dict(group=next(x['group'] for x in items if x['file'].startswith(folder+'/')),
            name=name, keywords='code-aligned procedural effect', priority='P0', loop=loop,
            duration=seconds + (.2 if loop else 0), file=filename, profile=profile,
            source=source or default_source, trigger=trigger or default_trigger,
            status='needs_audio_binding', peak_dbfs=peak))

    add('noise_maker','beacon_pulse','beacon',.42, trigger='One cue per pulse: 1, 2.5, 4, 5.5 seconds after spawn; no infinite loop')
    add('noise_maker','beacon_sequence_6s','beacon_sequence',6, trigger='Alternative to per-pulse playback: once at spawn, includes 1 s silence, four pulses and final tail. Do not play both.')
    for name, profile, duration, loop in [('download_start','click',.2,False),('download_progress_loop','data',2,True),('download_pause','down',.22,False),('download_resume','confirm',.22,False),('download_complete','confirm',.75,False)]:
        add('security_terminal',name,profile,duration,loop=loop)
    add('player','flashlight_on','switch',.1, N+'Player/NetworkPlayerFlashlight.cs','IsOn false -> true, suppress first snapshot')
    add('player','flashlight_off','switch',.08, N+'Player/NetworkPlayerFlashlight.cs','IsOn true -> false, suppress first snapshot')
    add('player','downed_heartbeat_loop','heartbeat',2, N+'Player/NetworkPlayerLifeState.cs','Local downed-player feedback only; stop on revive, elimination or escape',True,-18)
    add('player','eliminated','down',1.3, N+'Player/NetworkPlayerLifeState.cs','Status -> Eliminated once, distinct from Downed')
    add('player','help_ping','confirm',.35,N+'Interaction/NetworkPlayerInteractor.cs','RpcRequestHelpPing acceptance; cooldown 3 s; needs a replicated presentation event for teammates')
    add('first_aid','revive_progress_loop','cloth',1.5,loop=True,peak=-18)
    add('first_aid','revive_cancel','down',.22)
    add('first_aid','revive_complete','confirm',.65)
    add('stalker','recover_exhale','growl',.8,trigger='Attack recovery entry after resolved Hit/Miss; post-attack breath, not pain/stun')
    add('energy_core','carry_rattle','metal',.24,S+'EnergyCore/PlayerEnergyCoreCarrier.cs','Offline CarryNoiseEmitted defaults 2.5 s; network movement noise interval is 1.5 s. Audio cadence may follow actual movement.')
    add('sector_box_power_hub','core_insert_partial','confirm',.55,trigger='Deposited count increases but objective is incomplete; no hard-coded 1/3 or 2/3 assumption')
    add('escape_endgame','countdown_tick','tick',.09,trigger='Once per changed integer remaining second, never per render frame. Network escape timer defaults 45 s, legacy countdown 8 s.')
    add('escape_endgame','mission_failure','down',2,trigger='NetworkMatchState.Result -> Lose / MatchFlowController.MatchLost, once')
    add('escape_endgame','final_hunt_start','stinger',1.4,trigger='MatchFlowController.PhaseChanged -> FinalHunt; network phase mapping must be respected')
    # Independent contact variations avoid repeating the exact waveform every step.
    for folder, stem, profile, duration in [('player','metal_footstep','step',.28),('player','concrete_footstep','concrete',.24),('player','sprint_footstep','step',.22),('player','crouch_footstep','step',.24),('stalker','patrol_footstep','step',.48),('stalker','chase_footstep','step',.32)]:
        for i in range(1,5):
            add(folder,f'{stem}_{i:02}',profile,duration,trigger='Single grounded foot contact. Select variants without immediate repeat; cadence follows animation/movement, not AI noise TTL.',peak=-15 if 'crouch' in stem else -7)
    (OUT/'audio_manifest.json').write_text(json.dumps(items,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

    # Audit inventory of first-party runtime sources (not a claim of manual line-by-line review).
    sources = sorted([*ROOT.joinpath(S).rglob('*.cs'), *ROOT.joinpath(N).rglob('*.cs')])
    inventory = []
    for path in sources:
        raw = path.read_bytes()
        inventory.append(dict(file=path.relative_to(ROOT).as_posix(),lines=len(raw.splitlines()),sha256=hashlib.sha256(raw).hexdigest()))
    (OUT/'source_inventory.json').write_text(json.dumps(inventory,indent=2)+'\n',encoding='utf-8')
    rows=['# Audio theo code ECHO PROTOCOL', '',
        'Bộ WAV tổng hợp dùng thử, không phải bản thu thực tế. Đã đối chiếu các nhánh gameplay/network liên quan audio và thông số prefab; source_inventory.json ghi phạm vi quét mã nguồn. Chưa nghe thử trong Unity và chưa gắn AudioSource vào gameplay.', '',
        '## Các khác biệt đã xử lý', '',
        '- Cửa trượt: open/close 0.85 s theo NetworkSlidingDoor và PF_SciFiSlidingDoor. Nếu đổi animationDuration, điều chỉnh clip/pitch tương ứng.',
        '- Stalker: swing 0.75 s theo attackWindup; hit/miss phát khi kết quả attack được xác nhận. RECOVER 1 s là hồi sau đánh, không phải stun. Thêm recover_exhale 0.8 s.',
        '- Noise Maker: beacon_pulse 0.42 s hoặc beacon_sequence_6s gồm im lặng 1 s rồi 4 xung ở 1 / 2.5 / 4 / 5.5 s. Chọn một cách phát. Prefab hiện Instantiate ở host; muốn client nghe phải bổ sung replication sự kiện/presentation.',
        '- Terminal: tải mặc định 12 s, có pause/resume nên dùng progress loop và dừng theo trạng thái; không đóng cứng âm dài 12 s.',
        '- Hồi sinh: network mặc định 2.5 s; offline 6 s và First Aid có nhánh giảm 50%. Dùng loop dừng theo IsReviveInProgress/IsReviving.',
        '- Escape: network mặc định 45 s, legacy countdown 8 s. Dùng tick rời theo giây còn lại, không phát một bản đếm cố định.',
        '- Sector Box network mặc định 2 core; objective offline mặc định 3. Thêm core_insert_partial thay cho tên cố định 1/3, 2/3.',
        '- Thêm đèn pin, help ping, eliminated, thất bại trận, carry rattle và 4 biến thể cho mỗi nhóm footstep.', '',
        '## Tích hợp', '',
        'Gameplay hiện chưa có AudioSource/AudioClip playback. RuntimeNoiseCatalog là tín hiệu nghe của AI, không phải hệ thống phát âm thanh. Thời gian tồn tại noise và bán kính nghe không phải độ dài clip hoặc thông số mixer.', '',
        'Các trigger dưới đây là điểm tích hợp đề xuất, chưa được nối tự động. Với Fusion, phát trên presentation ở từng peer từ trạng thái đã replicate; lưu snapshot cũ, bỏ snapshot đầu và chống phát trùng host/OnChangedRender. Không phát one-shot liên tục trong FixedUpdateNetwork/Render. Clip không tự đồng bộ qua mạng.', '',
        'WAV mono 44.1 kHz PCM16. UI và hơi thở/heartbeat người chơi local: 2D; nguồn âm trong thế giới: 3D. Loop đã crossfade 0.2 s; bật AudioSource.loop theo cột Loop và fade 50–150 ms khi bật/tắt. Mức peak theo manifest, cần cân chỉnh volume theo cảnh thực tế.', '',
        'Những file đánh dấu reserved chưa có cơ chế tương ứng (phá cửa/jammer/stun/đập locker/blast-door). Giữ để dự phòng, không gắn vào sự kiện không liên quan. Âm locomotion không tự phân biệt mặt sàn: cần chọn surface ở gameplay.', '',
        'Tạo lại: `python scripts/align_audio_catalog.py`, rồi `python scripts/generate_audio.py --replace`. Giữ nguyên tên file WAV cũ và .meta hiện có để giữ GUID. Mở preview.html để nghe so sánh.', '',
        '| File | Giây | Loop | Trạng thái | Code / điểm phát đề xuất |', '| --- | ---: | --- | --- | --- |']
    preview=['<!doctype html><html lang="vi"><meta charset="utf-8"><title>ECHO Audio Preview</title><style>body{background:#111820;color:#dae6ec;font:15px system-ui;margin:32px}table{border-collapse:collapse;width:100%}td,th{padding:10px;border-bottom:1px solid #35404c;text-align:left}small{display:block;max-width:650px;color:#9fafbd}audio{height:32px}input{padding:10px;width:60%;margin:15px 0}</style><h1>ECHO PROTOCOL — Audio theo code</h1><p>Âm tổng hợp dùng thử. Chưa tích hợp playback trong Unity. Tìm theo tên, nhóm hoặc sự kiện.</p><input id="filter" placeholder="Tìm âm thanh..."><table><tr><th>File / sự kiện</th><th>Thời lượng</th><th>Nghe</th></tr>']
    for item in items:
        duration=round(item['duration']-(.2 if item['loop'] else 0),3)
        source_link='../../../'+item['source']
        rows.append(f'| [{item["file"]}]({item["file"]}) | {duration} | {"yes" if item["loop"] else "no"} | {item["status"]} | [{Path(item["source"]).name}]({source_link}): {item["trigger"]} |')
        preview.append(f'<tr class="clip"><td>{html.escape(item["file"])}<small>{html.escape(item["trigger"])} — {item["status"]}</small></td><td>{duration}s {"loop" if item["loop"] else ""}</td><td><audio controls preload="none" src="{html.escape(item["file"])}"></audio></td></tr>')
    preview.append('</table><script>document.querySelector("#filter").oninput=e=>document.querySelectorAll(".clip").forEach(r=>r.hidden=!r.textContent.toLowerCase().includes(e.target.value.toLowerCase()));document.addEventListener("play",e=>document.querySelectorAll("audio").forEach(a=>{if(a!==e.target)a.pause()}),true)</script></html>')
    (OUT/'README.md').write_text('\n'.join(rows)+'\n',encoding='utf-8')
    (OUT/'preview.html').write_text('\n'.join(preview),encoding='utf-8')
    print(f'{len(items)} clips mapped; {len(inventory)} runtime source files inventoried')


if __name__ == '__main__':
    main()
