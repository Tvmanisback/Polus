using MiraAPI.GameEnd;
using MiraAPI.Utilities;
using Polus.Plugin.Roles;
using UnityEngine;

namespace Polus.Plugin;

public sealed class PolusNeutralVictory : CustomGameOver
{
    public override bool VerifyCondition(PlayerControl playerControl, NetworkedPlayerInfo[] winners) =>
        winners.Length == 1 && winners[0].Role is PolusNeutralRole role &&
        PolusRoleMechanics.GetProgress(winners[0].PlayerId) >= role.ObjectiveTaskGoal;

    public override void AfterEndGameSetup(EndGameManager endGameManager)
    {
        endGameManager.WinText.text = "POLUS NEUTRAL OBJECTIVE COMPLETE";
        endGameManager.WinText.color = new Color32(190, 145, 255, 255);
        endGameManager.BackgroundBar.material.SetColor(ShaderID.Color, new Color32(90, 55, 135, 255));
    }
}
