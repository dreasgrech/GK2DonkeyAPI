@echo off
rem The cap placed by hand: out\place.aseprite opens in Aseprite (made first if missing), a frame per view of the
rem cap. Move or scale the "cap" layer on a frame and save: the cap is drawn again there in every frame and the
rem card "Jester" on the skins preview page is rebuilt (reload the page). Ctrl+C stops.
python -u "%~dp0place.py" edit
pause
