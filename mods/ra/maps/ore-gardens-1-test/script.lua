local mapBounds = { Width = 170, Height = 170 }

WorldLoaded = function()
	local moverPlayer = Player.GetPlayer("Multi0")

	for i=4,mapBounds.Width-4,4 do
		local jeep = Actor.Create("jeep", true, { Owner = moverPlayer, Location = CPos.New(i, 2) });

		Trigger.AfterDelay(DateTime.Seconds(3), function ()
			jeep.Move(CPos.New(mapBounds.Width - i, 160), 1)
		end)
	end

end
