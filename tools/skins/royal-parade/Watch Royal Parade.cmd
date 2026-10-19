@echo off
rem Watches out\royal.aseprite (the by-hand copy make_royal.py writes): every save exports to the pack, checks it and
rem writes previews (Ctrl+C stops). The generated frames win on the next make_royal.py run.
python -u "%~dp0..\watch_skin.py" "%~dp0out\royal.aseprite"
pause
