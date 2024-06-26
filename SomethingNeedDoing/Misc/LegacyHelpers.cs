﻿using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;

namespace SomethingNeedDoing.Misc;

public static class LegacyHelpers
{
    public static void SetTarget(this ITargetManager targetManager, IGameObject obj) => targetManager.Target = obj;
}