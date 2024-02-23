 --[[
  Description: party farm trial
  Author: McVaxius
]]

--[[
****************
**INSTRUCTIONS**
****************
This is just kind of proof of concept farming script for easy duties that vbm AI mode can solve.

******************
**REQUIRED REPOS**
******************
https://love.puni.sh/ment.json
https://puni.sh/api/repository/croizat
https://puni.sh/api/repository/veyn
https://raw.githubusercontent.com/a08381/Dalamud.SkipCutscene/dist/repo.json

******************
**OPTIONAL REPOS**
******************
https://puni.sh/api/repository/kawaii
https://puni.sh/api/repository/taurenkey
https://plugins.carvel.li
https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json

********************
**REQUIRED PLOGONS**
********************
*Something Need Doing (croizat fork)
*Pandora
*Rotation solver
*Simpletweaks
*YesAlready
*TextAdvance
*Cutscene Skip
*vnavmesh OR visland - decide which you want to use in the .ini file

*****************
**Plogon config**
*****************
simpletweaks -> turn on maincommand
simpletweaks -> (optional) turn on autoequip command and set it to /equipguud
pandora -> turn on auto interact on pandora set distance to 5 dist 5 height
bossmod -> self configured with this script
vnavmesh/visland -> nothing just leave it alone unless you know what you are doing
something need doing -> go to options and disable SND targeting
rotation solver -> self configured with this script
lazyloot -> optional you decide. set your lazyloot to /fulf need/green/pass/off etc
yesalready -> setup a leave from instance.. the first time you exit you can auto add with teh circular plus icon
textadvance -> auto on and add your char to the list
Final Fantasy XIV Itself -> Preselect port decumana in the duty finder menu on the designated party leader then close the window, OR
															Do it in the duty support window if thats the option you are choosing
Final Fantasy XIV Itself -> Character -> Targeting -> Ground Targeting -> Unlocked --*make this a pcall later but leave a note somewhere to user so they can change it if they wanna aim imanually later.
--------------
SCRIPT CONFIG
--------------
the reason we use an ini file is so you can have many different characters configured separately and also update the script without having to edit it at all. (aside from copy and paste)
Read the ini file - it should self explain the variables.  and it respects comments. just not same-line comments
the ini file goes into the folder you can see below
\\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\
you can change that to a different folder if you wish. just find the appropriate line of code in here to do that.
to use this find the arbitraryduty_McVaxius.ini file and rename it to arbitraryduty_Yourcharfirstlast.ini   notice no spaces.
so if your character is named Pomelo Pup'per then you would call the .ini file   arbitraryduty_PomeloPupper.ini
just remember it will strip spaces and apostrophes
--------------
enjoy
****END OF INSTRUCTIONS****
misc side note. the version number at end of file is meaningless. its just there so i can see if i copy pasted successfully over remote control of another PC ;p clipboard is finicky
]]


--todo
--convert all variable sanity (type) checks into a generic function to reduce code clutter
--test and start building the spread marker checker so we can farm level 90 duties with a premade in preparation for vnavmesh caching :~D

--we configure everything in a ini file.
--this way we can just copy paste the scripts and not need to edit the script per char

-- Function to load variables from a file
function loadVariablesFromFile(filename)
    local file = io.open(filename, "r")

    if file then
        for line in file:lines() do
            -- Remove single-line comments (lines starting with --) before processing
            line = line:gsub("%s*%-%-.*", "")
            
            -- Extract variable name and value
            local variable, value = line:match("(%S+)%s*=%s*(.+)")
            if variable and value then
                -- Convert the value to the appropriate type (number or string)
                value = tonumber(value) or value
                _G[variable] = value  -- Set the global variable with the extracted name and value
            end
        end

        io.close(file)
    else
        print("Error: Unable to open file " .. filename)
    end
end


-- Specify the path to your text file
-- forward slashes are actually backslashes.
--to use this find the arbitraryduty_McVaxius.ini file and rename it to arbitraryduty_Yourcharfirstlast.ini   notice no spaces.
--so if your character is named Pomelo Pup'per then you would call the .ini file   arbitraryduty_PomeloPupper.ini
--also be sure to update the folder name as per your preference
--just remember it will strip spaces and apostrophes
tempchar = GetCharacterName()
--tempchar = tempchar:match("%s*(.-)%s*") --remove spaces at start and end only
tempchar = tempchar:gsub("%s", "")  --remove all spaces
tempchar = tempchar:gsub("'", "")   --remove all apostrophes
local filename = os.getenv("appdata").."\\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\arbitraryduty_"..tempchar..".ini"

-- Call the function to load variables from the file
loadVariablesFromFile(filename)

-- Now you can use the variables in your Lua script
yield("/cl")
yield("/echo Character:"..GetCharacterName())
yield("/echo Filename+path:"..filename)
yield("/echo char_snake:"..char_snake)
yield("/echo enemy_snake:"..enemy_snake)
yield("/echo repeat_trial:"..repeat_trial)
yield("/echo repeat_type:"..repeat_type)
yield("/echo partymemberENUM:"..partymemberENUM)
yield("/echo dont_lockon:"..dont_lockon)
yield("/echo lockon_wait:"..lockon_wait)
yield("/echo snake_deest:"..snake_deest)
yield("/echo enemy_deest:"..enemy_deest)
yield("/echo meh_deest:"..meh_deest)
yield("/echo enemeh_deest:"..enemeh_deest)
yield("/echo limituse:"..limituse)
yield("/echo limitpct:"..limitpct)
yield("/echo limitlevel:"..limitlevel)
yield("/echo movetype:"..movetype)

--cleanup the variablesa  bit.  maybe well lowercase them later toohehe.
char_snake = char_snake:match("^%s*(.-)%s*$"):gsub('"', '')
enemy_snake = enemy_snake:match("^%s*(.-)%s*$"):gsub('"', '')

--see the .ini file for explanation on settings
--more comments at the end

--counter init
local repeated_trial = 0

--our current area
local we_are_in = 666
local we_were_in = 666

--placeholder for target location
local currentLocX = 1
local currentLocY = 1
local currentLocZ = 1

--our current location
local mecurrentLocX = GetPlayerRawXPos(tostring(1))
local mecurrentLocY = GetPlayerRawYPos(tostring(1))
local mecurrentLocZ = GetPlayerRawZPos(tostring(1))

--our current area
local we_are_in = 666
local we_were_in = 666

--declaring distance var
local dist_between_points = 500

local neverstop = true
local i = 0

local we_are_spreading = 0 --by default we aren't spreading

--duty specific vars
local dutycheck = 0
local dutycheckupdate = 1

--arbitrary duty solver
local dutyloaded = 0
local dutytoload = "buttcheeks"
local doodie = {} --initialize table for waypoints
local whereismydoodie = 1 --position in doodie table

local function distance(x1, y1, z1, x2, y2, z2)
    return math.sqrt((x2 - x1)^2 + (y2 - y1)^2 + (z2 - z1)^2)
end

local function limitbreak()
	if limituse == 1 then --are we a limit break user? we will only trigger via script if we are a dps. however that value is pulled from the ini
		local which_one = 666 --pointless variable init
		which_one = GetClassJobId()
		if type(which_one) ~= "number" then  --error trap variable type because we dont like SND pausing
			which_one = 9000 --invalid job placeholder
		end
		local GetLimoot = 0 --init lb value. its 10k per 1 bar
		GetLimoot = GetLimitBreakCurrentValue()
		if type(GetLimoot) ~= "number" then  --error trap variable type because we dont like SND pausing
			GetLimoot = 0 --well its 0 if its 0
		end
		local local_teext = "\"Limit Break\""
		--check the target life %
		if type(GetTargetHPP()) == "number" and GetTargetHPP() < limitpct then
			--seems like max lb is 1013040 when ultimate weapon buffs you to lb3 but you only have 30k on your bar O_o
			--anyways it will trigger if lb3 is ready or when lb2 is max and it hits lb2
			if (GetLimoot == (GetLimitBreakBarCount() * GetLimitBreakBarValue())) or GetLimoot > 29999 then
				yield("/rotation cancel")		
				yield("/echo Attempting "..local_teext)
				yield("/ac "..local_teext)
			end
			if GetLimoot < GetLimitBreakBarCount() * GetLimitBreakBarValue() then
				yield("/rotation auto")		
			end
			--yield("/echo limitpct "..limitpct.." HPP"..GetTargetHPP().." HP"..GetTargetHP().." get limoot"..GetLimitBreakBarCount() * GetLimitBreakBarValue()) --debug line
		end
	end
end

local function do_we_spread()
    did_we_find_one = 0
	--need to start getting the names of the ones that vbm doesn't resolve and add them here
	--now we iterate through the list of possible entities
    for _, entity_name in ipairs(spread_marker_entities) do
         if GetDistanceToObject(entity_name) < 40 then
             did_we_find_one = 1
             break --escape from loop we found one!!!
         end
    end
	if did_we_find_one == 1 then
		--return true
		we_are_spreading = 1 --indicate to the follow functions that we are spreading and not to try and do stuff
		spread_em(5) --default 5 "distance" movement for now IMPROVE LATER with multi variable array with distances for each spread marker? and maybe some actual math because 1,1 is actually 1.4 distance from origin.
		did_we_find_one = 0
	end
	if did_we_find_one == 0 then
		--return false
		--do nothing ;o
		we_are_spreading = 0 -- we aren't spreading
	end
end

local function spread_em(distance)
    local deltaX, deltaY
	deltaX = mecurrentLocX
	deltaY = mecurrentLocY
    if partymemberENUM == 1 then
        deltaX, deltaY = 0, -distance  -- Move up
    elseif partymemberENUM == 2 then
        deltaX, deltaY = distance, 0  -- Move right
    elseif partymemberENUM == 3 then
        deltaX, deltaY = 0, distance  -- Move down
    elseif partymemberENUM == 4 then
        deltaX, deltaY = -distance, 0  -- Move left
    elseif partymemberENUM == 5 then
        deltaX, deltaY = distance, -distance  -- Move up right
    elseif partymemberENUM == 6 then
        deltaX, deltaY = distance, distance  -- Move down right
    elseif partymemberENUM == 7 then
        deltaX, deltaY = -distance, distance  -- Move down left
    elseif partymemberENUM == 8 then
        deltaX, deltaY = -distance, -distance  -- Move up left
    else
        yield("/echo Invalid direction - check partymemberENUM in your .ini file")
    end
    --time to do the movement!
	yield("/"..movetype.." moveto "..deltaX.." "..deltaY.." "..mecurrentLocZ)
	yield("/wait 5")
end

local function setdeest()
	if currentLocX and currentLocY and currentLocZ and mecurrentLocX and mecurrentLocY and mecurrentLocZ then
		dist_between_points = distance(currentLocX, currentLocY, currentLocZ, mecurrentLocX, mecurrentLocY, mecurrentLocZ)
		-- dist_between_points will contain the distance between the two points
		--yield("/echo Distance between char_snake and point 1: " .. dist_between_points)
	else
		yield("/echo Failed to retrieve coordinates for one or both points.")
		dist_between_points = 500 -- default value haha
	end
end

--duty functions
local function load_duty_data()
	--tablestructure: visland OR vnavmesh (0, 1), x,y,z, wait at waypoint (seconds for /wait x) before /stop visland/vnavmesh, distance to be over to end waypoint if it was an area transition. default 0
	local file_path = os.getenv("appdata").."\\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\"..dutytoload
	local doodies = {}  -- Initialize an empty table
	if dutytoload ~= "buttcheeks" then
		yield("/echo Attempting to load -> "..file_path)
	    local file = io.open(file_path, "r")  -- Open the file in read mode
		if file then
			for line in file:lines() do  -- Iterate over each line in the file
				if line ~= "" then  -- Skip empty lines
					local row = {}  -- Initialize an empty table for each row
					for value in line:gmatch("[^,]+") do  -- Split the line by comma
						table.insert(row, value)  -- Insert each value into the row table
						--yield("/echo value "..value)
					end
					table.insert(doodies, row)  -- Insert the row into the main table
				end
			end
			file:close()  -- Close the file
			dutyloaded = 1
			return doodies  -- Return the loaded table
		else
			yield("/echo Error: Unable to open table file '" .. file_path .. "'")
			return nil
		end
	end
end

local function getmovetype(wheee)
	local funtimes = "vnavmesh"
	yield("/echo DEBUG get move type for muuvtype -> "..wheee)
	if tonumber(wheee) == 0 then
		funtimes = "visland"
		yield("/echo DEBUG get move type for muuvtype -> SHOULD BE VISLAND")
	end
	return funtimes
end

local function arbitrary_duty()
	if type(GetZoneID()) == "number" and GetZoneID() == 1044 and GetCharacterCondition(1) then --Praetorium
		--will spam 2 and auto lockon. eventually clear the garbage
		if string.len(GetTargetName()) > 0 then
			yield("/lockon on") --need this for various stuff hehe.
		end
		if GetCharacterCondition(4) == true and string.len(GetTargetName()) > 0 then
			yield("/automove")
			yield("/send w")
			yield("/wait 0.3")
			yield("/send q")
			yield("/send KEY_2")
			yield("/wait 0.5")
			yield("/send e")
			yield("/wait 0.3")
			yield("/send KEY_2")
			yield("/wait 0.5")
			yield("/send KEY_2")
			yield("/wait 0.5")
			yield("/send w")
			yield("/wait 0.3")
			yield("/send KEY_2")
			yield("/wait 0.5")
			yield("/wait 0.3")
			yield("/send w")
			yield("/send KEY_2")
			yield("/wait 0.5")
		end
		dutytoload = "arbitraryduty_Praetorium.duty"
	end
	--*if we die:
		--*wait 20 seconds then accept respawn (new counter var.. just in case we get a rez
		--*set waypoint to 1 so the whole thing can start over again. walk of shame back to boss.
	--*resume mode - if there is a shortcut available:
		--*search waypoint table for a waypoint closest to where we are after entering shortcut
		--*set the waypoint to that closest waypoint and resume
	
	--if we are in a duty
	if GetCharacterCondition(34) == true then
		--if we haven't loaded a duty file. load it
		if dutyloaded == 0 and dutytoload ~= "buttcheeks" then --we take a doodie from a .duty file
			doodie = load_duty_data()
			yield("/echo Waypoints loaded for this area -> "..#doodie)
		end
		if whereismydoodie < (#doodie+1) then
			local muuvtype = "wheeeeeeeeeeeeeeeeeeeee"
			local tempdist = distance(GetPlayerRawXPos(),GetPlayerRawYPos(),GetPlayerRawZPos(),doodie[whereismydoodie][2],doodie[whereismydoodie][3],doodie[whereismydoodie][4])
			--if we are in combat stop navmesh/visland
			if GetCharacterCondition(26) == true then
				yield("/visland stop")
				yield("/vnavmesh stop")
				yield("/automove off")
				yield("/echo stopping nav cuz in combat")
			end
			if GetCharacterCondition(26) == false then
				muuvtype = getmovetype(doodie[whereismydoodie][1]) --grab the movetype from the waypoint
				yield("/"..muuvtype.." moveto "..doodie[whereismydoodie][2].." "..doodie[whereismydoodie][3].." "..doodie[whereismydoodie][4]) --move to the x y z in the waypoint
				yield("/automove off")
				yield("/echo starting nav cuz not in combat, WP -> "..whereismydoodie.." navtype -> "..muuvtype.." nav code -> "..doodie[whereismydoodie][1].."  current dist to objective -> "..tempdist)
			end
			--*if we are <? yalms from waypoint, wait x seconds then stop visland/vnavmesh
			if tempdist < 2 or (tonumber(doodie[whereismydoodie][6]) > 0 and tempdist > tonumber(doodie[whereismydoodie][6]))then
				yield("/echo Onto the next waypoint! Current WP completed --> "..whereismydoodie)
				yield("/wait "..doodie[whereismydoodie][5])
				whereismydoodie = whereismydoodie + 1
				yield("/automove off")
				yield("/visland stop")
				yield("/vnavmesh stop")
			end	
		end
	end
end
	--[[
	--for followers -- probably delete once we get the above part working.
	--i heard you liked if statements so i nested an if statement in your if statement that i nested an if statement in your if statement that i ahahahah. i could have done 5-6 more nested if 
	if type(GetZoneID()) == "number" and GetZoneID() == 1048 and char_snake ~= "party leader"  and char_snake ~= "no follow" then
		--if we not in combat. target a terminal
		if GetCharacterCondition(34) == true and GetCharacterCondition(26) == false then --if we aren't in combat and in praetorium
		--GetTargetName()~="Magitek Terminal"--if we are within 5 yalms of a magitek terminal
			if GetDistanceToObject("Magitek Terminal") < 5 then
				if GetDistanceToObject("Magitek Terminal") < 5 then
					if GetDistanceToObject(char_snake) > 10 then
						yield("/target terminal")
						yield("/wait 1")
						yield("/lockon on")
						yield("/automove on")
						yield("/wait 3")
					end
				end
			end	
		end
	end
	]]

local function porta_decumana()
		if type(GetZoneID()) == "number" and GetZoneID() == 1048 then
			--check the area number before doing ANYTHING this breaks other areas.
			--porta decumana ultima weapon orbs in phase 2 near start of phase
			--very hacky kludge until movement isn't slidy
			--nested ifs because we don't want to get locked into this
			phase2 = distance(-692.46704, -185.53157, 468.43414, mecurrentLocX, mecurrentLocY, mecurrentLocZ)
			if dutycheck == 0 and dutycheckupdate == 1 and phase2 < 40 then
				--we in phase 2 boyo
				dutycheck = 1
			end
			mecurrentLocX = GetPlayerRawXPos(tostring(1))
			mecurrentLocY = GetPlayerRawYPos(tostring(1))
			mecurrentLocZ = GetPlayerRawZPos(tostring(1))
			if dutycheckupdate == 1 and dutycheckupdate == 1 and type(GetDistanceToObject("Magitek Bit")) == "number" and GetDistanceToObject("Magitek Bit") < 50 then
				dutycheck = 0 --turn off this check
				dutycheckupdate = 0
			end
			if dutycheck == 1 and phase2 < 40 and GetDistanceToObject("The Ultima Weapon") < 40 then
				if partymemberENUM == 1 then
					yield("/"..movetype.." moveto -692.46704 -185.53157 468.43414")
				end
				if partymemberENUM == 2 then
					yield("/"..movetype.." moveto -715.5604 -185.53159 468.4341")
				end
				if partymemberENUM == 3 then
					yield("/"..movetype.." moveto -715.5605 -185.53157 491.5273")
				end
				if partymemberENUM == 4 then
					yield("/"..movetype.." moveto -692.46704 -185.53159 491.52734")
				end
				--yield("/wait 5") -- is this too long? we'll see!  maybe this is bad
			end
		--[[
		--on hold for now until movement isnt slide-time-4000
				if type(GetDistanceToObject("Aetheroplasm")) == "number" then
		--			if GetObjectRawXPos("Aetheroplasm") > 0 then
					if GetDistanceToObject("Aetheroplasm") < 20 then
						--yield("/wait 1")
						yield("/echo Porta Decumana ball dodger distance to random ball: "..GetDistanceToObject("Aetheroplasm"))
						yield("/visland stop")
						yield("/vnavmesh stop")
						--yield("/vbm cfg AI Enabled false")
						while type(GetDistanceToObject("Aetheroplasm")) == "number" and GetDistanceToObject("Aetheroplasm") < 20 do
							if partymemberENUM == 1 then
								yield("/visland moveto -692.46704 -185.53157 468.43414")
							end
							if partymemberENUM == 2 then
								yield("/visland moveto -715.5604 -185.53159 468.4341")
							end
							if partymemberENUM == 3 then
								yield("/visland moveto -715.5605 -185.53157 491.5273")
							end
							if partymemberENUM == 4 then
								yield("/visland moveto -692.46704 -185.53159 491.52734")
							end
							yield("/wait 5")			
						end
						yield("/visland stop")
						yield("/vnavmesh stop")
						--yield("/vbm cfg AI Enabled true")
					end
				end	
			]]
	end
end

yield("/echo starting.....")
yield("/echo Turning AI On")
yield("/wait 0.5")
yield("/vbm cfg AI Enabled true")
yield("/echo Turning AI Self Follow On")
yield("/wait 0.5")
yield("/vbmai on")

while repeated_trial < (repeat_trial + 1) do
	--yield("/echo get limoooot"..GetLimitBreakCurrentValue().."get limootmax"..GetLimitBreakBarCount() * GetLimitBreakBarValue()) --debug for hpp. its bugged atm 2024 02 12 and seems to return 0

	yield("/targetenemy") --this will trigger RS to do stuff. this is also kind of spammy in the text box. how do i fix this so its not spammy?
	--some other spams.
	--the command "targetnenemy" is unavailable at this time
	--unable to execute command while occupied
	--unable to execute command while mounted
	if enemy_snake ~= "nothing" then --check if we are forcing a target or not
		yield("/target "..enemy_snake) --this will trigger RS to do stuff.
		currentLocX = GetTargetRawXPos()
		currentLocY = GetTargetRawYPos()
		currentLocZ = GetTargetRawZPos()
	end
	if char_snake ~= "no follow" and char_snake ~= "party leader" then --follow mode loc
		currentLocX = GetPlayerRawXPos(tostring(char_snake))
		currentLocY = GetPlayerRawYPos(tostring(char_snake))
		currentLocZ = GetPlayerRawZPos(tostring(char_snake))
	end
	--yield("Target x y z "..currentLocX.." "..currentLocY.." "..currentLocZ)
	mecurrentLocX = GetPlayerRawXPos(tostring(1))
	mecurrentLocY = GetPlayerRawYPos(tostring(1))
	mecurrentLocZ = GetPlayerRawZPos(tostring(1))
	
	limitbreak() --by the power of hydaelyn i smite thee
	
	if GetCharacterCondition(34)==false and char_snake == "party leader" then --if we are not in a duty --try to restart duty
		yield("/visland stop")
		yield("/vnavmesh stop")
		yield("/wait 2")
		yield("/echo We seem to be outside of the duty.. let us enter!")
		yield("/wait 15")	
		if repeat_type == 0 then --4 Real players (or scripts haha) using duty finder
			yield("/finder")
			yield("/echo attempting to trigger duty finder")
			yield("/pcall ContentsFinder true 12 0")
		end
		if repeat_type == 1 then --just you using duty support
			--("/pcall DawnStory true 20") open the window.. how?
			--we use simpletweaks
			yield("/maincommand Duty Support")
			yield("/wait 2")
			yield("/echo attempting to trigger duty support")
			yield("/pcall DawnStory true 11 0") --change tab to first tab
			yield("/pcall DawnStory true 12 35")--select port decumana
			yield("/wait 2")
			yield("/pcall DawnStory true 14") --START THE DUTY
		end
	
		yield("/echo Total Trials triggered for "..char_snake..": "..repeated_trial)
		yield("/wait 10")
	end

	if GetCharacterCondition(26)==false and GetCharacterCondition(34)==true then --if we are not in combat AND we are in a duty then we will look for an exit or shortcut
		--we dont need to manually exit. automaton can do that now
		--if type(GetDistanceToObject("Exit")) == "number" and GetDistanceToObject("Exit") < 25 then
		--	yield("/target exit")
		--end
		if type(GetDistanceToObject("Shortcut")) == "number" and GetDistanceToObject("shortcut") < 25 then
			yield("/target Shortcut")
		end
		yield("/wait 0.1")
		if GetTargetName()=="Exit" or GetTargetName()=="Shortcut" then --get out ! assuming pandora setup for auto interaction
			local minicounter = 0
			if NeedsRepair(99) then
				yield("/wait 10")
				while not IsAddonVisible("Repair") do
				  yield("/generalaction repair")
				  yield("/wait 1")
				  minicounter = minicounter + 1
				  if minicounter > 20 then
					minicounter = 0
					break
				  end
				end
				yield("/pcall Repair true 0")
				yield("/wait 0.1")
				if IsAddonVisible("SelectYesno") then
				  yield("/pcall SelectYesno true 0")
				  yield("/wait 1")
				end
				while GetCharacterCondition(39) do yield("/wait 1") end
				yield("/wait 1")
				yield("/pcall Repair true -1")
				  minicounter = minicounter + 1
				  if minicounter > 20 then
					minicounter = 0
					break
				  end
			end
			yield("/visland stop")
			yield("/wait 0.1")
			yield("/vnavmesh stop")
			yield("/wait 0.1")
			yield("/lockon on")
			yield("/automove on")
			yield("/wait 10")
		end
	end

	--test dist to the intended party leader
	if GetCharacterCondition(34)==true then --if we are in a duty
		--check for spread_marker_entities
		do_we_spread() --single target spread marker handler function
		--call the waypoint system
		arbitrary_duty()
		--duty specific stuff
		if type(we_are_in) == "number" and we_are_in == 1048 then --porta decumana
			--yield("/echo Decumana Check!")
			porta_decumana()
		end
		--regular movement to target
		if char_snake ~= "no follow" and char_snake ~= "party leader" and enemy_snake == "nothing" and we_are_spreading == 0 then --close gaps to party leader only if we are on follow mode
			setdeest()
			if dist_between_points > snake_deest and dist_between_points < meh_deest then
					--yield("/visland moveto "..currentLocX.." "..currentLocY.." "..currentLocZ) --sneak around when navmesh being weird
					yield("/"..movetype.." moveto "..currentLocX.." "..currentLocY.." "..currentLocZ)
					--yield("/echo vnavmesh moveto "..math.ceil(currentLocX).." "..math.ceil(currentLocY).." "..math.ceil(currentLocZ))
					--DEBUG echo
					--yield("/echo player follow distance between points: "..dist_between_points.." enemy deest"..enemy_deest.." char deest :"..snake_deest)
			end
		end
		if enemy_snake ~= "nothing" and dutycheck == 0 and we_are_spreading == 0 then --close gaps to enemy only if we are on follow mode
			setdeest()
			if dist_between_points > enemy_deest and dist_between_points < enemeh_deest then
					--yield("/visland moveto "..currentLocX.." "..currentLocY.." "..currentLocZ)
					yield("/"..movetype.." moveto "..currentLocX.." "..currentLocY.." "..currentLocZ)
					--yield("/echo vnavmesh moveto "..math.ceil(currentLocX).." "..math.ceil(currentLocY).." "..math.ceil(currentLocZ))
			end
		end
		--yield("/echo distance between points: "..dist_between_points.." snake_deest"..snake_deest.." meh_deest :"..meh_deest)
	end
	
	--test dist to the intended party leader
	i = 0
	if GetCharacterCondition(28)==true then --if we are bound by qte
		if GetCharacterCondition(29)==true then --if we are bound by qte
			while i < 150 do
				i = i + 1
				yield("/send SPACE")
				yield("/send SPACE")
				yield("/wait 0.1")
				if GetCharacterCondition(28)==false then --if we are not bound by qte get out of the space bar spamming so we can resume following or whatever
					i = 150
				end
			end
		end
	end
	yield("/wait 1")

	--this part will be deprecated soon once there is some kind of autobuilding
	--check if we chagned areas or just wait as normal
	we_are_in = GetZoneID() --where are we?
	if type(we_are_in) ~= "number" then
		we_are_in = we_were_in --its an invalid type so lets just default it and wait 10 seconds
		yield("/echo invalid type for area waiting 10 seconds")
		yield("/wait 10")
	end
	if we_are_in ~= we_were_in then
		yield("/"..movetype.." stop")
		yield("/wait 1")
		yield("/"..movetype.." stop")
		yield("/wait 1")
		--if GetCharacterCondition(34) == true and char_snake ~= "no follow" then --only trigger rebuild in a duty and when following a party leader
		if GetCharacterCondition(34) == true then --only trigger rebuild in a duty and when following a party leader
			if char_snake == "party leader" then
			    yield("/vbmai on")
				repeated_trial = repeated_trial + 1
			end
		end
		yield("/echo trial has begun!")
		--reset duty specific stuff. can make smarter checks later but for now just set the duty related stuff to 0 so it doesn't get "in the way" of stuff if you aren't doing that specific duty.
		dutycheck = 0 --by default we aren't going to stop things because we are in a duty
		dutycheckupdate = 1 --sometimes we don't want to update dutycheck because we reached phase 2 in a fight.
		we_were_in = we_are_in --record this as we are in this area now
	end
	if GetCharacterCondition(34) == true and GetCharacterCondition(26) == false and GetTargetName()~="Exit" then --if we aren't in combat and in a duty
		--repair snippet stolen from https://github.com/Jaksuhn/SomethingNeedDoing/blob/master/Community%20Scripts/Gathering/DiademReentry_Caeoltoiri.lua
		yield("/equipguud")
		yield("/vbmai on")
		yield("/rotation auto")
		--only party leader will do cd 5 because otherwise its spammy
		if char_snake == "party leader" then
			yield("/cd 5")
		end
		yield("/send KEY_1")
		--yield("/wait 10")
		dutycheck = 0 --by default we aren't going to stop things because we are in a duty
		dutycheckupdate = 1 --sometimes we don't want to update dutycheck because we reached phase 2 in a fight.
	end
	--if we arent in a duty - reset some duty stuff
	if GetCharacterCondition(34) == false then
		dutyloaded = 0
		dutytoload = "buttcheeks"
		whereismydoodie = 1
	end
end

--/xldata object table, vbm debug, automaton debug
--eg
--17BB974B6D0:40000B89[38] - BattleNpc - Aetheroplasm - X-692.46704 Y-185.53157 Z468.43414 D9 R-0.7854581 - Target: E0000000
--17BB974E650:40000B8C[40] - BattleNpc - Aetheroplasm - X-715.5604 Y-185.53159 Z468.4341 D19 R0.78536224 - Target: E0000000
--17BB97515D0:40000B8A[42] - BattleNpc - Aetheroplasm - X-715.5605 Y-185.53157 Z491.5273 D21 R2.3561823 - Target: E0000000
--17BB9754550:40000B8B[44] - BattleNpc - Aetheroplasm - X-692.46704 Y-185.53159 Z491.52734 D12 R-2.3562784 - Target: E0000000

--v123321