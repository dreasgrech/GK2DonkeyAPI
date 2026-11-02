@echo off
rem The Armored review loop: regenerates the pictures into out\ (the pack is left alone), rebuilds the skins preview
rem page with the new cut as "Armored (in progress)" next to the shipped one, and opens it. See preview.py.
python -I "%~dp0preview.py"
if errorlevel 1 pause
