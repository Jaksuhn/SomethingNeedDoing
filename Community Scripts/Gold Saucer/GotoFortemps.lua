--[[
  Description: Run TT a bit before bed. make your own Vislands and use 1 radius for them.
  Author: McVaxius
  Link: https://discord.com/channels/1162031769403543643/1162799234874093661/1182415752389726280
]]

function visland_stop_moving()
 yield("/equipguud")
 yield("/wait 3")
 muuv = 1
 muuvX = GetPlayerRawXPos()
 muuvY = GetPlayerRawYPos()
 muuvZ = GetPlayerRawZPos()
 while muuv == 1 do
	yield("/wait 1")
	if muuvX == GetPlayerRawXPos() and muuvY == GetPlayerRawYPos() and muuvZ == GetPlayerRawZPos() then
		muuv = 0
	end
	muuvX = GetPlayerRawXPos()
	muuvY = GetPlayerRawYPos()
	muuvZ = GetPlayerRawZPos()
 end
 yield("/echo movement stopped")
 yield("/visland stop")
 yield("/wait 5")
end

--teleport to Ishgard when name plate is ready
yield("/tp Foundation <wait.8>")

--fire up saucy
yield("/saucy")

--don't forget to manually set how many matches you want and enable it!
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")
yield("/echo Set the number of matches and Enable TT after!!!")

--callback teleport to the last vigil
 yield("/li Vigil")
 visland_stop_moving()

--target manor entrance guy
 yield("/target \"House Fortemps Guard\" <wait.1>")
 yield("/vnav moveto "..GetObjectRawXPos(GetTargetName()).." "..GetObjectRawYPos(GetTargetName()).." "..GetObjectRawZPos(GetTargetName()))
 yield("/wait 3")
--proceed when not moving
 visland_stop_moving()
 yield("/pinteract <wait.5>")

--target the manservant
--House Fortemps Manservant
 yield("/target Manservant <wait.1>")
 yield("/vnav moveto "..GetObjectRawXPos(GetTargetName()).." "..GetObjectRawYPos(GetTargetName()).." "..GetObjectRawZPos(GetTargetName()))
 yield("/wait 3")
--proceed when not moving
 visland_stop_moving()
 yield("/interact <wait.5>")

--wait a few seconds then start checking status of player condition until its no longer playing TT
yield("/wait 5")

--its condition 13 = playing minigame that we care about
--?? check cond 13 when not cond 13 then we can move on after a short wait
--??
while GetCharacterCondition(13)==true do
	yield("/wait 1")
end