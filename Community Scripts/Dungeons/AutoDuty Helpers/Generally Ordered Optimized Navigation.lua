--[[
Generally Ordered Optimized Navigation.lua  (thanks for the idea fifi)
or "Something need Gooning"
thanks to @Akasha and @Ritsuko for some of the ideas/code

purpose: help autoduty with farming duties.

Plugins/configs (ill update as people realize i forgot instructions)
Automaton
Some form of bossmod
Rotation Solver Reborn
Vnavmesh
Pandora
Something Need doing (SND)
Simpletweaks
and more (?)
Simpletweaks -> targeting fix
SND -> disable snd targeting
SND -> disable addon errors

Yesalready configs (maybe only the first one is needed since the rest are done via pcalls w ya off) also make sure yesalready is on :p ad turns it off sometimes (???)
	"YesNo"
		Return to the starting point for the Praetorium?   ※You may be unable to re-enter ongoing battles.
		/Repair all displayed items for.*/
		/Exit.*/
	"Lists"
		/Retire to an inn room.*/

Enhanced Duty start/end
	duty start -> /pcraft run start_gooning
	duty end -> /ad stop
	leave duty -> 10 seconds
Use whatever path you want. but i reccommend the included path file for tank and others. Credit to @Akasha and @Ritsuko for the path files.  tank tries to w2w, tank2 does not.

todo:
duty specific stuff like only do the phantom targetioong for gaeuis in prae

known issues:
I SAW AN UNKNOWN LUA ERROR so i might add debug lines everywhere until i catch it.

recommended party:
war dps dps sch
for sch in RSR turn off adloquim, succor and physick

--]]
yield("/echo please get ready for G O O N ing time")

jigglecounter = 0
x1 = GetPlayerRawXPos()
y1 = GetPlayerRawYPos()
z1 = GetPlayerRawZPos()

stopcuckingme = 0    --counter for checking whento pop duty
imthecaptainnow = 0  --set this to 1 if its the party leader
maxjiggle = 6 -- = how much time before we jiggle
while 1 == 1 do
--safe check ifs
if IsPlayerAvailable() then
if type(GetCharacterCondition(34)) == "boolean" and type(GetCharacterCondition(26)) == "boolean" and type(GetCharacterCondition(4)) == "boolean" then
--
	--Do we need repairs?
	--check every 0.5 seconds 8 times so total looop is 5 seconds
	goat = 0
	while goat < 9 do
		goat = goat + 1
		yield("/wait 0.5")
		if GetCharacterCondition(34) == false then
			--SELF REPAIR
			local minicounter = 0
			--check if we even have g8dm, otherwise dont waste time, 10386 is g6dm if you wanna change it, 17837 is g7, 33916 is g8
			if GetItemCount(33916) > 0 then
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
					while GetCharacterCondition(39) do yield("/wait 1")
					yield("/wait 1")
					yield("/pcall Repair true -1")
					  minicounter = minicounter + 1
					  if minicounter > 20 then
						minicounter = 0
						break
					  end
					end
				end
			end
			--JUST OUTSIDE THE INN REPAIR
			if NeedsRepair(50) and  GetItemCount(1) > 4999 then
			--[[
			repair at 50%
			make sure we have more than 5k gil
			]]
			--turn off yesalready or we getting ROPED
			PauseYesAlready()
				--if GetItemCount(1) > 4999 then
				--Exit the inn room
				yield("/target Heavy")
				yield("/wait 1")
				yield("/lockon on")
				yield("/automove")
				yield("/wait 2")
				yield("/pcall _Notification true 0 17")
				yield("/pcall ContentsFinderConfirm true 9")
				yield("/interact")
				yield("/wait 2")
				yield("/pcall SelectYesno true 0")
				yield("/wait 8")
				
				--find the repair npc
				yield("/target Erkenbaud")  --gridania
				yield("/target Leofrun")    --limsa
				yield("/target Zuzutyro")   --uldah
				yield("/wait 1")
				yield("/lockon on")
				yield("/automove")
				yield("/wait 2")
				yield("/wait 1")
				yield("/pcall _Notification true 0 17")
				yield("/pcall ContentsFinderConfirm true 9")
				yield("/interact")
				yield("/wait 1")
				yield("/pcall SelectIconString true 1")
				yield("/wait 1")
				yield("/pcall Repair true 0")
				yield("/wait 2")
				--yield("/pcall Repair true 1")
				--yield("/wait 5")
				yield("/pcall SelectYesno true 0")
				yield("/wait 2")
				yield("/pcall SelectYesno true 0")
				yield("/send ESCAPE <wait.1.5>")
				yield("/send ESCAPE <wait.1.5>")
				yield("/send ESCAPE <wait.1.5>")
				yield("/send ESCAPE <wait.1>")
				yield("/wait 3")
				
				--reenter the inn room
				yield("/target Antoinaut") --gridania
				yield("/target Mytesyn")   --limsa
				yield("/target Otopa")     --uldah
				yield("/wait 1")
				yield("/lockon on")
				yield("/automove")
				yield("/wait 2")
				yield("/wait 0.5")
				yield("/pcall _Notification true 0 17")
				yield("/pcall ContentsFinderConfirm true 9")
				yield("/interact")
				yield("/wait 1")
				yield("/pcall _Notification true 0 17")
				yield("/pcall ContentsFinderConfirm true 9")
				yield("/pcall SelectIconString true 0")
				yield("/pcall _Notification true 0 17")
				yield("/pcall ContentsFinderConfirm true 9")
				yield("/pcall SelectString true 0")
				yield("/wait 1")
				yield("/wait 8")
				RestoreYesAlready()
			end
		end
	end
	--end safe check one
	end
	end
	--
	--safe check ifs part 2
	if IsPlayerAvailable() then
	if type(GetCharacterCondition(34)) == "boolean" and type(GetCharacterCondition(26)) == "boolean" and type(GetCharacterCondition(4)) == "boolean" then
	--

	--yield("/echo x diff"..math.abs(x1 - GetPlayerRawXPos()))
	--check if we are stuck somewhere.
	--first ensure we are in the duty and not in combat
--[[
	if GetZoneID() == 1044 and GetCharacterCondition(26) == false then --Praetorium
		maxjiggle = 6
		flurb = "????"
		if GetNodeText("_ToDoList", 22, 3) == "Arrive at the command chamber: 0/1"  and GetCharacterCondition(26) == false then
		flurb = GetNodeText("_ToDoList", 22, 3)
		end
		if GetNodeText("_ToDoList", 23, 3) == "Clear the command chamber: 0/1"  and GetCharacterCondition(26) == false then
		flurb = GetNodeText("_ToDoList", 23, 3)
		end
		if GetNodeText("_ToDoList", 24, 3) == "Arrive at the Laboratorium Primum: 0/1"  and GetCharacterCondition(26) == false then
		flurb = GetNodeText("_ToDoList", 24, 3)
		end
		if GetNodeText("_ToDoList", 25, 3) == "Clear the Laboratorium Primum: 0/1"  and GetCharacterCondition(26) == false then
		flurb = GetNodeText("_ToDoList", 25, 3)
		end
		if GetNodeText("_ToDoList", 26, 3) == "Arrive on the Echelon: 0/1"  and GetCharacterCondition(26) == false then
			maxjiggle = 20
			flurb = GetNodeText("_ToDoList", 26, 3)
		end
		if GetNodeText("_ToDoList", 27, 3) == "Defeat Gaius van Baelsar: 0/1" and GetCharacterCondition(26) == false then
			maxjiggle = 20
			flurb = GetNodeText("_ToDoList", 27, 3)
	--		yield("/target Shortcut")
	--		yield("/target Gauis")
	--		yield("/vnavmesh moveto "..GetTargetRawXPos().." "..GetTargetRawYPos().." "..GetTargetRawZPos())
		end
		yield("/echo Prae Duty Progress -> "..flurb)
	end--]]

	if GetCharacterCondition(34) == true and GetCharacterCondition(26) == false then
		if math.abs(x1 - GetPlayerRawXPos()) < 3 and math.abs(y1 - GetPlayerRawYPos()) < 3 and math.abs(z1 - GetPlayerRawZPos()) < 3 then
			yield("/echo we havent moved very much something is up ")
			jigglecounter = jigglecounter + 1
		end
		if jigglecounter > maxjiggle then --we stuck for 30+ seconds somewhere
			yield("/echo attempting to restart AD and hope for the best")
			jigglecounter = 0
			yield("/ad stop")
			yield("/wait 2")
			yield("/return")
			yield("/wait 1")
			yield("/pcall SelectYesno true 0")
			yield("/wait 12")
			yield("/ad start")
			yield("/wait 2")
		end
	end

	if GetZoneID() == 1044 then --Praetorium
		local mytarget = GetTargetName()
		if type(mytarget) == "string" and mytarget ~= "Phantom Gaius" then
			local ndist = GetDistanceToObject(null)
			local gdist = GetDistanceToObject("Phantom Gaius")
			local deltadist = ndist - gdist
			if (deltadist > 1 or deltadist < -1) and gdist < 100 then
				yield("/echo target")
				TargetClosestEnemy()
			end
		end
	end
	--if GetCharacterCondition(34) == false then --fix autoqueue just shitting out
		--yield("/send U")
	--end
	
	if GetCharacterCondition(34) == true and GetCharacterCondition(26) == false then
		yield("/equiprecommended")
	end
	
	if GetCharacterCondition(4) == false and GetCharacterCondition(26) == true then
		yield("/vnav stop")
		jigglecounter = 0 -- we reset the jiggle counter while we are in combat. combat is good means we are doing something productive
	end
	
	if GetCharacterCondition(34) == true then
		x1 = GetPlayerRawXPos()
		y1 = GetPlayerRawYPos()
		z1 = GetPlayerRawZPos()
	end
    yield("/wait 1.0")

	stopcuckingme = stopcuckingme + 1
	--autoqueue at the end because its least important thing
	if stopcuckingme > 2 and GetCharacterCondition(34) == false and imthecaptainnow == 1 then
		yield("/finder")
		yield("/echo attempting to trigger duty finder")
		yield("/pcall ContentsFinder true 12 0")
		stopcuckingme = 0
	end

--safe check ends
end
end
---
end