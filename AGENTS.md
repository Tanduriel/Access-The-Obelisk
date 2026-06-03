# Project Instructions

Always use the behavior and workflow template from:

`D:\Accessibility-mod-template-Codex-edition-master`

Key rules for this project:

- User is a blind screen reader user; keep output linear and readable.
- Do not use Markdown pipe tables.
- Use Windows PowerShell or cmd commands.
- Game directory: `D:\Across.the.Obelisk.v1.7.5.1`.
- Official mod name: `AccessTheObelisk`.
- Engine/runtime: Unity 2022.3.62f2, 64-bit, Mono.
- Mod loader: BepInEx 5.x, installed in the game directory.
- Screen reader bridge: `Tolk.dll` and `nvdaControllerClient64.dll` are in the game directory.
- Build: `.\scripts\Build-Mod.ps1`.
- Build and deploy: `.\scripts\Deploy-Mod.ps1`.
- For accessibility mod work, prefer the game's existing mechanics, UI, navigation, and controls.
- Do not guess game API names. Check `docs\game-api.md` first, then verify in `decompiled\` before implementation.
- Keep findings documented in `docs\game-api.md` and progress in `project_status.md`.
- Use safe mod keys documented in `docs\game-api.md`.
- All screen reader strings must go through `Loc.Get()`.
- Use handler classes named `[Feature]Handler`.
- Use `_camelCase` private fields.
- Logs and code comments should be in English.
- Public C# members need XML `<summary>` documentation.
- Build and deploy through project scripts, not raw `dotnet build`.
- Never distribute or commit decompiled game code.

Project-specific notes:

- The game already routes arrow/WASD navigation through `InputController.DoMovementVector()` to many active managers.
- First accessibility strategy: announce existing focus/state changes, do not replace the game's navigation.
- Avoid single-key mod controls that conflict with game bindings. See `docs\game-api.md`.

Reference template files:

- `D:\Accessibility-mod-template-Codex-edition-master\Accessibility-Mod-Template\AGENTS.md`
- `D:\Accessibility-mod-template-Codex-edition-master\Accessibility-Mod-Template\docs\ACCESSIBILITY_MODDING_GUIDE.md`
- `D:\Accessibility-mod-template-Codex-edition-master\Accessibility-Mod-Template\docs\setup-guide.md`
