# Map Scene v0.1 Notes

1. JSON source for regions is `Assets/StreamingAssets/game_data.json`; Guild HQ is injected at runtime (`guild_hq`) and used as squad base.
2. In Unity Editor run `Tools/FantasyGuildmaster/Generate Placeholder Icons` (optional, setup tool also auto-generates missing placeholders including `guild_hq`).
3. Run `Tools/FantasyGuildmaster/Setup Map Scene`, press Play, assign squad to a contract, verify loop: **HQ -> region -> encounter -> return to HQ**.
