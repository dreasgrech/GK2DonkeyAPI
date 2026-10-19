-- GK2: copy the picture on the current layer and frame to other frames, each copy placed where that frame's own
-- picture sits (lined up by its bottom centre, or its top left). For a part drawn once and moved per frame: redraw
-- the crown in one frame, then send it to the rest without losing where each frame has it. Frames with nothing on
-- this layer stay empty (the part is meant to be absent there). One undo step.

local spr, layer, cel = app.sprite, app.layer, app.cel
local function say(msg)
  if app.isUIAvailable then app.alert{ title = "GK2 Part to all frames", text = msg } else print(msg) end
end
if not spr or not layer or layer.isGroup then return say("Select a layer (not a group) first.") end
if not cel or cel.image:isEmpty() then return say("The current frame has nothing on this layer to copy.") end
if not layer.isEditable then return say("The layer is locked.") end

local function tag_of(frameNumber)
  for _, t in ipairs(spr.tags) do
    if frameNumber >= t.fromFrame.frameNumber and frameNumber <= t.toFrame.frameNumber then return t end
  end
end

local choice = { scope = "This tag", anchor = "Bottom centre" }
if app.isUIAvailable then
  local t = tag_of(app.frame.frameNumber)
  local d = Dialog{ title = "GK2 Part to all frames" }
  d:label{ text = "Copy layer \"" .. layer.name .. "\" of frame " .. app.frame.frameNumber .. " to:" }
  d:combobox{ id = "scope", option = "This tag",
              options = { "This tag", "Selected frames", "All frames" } }
  d:label{ text = t and ("(this tag: " .. t.name .. ")") or "(this frame has no tag: use another choice)" }
  d:combobox{ id = "anchor", label = "Line up by", option = "Bottom centre",
              options = { "Bottom centre", "Top left" } }
  d:button{ id = "ok", text = "Copy" }
  d:button{ id = "cancel", text = "Cancel" }
  d:show()
  if not d.data.ok then return end
  choice = d.data
end

local targets = {}
if choice.scope == "All frames" then
  for i = 1, #spr.frames do table.insert(targets, i) end
elseif choice.scope == "Selected frames" then
  for _, f in ipairs(app.range.frames) do table.insert(targets, f.frameNumber) end
else
  local t = tag_of(app.frame.frameNumber)
  if not t then return say("This frame is in no tag.") end
  for i = t.fromFrame.frameNumber, t.toFrame.frameNumber do table.insert(targets, i) end
end

-- the source, trimmed to what is drawn
local b = cel.image:shrinkBounds()
local part = Image(b.width, b.height, spr.colorMode)
part:drawImage(cel.image, Point(-b.x, -b.y))

local function anchor(c, w, h)
  local cb = c.image:shrinkBounds()
  local x, y = c.position.x + cb.x, c.position.y + cb.y
  if choice.anchor == "Top left" then return x, y end
  return x + cb.width // 2 - w // 2, y + cb.height - h
end

local done, skipped = 0, 0
app.transaction("GK2 Part to all frames", function()
  for _, i in ipairs(targets) do
    if i ~= cel.frameNumber then
      local c = layer:cel(i)
      if c and not c.image:isEmpty() then
        local x, y = anchor(c, b.width, b.height)
        spr:newCel(layer, i, part, Point(x, y))
        done = done + 1
      else
        skipped = skipped + 1
      end
    end
  end
end)
app.refresh()
say(string.format("Copied to %d frame(s); %d left alone (nothing there on this layer).", done, skipped))
