--set this to run when entering a duty
yield("/wait 2")
--loop wait for is char ready 
while IsPlayerAvailable() == false do
	yield("/echo waiting on player")
	yield("/wait 1")
end
yield("/ad stop")
yield("/wait 2")
yield("/hold W <wait.2.0>")
yield("/release W")
yield("/ad start")