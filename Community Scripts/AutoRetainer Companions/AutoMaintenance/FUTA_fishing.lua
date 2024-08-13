function fishing()  
	yield("/echo Load -> "..AADM_processors[1])
	
	--now we have to keep trying until we are on the right character.... just in case we are not.
	while AADM_processors[lowestID][1] ~= GetCharacterName(true) do
		yield("/ays relog " ..AADM_processors[lowestID][1])
		yield("/wait 3")

		yield("/waitaddon _ActionBar <maxwait.600><wait.5>")
	end

	--ok we made it to the right character. let us continue.
	
	-- Teleport to Lisma
	yield("/tp Limsa Lominsa Lower Decks <wait.5>")
	yield("/waitaddon _ActionBar <maxwait.600><wait.10>")
	
	yield("/ac sprint")
	become_feesher()
	--if all good then we move on to next part automagically
		
	--repair catte if we are at 50% durability or lower and have at least 5000 gil
	while NeedsRepair(50) and GetItemCount(1) > 4999 do
		PathfindAndMoveTo(-397.46423339844,3.0999958515167,78.562309265137,false) 
		visland_stop_moving()
		yield("/target Merchant & Mender")
		yield("/wait 1")
		yield("/lockon on")
		yield("/wait 1")
		yield("/pinteract")
		yield("/wait 1")
		yield("/pcall SelectIconString true 1")
		yield("/wait 1")
		yield("/pcall Repair true 0")
		yield("/wait 1")
		yield("/pcall Repair true 1")
		yield("/wait 1")
		ungabunga()
	end
	 yield("/bait Versatile Lure")
	 
	--check if we have less than 3 versatile lures and more than 20000 gil if not we buy 20 of them!
	while GetItemCount(29717) < 3 and GetItemCount(1) > 20000 do
		PathfindAndMoveTo(-397.46423339844,3.0999958515167,78.562309265137,false) 
		visland_stop_moving()
		yield("/target Merchant & Mender")
		yield("/wait 1")
		yield("/lockon on")
		yield("/wait 1")
		yield("/pinteract")
		yield("/wait 1")
		yield("/callback SelectIconString true 0")
		yield("/wait 3")
		yield("/callback Shop true 0 3 10")
		yield("/wait 1")
		yield("/callback Shop true 0 3 10")
		yield("/wait 1")
		ungabunga()
	end
	
	yield("/bait Versatile Lure")
	 
	yield("/echo Current area"..GetZoneID())
	zown = GetZoneID()
	fzown = GetZoneID()

	--dryskthota
	PathfindAndMoveTo(-409.42459106445,3.9999997615814,74.483444213867,false) 
	visland_stop_moving()
	yield("/wait 1")
	fishqtest = false
	toolong = 0
	fishqtest = GetCharacterCondition(91)
	while (type(fishqtest) == "boolean" and fishqtest == false) do
		yield("/target Dryskthota")
		yield("/pinteract <wait.2>")
		yield("/wait 1")
		ungabunga()
		yield("/wait 10")
		fishqtest = GetCharacterCondition(91)
		toolong = toolong  + 1
		if GetCharacterCondition(34) == true then  --sometimes we queue instantly. dont wanna get stuck!
			fishqtest = true
		end
		if toolong > 30 then
			fishqtest = true
		end
	end

	yield("/bait Versatile Lure")
 
	--get current area
	--check if area has changed every 5 seconds.
	while (zown == fzown) and (toolong < 30) do
		fzown = GetZoneID()	
		yield("/wait 5")
	end
	--if so then wait for 30 seconds then start heading to the visland location
	yield("<wait.30.0>")

	local randomNum = getRandomNumber(113,4043)
	randomNum = (randomNum * -1) / 1000
	yield("/visland moveto 7.451 6.750 "..randomNum)

	--keep checking for that original area - once it is back. turn /ays multi back on
	--also spam fishing
	omadamkhoneh = 0 --counter to stop trying to move to edge since it will do bad stuff outside of instance after
	while (zown ~= fzown) do
		omadamkhoneh = omadamkhoneh + 1
		fzown = GetZoneID()
		if omadamkhoneh > 100 then
			visland_stop_moving()
			omadamkhoneh = -200  --we dont want this to trigger again
		end
		if GetCharacterCondition(43)==false then
		   yield("/discardall")
		   yield("/wait 5")
		end
		if GetCharacterCondition(43)==false then
			if GetZoneID() ~= 132 then
				yield("/ac cast")
				yield("/wait 1")
			end
		end
		--try to exit the completion window faster
		if IsAddonVisible("IKDResult") then
			yield("/wait 15")
			yield("/pcall IKDResult false 0")
		end
		if GetCharacterCondition(33)==true then
			if GetCharacterCondition(34)==true then
				if GetCharacterCondition(35)==false then
				--LEAVE MENU!!!!!!!!
				yield("/send NUMPAD0 <wait.1.0>")
				yield("/send NUMPAD0 <wait.1.0>")
				end
			end
		end
		yield("/wait 1")
	end
	visland_stop_moving()
	yield("/wait 30")
	ungabungabunga()
	yield("/waitaddon NamePlate <maxwait.600><wait.5>")
	
	--if we are tp to limsa bell
	if AADM_processors[lowestID][2] == 2 then
		return_to_limsa_bell()
		yield("/wait 8")
	end
	
	--if we are tp to inn. we will go to gridania yo
	if AADM_processors[lowestID][2] == 3 then
		yield("/tp New Gridania")
		ZoneTransition()
		yield("/wait 2")
		PathfindAndMoveTo(48.969123840332, -1.5844612121582, 57.311756134033, false)
		visland_stop_moving() --added so we don't accidentally end before we get to the inn person
		yield("/visland exectemponce H4sIAAAAAAAACu3WS4/TMBAA4L9S+RxGfo0fuaEFpBUqLLtIXUAcDPVSS01cEgeEqv53nDSlWxAHUI65eWxnNPlkjb0nr1zlSUm+NGHt6uAWO9ek4LaLFBehrklBVu7HLoY6taT8sCc3sQ0pxJqUe3JPSmnAKsu4LMg7Uj5hgEZKxXhB3pMSNQjGNKpDDmPtr5+Rkom8duvWocv5GNCCLOM3X/k6kTIHNy5tHkK9JmVqOl+Q6zr5xn1Oq5A2r/vv6eXcWH0us93E76eVXF/O/uC27aMUQ9GsIM+rmPwpVfLVOHw67BiDN51v0+Pxnf86BMv4aZy+S3F3Fev1qJFnXobt9ip245/cxi75y/JWLqRzXX30IjaXOfrJt6Hyy7yPHoo/vFGBEGj1kZuCNohIe/7srQwgo8hm7qm4FQObDzQ/cUtpKcU+ztwawQqrZ+3JtCVIjsIctTkwTRH1YG0E5GNOJc7ak2nz3D14Bj9yK1BaS4sDt0VApZWdtSdr3AwYNxbHVmKASmWVPnIzKkFwTs3fvMV8Uf6jt8TcrM3wEjl7SzNyCxDa6rmZTHa8uQWRH4LmzP3rYDPUcr4kp5PWoASXVv0uzYAzIebH339Kfzz8BLifXG8MDQAA")
		visland_stop_moving() --added so we don't accidentally end before we get to the inn person
		yield("/target Antoinaut")
		yield("/wait 0.5")
		yield("/interact")
	end
	
	--options 1 and 2 are fc estate entrance or fc state bell so thats only time we will tp to fc estate
	if AADM_processors[lowestID][2] == 0 or AADM_processors[lowestID][2] == 1 then
		--yield("/tp Estate Hall (Free Company)")
		yield("/waitaddon NamePlate <maxwait.600><wait.5>")
		yield("/li fc")
		yield("/wait 1")
		--yield("/waitaddon Nowloading <maxwait.15>")
		yield("/wait 15")
		yield("/waitaddon NamePlate <maxwait.600><wait.5>")
	end

	--normal small house shenanigans
	if AADM_processors[lowestID][2] == 0 then
		yield("/hold W <wait.1.0>")
		yield("/release W")
		yield("/target Entrance <wait.1>")
		yield("/lockon on")
		yield("/automove on <wait.2.5>")
		yield("/interact")
		yield("/automove off <wait.1.5>")
		yield("/hold Q <wait.1.0>")
		yield("/interact")
		yield("/release Q")
		yield("/interact")
		yield("/hold Q <wait.1.0>")
		yield("/interact")
		yield("/release Q")
		yield("/interact")
		yield("/wait 1")
	end

	--retainer bell nearby shenanigans
	if AADM_processors[lowestID][2] == 1 then
		yield("/target \"Summoning Bell\"")
		yield("/wait 2")
		PathfindAndMoveTo(GetObjectRawXPos("Summoning Bell"), GetObjectRawYPos("Summoning Bell"), GetObjectRawZPos("Summoning Bell"), false)
		visland_stop_moving() --added so we don't accidentally end before we get to the bell
	end
end --of fishing()