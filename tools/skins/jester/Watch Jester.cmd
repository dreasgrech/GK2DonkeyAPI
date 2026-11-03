@echo off
rem Every save of out\pieces.aseprite in Aseprite: the pieces exported, the 44 frames built and checked,
rem and the card "Jester" on the skins preview page (Ctrl+C stops).
python -u "%~dp0..\stamp.py" watch "%~dp0." --stage "Jester"
pause
