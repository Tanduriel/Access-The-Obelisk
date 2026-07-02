using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace AccessTheObelisk
{
    /// <summary>
    /// Mod-local localization strings.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>();
        private static readonly Dictionary<string, Dictionary<string, string>> Languages = new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<string, System.DateTime> LoadedFiles = new Dictionary<string, System.DateTime>();
        private static Dictionary<string, string> _active = En;
        private static string _activeLanguage = "en";
        private static System.DateTime _lastReloadCheckUtc;

        /// <summary>
        /// Initializes localization dictionaries.
        /// </summary>
        public static void Initialize()
        {
            En.Clear();
            En["mod_loaded"] = "AccessTheObelisk v" + Main.PluginVersion + ".";
            En["patches_failed"] = "Accessibility mod warning: some game hooks failed to load.";
            En["update_available"] = "A new AccessTheObelisk version is available: {0}. Installed version: {1}.";
            En["update_current"] = "The latest AccessTheObelisk version is installed: {0}.";
            En["debug_enabled"] = "AccessTheObelisk debug logging enabled.";
            En["debug_disabled"] = "AccessTheObelisk debug logging disabled.";
            En["tutorial_popup"] = "Tutorial. {0}";
            En["tutorial_continue"] = "Continue.";
            En["main_menu_loaded"] = "Main menu.";
            En["game_mode_screen"] = "Play. Choose game mode.";
            En["save_slot_screen"] = "Choose save slot.";
            En["profile_screen"] = "Profile selection. Up and Down choose a profile or action. Enter selects the focused item.";
            En["dlc_screen"] = "DLC information. Up and Down choose a DLC. Enter opens its details.";
            En["dlc_item_owned"] = "{0}. Owned.";
            En["dlc_item_not_owned"] = "{0}. Not owned.";
            En["dlc_link"] = "Open in Steam store";
            En["dlc_close"] = "Close";
            En["profile_slot"] = "Profile slot {0}";
            En["profile_slot_named"] = "Profile slot {0}: {1}";
            En["profile_current_slot"] = "Current {0}";
            En["profile_create"] = "Create profile";
            En["profile_delete"] = "Delete current profile";
            En["pdx_input_focused"] = "{0}. Editing. Type text, then use Up or Down to leave the field.";
            En["pdx_dropdown_hint"] = "{0}. Use Left and Right to change.";
            En["pdx_text_field"] = "Text field";
            En["pdx_empty"] = "empty";
            En["pdx_entered"] = "entered";
            En["pdx_email"] = "Paradox account email: {0}";
            En["pdx_password"] = "Paradox account password: {0}";
            En["pdx_create_email"] = "New Paradox account email: {0}";
            En["pdx_create_password"] = "New Paradox account password: {0}";
            En["pdx_region"] = "Country or region: {0}";
            En["pdx_birth_day"] = "Birth day: {0}";
            En["pdx_birth_month"] = "Birth month: {0}";
            En["pdx_birth_year"] = "Birth year: {0}";
            En["pdx_marketing"] = "Marketing emails: {0}";
            En["pdx_logged_in"] = "Logged in as {0}";
            En["pdx_document_screen"] = "Paradox Interactive agreement. Up and Down read the agreement. Left and Right move by larger steps. The Close button is at the end. Press Enter or Space on Close.";
            En["pdx_document_close_focus"] = "Button: {0}. Press Enter to close.";
            En["pdx_document_not_button"] = "This is agreement text. Move to Close, then press Enter.";
            En["pdx_document_empty"] = "No agreement text available.";
            En["lobby_screen_region"] = "Multiplayer lobby. Choose a region and connect. Paradox account is only required for Crossplay; Steam multiplayer can connect without it.";
            En["lobby_screen_join"] = "Multiplayer lobby. Join a room or create one.";
            En["lobby_screen_create"] = "Create multiplayer room.";
            En["lobby_screen_room"] = "Multiplayer room.";
            En["lobby_screen_unknown"] = "Multiplayer lobby.";
            En["lobby_item"] = "{0}";
            En["lobby_status"] = "Status: {0}";
            En["lobby_region"] = "Region: {0}";
            En["lobby_crossplay"] = "Crossplay: {0}";
            En["lobby_crossplay_locked"] = "Crossplay: {0}. Locked until you log in to a Paradox account.";
            En["lobby_create_name"] = "Room name: {0}";
            En["lobby_create_players"] = "Maximum players: {0}";
            En["lobby_password_toggle"] = "Password protected room: {0}";
            En["lobby_create_password"] = "Room password: {0}";
            En["lobby_lfm_toggle"] = "Looking for players listing: {0}";
            En["lobby_room_item"] = "Room {0}: {1}";
            En["lobby_password_required"] = "Password required.";
            En["lobby_player_slot"] = "Player slot {0}: {1}";
            En["lobby_kick_player"] = "Kick player in slot {0}";
            En["lobby_launch_game"] = "Launch game";
            En["lobby_invite_steam"] = "Invite Steam friends";
            En["lobby_create_button"] = "Create room";
            En["lobby_join_button"] = "Join rooms";
            En["lobby_ready_button"] = "Ready";
            En["lobby_all_unready_button"] = "Clear ready status";
            En["lobby_input_submit_blocked"] = "Still editing. Use Up or Down to leave the text field before creating the room.";
            En["lobby_disconnect_region"] = "Disconnect from region";
            En["main_menu_join_multiplayer"] = "Join multiplayer game";
            En["give_screen"] = "Give resources.";
            En["give_resource_gold"] = "Resource: gold. Left or Right changes resource.";
            En["give_resource_shards"] = "Resource: shards. Left or Right changes resource.";
            En["give_target"] = "Recipient: {0}. Left or Right changes recipient.";
            En["give_quantity"] = "Quantity: {0}. Available: {1}. Left or Right changes by 1, Shift by 20, Control by 100.";
            En["give_send"] = "Give resources";
            En["give_close"] = "Close";
            En["give_closed"] = "Resource transfer closed.";
            En["give_quantity_zero"] = "Choose a quantity greater than zero.";
            En["give_sent"] = "Giving {0} to {1}.";
            En["multiplayer_chat_message"] = "Chat: {0}";
            En["multiplayer_room_joined"] = "Joined multiplayer room: {0}.";
            En["multiplayer_player_joined"] = "{0} joined the room.";
            En["multiplayer_player_left"] = "{0} left the room.";
            En["chat_input_focused"] = "Chat message. Type your message and press Enter to send. Press an arrow key to leave the chat field.";
            En["chat_input_left"] = "Chat field closed.";
            En["chat_input_unavailable"] = "Chat input is not available.";
            En["chat_history_empty"] = "Chat history is empty.";
            En["chat_history_item"] = "Chat history: {2}";
            En["players_popup_screen"] = "Players.";
            En["players_popup_unavailable"] = "Players list is not available.";
            En["players_popup_empty"] = "No players available.";
            En["players_popup_player"] = "Player slot {0}: {1}";
            En["players_popup_host"] = "Host.";
            En["players_popup_platform"] = "Platform: {0}";
            En["players_popup_ping"] = "Ping: {0} ms";
            En["players_popup_muted_state"] = "Muted.";
            En["players_popup_unmuted_state"] = "Not muted.";
            En["players_popup_mute"] = "Mute {0}";
            En["players_popup_unmute"] = "Unmute {0}";
            En["players_popup_muted"] = "{0} muted.";
            En["players_popup_unmuted"] = "{0} unmuted.";
            En["conflict_screen"] = "Path conflict.";
            En["conflict_waiting"] = "Waiting for path conflict resolution.";
            En["conflict_item"] = "{2}";
            En["conflict_option"] = "Resolution option: {0}. Press Enter to choose.";
            En["conflict_option_lowest"] = "lowest card cost wins";
            En["conflict_option_middle"] = "closest to two wins";
            En["conflict_option_highest"] = "highest card cost wins";
            En["conflict_selected_option"] = "Selected {0}";
            En["conflict_hero"] = "{0}. Player: {1}.";
            En["conflict_winner"] = "Conflict result: {0}";
            En["conflict_no_item"] = "No conflict item available.";
            En["hero_selection_screen"] = "Hero selection.";
            En["hero_selection_screen_structured"] = "Hero selection.";
            En["hero_selection_controls"] = "Up and Down switch between party slots, heroes, and actions. Left and Right move within the current section. On party slots in multiplayer, Shift plus Left and Shift plus Right change the slot owner for the host. Control plus Left and Control plus Right switch hero detail buffers. Control plus Up and Control plus Down read hero details. Press Enter on a party slot to choose the target slot. Press Enter on a hero to assign that hero to the target slot. Press Control plus Enter on a hero or filled party slot to open that hero's perk tree.";
            En["pre_run_madness_screen"] = "Madness options.";
            En["pre_run_madness_controls"] = "Up and Down move through levels, corruptors, modifiers, and buttons. Left and Right change the selected madness level. Enter toggles or activates the focused item.";
            En["pre_run_sandbox_screen"] = "Sandbox options.";
            En["pre_run_sandbox_controls"] = "Up and Down move through sandbox settings. Left and Right change the focused value. Enter toggles checkboxes or activates buttons. Closing the window saves the settings through the game.";
            En["pre_run_options_unavailable"] = "Pre-run options are not available.";
            En["pre_run_madness_level"] = "Madness level {0}";
            En["pre_run_madness_value"] = "Madness level {0}.";
            En["pre_run_madness_value_details"] = "Madness level {0}. {1}";
            En["pre_run_madness_edge"] = "Madness level {0}. No further level available.";
            En["pre_run_corruptor"] = "Corruptor {0}";
            En["pre_run_toggle_value"] = "{0}. {1} {2}";
            En["pre_run_sandbox_value"] = "{0}: {1}.";
            En["pre_run_text_item"] = "{0}. {1}";
            En["pre_run_confirm"] = "Confirm";
            En["pre_run_close"] = "Close";
            En["pre_run_weekly_modifiers"] = "Weekly modifiers";
            En["pre_run_weekly_trait"] = "Weekly trait";
            En["pre_run_sandbox_enable"] = "Enable sandbox";
            En["pre_run_sandbox_disable"] = "Disable sandbox";
            En["pre_run_sandbox_reset"] = "Reset sandbox";
            En["pre_run_sandbox_four_heroes"] = "4 heroes";
            En["pre_run_sandbox_heroes"] = "{0} heroes";
            En["pre_run_sandbox_less_monsters"] = "{0} fewer monsters";
            En["sandbox_sbEnergy"] = "Starting energy";
            En["sandbox_sbSpeed"] = "Starting speed";
            En["sandbox_sbGold"] = "Additional gold";
            En["sandbox_sbShards"] = "Additional shards";
            En["sandbox_sbCraftCost"] = "Craft cost";
            En["sandbox_sbUpgradeCost"] = "Upgrade cost";
            En["sandbox_sbTransformCost"] = "Transform cost";
            En["sandbox_sbRemoveCost"] = "Remove-card cost";
            En["sandbox_sbEquipmentCost"] = "Equipment cost";
            En["sandbox_sbPetsCost"] = "Pet cost";
            En["sandbox_sbDivinationCost"] = "Divination cost";
            En["sandbox_sbCraftUnlocked"] = "Crafting unlocked";
            En["sandbox_sbCardCraftRarity"] = "All craft rarities";
            En["sandbox_sbCraftAvailable"] = "Unlimited available cards";
            En["sandbox_sbArmoryRerolls"] = "Free armory rerolls";
            En["sandbox_sbUnlimitedRerolls"] = "Unlimited rerolls";
            En["sandbox_sbMinimumDeckSize"] = "No minimum deck size";
            En["sandbox_sbEventRolls"] = "Always pass event rolls";
            En["sandbox_sbTotalHeroes"] = "Party size";
            En["sandbox_sbLessMonsters"] = "Fewer monsters";
            En["sandbox_sbMonstersHP"] = "Monster HP";
            En["sandbox_sbMonstersDamage"] = "Monster damage";
            En["sandbox_sbDoubleChampions"] = "Double champions";
            En["first_adventure_auto_start"] = "First adventure tutorial. The game selected the starting party automatically and is starting the intro.";
            En["save_slot_position"] = "Slot {0}";
            En["save_slot_delete"] = "{0}: {1}";
            En["hero_slot_position"] = "Hero slot {0}";
            En["hero_selection_party_focus"] = "Party slot {0}: {1}. Owner: {2}. {3}";
            En["hero_selection_hero_focus"] = "Hero: {0}. Target slot {1}.";
            En["hero_selection_action_focus"] = "Action: {0}.";
            En["hero_selection_strength"] = "{0}";
            En["hero_selection_description"] = "{0}";
            En["hero_selection_slot_target"] = "Target slot set to slot {0}. Choose a hero and press Enter to assign.";
            En["hero_selection_slot_target_with_hero"] = "Target slot set to slot {0}. Current hero: {1}. Choose another hero and press Enter to replace.";
            En["hero_selection_current_target"] = "Current target.";
            En["hero_selection_assigning"] = "Assigning {0} to slot {1}.";
            En["hero_selection_open_perks"] = "Opening perk tree for {0}.";
            En["hero_selection_no_slot"] = "No party slot selected.";
            En["hero_selection_no_hero"] = "No hero selected.";
            En["hero_selection_owner_single_player"] = "Slot owner changes are only available in multiplayer.";
            En["hero_selection_owner_master_only"] = "Only the host can change party slot owners.";
            En["hero_selection_owner_assigned"] = "Slot {0} owner: {1}.";
            En["hero_selection_owner_ready"] = "Ready.";
            En["hero_selection_owner_not_ready"] = "Not ready.";
            En["hero_selection_no_details"] = "No hero details available.";
            En["hero_selection_detail_buffer"] = "{0}.";
            En["hero_selection_buffer_overview"] = "Hero overview";
            En["hero_selection_buffer_traits"] = "Traits and resistances";
            En["hero_selection_buffer_cards"] = "Starting cards and item";
            En["hero_selection_detail_name"] = "Name: {0}.";
            En["hero_selection_detail_class"] = "Class: {0}.";
            En["hero_selection_detail_secondary_class"] = "Secondary class: {0}.";
            En["hero_selection_detail_health"] = "Health: {0}.";
            En["hero_selection_detail_energy"] = "Energy: {0}. Energy per turn: {1}.";
            En["hero_selection_detail_speed"] = "Speed: {0}.";
            En["hero_selection_detail_resist"] = "{0} resistance: {1}.";
            En["hero_selection_trait_line"] = "{0}: {1}";
            En["hero_selection_starting_item"] = "Starting item: {0}";
            En["hero_selection_starting_card"] = "{0} copies: {1}";
            En["hero_selection_perk_points"] = "Unspent perk points: {0}";
            En["hero_selection_begin"] = "Begin adventure";
            En["hero_selection_ready"] = "Ready";
            En["hero_selection_madness"] = "Madness";
            En["hero_selection_sandbox"] = "Sandbox";
            En["hero_selection_seed"] = "Game seed";
            En["hero_selection_seed_value"] = "Game seed: {0}";
            En["hero_selection_seed_modify"] = "Change game seed";
            En["hero_selection_weekly"] = "Weekly modifiers";
            En["hero_selection_follow"] = "Follow";
            En["damage_slashing"] = "Slashing";
            En["damage_blunt"] = "Blunt";
            En["damage_piercing"] = "Piercing";
            En["damage_fire"] = "Fire";
            En["damage_cold"] = "Cold";
            En["damage_lightning"] = "Lightning";
            En["damage_mind"] = "Mind";
            En["damage_holy"] = "Holy";
            En["damage_shadow"] = "Shadow";
            En["damage_all"] = "All";
            En["card_rarity_common"] = "Common";
            En["card_rarity_uncommon"] = "Uncommon";
            En["card_rarity_rare"] = "Rare";
            En["card_rarity_epic"] = "Epic";
            En["card_rarity_mythic"] = "Mythic";
            En["card_type_none"] = "None";
            En["card_type_melee_attack"] = "Melee attack";
            En["card_type_ranged_attack"] = "Ranged attack";
            En["card_type_magic_attack"] = "Magic attack";
            En["card_type_defense"] = "Defense";
            En["card_type_fire_spell"] = "Fire spell";
            En["card_type_cold_spell"] = "Cold spell";
            En["card_type_lightning_spell"] = "Lightning spell";
            En["card_type_mind_spell"] = "Mind spell";
            En["card_type_shadow_spell"] = "Shadow spell";
            En["card_type_holy_spell"] = "Holy spell";
            En["card_type_curse_spell"] = "Curse spell";
            En["card_type_healing_spell"] = "Healing spell";
            En["card_type_book"] = "Book";
            En["card_type_small_weapon"] = "Small weapon";
            En["card_type_song"] = "Song";
            En["card_type_skill"] = "Skill";
            En["card_type_power"] = "Power";
            En["card_type_injury"] = "Injury";
            En["card_type_attack"] = "Attack";
            En["card_type_spell"] = "Spell";
            En["card_type_boon"] = "Boon";
            En["card_type_weapon"] = "Weapon";
            En["card_type_armor"] = "Armor";
            En["card_type_jewelry"] = "Jewelry";
            En["card_type_accesory"] = "Accessory";
            En["card_type_pet"] = "Pet";
            En["card_type_corruption"] = "Corruption";
            En["card_type_enchantment"] = "Enchantment";
            En["card_type_food"] = "Food";
            En["card_type_flask"] = "Flask";
            En["card_type_petrare"] = "Rare pet";
            En["hero_level_up"] = "Level up: {0}, level {1}. Trait: {2}. {3}";
            En["hero_level_up_with_changes"] = "Level up: {0}, level {1}. Trait: {2}. {3} {4}";
            En["hero_level_hp"] = "Maximum HP {0}.";
            En["hero_level_speed"] = "Speed {0}.";
            En["hero_level_energy"] = "Energy {0}.";
            En["hero_level_energy_turn"] = "Energy per turn {0}.";
            En["challenge_screen"] = "Obelisk Challenge draft.";
            En["challenge_controls"] = "Up and Down switch between choices, heroes, and actions. Left and Right move within the current section. Control plus Up and Control plus Down read choice details. Enter selects a pack, special card, perk, hero, or action.";
            En["challenge_current_hero"] = "Current hero: {0}.";
            En["challenge_bonus"] = "Current deck summary: {0}.";
            En["challenge_choice_position"] = "Challenge choice, {0} of {1}: {2}";
            En["challenge_hero_position"] = "Challenge hero, {0} of {1}: {2}";
            En["challenge_action_position"] = "Challenge action, {0} of {1}: {2}";
            En["challenge_pack_fallback"] = "Pack {0}";
            En["challenge_pack_summary"] = "Pack: {0}. {1}.";
            En["challenge_pack_cards"] = "Cards in this pack.";
            En["challenge_card_line"] = "{0}, {1} energy, {2}.";
            En["challenge_special_summary"] = "Special card: {0}. {1}.";
            En["challenge_perk_summary"] = "Perk: {0}. {1}.";
            En["challenge_selected"] = "Selected";
            En["challenge_reroll"] = "Reroll packs";
            En["challenge_reroll_summary"] = "{0}. Replaces the unselected packs for the current hero. Already selected packs stay unchanged. One use per hero.";
            En["challenge_reroll_done"] = "Rerolled unselected packs for the current hero.";
            En["challenge_ready"] = "Ready";
            En["challenge_no_choice"] = "No challenge choice selected.";
            En["challenge_no_details"] = "No details for this challenge choice.";
            En["challenge_already_selected"] = "Already selected.";
            En["challenge_speed"] = "Speed: {0}.";
            En["challenge_damage_summary"] = "Damage types: {0}.";
            En["challenge_effect_summary"] = "Combat effects: {0}.";
            En["settings_screen"] = "Settings.";
            En["settings_controls"] = "Up and Down move through settings. Press Enter on a tab to open it. Enter toggles checkboxes or activates buttons. Left and Right change sliders and dropdown values. Escape closes settings through the game.";
            En["settings_item"] = "Settings, {0} of {1}: {2}";
            En["settings_tab_item"] = "Tab: {0}. {1}. Press Enter to open.";
            En["settings_tab_selected"] = "Selected tab: {0}.";
            En["settings_tab_graphics"] = "Graphics";
            En["settings_tab_audio"] = "Audio";
            En["settings_tab_gameplay"] = "Gameplay";
            En["settings_on"] = "On.";
            En["settings_off"] = "Off.";
            En["settings_percent"] = "{0} percent.";
            En["settings_press_enter"] = "Press Enter.";
            En["settings_no_options"] = "No options.";
            En["settings_description"] = "{0}";
            En["settings_unknown_toggle"] = "Unnamed checkbox";
            En["settings_unknown_slider"] = "Unnamed slider";
            En["settings_unknown_dropdown"] = "Unnamed selection";
            En["settings_language_applied"] = "Language selected: {0}.";
            En["settings_alert"] = "Confirmation. {0}";
            En["settings_alert_choice"] = "{0}";
            En["settings_alert_single"] = "Button: {0}. Press Enter.";
            En["settings_alert_no_button"] = "No button available.";
            En["settings_alert_input"] = "{0} Type text, then press Enter.";
            En["settings_alert_input_empty"] = "Type text before confirming.";
            En["settings_resolution"] = "Resolution";
            En["settings_fullscreen"] = "Fullscreen";
            En["settings_vsync"] = "Vsync";
            En["settings_screen_shake"] = "Screen shake";
            En["settings_ac_backgrounds"] = "Aura and curse background effects";
            En["settings_language"] = "Language";
            En["settings_master_volume"] = "Master volume";
            En["settings_effects_volume"] = "Effects volume";
            En["settings_music_volume"] = "Music volume";
            En["settings_ambience_volume"] = "Ambience volume";
            En["settings_background_mute"] = "Mute in background";
            En["settings_legacy_sounds"] = "Legacy sounds";
            En["settings_legacy_sounds_extra"] = "Legacy sheep and owl sounds";
            En["settings_fast_mode"] = "Fast mode";
            En["settings_auto_end"] = "Auto end turn";
            En["settings_show_effects"] = "Show effects";
            En["settings_restart_combat"] = "Restart combat option";
            En["settings_keyboard_shortcuts"] = "Keyboard shortcuts";
            En["settings_extended_descriptions"] = "Extended descriptions";
            En["settings_follow_leader"] = "Follow the leader";
            En["settings_reset_tutorial"] = "Reset tutorial";
            En["settings_reset_saved"] = "Reset saved progress";
            En["settings_telemetry"] = "Optional telemetry";
            En["mod_settings_screen"] = "AccessTheObelisk settings.";
            En["mod_settings_controls"] = "Up and Down move through checkboxes. Enter or Space toggles the focused checkbox. Escape closes this menu.";
            En["mod_settings_checkbox"] = "{0}. {1}";
            En["mod_settings_map_details"] = "Detailed map speech";
            En["mod_settings_enemy_played_cards"] = "Speak enemy played card names";
            En["mod_settings_close"] = "Close settings.";
            En["mod_settings_closed"] = "AccessTheObelisk settings closed.";
            En["empty_slot"] = "Empty";
            En["random_hero"] = "Random hero";
            En["locked"] = "Locked";
            En["map_screen"] = "Map.";
            En["map_current_node"] = "Current location changed.";
            En["map_current_and_destinations"] = "Current location: {0}. Available destinations: {1}.";
            En["map_position"] = "Map, {0} of {1}: {2}.";
            En["map_node"] = "{0}.";
            En["map_node_available"] = "{0}. Available destination.";
            En["map_node_unavailable"] = "Cannot travel to {0}.";
            En["map_current_node_selected"] = "{0}. This is your current location.";
            En["map_no_available_destinations"] = "No available map destinations.";
            En["map_following_leader_selection_blocked"] = "Following the host. Your manual choice for {0} is ignored by the game; the host's path will be selected automatically.";
            En["map_no_path_direction"] = "No available path {0}.";
            En["map_paths_direction"] = "Paths {0}: {1}.";
            En["map_path_preview"] = "{0}. Then: {1}";
            En["map_path_preview_end"] = "{0}. No known onward paths";
            En["map_path_preview_branch"] = "{0}. Branches: {1}. Selected branch {2}: {3}.";
            En["map_preview_branch"] = "Selected branch {0} of {1}: {2}.";
            En["map_preview_branch_selected"] = "Selected branch {0} of {1}.";
            En["map_preview_branch_selected_node"] = "Selected branch {0} of {1}: {2}.";
            En["map_preview_current"] = "Path preview at current location: {0}.";
            En["map_future_path"] = "Route preview";
            En["map_explorer_current"] = "Map route preview: {0}.";
            En["map_explorer_after"] = "After {0}: {1}.";
            En["map_explorer_choices"] = "After this location, visible options: {0}. Selected: {1}.";
            En["map_explorer_no_onward"] = "No further visible paths.";
            En["map_explorer_sibling_left"] = "Another future option is to the left.";
            En["map_explorer_sibling_right"] = "Another future option is to the right.";
            En["map_explorer_siblings_both"] = "Other future options are to the left and right.";
            En["map_branch_simple"] = "Branch.";
            En["map_coordinates"] = "{0}, {1}";
            En["map_coordinates_unknown"] = "Coordinates unknown";
            En["map_branch_left_available"] = "Left branch available";
            En["map_branch_right_available"] = "Right branch available";
            En["map_branch_both_available"] = "Left and right branches available";
            En["map_unknown_event"] = "Unknown";
            En["map_ground"] = "Ground: {0}.";
            En["map_node_requirement"] = "Node requires: {0}.";
            En["map_path_requirement"] = "Path requires: {0}.";
            En["map_path_requirement_to"] = "Path to {0} requires: {1}.";
            En["map_event_name"] = "Assigned event: {0}.";
            En["map_event_tier"] = "Event tier: {0}.";
            En["map_event_description"] = "{0}";
            En["map_event_requires"] = "Event requires: {0}.";
            En["map_event_requires_class"] = "Event requires hero: {0}.";
            En["map_event_option_requirements"] = "Requirements or rewards: {0}.";
            En["map_quest_starts"] = "Can start or gain: {0}.";
            En["map_quest_ends"] = "Can complete or remove: {0}.";
            En["map_event_opens_card_corruption"] = "An event option can open card corruption.";
            En["map_event_opens_item_corruption"] = "An event option can open item corruption.";
            En["map_combat_id"] = "Assigned combat: {0}.";
            En["map_combat_tier"] = "Combat tier: {0}.";
            En["map_combat_enemies"] = "Enemies: {0}.";
            En["map_combat_followup_event"] = "After combat event: {0}.";
            En["map_combat_followup_event_requires"] = "After combat event: {0}, requires {1}.";
            En["map_combat_rift"] = "Rift combat.";
            En["map_obelisk_corruption_available"] = "Obelisk corruption can be accepted.";
            En["map_obelisk_corruption_disabled"] = "Obelisk corruption is disabled for this combat.";
            En["direction_up"] = "up";
            En["direction_down"] = "down";
            En["direction_left"] = "left";
            En["direction_right"] = "right";
            En["no_map_node"] = "No map node selected.";
            En["unknown_node"] = "Unknown node";
            En["current"] = "Current";
            En["available"] = "Available";
            En["unavailable"] = "Unavailable.";
            En["currency_gold"] = "Gold: {0}.";
            En["currency_dust"] = "Dust: {0}.";
            En["currency_supply"] = "Supply: {0}.";
            En["currency_all"] = "Currencies. Gold: {0}. Dust: {1}. Supply: {2}.";
            En["currency_unavailable"] = "Currencies are not available yet.";
            En["requirements_empty"] = "No quest items or tracked quests.";
            En["requirements_position"] = "{2}";
            En["requirements_entry"] = "{0}. {1}.";
            En["requirements_kind_item"] = "quest item";
            En["requirements_kind_track"] = "tracked quest";
            En["requirements_kind_item_and_track"] = "quest item and tracked quest";
            En["requirements_visible_on_map"] = "Shown on the current map.";
            En["requirements_not_visible_on_map"] = "Not shown on the current map.";
            En["requirements_filter"] = "{0}. {1} entries.";
            En["requirements_filter_all"] = "all";
            En["requirements_filter_items"] = "quest items";
            En["requirements_filter_tracks"] = "tracked quests";
            En["event_buffer_empty"] = "Event buffer is empty.";
            En["event_buffer_focused"] = "Event buffer.";
            En["event_buffer_left"] = "Returned to current screen.";
            En["event_buffer_position"] = "Event buffer, {0} of {1}: {2}";
            En["combat"] = "Combat";
            En["boss"] = "Boss";
            En["event"] = "Event";
            En["event_screen"] = "Event.";
            En["event_option_position"] = "";
            En["event_character_speaks"] = "{0} says.";
            En["event_character_option"] = "Hero option: {0}.";
            En["event_roll"] = "Roll: {0}.";
            En["event_probability"] = "Probability: {0}.";
            En["event_roll_result"] = "Roll result: {0}.";
            En["event_roll_result_character"] = "{0}: {1}.";
            En["event_result"] = "Result. {0}";
            En["event_continue"] = "Continue";
            En["event_no_options"] = "No event option selected.";
            En["story_continue"] = "Continue";
            En["story_no_text"] = "No story text available.";
            En["town"] = "Town";
            En["town_screen"] = "Town.";
            En["town_position"] = "Town, {0} of {1}: {2}.";
            En["town_building"] = "{0}. {1}.";
            En["town_ready"] = "Ready";
            En["town_upgrades"] = "Town upgrades.";
            En["town_upgrade_screen"] = "Town upgrades. Available supply: {0}. Spent supply: {1}.";
            En["town_upgrade_sell_screen"] = "Sell supply.";
            En["town_upgrade_summary"] = "{0}. {1}.";
            En["town_upgrade_position"] = "Column {0}, row {1}.";
            En["town_upgrade_cost"] = "Cost: {0} supply.";
            En["town_upgrade_requires"] = "Requires: {0}.";
            En["town_upgrade_requires_spent"] = "Requires {0} more supply spent in town upgrades.";
            En["town_upgrade_needs_supply"] = "Needs {0} more supply.";
            En["town_upgrade_bought"] = "Bought.";
            En["town_upgrade_already_bought"] = "This town upgrade is already bought.";
            En["town_upgrade_no_item"] = "No town upgrade selected.";
            En["town_upgrade_exit"] = "Exit town upgrades";
            En["town_upgrade_sell_supply"] = "Sell supply";
            En["town_upgrade_sell_quantity"] = "Quantity: {0}.";
            En["town_upgrade_sell_result"] = "Result: {0}.";
            En["town_reward"] = "Reward";
            En["town_community_reward"] = "Community reward";
            En["town_reward_text"] = "{0}: {1}.";
            En["town_no_item"] = "No town item selected.";
            En["craft_screen"] = "Magic forge. Craft cards.";
            En["craft_screen_owner"] = "Magic forge. {0}.";
            En["altar_screen"] = "Altar. Upgrade cards.";
            En["altar_screen_owner"] = "Altar. {0}.";
            En["church_screen"] = "Church. Remove cards.";
            En["church_screen_owner"] = "Church. {0}.";
            En["armory_screen"] = "Armory. Buy items.";
            En["armory_screen_owner"] = "Armory. Target hero: {0}.";
            En["armory_items_tab"] = "Items shop";
            En["armory_pets_tab"] = "Pet shop";
            En["armory_pet_locked"] = "Pet locked. Unlock this pet in the game before buying it.";
            En["armory_pet_already_owned"] = "Pet already owned by the team.";
            En["armory_reroll"] = "Reroll items";
            En["armory_reroll_cost"] = "Reroll cost: {0} gold. Current gold: {1}.";
            En["armory_reroll_not_enough_gold"] = "Not enough gold to reroll. Cost: {0}. Current gold: {1}.";
            En["armory_reroll_limited"] = "Rerolls are limited.";
            En["armory_reroll_unavailable"] = "Reroll unavailable.";
            En["armory_equipped_slot"] = "Equipped {0}: {1}";
            En["armory_shady_deal"] = "Shady deal.";
            En["armory_shady_cost"] = "Cost: {0}.";
            En["armory_shady_result"] = "Reward: {0}.";
            En["armory_item_cost"] = "Gold cost: {0}.";
            En["armory_item_cost_with_gold"] = "Gold cost: {0}. Current gold: {1}.";
            En["armory_item_cost_free"] = "Gold cost: free.";
            En["armory_not_enough_gold"] = "Not enough gold. Cost: {0}. Current gold: {1}.";
            En["divination_screen"] = "Zingarian cart. Buy a divination reward round.";
            En["divination_screen_owner"] = "Zingarian cart. {0}.";
            En["divination_option"] = "Divination option {0}.";
            En["divination_reward_tier"] = "Reward tier: {0}.";
            En["divination_reward_cards"] = "{0} {1} reward cards.";
            En["divination_reward_dust"] = "Dust reward: {0}.";
            En["divination_waiting"] = "Waiting for divination players.";
            En["reward_screen"] = "Rewards.";
            En["reward_screen_with_subtitle"] = "{0}. {1}";
            En["reward_character"] = "Reward character: {0}.";
            En["reward_character_owner"] = "Reward character: {0}. Player: {1}.";
            En["reward_character_owner_read_only"] = "Reward character: {0}. Player: {1}. Read only.";
            En["reward_position"] = "Reward, {0} of {1}: {2}";
            En["reward_dust"] = "Dust reward.";
            En["reward_dust_quantity"] = "Dust reward: {0}.";
            En["reward_no_item"] = "No reward selected.";
            En["reward_not_owner"] = "This is {0}'s reward. You can read it, but only that player can choose it.";
            En["reward_focus_owner"] = "{0}. Reward for {1}, player {2}.";
            En["reward_focus_read_only"] = "{0}. Reward for {1}, player {2}. Read only.";
            En["unknown_hero"] = "Unknown hero";
            En["unknown_player"] = "Unknown player";
            En["loot_screen"] = "Loot.";
            En["loot_screen_with_subtitle"] = "Loot. {0}";
            En["loot_character"] = "Loot character: {0}.";
            En["loot_character_owner"] = "Loot character: {0}. Player: {1}.";
            En["loot_character_owner_read_only"] = "Loot character: {0}. Player: {1}. Read only.";
            En["loot_position"] = "Loot, {0} of {1}: {2}";
            En["loot_gold"] = "Gold.";
            En["loot_gold_quantity"] = "Gold: {0}.";
            En["loot_no_item"] = "No loot item selected.";
            En["loot_no_other_buffer"] = "No other details for this loot item.";
            En["loot_character_selected_press_enter"] = "Character selected. Press Enter again to choose this loot.";
            En["loot_not_owner"] = "This is {0}'s loot. You can read it, but only that player can choose it.";
            En["loot_focus_current_choice"] = "{0}. Current item choice: {1}, player {2}.";
            En["loot_focus_read_only"] = "{0}. Current item choice: {1}, player {2}. Read only.";
            En["finish_unlocks_screen"] = "Unlocked cards.";
            En["finish_result_screen"] = "Run results.";
            En["finish_position"] = "Run results, {0} of {1}: {2}";
            En["finish_close_unlocks"] = "Continue to run results";
            En["finish_main_menu"] = "Main menu";
            En["finish_no_item"] = "No run result item selected.";
            En["finish_places"] = "Places visited: {0}. Score: {1}.";
            En["finish_expertise"] = "Combat expertise: {0}. Score: {1}.";
            En["finish_deaths"] = "Deaths: {0}. Score: {1}.";
            En["finish_experience"] = "Experience: {0}. Score: {1}.";
            En["finish_bosses"] = "Bosses: {0}. Score: {1}.";
            En["finish_corruptions"] = "Corruptions: {0}. Score: {1}.";
            En["finish_completed"] = "Completed: {0}. Score: {1}.";
            En["finish_final_score"] = "Final score: {0}.";
            En["finish_reward"] = "Run reward: {0}.";
            En["finish_character_progress"] = "Character {0}: {1}.";
            En["finish_progress_points"] = "Progress: {0}. Range: {1} to {2}.";
            En["corruption_screen"] = "Obelisk corruption.";
            En["corruption_position"] = "Obelisk corruption, {0} of {1}: {2}";
            En["corruption_difficulty_easy"] = "Difficulty: easy.";
            En["corruption_difficulty_average"] = "Difficulty: average.";
            En["corruption_difficulty_hard"] = "Difficulty: hard.";
            En["corruption_difficulty_extreme"] = "Difficulty: extreme.";
            En["corruption_reward_choice"] = "Reward {0}.";
            En["corruption_reward_card"] = "Reward card: {0}.";
            En["corruption_reward_card_for_hero"] = "Reward card: {0}, for {1}.";
            En["corruption_selected"] = "Selected.";
            En["corruption_reward_selected"] = "Selected reward {0}.";
            En["corruption_accepted"] = "Corruption accepted.";
            En["corruption_not_accepted"] = "Corruption not accepted.";
            En["corruption_continue"] = "Continue";
            En["corruption_no_item"] = "No corruption option selected.";
            En["deck_screen"] = "Deck.";
            En["deck_screen_with_title"] = "Deck. {0}";
            En["deck_controls"] = "Up and Down move between cards. Control plus Up and Control plus Down read details. Enter opens the card detail screen. Escape closes the deck. F opens the deck or combat draw pile. Shift plus F opens the combat discard pile. Control plus Shift plus F opens vanished cards in combat.";
            En["deck_position"] = "Deck, {0} of {1}: {2}";
            En["deck_empty"] = "No cards in this deck.";
            En["deck_opened"] = "Opening deck.";
            En["deck_draw_opened"] = "Opening draw pile.";
            En["deck_discard_opened"] = "Opening discard pile.";
            En["deck_vanish_opened"] = "Opening vanished cards.";
            En["deck_closed"] = "Deck closed.";
            En["deck_already_open"] = "Deck is already open.";
            En["deck_unavailable"] = "Deck view is not available here.";
            En["deck_discard_unavailable"] = "Discard pile is only available in combat.";
            En["deck_vanish_unavailable"] = "Vanished cards are only available in combat.";
            En["deck_card_detail"] = "Card details: {0}.";
            En["deck_card_detail_closed"] = "Card details closed.";
            En["card_screen_opened"] = "Card detail screen: {0}.";
            En["card_screen_controls"] = "Control plus Up and Control plus Down read details. Control plus Home and Control plus End jump to the first or last detail. Up and Down switch between details and Close. Enter activates Close when it is focused.";
            En["card_screen_detail_focus"] = "Card details: {0}.";
            En["card_screen_close_button"] = "Close card details.";
            En["card_screen_closed"] = "Card details closed.";
            En["card_screen_no_details"] = "No card details available.";
            En["character_window_screen"] = "Character window. {0}.";
            En["character_window_controls"] = "I opens this window. Left and Right switch tabs. Control plus Left and Control plus Right switch heroes. Up and Down move through information. Control plus Up and Control plus Down read details. Enter opens card details when a card is focused. Escape closes the window.";
            En["character_window_opened"] = "Opening character window.";
            En["character_window_closed"] = "Character window closed.";
            En["character_window_unavailable"] = "Character window is not available here.";
            En["character_window_already_open"] = "Character window is already open.";
            En["character_tab_deck"] = "Deck";
            En["character_tab_combatdeck"] = "Draw pile";
            En["character_tab_combatdiscard"] = "Discard pile";
            En["character_tab_combatvanish"] = "Vanished cards";
            En["character_tab_level"] = "Level";
            En["character_tab_items"] = "Items";
            En["character_tab_stats"] = "Stats";
            En["character_tab_perks"] = "Perks";
            En["character_unknown"] = "Unknown character.";
            En["character_name"] = "Name: {0}.";
            En["character_name_label"] = "Name";
            En["character_level"] = "Level: {0}.";
            En["character_level_info"] = "Level";
            En["character_draw"] = "Draw per turn: {0}.";
            En["character_perk_rank"] = "Perk rank: {0}.";
            En["character_speed"] = "Speed: {0} of {1}.";
            En["character_health_label"] = "Health";
            En["character_energy_label"] = "Energy";
            En["character_speed_label"] = "Speed";
            En["character_cards_label"] = "Cards";
            En["character_damage_done"] = "Damage done";
            En["character_healing_done_percent"] = "Healing done percent";
            En["character_healing_done_flat"] = "Healing done flat";
            En["character_healing_taken_percent"] = "Healing taken percent";
            En["character_healing_taken_flat"] = "Healing taken flat";
            En["character_damage_type"] = "Damage type";
            En["character_effect"] = "Effect";
            En["character_immunity"] = "Immunity";
            En["character_aura_bonus"] = "Aura and curse bonus";
            En["character_trait"] = "Trait";
            En["character_trait_status"] = "Trait: {0}. {1}.";
            En["character_trait_adds_card"] = "Adds card";
            En["character_trait_adds_card_all"] = "Adds card to all heroes";
            En["character_trait_card"] = "{0}: {1}.";
            En["character_trait_press_enter"] = "Press Enter to choose this trait and level up.";
            En["character_trait_no_description"] = "No description shown by the game.";
            En["character_cant_level_up"] = "{0}";
            En["character_level_not_owner"] = "This is {0}'s hero. You can read this character, but only that player can choose the level-up trait.";
            En["character_labeled_value"] = "{0}: {1}.";
            En["character_item_weapon"] = "Weapon";
            En["character_item_armor"] = "Armor";
            En["character_item_jewelry"] = "Jewelry";
            En["character_item_accessory"] = "Accessory";
            En["character_item_pet"] = "Pet";
            En["character_item_summary"] = "{0}: {1}.";
            En["character_item_empty"] = "{0}: empty.";
            En["character_close"] = "Close character window.";
            En["character_no_items"] = "No character information selected.";
            En["character_no_other_hero"] = "No other hero available.";
            En["character_opening_perks"] = "Opening perks for {0}.";
            En["character_card_detail"] = "Card details: {0}.";
            En["character_card_detail_closed"] = "Card details closed.";
            En["tome_screen"] = "Tome of Knowledge. {0}.";
            En["tome_controls"] = "B opens the Tome where available. Control plus Left and Control plus Right switch sections. Up and Down move through items. Left and Right change page when pages are available. Control plus Up and Control plus Down read details. Enter opens cards or activates the focused item. Control plus F focuses search on card and item pages. Escape goes back or closes the Tome.";
            En["tome_opened"] = "Opening Tome of Knowledge.";
            En["tome_closed"] = "Tome closed.";
            En["tome_unavailable"] = "Tome of Knowledge is not available here.";
            En["tome_already_open"] = "Tome of Knowledge is already open.";
            En["tome_section_main"] = "Summary";
            En["tome_section_cards"] = "Cards";
            En["tome_section_items"] = "Items";
            En["tome_section_glossary"] = "Glossary";
            En["tome_section_runs"] = "Runs";
            En["tome_section_scoreboard"] = "Scoreboard";
            En["tome_information"] = "Information";
            En["tome_main_stat"] = "";
            En["tome_glossary_entry"] = "Glossary entry";
            En["tome_glossary_index"] = "Glossary index.";
            En["tome_run"] = "Run";
            En["tome_run_detail"] = "Run detail";
            En["tome_run_path"] = "Run path";
            En["tome_run_character"] = "Run character";
            En["tome_close_run_detail"] = "Close run details.";
            En["tome_scoreboard_title"] = "Scoreboard";
            En["tome_scoreboard_status"] = "Scoreboard status";
            En["tome_scoreboard_button"] = "Scoreboard filter";
            En["tome_previous_week"] = "Previous week.";
            En["tome_next_week"] = "Next week.";
            En["tome_previous_page"] = "Previous page.";
            En["tome_next_page"] = "Next page.";
            En["tome_close"] = "Close Tome.";
            En["tome_labeled_value"] = "{0}: {1}.";
            En["tome_no_items"] = "No Tome item selected.";
            En["tome_card_detail"] = "Card details: {0}.";
            En["tome_card_detail_closed"] = "Card details closed.";
            En["tome_search_focused"] = "Search focused. Type a search term. Press Escape to leave search.";
            En["tome_search_closed"] = "Search closed.";
            En["tome_search_unavailable"] = "Search is available on card and item pages.";
            En["tome_class_warrior"] = "Warrior cards";
            En["tome_class_mage"] = "Mage cards";
            En["tome_class_healer"] = "Healer cards";
            En["tome_class_scout"] = "Scout cards";
            En["tome_class_boon"] = "Boons";
            En["tome_class_injury"] = "Injuries";
            En["tome_enchantments"] = "Enchantments";
            En["tome_button"] = "Tome button {0}";
            En["perk_screen"] = "Perk tree. Hero: {0}. {1}. {2}.";
            En["perk_controls"] = "Categories stay at the top. In perks, Left and Right move through perks in the same requirement group, and Up and Down move to the previous or next requirement group. Control plus Up and Control plus Down read details.";
            En["perk_position"] = "Perk tree, {0} of {1}: {2}";
            En["perk_category_position"] = "Perk categories, {0} of {1}: {2}";
            En["perk_perk_position"] = "Perks, {0} of {1}: {2}";
            En["perk_action_position"] = "Perk actions, {0} of {1}: {2}";
            En["perk_category"] = "Category: {0}.";
            En["perk_category_current"] = "Current category: {0}.";
            En["perk_category_fallback"] = "Category {0}";
            En["perk_node_position"] = "Row {0}, column {1}.";
            En["perk_node_summary"] = "{0}. {1}.";
            En["perk_group_requirement"] = "Requirement group: {0} spent perk points.";
            En["perk_node_cost"] = "Cost: {0} perk points.";
            En["perk_aura_bonus"] = "{0}: plus {1} charge.";
            En["perk_selected"] = "Selected.";
            En["perk_read_only"] = "Read only.";
            En["perk_not_enough_points"] = "Not enough perk points.";
            En["perk_choose_one"] = "Choose one perk group";
            En["perk_unknown"] = "Unknown perk";
            En["perk_no_item"] = "No perk tree item selected.";
            En["perk_confirm"] = "Confirm perks";
            En["perk_reset"] = "Reset perks";
            En["perk_import"] = "Import perk build";
            En["perk_export"] = "Export perk build";
            En["perk_exit"] = "Exit perk tree";
            En["perk_slot"] = "Perk slot {0}: {1}.";
            En["perk_slot_points"] = "Saved points: {0}.";
            En["perk_slot_save"] = "Save current perks to slot {0}";
            En["perk_slot_delete"] = "Delete perk slot {0}";
            En["alert_copy_code"] = "{0}. Code: {1}";
            En["alert_copy_controls"] = "Press Control plus C to copy the code. Press Enter or Escape to close.";
            En["alert_copy_done"] = "Code copied to clipboard.";
            En["alert_paste_controls"] = "Paste the code with Control plus V, then press Enter to import. Press Escape to cancel.";
            En["alert_paste_done"] = "Code pasted.";
            En["craft_save_load"] = "Save or load deck";
            En["craft_save_load_screen"] = "Save or load deck.";
            En["craft_save_load_return"] = "Return to magic forge";
            En["craft_saved_deck_slot"] = "Saved deck slot {0}: {1}. Cards: {2}.";
            En["craft_saved_deck_load_hint"] = "Press Enter to preview this deck and calculate the crafting cost.";
            En["craft_saved_deck_save"] = "Save current deck to slot {0}";
            En["craft_saved_deck_delete"] = "Delete saved deck slot {0}: {1}";
            En["craft_saved_deck_preview"] = "Selected saved deck.";
            En["craft_saved_deck_preview_named"] = "Selected saved deck: {0}.";
            En["craft_saved_deck_apply"] = "Craft selected saved deck";
            En["craft_saved_deck_card"] = "Card {0}: {1}";
            En["craft_exit"] = "Exit";
            En["craft_give_gold"] = "Give Gold";
            En["craft_affordable_only"] = "Affordable only";
            En["craft_advanced_mode"] = "Advanced crafting";
            En["craft_toggle_state"] = "{0}: {1}";
            En["craft_page"] = "Page {0}";
            En["craft_search"] = "Search cards";
            En["craft_search_value"] = "Search cards: {0}";
            En["craft_search_focused"] = "Search field focused.";
            En["craft_card_cost"] = "Dust cost: {0}.";
            En["craft_card_cost_with_dust"] = "Dust cost: {0}. Current dust: {1}.";
            En["craft_card_cost_free"] = "Dust cost: free.";
            En["craft_not_enough_dust"] = "Not enough dust. Cost: {0}. Current dust: {1}.";
            En["craft_no_item"] = "No craft item selected.";
            En["craft_no_other_hero"] = "No other available hero.";
            En["craft_current_card"] = "Current card.";
            En["craft_base_card"] = "Base card.";
            En["craft_upgrade_a"] = "Upgrade A.";
            En["craft_upgrade_b"] = "Upgrade B.";
            En["craft_transform_a"] = "Transform to A.";
            En["craft_transform_b"] = "Transform to B.";
            En["craft_no_upgrade_preview"] = "No upgrade preview for this card.";
            En["craft_choose_upgrade_buffer"] = "Choose upgrade A or B with Control plus Left or Right, then press Enter.";
            En["craft_upgrade_changes"] = "Changes from current card: {0}";
            En["craft_upgrade_change"] = "{0}: {1} to {2}.";
            En["craft_no_obvious_upgrade_changes"] = "No obvious text or number changes from the current card.";
            En["craft_change_cost"] = "Cost";
            En["craft_change_damage"] = "Damage";
            En["craft_change_heal"] = "Heal";
            En["craft_change_type"] = "Type";
            En["craft_change_target"] = "Target";
            En["craft_change_text"] = "Text";
            En["craft_upgrade_cost"] = "Dust cost: {0}.";
            En["craft_upgrade_cost_with_dust"] = "Dust cost: {0}. Current dust: {1}.";
            En["craft_upgrade_cost_free"] = "Dust cost: free.";
            En["craft_upgrade_not_enough_dust"] = "Not enough dust. Cost: {0}. Current dust: {1}.";
            En["craft_remove_cost"] = "Gold cost: {0}.";
            En["craft_remove_cost_with_gold"] = "Gold cost: {0}. Current gold: {1}.";
            En["craft_remove_cost_free"] = "Gold cost: free.";
            En["craft_remove_not_enough_gold"] = "Not enough gold. Cost: {0}. Current gold: {1}.";
            En["craft_remove_minimum_deck"] = "Minimum deck size: {0} cards.";
            En["craft_remove_minimum_blocked"] = "Cannot remove: minimum deck size reached.";
            En["craft_remove_unavailable"] = "Cannot remove this card.";
            En["buffer_named_summary"] = "{0} {1}";
            En["card_name_with_rarity"] = "{0}, {1}.";
            En["card_rarity_label"] = "Rarity";
            En["none_value"] = "none";
            En["unknown_value"] = "unknown";
            En["item_overview"] = "Item overview.";
            En["item_effects"] = "Item effects.";
            En["item_focus_summary"] = "{0}, {1}. {2}.";
            En["item_focus_summary_no_description"] = "{0}, {1}.";
            En["item_pet_activation"] = "Pet activation: {0}.";
            En["item_pet_card"] = "Pet card: {0}.";
            En["item_max_health"] = "Maximum HP: {0}.";
            En["item_energy"] = "Energy: {0}.";
            En["item_draw_cards"] = "Draw cards: {0}.";
            En["item_heal"] = "Heal: {0}.";
            En["item_heal_bonus"] = "Heal bonus: {0}.";
            En["item_heal_percent"] = "Heal bonus: {0} percent.";
            En["item_damage_bonus"] = "{0} damage bonus: {1}.";
            En["item_damage_percent"] = "{0} damage bonus: {1} percent.";
            En["item_resist"] = "{0} resistance: {1}.";
            En["item_aura_bonus"] = "{0} bonus: {1}.";
            En["item_aura_gain"] = "Gain {0}: {1}.";
            En["item_self_aura_gain"] = "Gain on self {0}: {1}.";
            En["item_aura_immunity"] = "Immune to {0}.";
            En["combat_loaded"] = "Combat.";
            En["combat_death_screen"] = "Hero death.";
            En["combat_retry_screen"] = "Combat defeat.";
            En["combat_modal_button"] = "Button: {0}.";
            En["combat_modal_selected"] = "Selected: {0}.";
            En["combat_modal_no_buttons"] = "Waiting for other players.";
            En["combat_cards_empty"] = "No cards in hand.";
            En["combat_enemies_empty"] = "No enemies.";
            En["combat_party_empty"] = "No party members.";
            En["combat_actions_empty"] = "No combat actions.";
            En["combat_resign"] = "Resign.";
            En["combat_zone_cards"] = "Cards";
            En["combat_zone_enemies"] = "Enemies";
            En["combat_zone_party"] = "Party";
            En["combat_position"] = "{0}, {1} of {2}: {3}.";
            En["combat_buffer_position"] = "{0}. {1} of {2}.";
            En["combat_turn_hero"] = "Turn: {0}.";
            En["combat_turn_hero_owner"] = "Turn: {0}. Player: {1}.";
            En["combat_turn_enemy"] = "Enemy turn: {0}.";
            En["combat_turn_status"] = "Combat phase: {0}.";
            En["combat_turn_order"] = "Turn order. {0}";
            En["combat_turn_order_entry"] = "{0}. {1}.";
            En["combat_turn_order_current"] = "Current: {0}";
            En["combat_turn_order_unavailable"] = "Turn order unavailable.";
            En["combat_round"] = "Round {0}.";
            En["combat_round_unavailable"] = "Round unavailable.";
            En["combat_hp_changed"] = "{0}: {1} {2}, now {3} of {4}.";
            En["combat_hp_damage"] = "{0}: {1} damage.";
            En["combat_hp_heal"] = "{0}: {1} healing.";
            En["combat_hero_died"] = "Hero died: {0}.";
            En["combat_enemy_died"] = "Monster died: {0}.";
            En["combat_will_die_start_turn"] = "Will die at the start of their turn.";
            En["combat_effect_changed"] = "{0}: {1} {2}, now {3}.";
            En["combat_effect_gained"] = "{0}: gained {1} {2}.";
            En["combat_effect_lost"] = "{0}: lost {1}.";
            En["combat_effect_delta"] = "{0}: {1} {2}.";
            En["combat_up"] = "up";
            En["combat_down"] = "down";
            En["combat_no_effects"] = "No effects.";
            En["combat_target_mode"] = "Choose target.";
            En["combat_no_targets"] = "No valid targets.";
            En["combat_target_preview"] = "Preview: {0}.";
            En["combat_target_selected"] = "Target selected: {0}.";
            En["combat_action_select_cards"] = "Choose cards.";
            En["combat_action_discard_cards"] = "Choose cards to discard. Cards left: {0}. Enter marks a card. Control plus Enter or Space confirms.";
            En["combat_action_discard_up_to_cards"] = "Choose cards to discard. You may choose up to {0} more cards. Enter marks a card. Control plus Enter or Space confirms.";
            En["combat_action_add_cards"] = "Choose cards. Cards left: {0}. Enter marks a card. Control plus Enter or Space confirms.";
            En["combat_action_position"] = "Card choice, {0} of {1}: {2}";
            En["combat_action_no_cards"] = "No selectable cards.";
            En["combat_action_card_selected"] = "Selected.";
            En["combat_action_card_not_selected"] = "Not selected.";
            En["combat_action_card_marked"] = "Selected {0}.";
            En["combat_action_card_unmarked"] = "Unselected {0}.";
            En["combat_action_cards_left"] = "Choose {0} more cards.";
            En["combat_action_confirmed"] = "Confirmed card choice.";
            En["combat_energy"] = "Energy: {0}.";
            En["combat_energy_unavailable"] = "Energy unavailable.";
            En["combat_energy_selector"] = "{0}. {1} Left and Right change energy. Enter confirms.";
            En["combat_energy_selector_value"] = "Selected {0}.";
            En["combat_energy_selector_value_range"] = "Selected {0} of {1}.";
            En["combat_energy_assigned"] = "Assigned {0} energy.";
            En["combat_card_focus_summary"] = "{0}, {1} energy, {2}, {3}. {4}.";
            En["combat_card_focus_summary_no_description"] = "{0}, {1} energy, {2}, {3}.";
            En["combat_card_focus_summary_requirement"] = "{0}, {1} energy, {2}, {3}, {4}. {5}.";
            En["combat_card_focus_summary_requirement_no_description"] = "{0}, {1} energy, {2}, {3}, {4}.";
            En["combat_immediate_card_preview"] = "Preview: {0}";
            En["combat_immediate_card_preview_target"] = "{0}: {1}.";
            En["combat_card_cost"] = "Cost: {0}.";
            En["combat_card_requirement"] = "Requires: {0}.";
            En["combat_card_type"] = "Type: {0}.";
            En["combat_card_target"] = "Target: {0}.";
            En["combat_card_damage"] = "Damage: {0}.";
            En["combat_card_heal"] = "Heal: {0}.";
            En["combat_card_effect"] = "Effect: {0} {1}.";
            En["combat_card_description"] = "{0}.";
            En["combat_character_hp"] = "HP: {0} of {1}.";
            En["combat_character_hp_named"] = "{0}, HP {1} of {2}.";
            En["combat_party_hp"] = "Party HP. {0}";
            En["combat_party_hp_unavailable"] = "Party HP unavailable.";
            En["combat_character_unavailable"] = "Character unavailable.";
            En["combat_character_speed"] = "Speed: {0}.";
            En["combat_character_speed_modified"] = "Speed: {0}. Base {1}, modifier {2}.";
            En["combat_character_energy"] = "Energy: {0}.";
            En["combat_character_effect"] = "Effect: {0} {1}.";
            En["combat_character_effect_named"] = "{0} {1}.";
            En["combat_character_effects"] = "{0}, effects: {1}";
            En["combat_character_effects_none"] = "{0}, no effects.";
            En["combat_effect_description"] = "{0}: {1}.";
            En["combat_enemy_intents"] = "Enemy intentions. {0}";
            En["combat_enemy_intent_entry"] = "{0}: {1}.";
            En["combat_enemy_intents_none"] = "No revealed enemy intention cards.";
            En["combat_enemy_intents_none_for"] = "{0}: no revealed intention cards.";
            En["combat_enemy_played_card"] = "{0} plays {1}.";
            En["combat_end_turn"] = "End turn.";
            En["combat_revealed_cards"] = "Revealed monster cards.";
            En["combat_revealed_card"] = "Revealed card {0}: {1}.";
            En["combat_no_revealed_cards"] = "No monster cards revealed.";
            En["combat_enemy_info_buffer"] = "Enemy information.";
            En["card_fluff"] = "{0}";
            En["speech_sprite_card"] = "card";
            En["speech_sprite_cardrandom"] = "random card";
            En["speech_sprite_energy"] = "energy";
            En["speech_sprite_heart"] = "HP";
            En["speech_sprite_heal"] = "healing";
            En["speech_unit_damage_one"] = "damage";
            En["speech_unit_damage_few"] = "damage";
            En["speech_unit_damage_many"] = "damage";
            En["speech_unit_heal_one"] = "healing";
            En["speech_unit_heal_few"] = "healing";
            En["speech_unit_heal_many"] = "healing";
            En["speech_unit_aura_one"] = "aura";
            En["speech_unit_aura_few"] = "auras";
            En["speech_unit_aura_many"] = "auras";
            En["speech_unit_curse_one"] = "curse";
            En["speech_unit_curse_few"] = "curses";
            En["speech_unit_curse_many"] = "curses";
            En["card_player_screen"] = "Card shuffle. Review the visible cards, then choose Shuffle. After the cards are shuffled, choose one face-down card.";
            En["card_player_pairs_screen"] = "Card pairs. Review the visible pairs, then choose Shuffle. After the cards are shuffled, choose face-down cards to find matching pairs.";
            En["card_player_controls"] = "Up and Down or Left and Right move through the visible cards and the Shuffle button. Enter or Space activates the focused item.";
            En["card_player_pairs_controls"] = "Up and Down or Left and Right move through cards, Shuffle, and Finish when available. Enter or Space activates the focused item.";
            En["card_player_shuffle"] = "Shuffle";
            En["card_player_shuffling"] = "Shuffling cards. Please wait.";
            En["card_player_face_down"] = "Face-down card {0}.";
            En["card_player_pair_face_down"] = "Face-down pair card {0}.";
            En["card_player_visible_card"] = "Card {0}: {1}";
            En["card_player_selected"] = "Selected: {0}";
            En["card_player_finish_pairs"] = "Finish card pairs";
            En["card_player_no_action"] = "This item cannot be activated right now.";
            En["card_player_no_item"] = "No card player item selected.";
            En["unknown_card"] = "Unknown card.";
            En["menu_item"] = "{0}.";
            En["menu_item_unavailable"] = "{0}, unavailable.";
            En["menu_description"] = "{0}";
            En["activated"] = "{0}.";
            En["activated_loading"] = "Loading.";
            En["no_menu_item"] = "";
            Languages.Clear();
            Languages["en"] = En;
            LoadExternalLocalizations(force: true);
            UpdateActiveLanguage();
        }

        /// <summary>
        /// Gets a localized mod string by key.
        /// </summary>
        public static string Get(string key)
        {
            LoadExternalLocalizations(force: false);
            UpdateActiveLanguage();

            string value;
            if (_active.TryGetValue(key, out value))
            {
                return value;
            }

            if (En.TryGetValue(key, out value))
            {
                return value;
            }

            return key;
        }

        /// <summary>
        /// Gets and formats a localized mod string.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        private static void UpdateActiveLanguage()
        {
            string language = NormalizeLanguage(GetGameLanguage());
            Dictionary<string, string> strings;
            if (!Languages.TryGetValue(language, out strings))
            {
                language = "en";
                strings = En;
            }

            _activeLanguage = language;
            _active = strings;
        }

        private static string GetGameLanguage()
        {
            try
            {
                if (Globals.Instance != null && !string.IsNullOrWhiteSpace(Globals.Instance.CurrentLang))
                {
                    return Globals.Instance.CurrentLang;
                }
            }
            catch (System.Exception ex)
            {
                if (Main.Log != null)
                {
                    Main.Log.LogWarning("Could not read game language: " + ex.Message);
                }
            }

            return _activeLanguage;
        }

        private static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "en";
            }

            string normalized = language.Trim().Replace('_', '-').ToLowerInvariant();
            if (normalized.StartsWith("ru"))
            {
                return "ru";
            }

            if (normalized.StartsWith("en"))
            {
                return "en";
            }

            return normalized;
        }

        private static void LoadExternalLocalizations(bool force)
        {
            System.DateTime now = System.DateTime.UtcNow;
            if (!force && (now - _lastReloadCheckUtc).TotalSeconds < 2)
            {
                return;
            }

            _lastReloadCheckUtc = now;
            string directory = GetLocalizationDirectory();
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string path in Directory.GetFiles(directory, "*.txt"))
            {
                System.DateTime changed = File.GetLastWriteTimeUtc(path);
                System.DateTime previous;
                if (!force && LoadedFiles.TryGetValue(path, out previous) && previous == changed)
                {
                    continue;
                }

                string language = NormalizeLanguage(Path.GetFileNameWithoutExtension(path));
                Dictionary<string, string> target = language == "en" ? En : GetOrCreateLanguage(language);
                if (language != "en")
                {
                    target.Clear();
                }

                LoadFile(path, target);
                LoadedFiles[path] = changed;
            }
        }

        private static Dictionary<string, string> GetOrCreateLanguage(string language)
        {
            Dictionary<string, string> strings;
            if (!Languages.TryGetValue(language, out strings))
            {
                strings = new Dictionary<string, string>();
                Languages[language] = strings;
            }

            return strings;
        }

        private static void LoadFile(string path, Dictionary<string, string> target)
        {
            try
            {
                foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }

                    int separator = FindSeparator(line);
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string key = Unescape(line.Substring(0, separator).Trim());
                    string value = Unescape(line.Substring(separator + 1).Trim());
                    if (key.Length > 0)
                    {
                        target[key] = value;
                    }
                }
            }
            catch (IOException ex)
            {
                if (Main.Log != null)
                {
                    Main.Log.LogWarning("Could not load localization file " + path + ": " + ex.Message);
                }
            }
            catch (System.UnauthorizedAccessException ex)
            {
                if (Main.Log != null)
                {
                    Main.Log.LogWarning("Could not load localization file " + path + ": " + ex.Message);
                }
            }
        }

        private static int FindSeparator(string line)
        {
            bool escaped = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private static string Unescape(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            bool escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!escaped)
                {
                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    builder.Append(c);
                    continue;
                }

                switch (c)
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        builder.Append(c);
                        break;
                }

                escaped = false;
            }

            if (escaped)
            {
                builder.Append('\\');
            }

            return builder.ToString();
        }

        private static string GetLocalizationDirectory()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string pluginDirectory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                pluginDirectory = ".";
            }

            string localDirectory = Path.Combine(pluginDirectory, "Localization");
            if (Directory.Exists(localDirectory))
            {
                return localDirectory;
            }

            return Path.Combine(pluginDirectory, "AccessTheObelisk", "Localization");
        }
    }
}
