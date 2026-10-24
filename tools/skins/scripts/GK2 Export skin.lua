-- GK2: export this skin to its pack folder (docs/sprite-tools.md in GK2DonkeyAPI).
--
-- Every visible layer goes into sheet.png, one 96x96 (canvas-size) cell per frame, left to right then down,
-- except the reference layers (name starting with "donkey": the game's frames to paint over) and the layers whose
-- name starts with "front" (those go to front.png: drawn in front of the rider). The settings live next to the
-- .aseprite file in "<name>.gk2export.txt":
--   pack = C:\...\BepInEx\plugins\gk2mounts\donkey\skins\royal-parade   (where sheet.png goes; asked for the first time)
--   columns = 8                                            (cells per row; skin.cfg says the same)
--   check = python "C:\...\check_skin.py" "<pack>"         (optional: run after the export, its output shown)

local spr = app.sprite
local function say(msg)
  if app.isUIAvailable then app.alert{ title = "GK2 Export skin", text = msg } else print(msg) end
end
if not spr then return say("Open the skin's .aseprite file first.") end
if spr.colorMode ~= ColorMode.RGB then return say("The sprite must be in RGB colour mode (Sprite > Color Mode > RGB).") end

local fs = app.fs
local side = fs.filePathAndTitle(spr.filename) .. ".gk2export.txt"
local cfg = {}
if fs.isFile(side) then
  for line in io.lines(side) do
    local k, v = line:match("^%s*([%w_]+)%s*=%s*(.-)%s*$")
    if k then cfg[k] = v end
  end
end

if not cfg.pack or cfg.pack == "" then
  if not app.isUIAvailable then return say("No pack folder in " .. side) end
  local d = Dialog{ title = "GK2 Export skin: where to?" }
  d:label{ text = "The skin's pack folder (BepInEx\\plugins\\gk2mounts\\<mount>\\skins\\<id>):" }
  d:entry{ id = "pack", text = "" }
  d:button{ id = "ok", text = "Save and export" }
  d:button{ id = "cancel", text = "Cancel" }
  d:show()
  if not d.data.ok or d.data.pack == "" then return end
  cfg.pack = d.data.pack
  local f = io.open(side, "w")
  f:write("pack = " .. cfg.pack .. "\n", "columns = " .. (cfg.columns or "8") .. "\n")
  f:close()
end
if not fs.isDirectory(cfg.pack) then return say("The pack folder doesn't exist:\n" .. cfg.pack) end

local cols = tonumber(cfg.columns or "8") or 8
local W, H, n = spr.width, spr.height, #spr.frames
local rows = math.ceil(n / cols)

local function leaves(layers, out)
  for _, l in ipairs(layers) do
    if l.isGroup then
      if l.isVisible then leaves(l.layers, out) end
    elseif l.isVisible then
      table.insert(out, l)
    end
  end
  return out
end
local main, front = {}, {}
for _, l in ipairs(leaves(spr.layers, {})) do
  local name = l.name:lower()
  if name:sub(1, 6) == "donkey" then
    -- the reference: never exported
  elseif name:sub(1, 5) == "front" then
    table.insert(front, l)
  else
    table.insert(main, l)
  end
end

local function sheetOf(layers)
  local sheet = Image(W * cols, H * rows, ColorMode.RGB)
  local drawn = 0
  for i = 1, n do
    local cell = Image(W, H, ColorMode.RGB) -- one frame, clipped to the canvas
    for _, l in ipairs(layers) do
      local cel = l:cel(i)
      if cel then
        local op = math.floor(cel.opacity * (l.opacity or 255) / 255 + 0.5)
        cell:drawImage(cel.image, cel.position, op, BlendMode.NORMAL)
      end
    end
    if not cell:isEmpty() then drawn = drawn + 1 end
    sheet:drawImage(cell, Point(((i - 1) % cols) * W, ((i - 1) // cols) * H))
  end
  return sheet, drawn
end

local report = {}
local sheet, drawn = sheetOf(main)
local out = fs.joinPath(cfg.pack, "sheet.png")
sheet:saveAs(out)
table.insert(report, string.format("sheet.png: %d frames, %d with pixels (%d layer(s))", n, drawn, #main))
if #front > 0 then
  local fsheet, fdrawn = sheetOf(front)
  fsheet:saveAs(fs.joinPath(cfg.pack, "front.png"))
  table.insert(report, string.format("front.png: %d with pixels (%d layer(s))", fdrawn, #front))
end
table.insert(report, "into " .. cfg.pack)

if cfg.check and cfg.check ~= "" then
  -- cmd /c drops the outermost pair of quotes: one more pair keeps each quoted path whole
  local p = io.popen('"' .. cfg.check .. ' 2>&1"')
  if p then
    table.insert(report, "")
    for line in p:lines() do table.insert(report, line) end
    p:close()
  end
end
table.insert(report, "")
table.insert(report, "In game: dev panel, the skin's rows, \"Reload from the file\".")

if app.isUIAvailable then
  local d = Dialog{ title = "GK2 Export skin: done" }
  for _, line in ipairs(report) do d:label{ text = line } d:newrow() end
  d:button{ text = "OK" }
  d:show()
else
  print(table.concat(report, "\n"))
end
