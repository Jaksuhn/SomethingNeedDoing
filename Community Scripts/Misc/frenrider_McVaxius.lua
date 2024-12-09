--script to kind of autofollow specific person in party when not in a duty by riding their vehicule
--meant to use when your ahh botting treasure maps or fates with alts, but playing main char manually :~D

--[[
*repos sorted by length of string.
https://plugins.carvel.li
https://love.puni.sh/ment.json
https://puni.sh/api/repository/veyn
https://puni.sh/api/repository/croizat
https://puni.sh/api/repository/taurenkey
https://raw.githubusercontent.com/SubZero0/Dalamud.SkipCutscene/dist/repo.json
https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json

*recommendation:
delete all the comments before the vars once you get it working properly

*requirements:
croizats SND - disable SND targeting in config
simpletweaks with targeting fix enabled
vnavmesh
visland
_functions.lua into your SND folder in %AppData%\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\_functions
you can find it here https://raw.githubusercontent.com/Jaksuhn/SomethingNeedDoing/master/Community%20Scripts/AutoRetainer%20Companions/RobustGCturnin/_functions.lua

*optional:
bring some gysahl greens
bring some food and configure it properly
discardhelper
cutscene skipper (MSQ roullette cutscenes)
lazyloot plugin (if your doing anything other than fates)
VBM/BMR (bmr has slash commands for following and more modules)
RSR

***Few annoying problems that still exist
*dont follow during combat unless non caster. will require bmr contemplation - seems bmr has contemplated it with distance command will consider adding new setting for this :~D

*how do we change instances #s maybe custom chat commands? lifestream /li # works. now to add nodetext scanning for group. also have to use target and lockon until lim fixes /li x without los
	this is insanely buggy and perhaps crashy.. nodetext scanning too fast will break things

*it still doesnt follow in some weird cases
	figured this out. if the party leader doesnt enter zone first, the other party members may fail to recalibrate to follow

*lazyloot is a toggle not on or off so you have to turn it on yourself

*we can't get synced level (yet) I managed to isolate the part with nodetext but its using weird special characters i dont know how to convert to real numbers
text = GetNodeText("_Exp", 3)
number = string.match(text, "%u%u%u%s*(.-)%s*EXP")
yield("/echo "..number)

reason is i wanted to smartly auto equip xp gear based on your current synced level.... :(

I will do it a bit later once i uhh. make a lookup table for this trash here:
0123456789


*some people have incorrect auto interaction settings in pandora.. next time im playing actively ill add a new config option for that to set or not set the interaction settings.
]]

--*****************************************************************
--************************* START INIZER **************************
--*****************************************************************
-- Purpose: to have default .ini values and version control on configs
-- Personal ini file
-- if you want to use my ini file serializer just copy form start of inizer to end of inizer and look at how i implemented settings and go nuts :~D
tempchar = GetCharacterName()
tempchar = tempchar:gsub("%s", "")  -- Remove all spaces
tempchar = tempchar:gsub("'", "")   -- Remove all apostrophes
filename_prefix = "frenrider_" -- Script name
filename = os.getenv("appdata").."\\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\"..filename_prefix..tempchar..".ini"
open_on_next_load = 0          -- Set this to 1 if you want the next time the script loads, to open the explorer folder with all of the .ini files

-- Ensure the version is always written to the file
-- VERSION VAR --
-- VERSION VAR --
-- VERSION VAR --
-- VERSION VAR --
vershun = 1                    -- Version number used to decide if you want to delete/remake the ini files on next load. useful if your changing party leaders for groups of chars or new version of script with fundamental changes
ini_check("version", vershun)
-- VERSION VAR --
-- VERSION VAR --
-- VERSION VAR --
-- VERSION VAR --
loadfiyel = os.getenv("appdata").."\\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\_functions.lua"
functionsToLoad = loadfile(loadfiyel)
functionsToLoad()
ini_check("version", vershun)

--*****************************************************************
--************************** END INIZER ***************************
--*****************************************************************

---------CONFIGURATION SECTION---------
fren = ini_check("fren", "Fren Name")  						--can be partial as long as its unique
fly_you_fools = ini_check("fly_you_fools", false)			--(fly and follow instead of mount and wait) usecase: you dont have multi seater of sufficient size, or you want to have multiple multiseaters with diff peopel riding diff ones.  sometimes frendalf doesnt want you to ride him and will ask you to ride yourself right up into outer space
fool_flier = ini_check("fool_flier", "Beast with 3 backs")	--if you have fly you fools as true, which beast shall you summon?
fulftype = ini_check("fulftype", "unchanged")				-- If you have lazyloot installed AND enabled (has to be done manually as it only has a toggle atm) can setup how loot is handled. Leave on "unchanged" if you don't want it to set your loot settings. Other settings include need, greed, pass
cling = ini_check("cling", 1) 								-- Distance to cling to fren when > bistance
force_gyasahl = ini_check("force_gyasahl", false) 	   		-- force gysahl green usage . maybe cause problems in towns with follow
clingtype = ini_check("clingtype", 0)						-- Clingtype, 0 = navmesh, 1 = visland, 2 = bmr follow leader, 3 = automaton autofollow, 4 = vanilla game follow
clingtypeduty = ini_check("clingtypeduty", 2)				-- do we need a diff clingtype in duties? use same numbering as above 
follow_in_combat = ini_check("follow_in_combat", 0)		-- 0 = dont follow the leader while in combat, 1 = follow the leader while in combat
maxbistance = ini_check("maxbistance", 50) 					-- Max distance from fren that we will actually chase them, so that we dont get zone hopping situations ;p
limitpct = ini_check("limitpct", -1)						-- What percentage of life on target should we use LB at. It will automatically use LB3 if that's the cap or it will use LB2 if that's the cap, -1 disables it
rotationplogon = ini_check("rotationplogon", "RSR")			-- Which plogon for rotations? valid options are BMR, VBM, RSR
autorotationtype = ini_check("autorotationtype", "xan")		-- If we are using BossMod rotation, what preset name shall we use? use "none" to manually configure it yourself.  keep in mind you have to make the rotation and name it in the first place.  "xan" is what i call mine
rotationtype = ini_check("rotationtype", "Auto")			-- What RSR type shall we use?  Auto or Manual are common ones to pick. if you choose "none" it won't change existing setting.
bossmodAI = ini_check("bossmodAI", "on")					-- do we want bossmodAI to be "on" or "off"
xpitem = ini_check("xpitem", 0)								-- xp item - attemp to equip whenever possible azyma_earring = 41081 btw, if this value is 0 it won't do anything
feedme = ini_check("feedme", 4650)							-- eatfood, in this case itemID 4650 which is "Boiled Egg", use simpletweaks to show item IDs it won't try to eat if you have 0 of said food item
feedmeitem = ini_check("feedmeitem", "Boiled Egg")			-- eatfood, in this case the item name. for now this is how we'll do it. it isn't pretty but it will work.. for now..
--feedmeitem = ini_check("feedmeitem", "Baked Eggplant<hq>")-- eatfood, in this case the item name add a <hq> at the end if you want it to be hq. for now this is how we'll do it. it isn't pretty but it will work.. for now..
timefriction = ini_check("timefriction", 1)					-- how long to wait between "tics" of the main loop? 1 second default. smaller values will have potential crashy / fps impacts.
formation = ini_check("formation", false)					-- Follow in formation? If false, then it will "cling"
						--[[
						Like this -> . so that 1 is the main tank and the party will always kind of make this formation during combat
						8	1	5
						3		2
						7	4	6
						]]
--[[
this next setting is a dud for now until i figure out how to do it
seems like we will need to use puppetmaster.... 
repo
https://github.com/Aspher0/PuppetMaster_Fork
pluginmaster
https://raw.githubusercontent.com/Aspher0/PuppetMaster_Fork/main/PuppetMaster.json

you can go to "Default settings"

Default Trigger (use regex)  (in this case "weeehehe")
(?i)\b(?:weeehehe)\s+(?:\((.*?)\)|(\w+))
Replacement
/li $1$2

--]]
--binstance = ini_check("binstance", "let us travel to instance")
				--[[ group instance change prefix, it will take " x" where x is the instance number as an argument, so you can setup qolbar keys with lines like this presumable
after changing instances, followers will /cl their chat windows
exmample qolbar for telling group to go instance 2
/mount
/p let us travel to isntance 2
/li 2
]]
-- mker = "cross" -- In case you want the other shapes. Valid shapes are triangle square circle attack1-8 bind1-3 ignore1-2

-----------CONFIGURATION END-----------
if open_on_next_load == 1 then
    openFolderInExplorer(os.getenv("appdata").."\\XIVLauncher\\pluginConfigs\\SomethingNeedDoing\\")
end
----------------
--INIT SECTION--
----------------
yield("/echo Starting fren rider")
--yield("/target \""..fren.."\"")
yield("/wait 0.5")
--yield("/mk cross <t>")

yield("/vbmai "..bossmodAI)
yield("/bmrai "..bossmodAI)

--rotation handling
function rhandling()
	if rotationplogon == "BMR" or rotationplogon == "VBM" then
		yield("/rotation cancel")  --turn off RSR
		if autorotationtype ~= "none" then
			yield("/vbm ar set "..autorotationtype)
			yield("/bmr ar set "..autorotationtype)
		end
	end
	if rotationplogon == "RSR" or rotationplogon == "VBM" then
		yield("/bmr ar toggle") --turn off Boss Mod
		if rotationtype ~= "none" then
			yield("/rotation "..rotationtype)
		end
	end
end
rhandling()

if fulftype ~= "unchanged" then
--turns out its just a toggle we can't turn it on or off purposefully
--	yield("/wait 0.5")
--	yield("/fulf on")
--	yield("/echo turning FULF ON!")
	yield("/echo Configuring FULF!")
	yield("/wait 1")
	yield("/fulf "..fulftype)
end
----------------
----INIT END----
----------------

--why is this so complicated? well because sometimes we get bad values and we need to sanitize that so snd does not STB (shit the bed)
function distance(x1, y1, z1, x2, y2, z2)
	if type(x1) ~= "number" then x1 = 0 end
	if type(y1) ~= "number" then y1 = 0 end
	if type(z1) ~= "number" then z1 = 0 end
	if type(x2) ~= "number" then x2 = 0 end
	if type(y2) ~= "number" then y2	= 0 end
	if type(z2) ~= "number" then z2 = 0 end
	zoobz = math.sqrt((x2 - x1)^2 + (y2 - y1)^2 + (z2 - z1)^2)
	if type(zoobz) ~= "number" then
		zoobz = 0
	end
    --return math.sqrt((x2 - x1)^2 + (y2 - y1)^2 + (z2 - z1)^2)
    return zoobz
end

function can_i_lb()
    local dpsJobs = {
        [2]  = true, [4]  = true, [5]  = true, [7]  = true, [20] = true, [22] = true, [24] = true,
        [25] = true, [26] = true, [27] = true, [29] = true, [30] = true, [31] = true,
        [34] = true, [35] = true, [38] = true, [39] = true, [41] = true
    }
    local joeb = GetClassJobId()
    return dpsJobs[joeb] or false
end

function am_i_ranged()
	--*stub to be sorted out later to deal with known issue(s)
end

-- Function to calculate the offset based on follower index and leader's facing direction
function calculateOffset(followerIndex, leaderRotation)
    -- Calculate offsetX and offsetY based on follower index and leader's facing direction
    -- Example: Adjust offsetX and offsetY based on formation layout and leader's facing direction
    local offsetX, offsetY = 0, 0
    -- Adjust offsetX and offsetY based on formation layout and leader's facing direction
    if followerIndex == 1 then
        -- Example: Adjust offsetX and offsetY for follower 1
        offsetX, offsetY = -1 * cling * 2, cling * 2
    elseif followerIndex == 2 then
        -- Example: Adjust offsetX and offsetY for follower 2
        offsetX, offsetY = 0, cling * 2
    elseif followerIndex == 3 then
        -- Example: Adjust offsetX and offsetY for follower 3
        offsetX, offsetY = cling * 2, cling * 2
    elseif followerIndex == 4 then
        -- Example: Adjust offsetX and offsetY for follower 4
        offsetX, offsetY = -1 * cling * 2, 0
    -- Handle other follower indexes similarly
    end
    
    -- Rotate the offset based on the leader's facing direction
    local rotatedOffsetX = offsetX * math.cos(leaderRotation) - offsetY * math.sin(leaderRotation)
    local rotatedOffsetY = offsetX * math.sin(leaderRotation) + offsetY * math.cos(leaderRotation)
    
    return rotatedOffsetX, rotatedOffsetY
end

function moveToFormationPosition(followerIndex, leaderX, leaderY, leaderZ, leaderRotation)
    -- Calculate the formation position based on follower index and leader's facing direction
    local offsetX, offsetY = calculateOffset(followerIndex, leaderRotation)
    
    -- Move the follower to the formation position relative to the leader
    local targetX = leaderX + offsetX
    local targetY = leaderY + offsetY
    
    -- Example: Move follower to the calculated position
    PathfindAndMoveTo(targetX, targetY, leaderZ, false)
end

function clingmove(nemm)
	if GetTargetName() == "Vault Door" then --we in a treasure map dungeon and need to click the door without following the fren
		yield("/interact")
		yield("/wait 5")
		return --don't do the other stuff until we have opened the door
	end
	--jump if we are mounted and below the leader by 10 yalms
	if (GetObjectRawYPos(nemm) - GetPlayerRawYPos()) > 9 and GetCharacterCondition(4) == true then
		yield("/gaction jump")
	end
	zclingtype = clingtype
	if GetCharacterCondition(34) == true then
		zclingtype = clingtypeduty --get diff clingtype in duties
	end
	allowmovement = 0  --dont allow movement by default
	if (follow_in_combat == 1 and GetCharacterCondition(26) == true) or GetCharacterCondition(26) == false then
		allowmovement = 1
	end
	if allowmovement == 1 then
		--navmesh
		if zclingtype == 0 then
			PathfindAndMoveTo(GetObjectRawXPos(nemm),GetObjectRawYPos(nemm),GetObjectRawZPos(nemm), false)
		end
		--visland
		if zclingtype == 1 then
			yield("/visland moveto "..GetObjectRawXPos(nemm).." "..GetObjectRawYPos(nemm).." "..GetObjectRawZPos(nemm)) --* verify this is correct later when we can load dalamud
		end
		--not bmr
		if zclingtype > 2 or zclingtype < 2 then
				yield("/bmrai follow "..GetCharacterName())
				yield("/bmrai followoutofcombat on")
				yield("/bmrai maxdistancetarget 2.6")
		end
		--bmr
		if zclingtype == 2 then
			bistance = distance(GetPlayerRawXPos(), GetPlayerRawYPos(), GetPlayerRawZPos(), GetObjectRawXPos(nemm),GetObjectRawYPos(nemm),GetObjectRawZPos(nemm))
			if bistance < maxbistance then
				yield("/bmrai followtarget on") --* verify this is correct later when we can load dalamud
				yield("/bmrai followoutofcombat on")
				yield("/bmrai follow "..nemm) 	  --* verify this is correct later when we can load dalamud
			end
			if bistance > maxbistance then --follow ourselves if fren too far away or it will do weird shit
				yield("/bmrai followtarget on") --* verify this is correct later when we can load dalamud
				yield("/bmrai followoutofcombat on")
				yield("/bmrai follow "..GetCharacterName()) 	  --* verify this is correct later when we can load dalamud
				yield("/echo too far! stop following!")
			end
		end
		if zclingtype == 3 then
			yield("/autofollow "..nemm)
		end
		if zclingtype == 4 then
			--we only doing this silly method out of combat
			if GetCharacterCondition(26) == false then
				--yield("/target "..nemm)
				yield("/target \""..nemm.."\"")
				yield("/follow")
			end
			--if we in combat and target is nemm we will clear it becuase that may bork autotarget from RSR
			if GetCharacterCondition(26) == true then
				if nemm == GetTargetName() then
					ClearTarget()
				end
			end
		end
	end
end

weirdvar = 1
shartycardinality = 2 -- leader
partycardinality = 2 -- me
fartycardinality = 2 --leader ui cardinality
autotosscount = 0
we_are_in = GetZoneID()
we_were_in = GetZoneID()
for i=0,7 do
	if GetPartyMemberName(i) == fren then
		shartycardinality = i
	end
	if GetPartyMemberName(i) == GetCharacterName() then
		partycardinality = i
	end
end
partycardinality = partycardinality + 1
--turns out the above is worthless and not what i wanted for pillion. but we keep it anyways in case we need the data for something.

countfartula = 2
function counting_fartula()
countfartula = 2 --redeclare dont worry its fine. we need this so we can do it later in the code for recalibration
	while countfartula < 9 do
		yield("/target <"..countfartula..">")
		yield("/wait 0.5")
		yield("/echo is it "..GetTargetName().."?")
		if GetTargetName() == fren then
			fartycardinality = countfartula
			countfartula = 9
			--yield("Aha... count fartula is -> "..fartycardinality)
		end
		countfartula = countfartula + 1
	end
end
counting_fartula() --we can call it before mounting because the order changes sometimes after a duty ends or after changing areas (AFTER a duty ends?) idk it was hard to recreate but this solves it.

--yield("Friend is party slot -> "..partycardinality.." but actually is ff14 slot -> "..fartycardinality)
yield("/echo Friend is party slot -> "..fartycardinality .. " Order of join -> "..partycardinality.." Fren Join order -> "..shartycardinality)
ClearTarget()

--bmr follow off. default state. slot1 is the runner of this script
--yield("/bmrai follow slot1")
yield("/bmrai follow slot1")
yield("/echo Beginning fren rider main loop")

xp_item_equip = 0 --counter
re_engage = 0 --counter
renav_check = 0

while weirdvar == 1 do
	--catch if character is ready before doing anything
	if IsPlayerAvailable() then
		if type(GetCharacterCondition(34)) == "boolean" and type(GetCharacterCondition(26)) == "boolean" and type(GetCharacterCondition(4)) == "boolean" then
			bistance = distance(GetPlayerRawXPos(), GetPlayerRawYPos(), GetPlayerRawZPos(), GetObjectRawXPos(fren),GetObjectRawYPos(fren),GetObjectRawZPos(fren))
			if bistance > maxbistance then --follow ourselves if fren too far away or it will do weird shit
				clingmove(GetCharacterName())
			end

			--if we in combat and target is <3 yalms dont nav anywhere.
			if GetCharacterCondition(26) == true and type(GetTargetName()) == "string" and string.len(GetTargetName()) > 1 then
				if distance(GetPlayerRawXPos(), GetPlayerRawYPos(), GetPlayerRawZPos(), GetObjectRawXPos(GetTargetName()),GetObjectRawYPos(GetTargetName()),GetObjectRawZPos(GetTargetName())) < 3 then
					yield("/vnav stop")
				end
			end

			--renav condition while in a duty. if we stuck for more than 10 seconds in place. renav damnit
			if GetCharacterCondition(4) == true and bistance > cling and GetCharacterCondition(34) == true then 
				renav_check = renav_check + 1
				if renav_check > 10 then
					renav_check = 0
					yield("/echo Gently checking nav")
					double_check_navGO(GetObjectRawXPos(GetCharacterName()), GetObjectRawYPos(GetCharacterName()), GetObjectRawZPos(GetCharacterName()))
				end
			end

			--dismount regardless of in duty or not
			if IsPartyMemberMounted(shartycardinality) == false and fly_you_fools == true and GetCharacterCondition(4) == true then
				--continually try to dismount
				--bmr follow off.
				yield("/bmrai follow slot1")
				yield("/ac dismount")
				yield("/wait 0.5")
				rhandling()
			end

			xp_item_equip = xp_item_equip + 1		
			if xp_item_equip > ((1/timefriction)) * 5 and xpitem > 0 and GetItemCountInContainer(xpitem,1000) ~= 1 then -- every 5 seconds try to equip xp item(s) if they aren't already equipped
					yield("/equipitem "..xpitem)
					xp_item_equip = 0
			end
			
			re_engage = re_engage + 1
			if re_engage > 2 then --every 3 seconds we will do rhandling() just to make sure we are attacking stuff if we aren't mounted.
				if GetCharacterCondition(4) == false then
					rhandling()
				end
				re_engage = 0
			end
			
			--Food check!
			statoos = GetStatusTimeRemaining(48)
			---yield("/echo "..statoos)
			if type(GetItemCount(feedme)) == "number" then
				if GetItemCount(feedme) > 0 and statoos < 300 then --refresh food if we are below 5 minutes left
					yield("/item "..feedmeitem)
					yield("/echo Attempting to eat "..feedmeitem)
				end
			end

			if GetCharacterCondition(34) == true then --in duty we might do some special things. mostly just follow the leader and let the ai do its thing.
				--bmr follow on
				--yield("/bmrai follow slot"..fartycardinality.."")
				--yield("/bmrai follow "..fren)
				--we will use clingmove not bmrai follow as it breaks pathing from that point onwards
				clingmove(fren)
				--allright im getting sick of pratorium. its time to do something.
				if type(GetZoneID()) == "number" and GetZoneID() == 1044 and GetCharacterCondition(4) then --Praetorium
					--if string.len(GetTargetName()) == 0 then
					TargetClosestEnemy()
					--end
					yield("/send KEY_2")
					flandom = getRandomNumber(1,3)
					if flandom == 1 then yield("/send E") end
					yield("/wait 0.5")
				end
			end
			if GetCharacterCondition(34) == false then  --not in duty  
				--SAFETY CHECKS DONE, can do whatever you want now with characterconditions etc			
				--movement with formation - initially we test while in any situation not just combat
				--check distance to fren, if its more than cling, then
	
				if formation == true and bistance < maxbistance then
					-- Inside combat and formation enabled
					local leaderX, leaderY, leaderZ = GetObjectRawXPos(fren), GetObjectRawYPos(fren), GetObjectRawZPos(fren)
					local leaderRotation = GetObjectRotation(fren)
					moveToFormationPosition(partycardinality, leaderX, leaderY, leaderZ, leaderRotation)
					yield("/wait 0.5")
				end
				--movement without formation
				if GetCharacterCondition(26) == true and formation == false then --in combat
					if formation == false then
						if bistance > cling and bistance < maxbistance then
						--yield("/target \""..fren.."\"")
							--PathfindAndMoveTo(GetObjectRawXPos(fren),GetObjectRawYPos(fren),GetObjectRawZPos(fren), false)
							clingmove(fren) --movement func
						end
						yield("/wait 0.5")
					end	
				end
				
				--we are limitbreaking all over ourselves
				if can_i_lb() == true and limitpct > -1 then
					GetLimoot = 0 --init lb value. its 10k per 1 bar
					GetLimoot = GetLimitBreakCurrentValue()
					if type(GetLimoot) ~= "number" then  --error trap variable type because we dont like SND pausing
						GetLimoot = 0 --well its 0 if its 0
					end
					local_teext = "\"Limit Break\""
					--check the target life %
					if type(GetTargetHPP()) == "number" and GetTargetHPP() < limitpct then
						--seems like max lb is 1013040 when ultimate weapon buffs you to lb3 but you only have 30k on your bar O_o
						--anyways it will trigger if lb3 is ready or when lb2 is max and it hits lb2
						if (GetLimoot == (GetLimitBreakBarCount() * GetLimitBreakBarValue())) or GetLimoot > 29999 then
							yield("/rotation Cancel")		
							yield("/echo Attempting "..local_teext)
							yield("/ac "..local_teext)
						end
						if GetLimoot < GetLimitBreakBarCount() * GetLimitBreakBarValue() then
							yield("/rotation auto")		
						end
						--yield("/echo limitpct "..limitpct.." HPP"..GetTargetHPP().." HP"..GetTargetHP().." get limoot"..GetLimitBreakBarCount() * GetLimitBreakBarValue()) --debug line
					end
				end

				autotosscount = autotosscount  + 1
				if autotoss == true and autotosscount > 100 then
				   yield("/discardall")
				   autotosscount = 0
				end
				--check if we changed areas and stop movement and clear target
				we_are_in = GetZoneID()
				if we_are_in ~= we_were_in then
					yield("/wait 0.5")
					yield("/visland stop")
					yield("/vnavmesh stop")
					yield("/wait 0.5")
					yield("/visland stop")
					yield("/vnavmesh stop")
					ClearTarget()
					we_were_in = we_are_in
				end
							
				--the code block that got this all started haha
				--follow and mount fren
				if GetCharacterCondition(26) == false then --not in combat
					if GetCharacterCondition(4) == true and fly_you_fools == true then
						--follow the fren
						if GetCharacterCondition(4) == true and bistance > cling and PathIsRunning() == false and PathfindInProgress() == false then
							--yield("/echo attempting to fly to fren")
							--bmr follow on. we comin
							--yield("/bmrai follow slot"..fartycardinality)
							--yield("/bmrai follow "..fren)
							clingmove(fren)

							yield("/target <"..fartycardinality..">")
							
							--yield("/follow")
							--yield("/wait 0.1") --we dont want to go tooo hard on this
							
							--i could't make the following method smooth please help :(
							--[[
							yield("/vnavmesh flyto "..GetObjectRawXPos(fren).." "..GetObjectRawYPos(fren).." "..GetObjectRawZPos(fren))
							looptillwedroop = 0
							while looptillwedroop == 0 do
								if PathIsRunning() == false and PathfindInProgress() == false then
									looptillwedroop = 1
									yield("/echo Debug Ok we reached path")
								end
								yield("/wait 0.1")
							end
							]]
						end
					end
					if GetCharacterCondition(4) == false and GetCharacterCondition(10) == false then --not mounted and not mounted2 (riding friend)
						--chocobo stuff. first check if we can fly. if not don't try to chocobo
						if HasFlightUnlocked() == true or force_gyasahl == true then
							--check if chocobro is up or (soon) not!
							if GetBuddyTimeRemaining() < 900 and GetItemCount(4868) > 0 then
								yield("/visland stop")
								yield("/vnavmesh stop")
								yield("/item Gysahl Greens")
								yield("/wait 3")
							end
						end
						--yield("/target <cross>")
						if formation == false then
							--check distance to fren, if its more than cling, then
							bistance = distance(GetPlayerRawXPos(), GetPlayerRawYPos(), GetPlayerRawZPos(), GetObjectRawXPos(fren),GetObjectRawYPos(fren),GetObjectRawZPos(fren))
							if bistance > cling and bistance < maxbistance then
							--yield("/target \""..fren.."\"")
								--PathfindAndMoveTo(GetObjectRawXPos(fren),GetObjectRawYPos(fren),GetObjectRawZPos(fren), false)
								clingmove(fren) --movement func
								--yield("/echo DEBUG line 467ish")
							end
							yield("/wait 0.5")
						end	
						--yield("/lockon on")
						--yield("/automove on")

						--[[yield("/ridepillion <"..mker.."> 1")
						yield("/ridepillion <"..mker.."> 2")
						yield("/ridepillion <"..mker.."> 3")]]
						--yield("/echo fly fools .."..tostring(fly_you_fools))
						if fly_you_fools == true then
							if GetCharacterCondition(4) == true then
								yield("/rotation cancel") --keep rotations off
							end
							if GetCharacterCondition(4) == false and GetCharacterCondition(10) == false and IsPartyMemberMounted(shartycardinality) == true then
								--mountup your own mount
								--cancel movement
								--yield("/send s")
								yield("/mount \""..fool_flier.."\"")
								yield("/wait 5")
								ClearTarget()
								yield("/rotation Cancel")
								--try to fly 
								--yield("/gaction jump")
								--yield("/lockon on")
							end
						end
						if IsPartyMemberMounted(shartycardinality) == true and fly_you_fools == false then
							--for i=1,7 do
								--yield("/ridepillion <"..partycardinality.."> "..i)
								counting_fartula()
								yield("/ridepillion <"..fartycardinality.."> 2")
								yield("/rotation Cancel")
							--end
							yield("/echo Attempting to Mount Friend")
							yield("/wait 0.5")
						end
					end
				end
			end
		end
	end
	yield("/wait "..timefriction)
end