# Foal sound audition

`foalaudition.py` renders the listening files for [docs/foal-sounds.md](../../docs/foal-sounds.md): the game's donkey
brays, snort, grazing and footsteps pitched and filtered the ways the mod could do it in game.

- **Needs:** Python 3 with numpy (no scipy), and the WAVs exported from the game in PROBE-FINDINGS Round 12, in
  `C:\ClaudeCodeTemp\GK2-donkey-sfx-audition\` (`v_donkey_1..4`, `donkey_lips_1`, `donkey_eating_grass`,
  `grass_footstep_1`, `road_footstep_1`).
- **Run:** `python foalaudition.py`. It empties and refills `C:\ClaudeCodeTemp\GK2-foal-sound-audition\` and writes
  a `README.md` there listing each file.
- **Not in the repo:** the rendered WAVs. They're made from the game's own clips.

Plain pitch (the `A_` files) is exactly what `SoundHandler.SetPitch` does in game, because Unity resamples. The
filters (`B_`) and the custom DSP (`C_`) are close approximations of Unity's filters and of an `OnAudioFilterRead`
shifter.
